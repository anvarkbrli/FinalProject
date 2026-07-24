using Ludora.Domain.Entities.GameTracking;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.Steam;

public sealed class SteamOwnedGame
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public int SteamAppId { get; set; }

    // Legacy/local navigation. Generic catalog matches are stored below.
    public int? LocalGameId { get; set; }
    public CatalogSource? MatchedGameSource { get; set; }
    public long? MatchedGameId { get; set; }
    public string? MatchedGameName { get; set; }
    public string? MatchedCoverImageUrl { get; set; }
    public DateOnly? MatchedReleaseDate { get; set; }
    public SteamGameMatchMethod MatchMethod { get; set; }
    public decimal MatchConfidence { get; set; }
    public bool IsSteamFallback { get; set; }

    public string Name { get; set; } = string.Empty;
    public int PlaytimeMinutes { get; set; }
    public int PlaytimeWindowsMinutes { get; set; }
    public int PlaytimeMacMinutes { get; set; }
    public int PlaytimeLinuxMinutes { get; set; }
    public int? PlaytimeTwoWeeksMinutes { get; set; }
    public DateTime? LastPlayedAtUtc { get; set; }
    public string? IconUrl { get; set; }
    public DateTime SyncedAtUtc { get; set; }

    // The original manual values are kept so applying Steam never destroys user data.
    public SteamPlaytimeResolution PlaytimeResolution { get; set; }
    public int? ManualPlaytimeSnapshotMinutes { get; set; }
    public CatalogSource? ManualPlatformSourceSnapshot { get; set; }
    public long? ManualPlatformIdSnapshot { get; set; }
    public DateOnly? ManualPlayedOnSnapshot { get; set; }
    public int? ResolvedPlaytimeMinutes { get; set; }
    public DateTime? ResolutionConfirmedAtUtc { get; set; }

    public AppUser User { get; set; } = null!;
    public Game? LocalGame { get; set; }
}
