using Ludora.Application.Interfaces;

namespace Ludora.API.BackgroundServices;

public sealed class SteamSyncWorker : BackgroundService
{
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
            var lease = await _queue.TryDequeueAsync(stoppingToken);
            if (lease is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var steamService = scope.ServiceProvider.GetRequiredService<ISteamService>();
                await steamService.SyncOwnedGamesAsync(lease.UserId, stoppingToken);
                await _badgeQueue.EnqueueAsync(lease.UserId, stoppingToken);
                await _queue.CompleteAsync(lease.JobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
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

                await _queue.FailAsync(
                    lease.JobId,
                    exception.Message,
                    CancellationToken.None);
            }
        }
    }
}
