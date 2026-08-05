using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.API.Configuration;

public static class DatabaseMigrationRunner
{
    public static async Task ApplyPendingMigrationsAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseMigrationRunner");
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendingMigrations = (await dbContext.Database
                .GetPendingMigrationsAsync(cancellationToken))
            .ToArray();

        if (pendingMigrations.Length == 0)
        {
            logger.LogInformation("No pending database migrations were found.");
            return;
        }

        logger.LogInformation(
            "Applying {MigrationCount} pending database migrations: {Migrations}",
            pendingMigrations.Length,
            string.Join(", ", pendingMigrations));

        await dbContext.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Database migrations completed successfully.");
    }
}
