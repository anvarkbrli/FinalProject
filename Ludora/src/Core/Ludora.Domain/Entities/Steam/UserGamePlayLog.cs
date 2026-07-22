using Ludora.Domain.Entities.GameTracking;
using Ludora.Domain.Entities.User;

namespace Ludora.Domain.Entities.Steam;

public sealed class UserGamePlayLog
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public int GameId { get; set; }
    public int SteamAppId { get; set; }
    public int PlaytimeMinutes { get; set; }
    public DateTime? LastPlayedAtUtc { get; set; }
    public DateTime FirstSyncedAtUtc { get; set; }
    public DateTime LastSyncedAtUtc { get; set; }

    public AppUser User { get; set; } = null!;
    public Game Game { get; set; } = null!;
}
