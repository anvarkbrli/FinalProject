using System.Text.Json.Serialization;
using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Steam;

public sealed record SteamConnectStartDTO
{
    public string AuthorizationUrl { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
}

public sealed record SteamConnectionResultDTO
{
    [JsonIgnore]
    public Guid? UserId { get; init; }
    public bool Success { get; init; }
    public string? SteamId { get; init; }
    public string? SteamName { get; init; }
    public string? AvatarUrl { get; init; }
    public string? ProfileUrl { get; init; }
    public DateTime? LinkedAtUtc { get; init; }
    public string? Error { get; init; }
}

public sealed record SteamConnectionStatusDTO
{
    public bool IsConnected { get; init; }
    public string? SteamId { get; init; }
    public string? SteamName { get; init; }
    public string? AvatarUrl { get; init; }
    public string? ProfileUrl { get; init; }
    public DateTime? LinkedAtUtc { get; init; }
    public DateTime? LastSyncedAtUtc { get; init; }
}

public sealed record SteamSyncResultDTO
{
    public int SteamOwnedGameCount { get; init; }
    public int MatchedGameCount { get; init; }
    public int UnmatchedGameCount { get; init; }
    public int CreatedUserGameCount { get; init; }
    public int UpdatedUserGameCount { get; init; }
    public int CreatedAutoLogCount { get; init; }
    public int UpdatedAutoLogCount { get; init; }
    public int UpdatedReviewPlaytimeCount { get; init; }
    public int CreatedPlayLogCount { get; init; }
    public int UpdatedPlayLogCount { get; init; }
    public int PendingResolutionCount { get; init; }
    public int ReadyToApplyCount { get; init; }
    public int AutoUpdatedResolvedLogCount { get; init; }
    public bool RequiresConfirmation => PendingResolutionCount > 0 || ReadyToApplyCount > 0;
    public DateTime SyncedAtUtc { get; init; }
}

public sealed record SteamOwnedGameDTO
{
    public int SteamAppId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int PlaytimeMinutes { get; init; }
    public decimal PlaytimeHours { get; init; }
    public int PlaytimeWindowsMinutes { get; init; }
    public int PlaytimeMacMinutes { get; init; }
    public int PlaytimeLinuxMinutes { get; init; }
    public int? PlaytimeTwoWeeksMinutes { get; init; }
    public DateTime? LastPlayedAtUtc { get; init; }
    public string? SteamIconUrl { get; init; }
    public bool IsMatched { get; init; }
    public CatalogSource? MatchedGameSource { get; init; }
    public long? MatchedGameId { get; init; }
    public string? MatchedGameName { get; init; }
    public string? MatchedCoverImageUrl { get; init; }
    public DateOnly? MatchedReleaseDate { get; init; }
    public SteamGameMatchMethod MatchMethod { get; init; }
    public decimal MatchConfidence { get; init; }
    public bool IsSteamFallback { get; init; }
    public SteamPlaytimeResolution PlaytimeResolution { get; init; }
    public int? ResolvedPlaytimeMinutes { get; init; }
    public bool NeedsResolution { get; init; }
    public string? DetailUrl { get; init; }
    public int? LocalGameId { get; init; }
    public string? LocalGameName { get; init; }
    public string? LocalGameSlug { get; init; }
    public string? LocalCoverImageUrl { get; init; }
    public DateOnly? LocalReleaseDate { get; init; }
    public DateTime SyncedAtUtc { get; init; }
}

public sealed record SteamPlaytimeReconciliationItemDTO
{
    public int SteamAppId { get; init; }
    public string SteamName { get; init; } = string.Empty;
    public string? SteamIconUrl { get; init; }
    public int SteamPlaytimeMinutes { get; init; }
    public CatalogSource MatchedGameSource { get; init; }
    public long MatchedGameId { get; init; }
    public string MatchedGameName { get; init; } = string.Empty;
    public string? MatchedCoverImageUrl { get; init; }
    public SteamGameMatchMethod MatchMethod { get; init; }
    public int ManualPlaytimeMinutes { get; init; }
    public string? ManualPlatformName { get; init; }
    public SteamPlaytimeResolution CurrentResolution { get; init; }
    public SteamPlaytimeResolution SuggestedResolution { get; init; }
    public bool NeedsUserDecision { get; init; }
    public int PreviewResolvedPlaytimeMinutes { get; init; }
}

public sealed record SteamPlaytimeReconciliationDTO
{
    public int TotalPlayedGames { get; init; }
    public int PendingCount { get; init; }
    public int ReadyToApplyCount { get; init; }
    public int AlreadyConfirmedCount { get; init; }
    public IReadOnlyList<SteamPlaytimeReconciliationItemDTO> Items { get; init; } = [];
}

public sealed record SteamPlaytimeDecisionDTO
{
    public int SteamAppId { get; init; }
    public SteamPlaytimeResolution Resolution { get; init; }
}

public sealed record SteamApplyPlaytimeReconciliationDTO
{
    public SteamPlaytimeResolution DefaultPendingResolution { get; init; } =
        SteamPlaytimeResolution.ConservativeMaximum;
    public IReadOnlyList<SteamPlaytimeDecisionDTO> Decisions { get; init; } = [];
}

public sealed record SteamApplyPlaytimeResultDTO
{
    public int AppliedGameCount { get; init; }
    public int CreatedAutoLogCount { get; init; }
    public int UpdatedAutoLogCount { get; init; }
    public int AssignedPcPlatformCount { get; init; }
    public DateTime AppliedAtUtc { get; init; }
}

public sealed record SteamComingSoonGameDTO
{
    public int Rank { get; init; }
    public int SteamAppId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? HeaderImageUrl { get; init; }
    public string? SmallCapsuleImageUrl { get; init; }
    public string? PortraitImageUrl { get; init; }
    public string? ReleaseDateText { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public string StoreUrl { get; init; } = string.Empty;
    public bool IsDiscounted { get; init; }
    public int DiscountPercent { get; init; }
    public decimal? OriginalPrice { get; init; }
    public decimal? FinalPrice { get; init; }
    public string? Currency { get; init; }
    public string RankingSource { get; init; } = "Steam Top Wishlists order";
    public bool IsMatched { get; init; }
    public int? LocalGameId { get; init; }
    public string? LocalGameSlug { get; init; }
}

public sealed record SteamAchievementDTO
{
    public int SteamAppId { get; init; }
    public string GameName { get; init; } = string.Empty;
    public string ApiName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? IconUrl { get; init; }
    public DateTime UnlockedAtUtc { get; init; }
}

public sealed record SteamPresenceDTO
{
    public string SteamId { get; init; } = string.Empty;
    public string SteamName { get; init; } = string.Empty;
    public bool IsOnline { get; init; }
    public bool IsPlaying { get; init; }
    public int PersonaState { get; init; }
    public string PersonaStateName { get; init; } = string.Empty;
    public int? SteamAppId { get; init; }
    public string? GameName { get; init; }
    public DateTime? LastLogoffAtUtc { get; init; }
}

public sealed record SteamFriendSuggestionDTO
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? ProfileImageUrl { get; init; }
    public string SteamId { get; init; } = string.Empty;
    public string? SteamName { get; init; }
    public DateTime? FriendSinceUtc { get; init; }
}

public sealed record SteamFriendSyncDTO
{
    public bool IsFriendListVisible { get; init; }
    public int SteamFriendCount { get; init; }
    public int WellPlaydMatchCount { get; init; }
    public DateTime SyncedAtUtc { get; init; }
    public IReadOnlyList<SteamFriendSuggestionDTO> Suggestions { get; init; } = [];
}
