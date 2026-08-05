using Ludora.Application.Interfaces;

namespace Ludora.API.BackgroundServices;

public sealed class BadgeEvaluationWorker : BackgroundService
{
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
            var lease = await _queue.TryDequeueAsync(cancellationToken);
            if (lease is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                continue;
            }

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

                await _queue.FailAsync(
                    lease.JobId,
                    exception.Message,
                    CancellationToken.None);
            }
        }
    }

    private async Task RunScheduledSweepAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));

        do
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
                    break;

                skip += userIds.Count;
                await Task.Delay(100, cancellationToken);
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
    }
}
