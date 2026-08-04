namespace Ludora.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailConfirmationAsync(
        string email,
        string userName,
        string confirmationLink,
        CancellationToken cancellationToken = default);

    Task SendEmailChangeConfirmationAsync(
        string newEmail,
        string userName,
        string confirmationLink,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(
        string email,
        string userName,
        string resetLink,
        CancellationToken cancellationToken = default);

    Task SendContactMessageReplyAsync(
        string email,
        string recipientName,
        string subject,
        string replyBody,
        string originalSubject,
        string originalMessage,
        string language,
        int contactMessageId,
        CancellationToken cancellationToken = default);

}
