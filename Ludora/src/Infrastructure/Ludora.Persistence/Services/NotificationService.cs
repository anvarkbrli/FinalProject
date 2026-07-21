using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Notification;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Notification;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class NotificationService : INotificationService
{
    private const int MaxPageSize = 100;

    private static readonly NotificationType[] ConfigurableTypes =
    [
        NotificationType.FollowingPostCreated,
        NotificationType.FollowingReviewCreated,
        NotificationType.FollowingGameStatusChanged,
        NotificationType.FollowingPublicListCreated,
        NotificationType.FollowingDiscussionCreated,
        NotificationType.PostLiked,
        NotificationType.PostCommented,
        NotificationType.ReviewLiked,
        NotificationType.ReviewCommented,
        NotificationType.GameListLiked,
        NotificationType.GameListCommented,
        NotificationType.DiscussionLiked,
        NotificationType.DiscussionReplied,
        NotificationType.DiscussionReplyLiked,
        NotificationType.FollowRequestReceived,
        NotificationType.UserFollowed,
        NotificationType.FollowRequestAccepted
    ];

    private readonly AppDbContext _context;

    public NotificationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDTO<NotificationDTO>> GetMineAsync(
        Guid userId,
        NotificationFilterDTO filter,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ArgumentNullException.ThrowIfNull(filter);
        ValidatePagination(filter.PageNumber, filter.PageSize);

        var query = _context.Notifications
            .AsNoTracking()
            .Where(x => x.RecipientUserId == userId);

        if (filter.IsRead.HasValue)
            query = query.Where(x => x.IsRead == filter.IsRead.Value);

        if (filter.Type.HasValue)
            query = query.Where(x => x.Type == filter.Type.Value);

        if (filter.Priority.HasValue)
            query = query.Where(x => x.Priority == filter.Priority.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await Project(query)
            .OrderBy(x => x.IsRead)
            .ThenByDescending(x => x.Priority)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDTO<NotificationDTO>
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

    public async Task<UnreadNotificationCountDTO> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        var count = await _context.Notifications
            .AsNoTracking()
            .CountAsync(
                x => x.RecipientUserId == userId && !x.IsRead,
                cancellationToken);

        return new UnreadNotificationCountDTO { Count = count };
    }

    public async Task<NotificationDTO> MarkAsReadAsync(
        Guid userId,
        long notificationId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateNotificationId(notificationId);

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(
                x => x.Id == notificationId && x.RecipientUserId == userId,
                cancellationToken)
            ?? throw new NotFoundException("Bildiriş tapılmadı.");

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await Project(
                _context.Notifications
                    .AsNoTracking()
                    .Where(x => x.Id == notificationId))
            .FirstAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var now = DateTime.UtcNow;

        await _context.Notifications
            .Where(x => x.RecipientUserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.IsRead, true)
                    .SetProperty(x => x.ReadAtUtc, now),
                cancellationToken);
    }

    public async Task DeleteAsync(
        Guid userId,
        long notificationId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateNotificationId(notificationId);

        var affected = await _context.Notifications
            .Where(x => x.Id == notificationId && x.RecipientUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (affected == 0)
            throw new NotFoundException("Bildiriş tapılmadı.");
    }

    public async Task<NotificationSettingsDTO> GetSettingsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserExistsAsync(userId, cancellationToken);

        var overrides = await _context.NotificationPreferences
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.Type, x => x.Audience, cancellationToken);

        return new NotificationSettingsDTO
        {
            Items = ConfigurableTypes
                .Select(type => new NotificationPreferenceDTO
                {
                    Type = type,
                    Audience = overrides.GetValueOrDefault(type, GetDefaultAudience(type)),
                    IsOverridden = overrides.ContainsKey(type)
                })
                .ToArray()
        };
    }

    public async Task<NotificationPreferenceDTO> UpdatePreferenceAsync(
        Guid userId,
        NotificationType type,
        UpdateNotificationPreferenceDTO dto,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserExistsAsync(userId, cancellationToken);
        ArgumentNullException.ThrowIfNull(dto);

        if (!ConfigurableTypes.Contains(type))
            throw new BadRequestException("Bu bildiriş növü istifadəçi tərəfindən dəyişdirilə bilməz.");

        if (!Enum.IsDefined(dto.Audience))
            throw new BadRequestException("Bildiriş auditoriyası düzgün deyil.");

        var preference = await _context.NotificationPreferences
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.Type == type,
                cancellationToken);

        if (preference is null)
        {
            preference = new NotificationPreference
            {
                UserId = userId,
                Type = type,
                Audience = dto.Audience,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _context.NotificationPreferences.AddAsync(preference, cancellationToken);
        }
        else
        {
            preference.Audience = dto.Audience;
            preference.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new NotificationPreferenceDTO
        {
            Type = type,
            Audience = dto.Audience,
            IsOverridden = true
        };
    }

    public async Task ResetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserExistsAsync(userId, cancellationToken);

        await _context.NotificationPreferences
            .Where(x => x.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task QueueAsync(
        NotificationCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUserId(request.RecipientUserId);

        if (request.ActorUserId == request.RecipientUserId)
            return;

        if (!request.BypassPreferences &&
            !await ShouldDeliverAsync(
                request.RecipientUserId,
                request.ActorUserId,
                request.Type,
                cancellationToken))
        {
            return;
        }

        AddNotification(request);
    }

    public async Task QueueForFollowersAsync(
        FollowerNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUserId(request.ActorUserId);

        if (!ConfigurableTypes.Contains(request.Type))
            throw new BadRequestException("İzləyicilərə göndərilən bildiriş növü düzgün deyil.");

        var recipients = await _context.UserFollows
            .AsNoTracking()
            .Where(x =>
                x.FollowingId == request.ActorUserId &&
                x.Status == FollowStatus.Accepted &&
                !_context.UserBlocks.Any(block =>
                    (block.BlockerId == request.ActorUserId && block.BlockedUserId == x.FollowerId) ||
                    (block.BlockerId == x.FollowerId && block.BlockedUserId == request.ActorUserId)))
            .Select(x => new
            {
                RecipientUserId = x.FollowerId,
                Audience = _context.NotificationPreferences
                    .Where(preference =>
                        preference.UserId == x.FollowerId &&
                        preference.Type == request.Type)
                    .Select(preference => (NotificationAudience?)preference.Audience)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        foreach (var recipient in recipients)
        {
            var audience = recipient.Audience ?? GetDefaultAudience(request.Type);

            if (audience == NotificationAudience.Never)
                continue;

            AddNotification(new NotificationCreateRequest
            {
                RecipientUserId = recipient.RecipientUserId,
                ActorUserId = request.ActorUserId,
                Type = request.Type,
                Priority = request.Priority,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                Title = request.Title,
                Message = request.Message,
                ActionUrl = request.ActionUrl,
                DataJson = request.DataJson,
                BypassPreferences = true
            });
        }
    }

    public async Task QueueAnnouncementAsync(
        Guid adminUserId,
        int announcementId,
        string title,
        string content,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(adminUserId);

        if (announcementId <= 0)
            throw new BadRequestException("Elan ID-si düzgün deyil.");

        var normalizedTitle = NormalizeRequired(title, 200, "Bildiriş başlığı");
        var normalizedMessage = NormalizeRequired(content, 1000, "Bildiriş mətni");
        var entityId = announcementId.ToString();
        var now = DateTime.UtcNow;
        var type = (int)NotificationType.AnnouncementPublished;
        var priority = (int)NotificationPriority.Critical;
        var entityType = (int)NotificationEntityType.Announcement;

        await _context.Database.ExecuteSqlInterpolatedAsync($@"
DECLARE @InsertedNotifications TABLE
(
    [Id] bigint NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL
);

INSERT INTO [Notifications]
(
    [RecipientUserId],
    [ActorUserId],
    [Type],
    [Priority],
    [EntityType],
    [EntityId],
    [Title],
    [Message],
    [ActionUrl],
    [DataJson],
    [IsRead],
    [ReadAtUtc],
    [CreatedAtUtc]
)
OUTPUT INSERTED.[Id], INSERTED.[CreatedAtUtc]
INTO @InsertedNotifications ([Id], [CreatedAtUtc])
SELECT
    [Id],
    CASE WHEN [Id] = {adminUserId} THEN NULL ELSE {adminUserId} END,
    {type},
    {priority},
    {entityType},
    {entityId},
    {normalizedTitle},
    {normalizedMessage},
    '/',
    NULL,
    CAST(0 AS bit),
    NULL,
    {now}
FROM [AspNetUsers];

INSERT INTO [NotificationOutboxMessages]
(
    [NotificationId],
    [EventName],
    [CreatedAtUtc],
    [ProcessedAtUtc],
    [AttemptCount],
    [NextAttemptAtUtc],
    [LastError],
    [LockToken],
    [LockedUntilUtc]
)
SELECT
    [Id],
    'NotificationCreated',
    [CreatedAtUtc],
    NULL,
    0,
    [CreatedAtUtc],
    NULL,
    NULL,
    NULL
FROM @InsertedNotifications;", cancellationToken);
    }

    private async Task<bool> ShouldDeliverAsync(
        Guid recipientUserId,
        Guid? actorUserId,
        NotificationType type,
        CancellationToken cancellationToken)
    {
        if (IsAdminNotification(type))
            return true;

        var audience = await _context.NotificationPreferences
            .AsNoTracking()
            .Where(x => x.UserId == recipientUserId && x.Type == type)
            .Select(x => (NotificationAudience?)x.Audience)
            .FirstOrDefaultAsync(cancellationToken)
            ?? GetDefaultAudience(type);

        if (audience == NotificationAudience.Never)
            return false;

        if (!actorUserId.HasValue)
            return true;

        var actorId = actorUserId.Value;

        var isBlocked = await _context.UserBlocks
            .AsNoTracking()
            .AnyAsync(x =>
                (x.BlockerId == recipientUserId && x.BlockedUserId == actorId) ||
                (x.BlockerId == actorId && x.BlockedUserId == recipientUserId),
                cancellationToken);

        if (isBlocked)
            return false;

        if (audience == NotificationAudience.Everyone)
            return true;

        var localAcceptedConnection = _context.UserFollows.Local.Any(x =>
            x.Status == FollowStatus.Accepted &&
            ((x.FollowerId == recipientUserId && x.FollowingId == actorId) ||
             (x.FollowerId == actorId && x.FollowingId == recipientUserId)));

        if (localAcceptedConnection)
            return true;

        return await _context.UserFollows
            .AsNoTracking()
            .AnyAsync(x =>
                x.Status == FollowStatus.Accepted &&
                ((x.FollowerId == recipientUserId && x.FollowingId == actorId) ||
                 (x.FollowerId == actorId && x.FollowingId == recipientUserId)),
                cancellationToken);
    }

    private void AddNotification(NotificationCreateRequest request)
    {
        var notification = new Notification
        {
            RecipientUserId = request.RecipientUserId,
            ActorUserId = request.ActorUserId,
            Type = request.Type,
            Priority = request.Priority,
            EntityType = request.EntityType,
            EntityId = NormalizeOptional(request.EntityId, 64),
            Title = NormalizeRequired(request.Title, 200, "Bildiriş başlığı"),
            Message = NormalizeRequired(request.Message, 1000, "Bildiriş mətni"),
            ActionUrl = NormalizeOptional(request.ActionUrl, 500),
            DataJson = string.IsNullOrWhiteSpace(request.DataJson)
                ? null
                : request.DataJson,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        var outboxMessage = new NotificationOutboxMessage
        {
            Notification = notification,
            EventName = "NotificationCreated",
            CreatedAtUtc = notification.CreatedAtUtc,
            NextAttemptAtUtc = notification.CreatedAtUtc
        };

        _context.Notifications.Add(notification);
        _context.NotificationOutboxMessages.Add(outboxMessage);
    }

    private async Task EnsureUserExistsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        ValidateUserId(userId);

        if (!await _context.Users.AsNoTracking().AnyAsync(x => x.Id == userId, cancellationToken))
            throw new NotFoundException("İstifadəçi tapılmadı.");
    }

    private static IQueryable<NotificationDTO> Project(IQueryable<Notification> query)
    {
        return query.Select(x => new NotificationDTO
        {
            Id = x.Id,
            Type = x.Type,
            Priority = x.Priority,
            EntityType = x.EntityType,
            EntityId = x.EntityId,
            ActorUserId = x.ActorUserId,
            ActorUserName = x.ActorUser == null ? null : x.ActorUser.UserName,
            ActorProfileImageUrl = x.ActorUser == null ? null : x.ActorUser.ProfileImageUrl,
            Title = x.Title,
            Message = x.Message,
            ActionUrl = x.ActionUrl,
            DataJson = x.DataJson,
            IsAdminNotification = x.Type == NotificationType.AnnouncementPublished ||
                                  x.Type == NotificationType.WarningIssued,
            IsRead = x.IsRead,
            ReadAtUtc = x.ReadAtUtc,
            CreatedAtUtc = x.CreatedAtUtc
        });
    }

    private static NotificationAudience GetDefaultAudience(NotificationType type)
    {
        return type switch
        {
            NotificationType.FollowingPostCreated or
            NotificationType.FollowingReviewCreated or
            NotificationType.FollowingGameStatusChanged or
            NotificationType.FollowingPublicListCreated or
            NotificationType.FollowingDiscussionCreated
                => NotificationAudience.ConnectionsOnly,

            NotificationType.AnnouncementPublished or
            NotificationType.WarningIssued
                => NotificationAudience.Everyone,

            _ => NotificationAudience.Everyone
        };
    }

    private static bool IsAdminNotification(NotificationType type)
    {
        return type is NotificationType.AnnouncementPublished or NotificationType.WarningIssued;
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BadRequestException($"{fieldName} boş ola bilməz.");

        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new BadRequestException("İstifadəçi ID-si düzgün deyil.");
    }

    private static void ValidateNotificationId(long notificationId)
    {
        if (notificationId <= 0)
            throw new BadRequestException("Bildiriş ID-si düzgün deyil.");
    }

    private static void ValidatePagination(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (pageSize is < 1 or > MaxPageSize)
            throw new BadRequestException($"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");
    }
}
