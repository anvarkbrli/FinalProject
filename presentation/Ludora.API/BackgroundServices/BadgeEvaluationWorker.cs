using Ludora.Application.Interfaces;

namespace Ludora.API.BackgroundServices;

public sealed class BadgeEvaluationWorker : BackgroundService
{
    private static readonly TimeSpan EmptyQueueDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DatabaseRetryDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan InitialSweepDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(30);

    private readonly IBadgeEvaluationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BadgeEvaluationWorker> _logger;

    public BadgeEvaluationWorker(
        IBadgeEvaluationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BadgeEvaluationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(
            ProcessQueueAsync(stoppingToken),
            RunScheduledSweepAsync(stoppingToken));

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var lease = await _queue.TryDequeueAsync(cancellationToken);
                if (lease is null)
                {
                    await Task.Delay(EmptyQueueDelay, cancellationToken);
                    continue;
                }

                await ProcessLeaseAsync(lease, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Badge background queue is temporarily unavailable. The API will stay online and retry in {RetrySeconds} seconds. Ensure the latest database migrations are applied.",
                    DatabaseRetryDelay.TotalSeconds);

                await DelayAfterFailureAsync(DatabaseRetryDelay, cancellationToken);
            }
        }
    }

    private async Task ProcessLeaseAsync(
        BackgroundJobLease lease,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IBadgeService>();
            await service.EvaluateUserAsync(lease.UserId, cancellationToken);
            await _queue.CompleteAsync(lease.JobId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Badge evaluation failed for user {UserId}; durable job {JobId} will be retried.",
                lease.UserId,
                lease.JobId);

            try
            {
                await _queue.FailAsync(
                    lease.JobId,
                    exception.Message,
                    CancellationToken.None);
            }
            catch (Exception persistenceException)
            {
                _logger.LogError(
                    persistenceException,
                    "Could not persist the failed state for badge job {JobId}. Its lease will expire and be recovered later.",
                    lease.JobId);
            }
        }
    }

    private async Task RunScheduledSweepAsync(CancellationToken cancellationToken)
    {
        if (!await DelayAfterFailureAsync(InitialSweepDelay, cancellationToken))
            return;

        while (!cancellationToken.IsCancellationRequested)
        {
            var nextDelay = SweepInterval;
            try
            {
                await EnqueueSweepAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                nextDelay = DatabaseRetryDelay;
                _logger.LogError(
                    exception,
                    "Scheduled badge sweep failed. The API will stay online and the sweep will retry in {RetrySeconds} seconds.",
                    DatabaseRetryDelay.TotalSeconds);
            }

            if (!await DelayAfterFailureAsync(nextDelay, cancellationToken))
                return;
        }
    }

    private async Task EnqueueSweepAsync(CancellationToken cancellationToken)
    {
        var skip = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IBadgeService>();
            var userIds = await service.GetUserBatchAsync(
                skip,
                100,
                cancellationToken);

            foreach (var userId in userIds)
                await _queue.EnqueueAsync(userId, cancellationToken);

            if (userIds.Count < 100)
                return;

            skip += userIds.Count;
            await Task.Delay(100, cancellationToken);
        }
    }

    private static async Task<bool> DelayAfterFailureAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
