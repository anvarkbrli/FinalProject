using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.Encodings.Web;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.Email;

public sealed class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(
        IOptions<EmailSettings> options,
        ILogger<SmtpEmailService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public Task SendEmailConfirmationAsync(
        string email,
        string userName,
        string confirmationLink,
        CancellationToken cancellationToken = default)
    {
        var safeUserName = HtmlEncoder.Default.Encode(userName);
        var safeLink = HtmlEncoder.Default.Encode(confirmationLink);

        var htmlBody = $$"""
            <!doctype html>
            <html lang="az">
            <body style="margin:0;background:#f4f4f5;font-family:Arial,sans-serif;line-height:1.6;color:#1f2937">
                <div style="max-width:600px;margin:0 auto;padding:32px 18px">
                    <div style="background:#ffffff;border:1px solid #e4e4e7;border-radius:16px;padding:30px">
                        <h2 style="margin-top:0">Ludora hesabını təsdiqlə</h2>
                        <p>Salam, <strong>{{safeUserName}}</strong>.</p>
                        <p>Qeydiyyatı tamamlamaq üçün aşağıdakı düyməyə kliklə:</p>
                        <p style="margin:26px 0">
                            <a href="{{safeLink}}"
                               style="display:inline-block;padding:12px 18px;background:#6d28d9;color:#ffffff;text-decoration:none;border-radius:9px;font-weight:700">
                                Email ünvanını təsdiqlə
                            </a>
                        </p>
                        <p style="font-size:13px;color:#71717a">
                            Düymə işləməsə, bu linki brauzerə köçür:<br>
                            <a href="{{safeLink}}" style="color:#6d28d9;word-break:break-all">{{safeLink}}</a>
                        </p>
                        <p style="font-size:13px;color:#71717a">
                            Link 24 saat etibarlıdır. Qeydiyyatı sən etməmisənsə, bu məktubu nəzərə alma.
                        </p>
                    </div>
                </div>
            </body>
            </html>
            """;

        var plainTextBody = $$"""
            Salam, {{userName}}.

            Ludora qeydiyyatını tamamlamaq üçün bu linkə keç:
            {{confirmationLink}}

            Link 24 saat etibarlıdır. Qeydiyyatı sən etməmisənsə, bu məktubu nəzərə alma.
            """;

        return SendAsync(
            email,
            "Ludora email təsdiqi",
            plainTextBody,
            htmlBody,
            cancellationToken);
    }

    public Task SendEmailChangeConfirmationAsync(
        string newEmail,
        string userName,
        string confirmationLink,
        CancellationToken cancellationToken = default)
    {
        var safeUserName = HtmlEncoder.Default.Encode(userName);
        var safeLink = HtmlEncoder.Default.Encode(confirmationLink);

        var htmlBody = $$"""
            <!doctype html>
            <html lang="az">
            <body style="margin:0;background:#f4f4f5;font-family:Arial,sans-serif;line-height:1.6;color:#1f2937">
                <div style="max-width:600px;margin:0 auto;padding:32px 18px">
                    <div style="background:#ffffff;border:1px solid #e4e4e7;border-radius:16px;padding:30px">
                        <h2 style="margin-top:0">Email dəyişiklik təsdiqi</h2>
                        <p>Salam, <strong>{{safeUserName}}</strong>.</p>
                        <p>Ludora hesabının email ünvanını bu ünvana dəyişmək üçün aşağıdakı düyməyə kliklə:</p>
                        <p style="margin:26px 0">
                            <a href="{{safeLink}}"
                               style="display:inline-block;padding:12px 18px;background:#6d28d9;color:#ffffff;text-decoration:none;border-radius:9px;font-weight:700">
                                Yeni email ünvanını təsdiqlə
                            </a>
                        </p>
                        <p style="font-size:13px;color:#71717a">
                            Düymə işləməsə, bu linki brauzerə köçür:<br>
                            <a href="{{safeLink}}" style="color:#6d28d9;word-break:break-all">{{safeLink}}</a>
                        </p>
                        <p style="font-size:13px;color:#71717a">
                            Bu əməliyyatı sən başlamamısansa, linkə klikləmə və hesab şifrəni dəyiş.
                        </p>
                    </div>
                </div>
            </body>
            </html>
            """;

        var plainTextBody = $$"""
            Salam, {{userName}}.

            Ludora hesabının email ünvanını dəyişmək üçün bu linkə keç:
            {{confirmationLink}}

            Bu əməliyyatı sən başlamamısansa, linkə klikləmə və hesab şifrəni dəyiş.
            """;

        return SendAsync(
            newEmail,
            "Ludora email dəyişikliyini təsdiqlə",
            plainTextBody,
            htmlBody,
            cancellationToken);
    }

    private async Task SendAsync(
        string recipient,
        string subject,
        string plainTextBody,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
        {
            throw new ExternalServiceException(
                "Email xidməti aktiv deyil. EmailSettings konfiqurasiyasını və user-secrets dəyərlərini yoxlayın.");
        }

        var maskedRecipient = MaskEmail(recipient);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= _settings.MaxRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var message = CreateMessage(
                    recipient,
                    subject,
                    plainTextBody,
                    htmlBody);

                using var client = CreateClient();
                using var timeoutCts =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                timeoutCts.CancelAfter(
                    TimeSpan.FromSeconds(_settings.SendTimeoutSeconds));

                _logger.LogInformation(
                    "Email SMTP serverinə göndərilir. Recipient: {Recipient}; Subject: {Subject}; Attempt: {Attempt}/{MaxAttempts}.",
                    maskedRecipient,
                    subject,
                    attempt,
                    _settings.MaxRetryAttempts);

                await client.SendMailAsync(message, timeoutCts.Token);

                _logger.LogInformation(
                    "SMTP server emaili qəbul etdi. Recipient: {Recipient}; Subject: {Subject}; Attempt: {Attempt}.",
                    maskedRecipient,
                    subject,
                    attempt);

                return;
            }
            catch (OperationCanceledException exception)
                when (!cancellationToken.IsCancellationRequested)
            {
                lastException = exception;

                _logger.LogWarning(
                    exception,
                    "Email göndərişi {TimeoutSeconds} saniyədə timeout oldu. Recipient: {Recipient}; Attempt: {Attempt}/{MaxAttempts}.",
                    _settings.SendTimeoutSeconds,
                    maskedRecipient,
                    attempt,
                    _settings.MaxRetryAttempts);
            }
            catch (SmtpException exception)
            {
                lastException = exception;

                _logger.LogWarning(
                    exception,
                    "SMTP xətası. Recipient: {Recipient}; StatusCode: {StatusCode}; Attempt: {Attempt}/{MaxAttempts}.",
                    maskedRecipient,
                    exception.StatusCode,
                    attempt,
                    _settings.MaxRetryAttempts);
            }
            catch (InvalidOperationException exception)
            {
                // Konfiqurasiya və client state xətaları retry ilə düzəlmir.
                throw new ExternalServiceException(
                    "Email SMTP konfiqurasiyasına görə göndərilə bilmədi. Server loglarını yoxlayın.",
                    exception);
            }
            catch (FormatException exception)
            {
                throw new ExternalServiceException(
                    "Göndərən və ya qəbul edən email ünvanının formatı düzgün deyil.",
                    exception);
            }

            if (attempt >= _settings.MaxRetryAttempts)
                break;

            var delay = TimeSpan.FromSeconds(
                _settings.RetryDelaySeconds * attempt);

            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);
        }

        throw new ExternalServiceException(
            "Təsdiq emaili SMTP serverinə göndərilə bilmədi. Bir müddət sonra yenidən yoxlayın.",
            lastException ?? new SmtpException("Naməlum SMTP xətası."));
    }

    private MailMessage CreateMessage(
        string recipient,
        string subject,
        string plainTextBody,
        string htmlBody)
    {
        var message = new MailMessage
        {
            From = new MailAddress(
                _settings.FromEmail,
                _settings.FromName,
                Encoding.UTF8),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = plainTextBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
            DeliveryNotificationOptions =
                DeliveryNotificationOptions.OnFailure |
                DeliveryNotificationOptions.Delay
        };

        message.To.Add(recipient);
        message.Headers.Add("Auto-Submitted", "auto-generated");
        message.Headers.Add("X-Auto-Response-Suppress", "All");

        message.AlternateViews.Add(
            AlternateView.CreateAlternateViewFromString(
                htmlBody,
                Encoding.UTF8,
                MediaTypeNames.Text.Html));

        return message;
    }

    private SmtpClient CreateClient()
    {
        var client = new SmtpClient(
            _settings.Host,
            _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            UseDefaultCredentials = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_settings.UserName))
        {
            client.Credentials = new NetworkCredential(
                _settings.UserName,
                _settings.Password ?? string.Empty);
        }

        return client;
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');

        if (atIndex <= 1)
            return "***";

        return $"{email[0]}***{email[(atIndex - 1)..]}";
    }
}
