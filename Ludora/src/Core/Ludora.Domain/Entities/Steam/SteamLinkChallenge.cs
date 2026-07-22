using Ludora.Domain.Entities.User;

namespace Ludora.Domain.Entities.Steam;

public sealed class SteamLinkChallenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StateHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }

    public AppUser User { get; set; } = null!;
}
