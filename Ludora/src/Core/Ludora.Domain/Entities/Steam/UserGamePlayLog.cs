using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.Steam;

public sealed class UserGamePlayLog
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string SteamAccountId { get; set; } = string.Empty;
    public CatalogSource GameSource { get; set; }
    public long GameId { get; set; }
    public int SteamAppId { get; set; }
    public int InitialImportedPlaytimeMinutes { get; set; }
    public int PlaytimeMinutes { get; set; }
    public DateTime? LastPlayedAtUtc { get; set; }
    public DateTime FirstSyncedAtUtc { get; set; }
    public DateTime LastSyncedAtUtc { get; set; }

    public AppUser User { get; set; } = null!;
    public ICollection<SteamPlaytimeDelta> Deltas { get; set; } = new List<SteamPlaytimeDelta>();
}
