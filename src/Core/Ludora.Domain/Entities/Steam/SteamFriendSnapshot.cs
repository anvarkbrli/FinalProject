using Ludora.Domain.Entities.User;

namespace Ludora.Domain.Entities.Steam;

public sealed class SteamFriendSnapshot
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string FriendSteamId { get; set; } = string.Empty;
    public DateTime? FriendSinceUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public bool IsActive { get; set; }

    public AppUser User { get; set; } = null!;
}
