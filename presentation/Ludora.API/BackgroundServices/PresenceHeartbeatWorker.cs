using Ludora.API.Configuration;
using Ludora.API.Hubs;
using Microsoft.Extensions.Options;

namespace Ludora.API.BackgroundServices;

public sealed class PresenceHeartbeatWorker : BackgroundService
{
    private readonly RedisUserPresenceTracker _tracker;
    private readonly PresenceSettings _settings;
    private readonly ILogger<PresenceHeartbeatWorker> _logger;

    public PresenceHeartbeatWorker(
        RedisUserPresenceTracker tracker,
        IOptions<PresenceSettings> settings,
        ILogger<PresenceHeartbeatWorker> logger)
    {
        _tracker = tracker;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_settings.HeartbeatSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await _tracker.RefreshLocalConnectionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unexpected distributed presence heartbeat failure.");
            }
        }
    }
}
