using Ludora.API.Configuration;
using Ludora.Application.Settings;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ludora.API.HealthChecks;

public sealed class StorageHealthCheck : IHealthCheck
{
    private readonly FileStorageSettings _storageSettings;
    private readonly DataProtectionSettings _dataProtectionSettings;

    public StorageHealthCheck(
        FileStorageSettings storageSettings,
        DataProtectionSettings dataProtectionSettings)
    {
        _storageSettings = storageSettings;
        _dataProtectionSettings = dataProtectionSettings;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var directories = new Dictionary<string, string>
        {
            ["public"] = _storageSettings.WebRootPath,
            ["private"] = _storageSettings.PrivateStoragePath,
            ["dataProtection"] = _dataProtectionSettings.KeyRingPath
        };

        try
        {
            foreach (var pair in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(pair.Value);
                VerifyWritable(pair.Value);
            }

            return Task.FromResult(
                HealthCheckResult.Healthy(
                    "Persistent storage directories are writable.",
                    directories.ToDictionary(
                        pair => pair.Key,
                        pair => (object)pair.Value)));
        }
        catch (Exception exception)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "A persistent storage directory is unavailable or not writable.",
                    exception,
                    directories.ToDictionary(
                        pair => pair.Key,
                        pair => (object)pair.Value)));
        }
    }

    private static void VerifyWritable(string directory)
    {
        var path = Path.Combine(
            directory,
            $".wellplayd-health-{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(path, "ok");
            _ = File.ReadAllText(path);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
