namespace Ludora.Application.Interfaces
{
    public interface IEmailTransport
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

        Task SendCustomEmailAsync(
            string email,
            string subject,
            string plainTextBody,
            string htmlBody,
            CancellationToken cancellationToken = default);

    }
}
