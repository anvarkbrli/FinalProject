using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ludora.API.HealthChecks;

public sealed class DatabaseMigrationHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseMigrationHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pendingMigrations = (await dbContext.Database
                    .GetPendingMigrationsAsync(cancellationToken))
                .ToArray();

            return pendingMigrations.Length == 0
                ? HealthCheckResult.Healthy("Database schema is up to date.")
                : HealthCheckResult.Unhealthy(
                    "Database has pending migrations.",
                    data: new Dictionary<string, object>
                    {
                        ["pendingMigrations"] = pendingMigrations
                    });
        }
        catch (Exception exception) when (exception is not OperationCanceledException ||
                                           !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                "Database migration state could not be determined.",
                exception);
        }
    }
}
