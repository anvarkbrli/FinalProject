using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User
{
    public sealed class EmailOutboxMessage
    {
        public long Id { get; set; }
        public EmailTemplateType TemplateType { get; set; }
        public string RecipientEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string ActionUrl { get; set; } = string.Empty;
        public string? EmailSubject { get; set; }
        public string? PlainTextBody { get; set; }
        public string? HtmlBody { get; set; }
        public int? ContactMessageId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ProcessedAtUtc { get; set; }
        public DateTime? DeadLetteredAtUtc { get; set; }
        public int AttemptCount { get; set; }
        public DateTime? NextAttemptAtUtc { get; set; }
        public string? LastError { get; set; }
        public Guid? LockToken { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
    }
}
