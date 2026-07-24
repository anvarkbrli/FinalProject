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
        await foreach (var userId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var steamService = scope.ServiceProvider.GetRequiredService<ISteamService>();
                await steamService.SyncOwnedGamesAsync(userId, stoppingToken);
                _badgeQueue.Enqueue(userId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Background Steam sync failed for user {UserId}.",
                    userId);
            }
        }
    }
}
