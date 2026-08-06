using Ludora.Application.Settings;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.Email
{
    internal sealed class EmailConfigurationLogger : IHostedService
    {
        private readonly EmailSettings _settings;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<EmailConfigurationLogger> _logger;

        public EmailConfigurationLogger(
            IOptions<EmailSettings> options,
            IHostEnvironment environment,
            ILogger<EmailConfigurationLogger> logger)
        {
            _settings = options.Value;
            _environment = environment;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_settings.Enabled)
            {
                _logger.LogWarning(
                    "Email xidməti deaktivdir. Environment: {Environment}. " +
                    "Outbox-a əlavə edilən emaillər göndərilmədən növbədə qalacaq. User-secrets və ya environment variable-ları yoxlayın.",
                    _environment.EnvironmentName);

                return Task.CompletedTask;
            }

            _logger.LogInformation(
                "Email xidməti aktivdir. Environment: {Environment}; SMTP: {Host}:{Port}; " +
                "SSL: {EnableSsl}; Auth: {AuthenticationConfigured}; From: {FromEmail}; " +
                "Frontend URL: {FrontendUrl}; Retry: {RetryCount}; Timeout: {TimeoutSeconds}s.",
                _environment.EnvironmentName,
                _settings.Host,
                _settings.Port,
                _settings.EnableSsl,
                !string.IsNullOrWhiteSpace(_settings.UserName),
                _settings.FromEmail,
                _settings.PublicFrontendBaseUrl,
                _settings.MaxRetryAttempts,
                _settings.SendTimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(_settings.UserName) &&
                _settings.UserName.Contains('@') &&
                !string.Equals(
                    _settings.UserName.Trim(),
                    _settings.FromEmail.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "SMTP istifadəçi adı ({UserName}) ilə FromEmail ({FromEmail}) fərqlidir. " +
                    "Provider alias icazəsi vermirsə məktub qəbul olunsa belə çatdırılmaya bilər.",
                    _settings.UserName,
                    _settings.FromEmail);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
