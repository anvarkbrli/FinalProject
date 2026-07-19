using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class UserActivityService : IUserActivityService
{
    private const int MaxPageSize = 50;
    private readonly AppDbContext _context;
    private readonly IIgdbClient _igdbClient;

    public UserActivityService(
        AppDbContext context,
        IIgdbClient igdbClient)
    {
        _context = context;
        _igdbClient = igdbClient;
    }

    public async Task<PagedResultDTO<UserActivityDTO>> GetMineAsync(
        Guid userId,
        UserActivityFilterDTO filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ValidateFilter(filter);

        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        var query = _context.UserActivities
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var activities = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var items = await MapManyAsync(activities, cancellationToken);

        return new PagedResultDTO<UserActivityDTO>
        {
            Items = items,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)filter.PageSize)
        };
    }

    private async Task<IReadOnlyList<UserActivityDTO>> MapManyAsync(
        IReadOnlyList<UserActivity> activities,
        CancellationToken cancellationToken)
    {
        var targetUserIds = activities
            .Where(x => x.TargetUserId.HasValue)
            .Select(x => x.TargetUserId!.Value)
            .Distinct()
            .ToList();

        var users = await _context.Users
            .AsNoTracking()
            .Where(x => targetUserIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.UserName,
                x.ProfileImageUrl
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var localGameIds = activities
            .Where(x =>
                x.GameSource == CatalogSource.Local &&
                x.GameId.HasValue &&
                x.GameId.Value <= int.MaxValue)
            .Select(x => (int)x.GameId!.Value)
            .Distinct()
            .ToList();

        var localGames = await _context.Games
            .AsNoTracking()
            .Where(x => localGameIds.Contains(x.Id))
            .Select(x => new ActivityGameDetails(
                CatalogSource.Local,
                x.Id,
                x.Name,
                x.Slug,
                x.CoverImageUrl))
            .ToDictionaryAsync(x => x.GameId, cancellationToken);

        var igdbGameIds = activities
            .Where(x =>
                x.GameSource == CatalogSource.Igdb &&
                x.GameId.HasValue)
            .Select(x => x.GameId!.Value)
            .Distinct()
            .ToList();

        var igdbGames = new Dictionary<long, ActivityGameDetails>();

        foreach (var gameId in igdbGameIds)
        {
            var game = await _igdbClient.GetGameByIdAsync(
                gameId,
                cancellationToken);

            if (game is null)
                continue;

            igdbGames[gameId] = new ActivityGameDetails(
                CatalogSource.Igdb,
                game.Id,
                game.Name,
                null,
                game.CoverImageUrl);
        }

        return activities.Select(activity =>
        {
            users.TryGetValue(
                activity.TargetUserId ?? Guid.Empty,
                out var targetUser);

            ActivityGameDetails? game = null;

            if (activity.GameSource == CatalogSource.Local &&
                activity.GameId.HasValue &&
                activity.GameId.Value <= int.MaxValue)
            {
                localGames.TryGetValue(
                    activity.GameId.Value,
                    out game);
            }
            else if (activity.GameSource == CatalogSource.Igdb &&
                     activity.GameId.HasValue)
            {
                igdbGames.TryGetValue(
                    activity.GameId.Value,
                    out game);
            }

            return new UserActivityDTO
            {
                Id = activity.Id,
                Type = activity.Type,
                CreatedAt = activity.CreatedAt,
                TargetUserId = activity.TargetUserId,
                TargetUserName = targetUser?.UserName,
                TargetUserProfileImageUrl = targetUser?.ProfileImageUrl,
                GameSource = activity.GameSource,
                GameId = activity.GameId,
                GameName = game?.Name,
                GameSlug = game?.Slug,
                GameCoverImageUrl = game?.CoverImageUrl,
                ReviewId = activity.ReviewId,
                Status = activity.Status,
                Rating = activity.Rating,
                PostId = activity.PostId,
                GameListId = activity.GameListId,
                SubjectTitle = activity.SubjectTitle
            };
        }).ToList();
    }

    private static void ValidateFilter(UserActivityFilterDTO filter)
    {
        if (filter.PageNumber < 1)
            throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (filter.PageSize < 1 || filter.PageSize > MaxPageSize)
        {
            throw new BadRequestException(
                $"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");
        }
    }

    private sealed record ActivityGameDetails(
        CatalogSource Source,
        long GameId,
        string Name,
        string? Slug,
        string? CoverImageUrl);
}
