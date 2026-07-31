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
