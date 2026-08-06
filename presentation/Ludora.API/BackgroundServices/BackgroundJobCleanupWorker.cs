using Ludora.Domain.Enums;
using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore;

namespace Ludora.API.BackgroundServices
{
    public sealed class BackgroundJobCleanupWorker : BackgroundService
    {
        private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(12);
        private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundJobCleanupWorker> _logger;

        public BackgroundJobCleanupWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<BackgroundJobCleanupWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!await DelayAsync(InitialDelay, stoppingToken))
                return;

            while (!stoppingToken.IsCancellationRequested)
            {
                var nextDelay = CleanupInterval;
                try
                {
                    await CleanupAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    nextDelay = FailureRetryDelay;
                    _logger.LogError(
                        exception,
                        "Background job cleanup failed. The API will stay online and cleanup will retry in {RetryMinutes} minute(s).",
                        FailureRetryDelay.TotalMinutes);
                }

                if (!await DelayAsync(nextDelay, stoppingToken))
                    return;
            }
        }

        private async Task CleanupAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var completedBefore = DateTime.UtcNow.AddDays(-7);
            var failedBefore = DateTime.UtcNow.AddDays(-30);

            var deleted = await dbContext.BackgroundJobs
                .Where(job =>
                    (job.Status == BackgroundJobStatus.Completed &&
                     job.CompletedAtUtc < completedBefore) ||
                    (job.Status == BackgroundJobStatus.Failed &&
                     job.FailedAtUtc < failedBefore))
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Removed {DeletedCount} expired background job records.",
                    deleted);
            }
        }

        private static async Task<bool> DelayAsync(
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
}
