namespace Ludora.Domain.Entities.User;

public sealed class UserReportEvidence
{
    public long Id { get; set; }
    public long UserReportId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public UserReport UserReport { get; set; } = null!;
}
