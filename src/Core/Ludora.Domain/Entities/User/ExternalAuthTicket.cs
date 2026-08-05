using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User;

public sealed class ExternalAuthTicket
{
    public Guid Id { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public ExternalAuthTicketKind Kind { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string? DisplayName { get; set; }
    public string? PictureUrl { get; set; }
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }
    public string ReturnPath { get; set; } = "/";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
