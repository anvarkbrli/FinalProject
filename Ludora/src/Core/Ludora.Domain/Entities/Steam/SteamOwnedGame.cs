using Ludora.Domain.Entities.GameTracking;
using Ludora.Domain.Entities.User;

namespace Ludora.Domain.Entities.Steam;

public sealed class SteamOwnedGame
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public int SteamAppId { get; set; }
    public int? LocalGameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PlaytimeMinutes { get; set; }
    public int PlaytimeWindowsMinutes { get; set; }
    public int PlaytimeMacMinutes { get; set; }
    public int PlaytimeLinuxMinutes { get; set; }
    public int? PlaytimeTwoWeeksMinutes { get; set; }
    public DateTime? LastPlayedAtUtc { get; set; }
    public string? IconUrl { get; set; }
    public DateTime SyncedAtUtc { get; set; }

    public AppUser User { get; set; } = null!;
    public Game? LocalGame { get; set; }
}
