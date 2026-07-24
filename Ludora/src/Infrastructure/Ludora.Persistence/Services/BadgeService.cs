using Ludora.Application.DTOs.Badge;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class BadgeService : IBadgeService
{
    private readonly AppDbContext _context;

    public BadgeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<BadgeDTO>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = _context.BadgeDefinitions.AsNoTracking();
        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var badges = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return badges.Select(MapBadge).ToList();
    }

    public async Task<IReadOnlyList<UserBadgeDTO>> GetUserBadgesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);
        if (!userExists)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        return await _context.UserBadges
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.BadgeDefinition.IsActive)
            .OrderBy(x => x.BadgeDefinition.DisplayOrder)
            .ThenByDescending(x => x.AwardedAtUtc)
            .Select(x => new UserBadgeDTO
            {
                Id = x.BadgeDefinitionId,
                Code = x.BadgeDefinition.Code,
                Name = x.BadgeDefinition.Name,
                Description = x.BadgeDefinition.Description,
                Icon = x.BadgeDefinition.Icon,
                Color = x.BadgeDefinition.Color,
                MetricValueAtAward = x.MetricValueAtAward,
                AwardedAtUtc = x.AwardedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<BadgeDTO> CreateAsync(
        BadgeUpsertDTO dto,
        CancellationToken cancellationToken = default)
    {
        var normalized = Validate(dto);
        var codeExists = await _context.BadgeDefinitions
            .AnyAsync(x => x.Code == normalized.Code, cancellationToken);
        if (codeExists)
            throw new ConflictException("Bu badge kodu artıq mövcuddur.");

        var entity = new BadgeDefinition
        {
            Code = normalized.Code,
            Name = normalized.Name,
            Description = normalized.Description,
            Icon = normalized.Icon,
            Color = normalized.Color,
            Metric = normalized.Metric,
            Threshold = normalized.Threshold,
            IsActive = normalized.IsActive,
            DisplayOrder = normalized.DisplayOrder,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.BadgeDefinitions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return MapBadge(entity);
    }

    public async Task<BadgeDTO> UpdateAsync(
        int id,
        BadgeUpsertDTO dto,
        CancellationToken cancellationToken = default)
    {
        var normalized = Validate(dto);
        var entity = await _context.BadgeDefinitions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Badge tapılmadı.");

        var duplicate = await _context.BadgeDefinitions
            .AnyAsync(x => x.Id != id && x.Code == normalized.Code, cancellationToken);
        if (duplicate)
            throw new ConflictException("Bu badge kodu artıq mövcuddur.");

        entity.Code = normalized.Code;
        entity.Name = normalized.Name;
        entity.Description = normalized.Description;
        entity.Icon = normalized.Icon;
        entity.Color = normalized.Color;
        entity.Metric = normalized.Metric;
        entity.Threshold = normalized.Threshold;
        entity.IsActive = normalized.IsActive;
        entity.DisplayOrder = normalized.DisplayOrder;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return MapBadge(entity);
    }

    public async Task DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.BadgeDefinitions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Badge tapılmadı.");

        _context.BadgeDefinitions.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> EvaluateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadMetricSnapshotAsync(userId, cancellationToken);
        var definitions = await _context.BadgeDefinitions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        var existingBadgeIds = await _context.UserBadges
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.BadgeDefinitionId)
            .ToListAsync(cancellationToken);
        var existingIds = existingBadgeIds.ToHashSet();

        var now = DateTime.UtcNow;
        var awards = definitions
            .Where(x => !existingIds.Contains(x.Id))
            .Select(x => new
            {
                Definition = x,
                Value = snapshot.GetValue(x.Metric)
            })
            .Where(x => x.Value >= x.Definition.Threshold)
            .Select(x => new UserBadge
            {
                UserId = userId,
                BadgeDefinitionId = x.Definition.Id,
                MetricValueAtAward = x.Value,
                AwardedAtUtc = now
            })
            .ToList();

        if (awards.Count == 0)
            return 0;

        _context.UserBadges.AddRange(awards);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return awards.Count;
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();
            return 0;
        }
    }

    public async Task<IReadOnlyList<Guid>> GetUserBatchAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0 || take is < 1 or > 500)
            throw new BadRequestException("Badge batch parametrləri düzgün deyil.");

        return await _context.Users
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<MetricSnapshot> LoadMetricSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var core = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.CreatedAt,
                SteamLinked = x.SteamId != null,
                GamesLogged = x.UserGames.Count,
                GamesPlayed = x.UserGames.Count(g => g.Status == UserGameStatus.Played),
                RatingsGiven = x.UserGames.Count(g => g.Rating.HasValue),
                Favorites = x.UserGames.Count(g => g.IsFavorite),
                WishlistItems = x.UserGames.Count(g => g.IsWishlisted),
                PostsCreated = x.Posts.Count,
                Followers = x.Followers.Count(f => f.Status == FollowStatus.Accepted),
                Following = x.Following.Count(f => f.Status == FollowStatus.Accepted),
                ListsCreated = x.GameLists.Count,
                SteamGamesImported = x.SteamOwnedGames.Count,
                MessagesSent = x.SentChatMessages.Count
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        var reviewStats = await _context.Reviews
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .GroupBy(_ => 1)
            .Select(x => new
            {
                ReviewsWritten = x.Count(r => r.Content != string.Empty),
                TotalPlaytimeMinutes = x.Sum(r => (long)(r.PlaytimeMinutes ?? 0))
            })
            .FirstOrDefaultAsync(cancellationToken);

        var forumDiscussions = await _context.Discussions
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId, cancellationToken);
        var forumReplies = await _context.DiscussionReplies
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId, cancellationToken);
        var commentsWritten =
            await _context.PostComments.AsNoTracking().CountAsync(x => x.UserId == userId, cancellationToken) +
            await _context.ReviewComments.AsNoTracking().CountAsync(x => x.UserId == userId, cancellationToken) +
            await _context.GameListComments.AsNoTracking().CountAsync(x => x.UserId == userId, cancellationToken);

        return new MetricSnapshot(new Dictionary<BadgeMetric, int>
        {
            [BadgeMetric.AccountAgeDays] = Math.Max(0, (DateTime.UtcNow.Date - core.CreatedAt.Date).Days),
            [BadgeMetric.GamesLogged] = core.GamesLogged,
            [BadgeMetric.GamesPlayed] = core.GamesPlayed,
            [BadgeMetric.ReviewsWritten] = reviewStats?.ReviewsWritten ?? 0,
            [BadgeMetric.RatingsGiven] = core.RatingsGiven,
            [BadgeMetric.PostsCreated] = core.PostsCreated,
            [BadgeMetric.Followers] = core.Followers,
            [BadgeMetric.Following] = core.Following,
            [BadgeMetric.ListsCreated] = core.ListsCreated,
            [BadgeMetric.Favorites] = core.Favorites,
            [BadgeMetric.WishlistItems] = core.WishlistItems,
            [BadgeMetric.TotalPlaytimeHours] =
                (int)Math.Min(int.MaxValue, (reviewStats?.TotalPlaytimeMinutes ?? 0) / 60),
            [BadgeMetric.ForumDiscussions] = forumDiscussions,
            [BadgeMetric.ForumReplies] = forumReplies,
            [BadgeMetric.CommentsWritten] = commentsWritten,
            [BadgeMetric.SteamGamesImported] = core.SteamGamesImported,
            [BadgeMetric.SteamAccountLinked] = core.SteamLinked ? 1 : 0,
            [BadgeMetric.MessagesSent] = core.MessagesSent
        });
    }

    private static BadgeUpsertDTO Validate(BadgeUpsertDTO dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (!Enum.IsDefined(dto.Metric))
            throw new BadRequestException("Badge metrikası düzgün deyil.");
        if (dto.Threshold <= 0)
            throw new BadRequestException("Badge limiti sıfırdan böyük olmalıdır.");

        return dto with
        {
            Code = Normalize(dto.Code, 80, "Badge kodu").ToLowerInvariant(),
            Name = Normalize(dto.Name, 120, "Badge adı"),
            Description = Normalize(dto.Description, 500, "Badge açıqlaması"),
            Icon = Normalize(dto.Icon, 60, "Badge ikonu"),
            Color = Normalize(dto.Color, 20, "Badge rəngi")
        };
    }

    private static string Normalize(string value, int maxLength, string field)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maxLength)
            throw new BadRequestException($"{field} 1-{maxLength} simvol aralığında olmalıdır.");
        return normalized;
    }

    private static BadgeDTO MapBadge(BadgeDefinition x) =>
        new()
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            Description = x.Description,
            Icon = x.Icon,
            Color = x.Color,
            Metric = x.Metric,
            Threshold = x.Threshold,
            IsActive = x.IsActive,
            DisplayOrder = x.DisplayOrder
        };

    private sealed record MetricSnapshot(IReadOnlyDictionary<BadgeMetric, int> Values)
    {
        public int GetValue(BadgeMetric metric) => Values.GetValueOrDefault(metric);
    }
}
