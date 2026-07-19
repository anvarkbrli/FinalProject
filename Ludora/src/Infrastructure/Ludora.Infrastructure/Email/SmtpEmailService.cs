using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Encodings.Web;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.Email;

public sealed class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public SmtpEmailService(IOptions<EmailSettings> options)
    {
        _settings = options.Value;
    }

    public Task SendEmailConfirmationAsync(
        string email,
        string userName,
        string confirmationLink,
        CancellationToken cancellationToken = default)
    {
        var safeUserName = HtmlEncoder.Default.Encode(userName);
        var safeLink = HtmlEncoder.Default.Encode(confirmationLink);

        var body = $$"""
            <!doctype html>
            <html lang="az">
            <body style="font-family:Arial,sans-serif;line-height:1.6;color:#1f2937">
                <h2>Ludora hesabını təsdiqlə</h2>
                <p>Salam, <strong>{{safeUserName}}</strong>.</p>
                <p>Qeydiyyatı tamamlamaq üçün aşağıdakı düyməyə kliklə:</p>
                <p>
                    <a href="{{safeLink}}"
                       style="display:inline-block;padding:12px 18px;background:#111827;color:#fff;text-decoration:none;border-radius:8px">
                        Email ünvanını təsdiqlə
                    </a>
                </p>
                <p>Bu link məhdud müddət ərzində etibarlıdır. Qeydiyyatı sən etməmisənsə, bu məktubu nəzərə alma.</p>
            </body>
            </html>
            """;

        return SendAsync(
            email,
            "Ludora email təsdiqi",
            body,
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

        var body = $$"""
            <!doctype html>
            <html lang="az">
            <body style="font-family:Arial,sans-serif;line-height:1.6;color:#1f2937">
                <h2>Email dəyişiklik təsdiqi</h2>
                <p>Salam, <strong>{{safeUserName}}</strong>.</p>
                <p>Ludora hesabının email ünvanını bu ünvana dəyişmək üçün aşağıdakı düyməyə kliklə:</p>
                <p>
                    <a href="{{safeLink}}"
                       style="display:inline-block;padding:12px 18px;background:#111827;color:#fff;text-decoration:none;border-radius:8px">
                        Yeni email ünvanını təsdiqlə
                    </a>
                </p>
                <p>Bu əməliyyatı sən başlamamısansa, linkə klikləmə və hesab şifrəni dəyiş.</p>
            </body>
            </html>
            """;

        return SendAsync(
            newEmail,
            "Ludora email dəyişikliyini təsdiqlə",
            body,
            cancellationToken);
    }

    private async Task SendAsync(
        string recipient,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
        {
            throw new ExternalServiceException(
                "Email xidməti aktiv deyil. EmailSettings konfiqurasiyasını tamamlayın.");
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(
                    _settings.FromEmail,
                    _settings.FromName),
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                Body = htmlBody,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };
            message.To.Add(recipient);

            using var client = new SmtpClient(
                _settings.Host,
                _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(_settings.UserName))
            {
                client.Credentials = new NetworkCredential(
                    _settings.UserName,
                    _settings.Password ?? string.Empty);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is SmtpException or
                         InvalidOperationException or
                         FormatException)
        {
            throw new ExternalServiceException(
                "Təsdiq emaili göndərilə bilmədi.",
                exception);
        }
    }
}
