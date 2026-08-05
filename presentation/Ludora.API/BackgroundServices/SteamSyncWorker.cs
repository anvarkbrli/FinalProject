using Ludora.Application.Interfaces;

namespace Ludora.API.BackgroundServices;

public sealed class SteamSyncWorker : BackgroundService
{
    private static readonly TimeSpan EmptyQueueDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DatabaseRetryDelay = TimeSpan.FromSeconds(15);

    private readonly ISteamSyncQueue _queue;
    private readonly IBadgeEvaluationQueue _badgeQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SteamSyncWorker> _logger;

    public SteamSyncWorker(
        ISteamSyncQueue queue,
        IBadgeEvaluationQueue badgeQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<SteamSyncWorker> logger)
    {
        _queue = queue;
        _badgeQueue = badgeQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var lease = await _queue.TryDequeueAsync(stoppingToken);
                if (lease is null)
                {
                    await Task.Delay(EmptyQueueDelay, stoppingToken);
                    continue;
                }

                await ProcessLeaseAsync(lease, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Steam background queue is temporarily unavailable. The API will stay online and retry in {RetrySeconds} seconds. Ensure the latest database migrations are applied.",
                    DatabaseRetryDelay.TotalSeconds);

                if (!await DelayAfterFailureAsync(DatabaseRetryDelay, stoppingToken))
                    return;
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
            var steamService = scope.ServiceProvider.GetRequiredService<ISteamService>();
            await steamService.SyncOwnedGamesAsync(lease.UserId, cancellationToken);
            await _badgeQueue.EnqueueAsync(lease.UserId, cancellationToken);
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
                "Background Steam sync failed for user {UserId}; durable job {JobId} will be retried.",
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
                    "Could not persist the failed state for Steam job {JobId}. Its lease will expire and be recovered later.",
                    lease.JobId);
            }
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
