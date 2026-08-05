using System.Data;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.API.BackgroundServices;

public abstract class DurableUserJobQueue
{
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private readonly BackgroundJobType _jobType;
    private readonly int _maxAttempts;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    protected DurableUserJobQueue(
        BackgroundJobType jobType,
        int maxAttempts,
        IServiceScopeFactory scopeFactory,
        ILogger logger)
    {
        _jobType = jobType;
        _maxAttempts = Math.Max(1, maxAttempts);
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected async Task<bool> EnqueueCoreAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            return false;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var alreadyQueued = await dbContext.BackgroundJobs
            .AsNoTracking()
            .AnyAsync(
                job => job.Type == _jobType &&
                       job.UserId == userId &&
                       (job.Status == BackgroundJobStatus.Pending ||
                        job.Status == BackgroundJobStatus.Processing),
                cancellationToken);

        if (alreadyQueued)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var now = DateTime.UtcNow;
        dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = _jobType,
            UserId = userId,
            Status = BackgroundJobStatus.Pending,
            AttemptCount = 0,
            MaxAttempts = _maxAttempts,
            CreatedAtUtc = now,
            AvailableAtUtc = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    protected async Task<BackgroundJobLease?> TryDequeueCoreAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var now = DateTime.UtcNow;

        var staleJobs = await dbContext.BackgroundJobs
            .Where(job => job.Type == _jobType &&
                          job.Status == BackgroundJobStatus.Processing &&
                          job.LockedUntilUtc != null &&
                          job.LockedUntilUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (var staleJob in staleJobs)
        {
            if (staleJob.AttemptCount >= staleJob.MaxAttempts)
            {
                staleJob.Status = BackgroundJobStatus.Failed;
                staleJob.FailedAtUtc = now;
                staleJob.LastError = "Job lock expired after the maximum retry count.";
            }
            else
            {
                staleJob.Status = BackgroundJobStatus.Pending;
                staleJob.AvailableAtUtc = now;
                staleJob.LastError = "Previous worker stopped before completing the job.";
            }

            staleJob.LockToken = null;
            staleJob.LockedUntilUtc = null;
        }

        var job = await dbContext.BackgroundJobs
            .Where(item => item.Type == _jobType &&
                           item.Status == BackgroundJobStatus.Pending &&
                           item.AvailableAtUtc <= now)
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            if (staleJobs.Count > 0)
                await dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        job.Status = BackgroundJobStatus.Processing;
        job.AttemptCount++;
        job.StartedAtUtc = now;
        job.LockToken = Guid.NewGuid();
        job.LockedUntilUtc = now.Add(LockDuration);
        job.LastError = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BackgroundJobLease(job.Id, job.UserId, job.AttemptCount);
    }

    protected async Task CompleteCoreAsync(
        long jobId,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await dbContext.BackgroundJobs
            .FirstOrDefaultAsync(
                item => item.Id == jobId && item.Type == _jobType,
                cancellationToken);

        if (job is null)
            return;

        job.Status = BackgroundJobStatus.Completed;
        job.CompletedAtUtc = DateTime.UtcNow;
        job.LockToken = null;
        job.LockedUntilUtc = null;
        job.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    protected async Task FailCoreAsync(
        long jobId,
        string error,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await dbContext.BackgroundJobs
            .FirstOrDefaultAsync(
                item => item.Id == jobId && item.Type == _jobType,
                cancellationToken);

        if (job is null)
            return;

        var now = DateTime.UtcNow;
        job.LastError = Truncate(error, 2000);
        job.LockToken = null;
        job.LockedUntilUtc = null;

        if (job.AttemptCount >= job.MaxAttempts)
        {
            job.Status = BackgroundJobStatus.Failed;
            job.FailedAtUtc = now;
            _logger.LogError(
                "Background job {JobId} ({JobType}) was dead-lettered after {AttemptCount} attempts.",
                job.Id,
                _jobType,
                job.AttemptCount);
        }
        else
        {
            job.Status = BackgroundJobStatus.Pending;
            job.AvailableAtUtc = now.Add(GetRetryDelay(job.AttemptCount));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TimeSpan GetRetryDelay(int attemptCount)
    {
        var seconds = Math.Min(900, 5 * Math.Pow(2, Math.Max(0, attemptCount - 1)));
        return TimeSpan.FromSeconds(seconds);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public sealed class BadgeEvaluationQueue : DurableUserJobQueue, IBadgeEvaluationQueue
{
    public BadgeEvaluationQueue(
        IServiceScopeFactory scopeFactory,
        ILogger<BadgeEvaluationQueue> logger)
        : base(
            BackgroundJobType.BadgeEvaluation,
            maxAttempts: 5,
            scopeFactory,
            logger)
    {
    }

    public Task<bool> EnqueueAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        EnqueueCoreAsync(userId, cancellationToken);

    public Task<BackgroundJobLease?> TryDequeueAsync(
        CancellationToken cancellationToken = default) =>
        TryDequeueCoreAsync(cancellationToken);

    public Task CompleteAsync(
        long jobId,
        CancellationToken cancellationToken = default) =>
        CompleteCoreAsync(jobId, cancellationToken);

    public Task FailAsync(
        long jobId,
        string error,
        CancellationToken cancellationToken = default) =>
        FailCoreAsync(jobId, error, cancellationToken);

}

public sealed class SteamSyncQueue : DurableUserJobQueue, ISteamSyncQueue
{
    public SteamSyncQueue(
        IServiceScopeFactory scopeFactory,
        ILogger<SteamSyncQueue> logger)
        : base(
            BackgroundJobType.SteamSync,
            maxAttempts: 5,
            scopeFactory,
            logger)
    {
    }

    public Task<bool> EnqueueAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        EnqueueCoreAsync(userId, cancellationToken);

    public Task<BackgroundJobLease?> TryDequeueAsync(
        CancellationToken cancellationToken = default) =>
        TryDequeueCoreAsync(cancellationToken);

    public Task CompleteAsync(
        long jobId,
        CancellationToken cancellationToken = default) =>
        CompleteCoreAsync(jobId, cancellationToken);

    public Task FailAsync(
        long jobId,
        string error,
        CancellationToken cancellationToken = default) =>
        FailCoreAsync(jobId, error, cancellationToken);

}
