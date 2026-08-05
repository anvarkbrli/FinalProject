using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.API.BackgroundServices;

public sealed class BackgroundJobCleanupWorker : BackgroundService
{
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
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(12));

        do
        {
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
                _logger.LogError(exception, "Background job cleanup failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
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
}
