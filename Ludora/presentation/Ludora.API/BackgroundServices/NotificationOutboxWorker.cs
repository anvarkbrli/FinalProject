using Ludora.API.Hubs;
using Ludora.Application.DTOs.Notification;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Ludora.API.BackgroundServices;

public sealed class NotificationOutboxWorker : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ProcessedMessageRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationOutboxWorker> _logger;
    private DateTime _nextCleanupAtUtc = DateTime.MinValue;

    public NotificationOutboxWorker(
        IServiceScopeFactory scopeFactory,
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await ProcessBatchAsync(stoppingToken);

                if (processedCount == 0)
                    await Task.Delay(IdleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Notification outbox işlənərkən gözlənilməz xəta baş verdi.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        if (now >= _nextCleanupAtUtc)
        {
            await CleanupProcessedMessagesAsync(context, now, cancellationToken);
            _nextCleanupAtUtc = now.Add(CleanupInterval);
        }

        var candidateIds = await context.NotificationOutboxMessages
            .AsNoTracking()
            .Where(x =>
                x.ProcessedAtUtc == null &&
                (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now) &&
                (x.LockedUntilUtc == null || x.LockedUntilUtc < now))
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => x.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
            return 0;

        var lockToken = Guid.NewGuid();
        var lockedUntilUtc = now.Add(LockDuration);

        await context.NotificationOutboxMessages
            .Where(x =>
                candidateIds.Contains(x.Id) &&
                x.ProcessedAtUtc == null &&
                (x.LockedUntilUtc == null || x.LockedUntilUtc < now))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.LockToken, lockToken)
                    .SetProperty(x => x.LockedUntilUtc, lockedUntilUtc),
                cancellationToken);

        var messages = await context.NotificationOutboxMessages
            .Include(x => x.Notification)
                .ThenInclude(x => x.ActorUser)
            .Where(x => x.LockToken == lockToken && x.ProcessedAtUtc == null)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var dto = Map(message.Notification);

                await _hubContext.Clients
                    .User(message.Notification.RecipientUserId.ToString())
                    .SendAsync(message.EventName, dto, cancellationToken);

                message.ProcessedAtUtc = DateTime.UtcNow;
                message.LastError = null;
                message.LockToken = null;
                message.LockedUntilUtc = null;
            }
            catch (Exception exception)
            {
                message.AttemptCount++;
                message.LastError = Truncate(exception.Message, 2000);
                message.NextAttemptAtUtc = DateTime.UtcNow.Add(GetRetryDelay(message.AttemptCount));
                message.LockToken = null;
                message.LockedUntilUtc = null;

                _logger.LogWarning(
                    exception,
                    "Notification {NotificationId} real-time göndərilmədi. Cəhd: {AttemptCount}",
                    message.NotificationId,
                    message.AttemptCount);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }

    private async Task CleanupProcessedMessagesAsync(
        AppDbContext context,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var cutoff = now.Subtract(ProcessedMessageRetention);

        var deletedCount = await context.NotificationOutboxMessages
            .Where(x => x.ProcessedAtUtc != null && x.ProcessedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "{DeletedCount} işlənmiş notification outbox qeydi təmizləndi.",
                deletedCount);
        }
    }

    private static NotificationDTO Map(Notification notification)
    {
        return new NotificationDTO
        {
            Id = notification.Id,
            Type = notification.Type,
            Priority = notification.Priority,
            EntityType = notification.EntityType,
            EntityId = notification.EntityId,
            ActorUserId = notification.ActorUserId,
            ActorUserName = notification.ActorUser?.UserName,
            ActorProfileImageUrl = notification.ActorUser?.ProfileImageUrl,
            Title = notification.Title,
            Message = notification.Message,
            ActionUrl = notification.ActionUrl,
            DataJson = notification.DataJson,
            IsAdminNotification = notification.Type is
                NotificationType.AnnouncementPublished or
                NotificationType.WarningIssued,
            IsRead = notification.IsRead,
            ReadAtUtc = notification.ReadAtUtc,
            CreatedAtUtc = notification.CreatedAtUtc
        };
    }

    private static TimeSpan GetRetryDelay(int attemptCount)
    {
        var seconds = Math.Min(600, 5 * Math.Pow(2, Math.Min(attemptCount, 7)));
        return TimeSpan.FromSeconds(seconds);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
