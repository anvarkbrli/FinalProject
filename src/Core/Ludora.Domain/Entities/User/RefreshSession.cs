namespace Ludora.Domain.Entities.User;

public sealed class RefreshSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public string SecurityStamp { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime SignedInAtUtc { get; set; }
    public DateTime LastActiveAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }

    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedByIp { get; set; }
    public string? RevocationReason { get; set; }
    public Guid? ReplacedBySessionId { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
