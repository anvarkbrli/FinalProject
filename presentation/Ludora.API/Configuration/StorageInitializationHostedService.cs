using Ludora.Application.Settings;

namespace Ludora.API.Configuration;

public sealed class StorageInitializationHostedService : IHostedService
{
    private readonly FileStorageSettings _storageSettings;
    private readonly DataProtectionSettings _dataProtectionSettings;
    private readonly ILogger<StorageInitializationHostedService> _logger;

    public StorageInitializationHostedService(
        FileStorageSettings storageSettings,
        DataProtectionSettings dataProtectionSettings,
        ILogger<StorageInitializationHostedService> logger)
    {
        _storageSettings = storageSettings;
        _dataProtectionSettings = dataProtectionSettings;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var directories = new[]
        {
            _storageSettings.WebRootPath,
            Path.Combine(_storageSettings.WebRootPath, "uploads"),
            _storageSettings.PrivateStoragePath,
            Path.Combine(_storageSettings.PrivateStoragePath, "quarantine"),
            _dataProtectionSettings.KeyRingPath
        };

        foreach (var directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(directory);
            VerifyWritable(directory);
        }

        _logger.LogInformation(
            "Persistent storage initialized. PublicRoot: {PublicRoot}; PrivateRoot: {PrivateRoot}; DataProtectionKeys: {KeyRingPath}",
            _storageSettings.WebRootPath,
            _storageSettings.PrivateStoragePath,
            _dataProtectionSettings.KeyRingPath);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void VerifyWritable(string directory)
    {
        var probePath = Path.Combine(
            directory,
            $".wellplayd-write-probe-{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(probePath, "ok");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Storage directory is not writable: {directory}",
                exception);
        }
        finally
        {
            if (File.Exists(probePath))
                File.Delete(probePath);
        }
    }
}
