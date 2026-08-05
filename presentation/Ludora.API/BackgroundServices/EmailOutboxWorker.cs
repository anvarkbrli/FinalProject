using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ludora.API.BackgroundServices;

public sealed class EmailOutboxWorker : BackgroundService
{
    private const int BatchSize = 25;
    private const int MaxAttempts = 10;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DisabledDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ProcessedRetention = TimeSpan.FromDays(14);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<EmailSettings> _settings;
    private readonly ILogger<EmailOutboxWorker> _logger;
    private DateTime _nextCleanupAtUtc = DateTime.MinValue;

    public EmailOutboxWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<EmailSettings> settings,
        ILogger<EmailOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_settings.CurrentValue.Enabled)
                {
                    await Task.Delay(DisabledDelay, stoppingToken);
                    continue;
                }

                var processed = await ProcessBatchAsync(stoppingToken);
                if (processed == 0)
                    await Task.Delay(IdleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Email outbox işlənərkən gözlənilməz xəta baş verdi.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transport = scope.ServiceProvider.GetRequiredService<IEmailTransport>();
        var now = DateTime.UtcNow;

        if (now >= _nextCleanupAtUtc)
        {
            await CleanupAsync(context, now, cancellationToken);
            _nextCleanupAtUtc = now.Add(CleanupInterval);
        }

        var processedCount = 0;
        for (var index = 0; index < BatchSize; index++)
        {
            var message = await ClaimNextAsync(context, cancellationToken);
            if (message is null)
                break;

            await ProcessMessageAsync(
                context,
                transport,
                message,
                cancellationToken);
            processedCount++;
            context.ChangeTracker.Clear();
        }

        return processedCount;
    }

    private static async Task<EmailOutboxMessage?> ClaimNextAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var now = DateTime.UtcNow;
            var candidateId = await context.EmailOutboxMessages
                .AsNoTracking()
                .Where(message =>
                    message.ProcessedAtUtc == null &&
                    message.DeadLetteredAtUtc == null &&
                    (message.NextAttemptAtUtc == null || message.NextAttemptAtUtc <= now) &&
                    (message.LockedUntilUtc == null || message.LockedUntilUtc < now))
                .OrderBy(message => message.CreatedAtUtc)
                .Select(message => (long?)message.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (!candidateId.HasValue)
                return null;

            var lockToken = Guid.NewGuid();
            var lockedUntilUtc = now.Add(LockDuration);
            var updatedRows = await context.EmailOutboxMessages
                .Where(message =>
                    message.Id == candidateId.Value &&
                    message.ProcessedAtUtc == null &&
                    message.DeadLetteredAtUtc == null &&
                    (message.NextAttemptAtUtc == null || message.NextAttemptAtUtc <= now) &&
                    (message.LockedUntilUtc == null || message.LockedUntilUtc < now))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(message => message.LockToken, lockToken)
                        .SetProperty(message => message.LockedUntilUtc, lockedUntilUtc),
                    cancellationToken);

            if (updatedRows == 0)
                continue;

            return await context.EmailOutboxMessages
                .SingleAsync(
                    message => message.Id == candidateId.Value &&
                               message.LockToken == lockToken,
                    cancellationToken);
        }

        return null;
    }

    private async Task ProcessMessageAsync(
        AppDbContext context,
        IEmailTransport transport,
        EmailOutboxMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await SendAsync(transport, message, cancellationToken);
            await UpdateContactMessageStatusAsync(
                context,
                message,
                ContactMessageStatus.Replied,
                cancellationToken);
            message.ProcessedAtUtc = DateTime.UtcNow;
            message.LastError = null;
            message.NextAttemptAtUtc = null;
            message.LockToken = null;
            message.LockedUntilUtc = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            message.AttemptCount++;
            message.LastError = Truncate(exception.Message, 2000);
            message.LockToken = null;
            message.LockedUntilUtc = null;

            if (message.AttemptCount >= MaxAttempts)
            {
                message.DeadLetteredAtUtc = DateTime.UtcNow;
                message.NextAttemptAtUtc = null;
                await UpdateContactMessageStatusAsync(
                    context,
                    message,
                    ContactMessageStatus.ReplyFailed,
                    cancellationToken);
                _logger.LogError(
                    exception,
                    "Email outbox mesajı {MessageId} dead-letter edildi.",
                    message.Id);
            }
            else
            {
                message.NextAttemptAtUtc = DateTime.UtcNow.Add(
                    GetRetryDelay(message.AttemptCount));
                _logger.LogWarning(
                    exception,
                    "Email outbox mesajı {MessageId} göndərilmədi. Cəhd: {AttemptCount}.",
                    message.Id,
                    message.AttemptCount);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Task SendAsync(
        IEmailTransport transport,
        EmailOutboxMessage message,
        CancellationToken cancellationToken) =>
        message.TemplateType switch
        {
            EmailTemplateType.EmailConfirmation =>
                transport.SendEmailConfirmationAsync(
                    message.RecipientEmail,
                    message.UserName,
                    message.ActionUrl,
                    cancellationToken),
            EmailTemplateType.EmailChangeConfirmation =>
                transport.SendEmailChangeConfirmationAsync(
                    message.RecipientEmail,
                    message.UserName,
                    message.ActionUrl,
                    cancellationToken),
            EmailTemplateType.PasswordReset =>
                transport.SendPasswordResetAsync(
                    message.RecipientEmail,
                    message.UserName,
                    message.ActionUrl,
                    cancellationToken),
            EmailTemplateType.ContactMessageReply =>
                transport.SendCustomEmailAsync(
                    message.RecipientEmail,
                    message.EmailSubject ?? throw new InvalidOperationException("Contact reply subject is missing."),
                    message.PlainTextBody ?? throw new InvalidOperationException("Contact reply plain-text body is missing."),
                    message.HtmlBody ?? throw new InvalidOperationException("Contact reply HTML body is missing."),
                    cancellationToken),
            _ => throw new InvalidOperationException(
                $"Dəstəklənməyən email template tipi: {message.TemplateType}")
        };

    private static async Task UpdateContactMessageStatusAsync(
        AppDbContext context,
        EmailOutboxMessage message,
        ContactMessageStatus status,
        CancellationToken cancellationToken)
    {
        if (!message.ContactMessageId.HasValue)
            return;

        var contactMessage = await context.ContactMessages
            .FirstOrDefaultAsync(
                x => x.Id == message.ContactMessageId.Value,
                cancellationToken);

        if (contactMessage is null)
            return;

        var now = DateTime.UtcNow;
        contactMessage.Status = status;
        contactMessage.UpdatedAtUtc = now;

        if (status == ContactMessageStatus.Replied)
        {
            contactMessage.RepliedAtUtc = now;
            contactMessage.ClosedAtUtc = null;
        }
        else if (status == ContactMessageStatus.ReplyFailed)
        {
            contactMessage.RepliedAtUtc = null;
        }
    }

    private static TimeSpan GetRetryDelay(int attemptCount)
    {
        var seconds = Math.Min(3600, 15 * Math.Pow(2, Math.Min(attemptCount, 8)));
        return TimeSpan.FromSeconds(seconds);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static async Task CleanupAsync(
        AppDbContext context,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var cutoff = now.Subtract(ProcessedRetention);
        await context.EmailOutboxMessages
            .Where(message =>
                message.ProcessedAtUtc != null &&
                message.ProcessedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
