using System.Text.RegularExpressions;
using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.Forum;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class ForumCommunityFeatureService : IForumCommunityFeatureService
{
    private static readonly Regex HexColorRegex = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private readonly AppDbContext _context;

    public ForumCommunityFeatureService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ForumCommunityRuleDTO>> GetRulesAsync(
        int communityId,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanViewAsync(communityId, currentUserId, cancellationToken);

        return await _context.ForumCommunityRules
            .AsNoTracking()
            .Where(x => x.CommunityId == communityId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new ForumCommunityRuleDTO
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                SortOrder = x.SortOrder
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ForumCommunityRuleDTO>> ReplaceRulesAsync(
        int communityId,
        Guid actorUserId,
        ForumCommunityRulesUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureOwnerAsync(communityId, actorUserId, cancellationToken);

        if (dto.Rules.Count > 15)
            throw new BadRequestException("İcmada maksimum 15 qayda ola bilər.");

        var existing = await _context.ForumCommunityRules
            .Where(x => x.CommunityId == communityId)
            .ToListAsync(cancellationToken);
        var requestedIds = dto.Rules.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();

        foreach (var removed in existing.Where(x => !requestedIds.Contains(x.Id)))
            _context.ForumCommunityRules.Remove(removed);

        for (var index = 0; index < dto.Rules.Count; index++)
        {
            var item = dto.Rules[index];
            var title = item.Title.NormalizeRequiredText("Qayda başlığı", 100);
            var description = item.Description.NormalizeRequiredText("Qayda açıqlaması", 500);
            var entity = item.Id.HasValue
                ? existing.FirstOrDefault(x => x.Id == item.Id.Value)
                    ?? throw new NotFoundException("Qayda tapılmadı.")
                : new ForumCommunityRule { CommunityId = communityId, CreatedAt = DateTime.UtcNow };

            entity.Title = title;
            entity.Description = description;
            entity.SortOrder = index;
            entity.UpdatedAt = DateTime.UtcNow;
            if (!item.Id.HasValue)
                _context.ForumCommunityRules.Add(entity);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetRulesAsync(communityId, actorUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<ForumCommunityMemberSearchDTO>> GetModeratorsAsync(
        int communityId,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanViewAsync(communityId, currentUserId, cancellationToken);

        return await _context.ForumCommunityMembers
            .AsNoTracking()
            .Where(x => x.CommunityId == communityId &&
                        x.Status == ForumCommunityMembershipStatus.Active &&
                        (x.Role == ForumCommunityMemberRole.Owner || x.Role == ForumCommunityMemberRole.Moderator))
            .OrderBy(x => x.Role == ForumCommunityMemberRole.Owner ? 0 : 1)
            .ThenBy(x => x.User.UserName)
            .Select(MemberProjection())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ForumCommunityFlairDTO>> GetFlairsAsync(
        int communityId,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanViewAsync(communityId, currentUserId, cancellationToken);

        var selectedFlairId = currentUserId.HasValue
            ? await _context.ForumCommunityMembers
                .AsNoTracking()
                .Where(x => x.CommunityId == communityId &&
                            x.UserId == currentUserId.Value &&
                            x.Status == ForumCommunityMembershipStatus.Active)
                .Select(x => x.FlairId)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return await _context.ForumCommunityFlairs
            .AsNoTracking()
            .Where(x => x.CommunityId == communityId && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new ForumCommunityFlairDTO
            {
                Id = x.Id,
                Name = x.Name,
                BackgroundColor = x.BackgroundColor,
                TextColor = x.TextColor,
                SortOrder = x.SortOrder,
                IsSelectedByCurrentUser = selectedFlairId.HasValue && selectedFlairId.Value == x.Id
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ForumCommunityFlairDTO>> ReplaceFlairsAsync(
        int communityId,
        Guid actorUserId,
        ForumCommunityFlairsUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureOwnerAsync(communityId, actorUserId, cancellationToken);

        if (dto.Flairs.Count > 20)
            throw new BadRequestException("İcmada maksimum 20 user flair ola bilər.");

        var normalizedNames = dto.Flairs
            .Select(x => x.Name.NormalizeRequiredText("Flair adı", 40))
            .ToList();
        if (normalizedNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedNames.Count)
            throw new ConflictException("Flair adları təkrarlana bilməz.");

        var existing = await _context.ForumCommunityFlairs
            .Where(x => x.CommunityId == communityId)
            .ToListAsync(cancellationToken);
        var requestedIds = dto.Flairs.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();

        var removedFlairs = existing
            .Where(x => !requestedIds.Contains(x.Id))
            .ToList();
        var removedFlairIds = removedFlairs.Select(x => x.Id).ToArray();
        if (removedFlairIds.Length > 0)
        {
            var updatedAtUtc = DateTime.UtcNow;
            await _context.ForumCommunityMembers
                .Where(x => x.CommunityId == communityId &&
                            x.FlairId.HasValue &&
                            removedFlairIds.Contains(x.FlairId.Value))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.FlairId, (int?)null)
                        .SetProperty(x => x.UpdatedAt, (DateTime?)updatedAtUtc),
                    cancellationToken);

            _context.ForumCommunityFlairs.RemoveRange(removedFlairs);
        }

        for (var index = 0; index < dto.Flairs.Count; index++)
        {
            var item = dto.Flairs[index];
            var entity = item.Id.HasValue
                ? existing.FirstOrDefault(x => x.Id == item.Id.Value)
                    ?? throw new NotFoundException("Flair tapılmadı.")
                : new ForumCommunityFlair { CommunityId = communityId, CreatedAt = DateTime.UtcNow };

            entity.Name = normalizedNames[index];
            entity.BackgroundColor = NormalizeColor(item.BackgroundColor, "Flair fon rəngi");
            entity.TextColor = NormalizeColor(item.TextColor, "Flair mətn rəngi");
            entity.SortOrder = index;
            entity.IsActive = true;
            entity.UpdatedAt = DateTime.UtcNow;
            if (!item.Id.HasValue)
                _context.ForumCommunityFlairs.Add(entity);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetFlairsAsync(communityId, actorUserId, cancellationToken);
    }

    public async Task SetMyFlairAsync(
        int communityId,
        Guid userId,
        ForumCommunitySetFlairDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var membership = await EnsureActiveMemberAsync(communityId, userId, cancellationToken);

        if (dto.FlairId.HasValue)
        {
            var valid = await _context.ForumCommunityFlairs
                .AsNoTracking()
                .AnyAsync(x => x.Id == dto.FlairId.Value && x.CommunityId == communityId && x.IsActive, cancellationToken);
            if (!valid)
                throw new NotFoundException("Flair tapılmadı.");
        }

        membership.FlairId = dto.FlairId;
        membership.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResultDTO<ForumCommunityMemberSearchDTO>> SearchMembersAsync(
        int communityId,
        Guid currentUserId,
        string? query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await EnsureActiveMemberAsync(communityId, currentUserId, cancellationToken);
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var normalizedQuery = query.TrimOrNull();

        var members = _context.ForumCommunityMembers
            .AsNoTracking()
            .Where(x => x.CommunityId == communityId && x.Status == ForumCommunityMembershipStatus.Active);

        if (normalizedQuery is not null)
            members = members.Where(x => x.User.UserName != null && x.User.UserName.Contains(normalizedQuery));

        var totalCount = await members.CountAsync(cancellationToken);
        var items = await members
            .OrderBy(x => x.Role == ForumCommunityMemberRole.Owner ? 0 : x.Role == ForumCommunityMemberRole.Moderator ? 1 : 2)
            .ThenBy(x => x.User.UserName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(MemberProjection())
            .ToListAsync(cancellationToken);

        return new PagedResultDTO<ForumCommunityMemberSearchDTO>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<PagedResultDTO<ForumCommunityEventDTO>> GetEventsAsync(
        int communityId,
        Guid? currentUserId,
        bool past,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanViewAsync(communityId, currentUserId, cancellationToken);
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 1, 30);
        var now = DateTime.UtcNow;
        var canManage = currentUserId.HasValue && await IsModeratorAsync(communityId, currentUserId.Value, cancellationToken);

        var events = _context.ForumCommunityEvents
            .AsNoTracking()
            .Where(x => x.CommunityId == communityId);
        events = past
            ? events.Where(x => x.StartsAtUtc < now || x.IsCancelled)
            : events.Where(x => x.StartsAtUtc >= now && !x.IsCancelled);

        var totalCount = await events.CountAsync(cancellationToken);
        var ordered = past
            ? events.OrderByDescending(x => x.StartsAtUtc)
            : events.OrderBy(x => x.StartsAtUtc);
        var items = await ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ForumCommunityEventDTO
            {
                Id = x.Id,
                CommunityId = x.CommunityId,
                Title = x.Title,
                Description = x.Description,
                Type = x.Type,
                GameName = x.GameName,
                Platform = x.Platform,
                JoinUrl = canManage ||
                    (currentUserId.HasValue && x.Participants.Any(p => p.UserId == currentUserId.Value))
                        ? x.JoinUrl
                        : null,
                StartsAtUtc = x.StartsAtUtc,
                EndsAtUtc = x.EndsAtUtc,
                MaxParticipants = x.MaxParticipants,
                ParticipantCount = x.Participants.Count,
                IsJoinedByCurrentUser = currentUserId.HasValue && x.Participants.Any(p => p.UserId == currentUserId.Value),
                IsCancelled = x.IsCancelled,
                CreatedByUserId = x.CreatedByUserId,
                CreatedByUserName = x.CreatedByUser.UserName ?? string.Empty,
                CanManage = canManage,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDTO<ForumCommunityEventDTO>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<ForumCommunityEventDTO> CreateEventAsync(
        int communityId,
        Guid actorUserId,
        ForumCommunityEventUpsertDTO dto,
        CancellationToken cancellationToken = default)
    {
        await EnsureModeratorAsync(communityId, actorUserId, cancellationToken);
        var normalized = NormalizeEvent(dto, requireFutureStart: true);
        var entity = new ForumCommunityEvent
        {
            CommunityId = communityId,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow,
            Title = normalized.Title,
            Description = normalized.Description,
            Type = normalized.Type,
            GameName = normalized.GameName,
            Platform = normalized.Platform,
            JoinUrl = normalized.JoinUrl,
            StartsAtUtc = normalized.StartsAtUtc,
            EndsAtUtc = normalized.EndsAtUtc,
            MaxParticipants = normalized.MaxParticipants
        };
        _context.ForumCommunityEvents.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return await GetEventAsync(communityId, entity.Id, actorUserId, cancellationToken);
    }

    public async Task<ForumCommunityEventDTO> UpdateEventAsync(
        int communityId,
        int eventId,
        Guid actorUserId,
        ForumCommunityEventUpsertDTO dto,
        CancellationToken cancellationToken = default)
    {
        await EnsureModeratorAsync(communityId, actorUserId, cancellationToken);
        var entity = await _context.ForumCommunityEvents
            .FirstOrDefaultAsync(x => x.Id == eventId && x.CommunityId == communityId, cancellationToken)
            ?? throw new NotFoundException("Tədbir tapılmadı.");
        var normalized = NormalizeEvent(dto, requireFutureStart: false);
        entity.Title = normalized.Title;
        entity.Description = normalized.Description;
        entity.Type = normalized.Type;
        entity.GameName = normalized.GameName;
        entity.Platform = normalized.Platform;
        entity.JoinUrl = normalized.JoinUrl;
        entity.StartsAtUtc = normalized.StartsAtUtc;
        entity.EndsAtUtc = normalized.EndsAtUtc;
        entity.MaxParticipants = normalized.MaxParticipants;
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return await GetEventAsync(communityId, eventId, actorUserId, cancellationToken);
    }

    public async Task CancelEventAsync(
        int communityId,
        int eventId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureModeratorAsync(communityId, actorUserId, cancellationToken);
        var entity = await _context.ForumCommunityEvents
            .FirstOrDefaultAsync(x => x.Id == eventId && x.CommunityId == communityId, cancellationToken)
            ?? throw new NotFoundException("Tədbir tapılmadı.");
        entity.IsCancelled = true;
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task JoinEventAsync(
        int communityId,
        int eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureActiveMemberAsync(communityId, userId, cancellationToken);
        var eventInfo = await _context.ForumCommunityEvents
            .AsNoTracking()
            .Where(x => x.Id == eventId && x.CommunityId == communityId)
            .Select(x => new
            {
                x.IsCancelled,
                x.StartsAtUtc,
                x.MaxParticipants,
                ParticipantCount = x.Participants.Count,
                IsJoined = x.Participants.Any(p => p.UserId == userId)
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Tədbir tapılmadı.");

        if (eventInfo.IsCancelled || eventInfo.StartsAtUtc < DateTime.UtcNow)
            throw new ConflictException("Bu tədbirə artıq qoşulmaq mümkün deyil.");
        if (eventInfo.IsJoined)
            return;
        if (eventInfo.MaxParticipants.HasValue && eventInfo.ParticipantCount >= eventInfo.MaxParticipants.Value)
            throw new ConflictException("Tədbirdə boş yer qalmayıb.");

        _context.ForumCommunityEventParticipants.Add(new ForumCommunityEventParticipant
        {
            EventId = eventId,
            UserId = userId,
            JoinedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task LeaveEventAsync(
        int communityId,
        int eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var participant = await _context.ForumCommunityEventParticipants
            .FirstOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId && x.Event.CommunityId == communityId, cancellationToken);
        if (participant is null)
            return;
        _context.ForumCommunityEventParticipants.Remove(participant);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<ForumCommunityEventDTO> GetEventAsync(int communityId, int eventId, Guid currentUserId, CancellationToken cancellationToken)
    {
        var canManage = await IsModeratorAsync(communityId, currentUserId, cancellationToken);
        return await _context.ForumCommunityEvents
            .AsNoTracking()
            .Where(x => x.Id == eventId && x.CommunityId == communityId)
            .Select(x => new ForumCommunityEventDTO
            {
                Id = x.Id,
                CommunityId = x.CommunityId,
                Title = x.Title,
                Description = x.Description,
                Type = x.Type,
                GameName = x.GameName,
                Platform = x.Platform,
                JoinUrl = canManage || x.Participants.Any(p => p.UserId == currentUserId)
                    ? x.JoinUrl
                    : null,
                StartsAtUtc = x.StartsAtUtc,
                EndsAtUtc = x.EndsAtUtc,
                MaxParticipants = x.MaxParticipants,
                ParticipantCount = x.Participants.Count,
                IsJoinedByCurrentUser = x.Participants.Any(p => p.UserId == currentUserId),
                IsCancelled = x.IsCancelled,
                CreatedByUserId = x.CreatedByUserId,
                CreatedByUserName = x.CreatedByUser.UserName ?? string.Empty,
                CanManage = canManage,
                CreatedAt = x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Tədbir tapılmadı.");
    }

    private static ForumCommunityEventUpsertDTO NormalizeEvent(ForumCommunityEventUpsertDTO dto, bool requireFutureStart)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (!Enum.IsDefined(dto.Type))
            throw new BadRequestException("Tədbir növü düzgün deyil.");
        if (requireFutureStart && dto.StartsAtUtc <= DateTime.UtcNow.AddMinutes(-5))
            throw new BadRequestException("Tədbirin başlama vaxtı gələcəkdə olmalıdır.");
        if (dto.EndsAtUtc.HasValue && dto.EndsAtUtc.Value <= dto.StartsAtUtc)
            throw new BadRequestException("Bitmə vaxtı başlama vaxtından sonra olmalıdır.");
        if (dto.MaxParticipants is < 2 or > 500)
            throw new BadRequestException("İştirakçı limiti 2-500 aralığında olmalıdır.");

        var joinUrl = dto.JoinUrl.NormalizeOptionalText("Qoşulma linki", 2048);
        if (joinUrl is not null && (!Uri.TryCreate(joinUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            throw new BadRequestException("Qoşulma linki düzgün deyil.");

        return dto with
        {
            Title = dto.Title.NormalizeRequiredText("Tədbir adı", 120),
            Description = dto.Description.NormalizeRequiredText("Tədbir açıqlaması", 1500),
            GameName = dto.GameName.NormalizeOptionalText("Oyun adı", 120),
            Platform = dto.Platform.NormalizeOptionalText("Platforma", 80),
            JoinUrl = joinUrl
        };
    }

    private async Task EnsureCanViewAsync(int communityId, Guid? currentUserId, CancellationToken cancellationToken)
    {
        var canView = await _context.ForumCommunities.AsNoTracking().AnyAsync(x =>
            x.Id == communityId &&
            (x.Visibility != ForumCommunityVisibility.Private ||
             (currentUserId.HasValue && x.Members.Any(m => m.UserId == currentUserId.Value &&
                 (m.Status == ForumCommunityMembershipStatus.Active || m.Status == ForumCommunityMembershipStatus.Invited)))),
            cancellationToken);
        if (!canView)
            throw new NotFoundException("İcma tapılmadı.");
    }

    private async Task EnsureOwnerAsync(int communityId, Guid userId, CancellationToken cancellationToken)
    {
        var ownerId = await _context.ForumCommunities.AsNoTracking()
            .Where(x => x.Id == communityId)
            .Select(x => (Guid?)x.OwnerId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İcma tapılmadı.");
        if (ownerId != userId)
            throw new ForbiddenException("Bu əməliyyatı yalnız icma sahibi edə bilər.");
    }

    private async Task EnsureModeratorAsync(int communityId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await IsModeratorAsync(communityId, userId, cancellationToken))
            throw new ForbiddenException("Bu əməliyyatı yalnız icma sahibi və ya moderator edə bilər.");
    }

    private Task<bool> IsModeratorAsync(int communityId, Guid userId, CancellationToken cancellationToken) =>
        _context.ForumCommunityMembers.AsNoTracking().AnyAsync(x =>
            x.CommunityId == communityId && x.UserId == userId &&
            x.Status == ForumCommunityMembershipStatus.Active &&
            (x.Role == ForumCommunityMemberRole.Owner || x.Role == ForumCommunityMemberRole.Moderator), cancellationToken);

    private async Task<ForumCommunityMember> EnsureActiveMemberAsync(int communityId, Guid userId, CancellationToken cancellationToken)
    {
        return await _context.ForumCommunityMembers.FirstOrDefaultAsync(x =>
            x.CommunityId == communityId && x.UserId == userId &&
            x.Status == ForumCommunityMembershipStatus.Active, cancellationToken)
            ?? throw new ForbiddenException("Bu əməliyyat üçün icmanın aktiv üzvü olmalısınız.");
    }

    private static System.Linq.Expressions.Expression<Func<ForumCommunityMember, ForumCommunityMemberSearchDTO>> MemberProjection() =>
        x => new ForumCommunityMemberSearchDTO
        {
            MembershipId = x.Id,
            UserId = x.UserId,
            UserName = x.User.UserName ?? string.Empty,
            ProfileImageUrl = x.User.ProfileImageUrl,
            Role = x.Role,
            FlairId = x.FlairId,
            FlairName = x.Flair != null ? x.Flair.Name : null,
            FlairBackgroundColor = x.Flair != null ? x.Flair.BackgroundColor : null,
            FlairTextColor = x.Flair != null ? x.Flair.TextColor : null,
            JoinedAt = x.CreatedAt
        };

    private static string NormalizeColor(string? value, string fieldName)
    {
        var normalized = value.NormalizeRequiredText(fieldName, 7);
        if (!HexColorRegex.IsMatch(normalized))
            throw new BadRequestException($"{fieldName} #RRGGBB formatında olmalıdır.");
        return normalized.ToUpperInvariant();
    }
}
