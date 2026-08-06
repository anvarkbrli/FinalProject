using System.Net.Sockets;
using System.Text;

using Ludora.Application.Settings;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ludora.API.HealthChecks
{
    public sealed class ClamAvHealthCheck : IHealthCheck
    {
        private readonly MalwareScanningSettings _settings;

        public ClamAvHealthCheck(MalwareScanningSettings settings)
        {
            _settings = settings;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            if (!_settings.Enabled)
                return HealthCheckResult.Healthy("Malware scanning is disabled for this environment.");

            try
            {
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
                timeoutSource.CancelAfter(TimeSpan.FromSeconds(5));

                using var client = new TcpClient();
                await client.ConnectAsync(
                    _settings.Host,
                    _settings.Port,
                    timeoutSource.Token);

                await using var stream = client.GetStream();
                var ping = Encoding.ASCII.GetBytes("zPING\0");
                await stream.WriteAsync(ping, timeoutSource.Token);
                await stream.FlushAsync(timeoutSource.Token);

                var buffer = new byte[16];
                var read = await stream.ReadAsync(buffer, timeoutSource.Token);
                var response = Encoding.ASCII.GetString(buffer, 0, read).TrimEnd('\0', '\r', '\n');

                return string.Equals(response, "PONG", StringComparison.OrdinalIgnoreCase)
                    ? HealthCheckResult.Healthy("ClamAV responded to PING.")
                    : HealthCheckResult.Unhealthy($"Unexpected ClamAV response: {response}");
            }
            catch (Exception exception) when (exception is not OperationCanceledException ||
                                               !cancellationToken.IsCancellationRequested)
            {
                return HealthCheckResult.Unhealthy(
                    "ClamAV is unavailable.",
                    exception);
            }
        }
    }
}
