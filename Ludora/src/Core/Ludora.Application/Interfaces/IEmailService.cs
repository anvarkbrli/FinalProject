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
}
