using System.Text.Encodings.Web;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;

namespace Ludora.Persistence.Services;

public sealed class EmailOutboxService : IEmailService
{
    private readonly AppDbContext _context;

    public EmailOutboxService(AppDbContext context)
    {
        _context = context;
    }

    public Task SendEmailConfirmationAsync(
        string email,
        string userName,
        string confirmationLink,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync(
            EmailTemplateType.EmailConfirmation,
            email,
            userName,
            confirmationLink,
            cancellationToken);

    public Task SendEmailChangeConfirmationAsync(
        string newEmail,
        string userName,
        string confirmationLink,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync(
            EmailTemplateType.EmailChangeConfirmation,
            newEmail,
            userName,
            confirmationLink,
            cancellationToken);

    public Task SendPasswordResetAsync(
        string email,
        string userName,
        string resetLink,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync(
            EmailTemplateType.PasswordReset,
            email,
            userName,
            resetLink,
            cancellationToken);



    public async Task SendContactMessageReplyAsync(
        string email,
        string recipientName,
        string subject,
        string replyBody,
        string originalSubject,
        string originalMessage,
        string language,
        int contactMessageId,
        CancellationToken cancellationToken = default)
    {
        var isEnglish = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
        var normalizedName = string.IsNullOrWhiteSpace(recipientName)
            ? (isEnglish ? "WellPlayd user" : "WellPlayd istifadəçisi")
            : recipientName.Trim();
        var safeName = HtmlEncoder.Default.Encode(normalizedName);
        var safeReply = HtmlEncoder.Default.Encode(replyBody.Trim())
            .Replace("\r\n", "<br>")
            .Replace("\n", "<br>");
        var safeOriginalSubject = HtmlEncoder.Default.Encode(originalSubject.Trim());
        var safeOriginalMessage = HtmlEncoder.Default.Encode(originalMessage.Trim())
            .Replace("\r\n", "<br>")
            .Replace("\n", "<br>");

        var plainTextBody = isEnglish
            ? $"""
              Hello, {normalizedName}.

              WellPlayd support replied to your message:

              {replyBody.Trim()}

              Your original topic: {originalSubject.Trim()}
              Your original message:
              {originalMessage.Trim()}

              This email was sent in response to a message submitted through WellPlayd.
              """
            : $"""
              Salam, {normalizedName}.

              WellPlayd dəstək komandası mesajına cavab verdi:

              {replyBody.Trim()}

              Yazdığın mövzu: {originalSubject.Trim()}
              Yazdığın mesaj:
              {originalMessage.Trim()}

              Bu email WellPlayd əlaqə səhifəsindən göndərdiyin mesaja cavab olaraq göndərilib.
              """;

        var htmlBody = isEnglish
            ? $"""
              <!doctype html>
              <html lang="en">
              <body style="margin:0;background:#f4f4f5;font-family:Arial,sans-serif;line-height:1.65;color:#1f2937">
                <div style="max-width:640px;margin:0 auto;padding:32px 18px">
                  <div style="background:#ffffff;border:1px solid #e4e4e7;border-radius:18px;padding:30px">
                    <div style="font-size:12px;font-weight:700;letter-spacing:.16em;text-transform:uppercase;color:#6d28d9">WellPlayd support</div>
                    <h2 style="margin:12px 0 0">Hello, {safeName}.</h2>
                    <p style="color:#52525b">We reviewed the message you sent through the contact page.</p>
                    <div style="margin:24px 0;padding:18px;border-left:4px solid #7c3aed;background:#f5f3ff;border-radius:10px">{safeReply}</div>
                    <div style="margin-top:28px;padding-top:20px;border-top:1px solid #e4e4e7">
                      <p style="margin:0 0 8px;font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.12em;color:#71717a">Your original message</p>
                      <p style="margin:0 0 8px;font-weight:700">{safeOriginalSubject}</p>
                      <p style="margin:0;color:#71717a">{safeOriginalMessage}</p>
                    </div>
                  </div>
                </div>
              </body>
              </html>
              """
            : $"""
              <!doctype html>
              <html lang="az">
              <body style="margin:0;background:#f4f4f5;font-family:Arial,sans-serif;line-height:1.65;color:#1f2937">
                <div style="max-width:640px;margin:0 auto;padding:32px 18px">
                  <div style="background:#ffffff;border:1px solid #e4e4e7;border-radius:18px;padding:30px">
                    <div style="font-size:12px;font-weight:700;letter-spacing:.16em;text-transform:uppercase;color:#6d28d9">WellPlayd dəstək</div>
                    <h2 style="margin:12px 0 0">Salam, {safeName}.</h2>
                    <p style="color:#52525b">Əlaqə səhifəsindən göndərdiyin mesajı nəzərdən keçirdik.</p>
                    <div style="margin:24px 0;padding:18px;border-left:4px solid #7c3aed;background:#f5f3ff;border-radius:10px">{safeReply}</div>
                    <div style="margin-top:28px;padding-top:20px;border-top:1px solid #e4e4e7">
                      <p style="margin:0 0 8px;font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.12em;color:#71717a">Sənin ilkin mesajın</p>
                      <p style="margin:0 0 8px;font-weight:700">{safeOriginalSubject}</p>
                      <p style="margin:0;color:#71717a">{safeOriginalMessage}</p>
                    </div>
                  </div>
                </div>
              </body>
              </html>
              """;

        var message = new EmailOutboxMessage
        {
            TemplateType = EmailTemplateType.ContactMessageReply,
            RecipientEmail = email.Trim(),
            UserName = normalizedName,
            ActionUrl = string.Empty,
            EmailSubject = subject.Trim(),
            PlainTextBody = plainTextBody,
            HtmlBody = htmlBody,
            ContactMessageId = contactMessageId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.EmailOutboxMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnqueueAsync(
        EmailTemplateType templateType,
        string recipientEmail,
        string userName,
        string actionUrl,
        CancellationToken cancellationToken)
    {
        var message = new EmailOutboxMessage
        {
            TemplateType = templateType,
            RecipientEmail = recipientEmail.Trim(),
            UserName = string.IsNullOrWhiteSpace(userName)
                ? "WellPlayd istifadəçisi"
                : userName.Trim(),
            ActionUrl = actionUrl.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.EmailOutboxMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
