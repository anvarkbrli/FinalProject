namespace Ludora.Application.DTOs.Steam;

public sealed record SteamConnectStartDTO
{
    public string AuthorizationUrl { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
}

public sealed record SteamConnectionResultDTO
{
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
    public int? LocalGameId { get; init; }
    public string? LocalGameName { get; init; }
    public string? LocalGameSlug { get; init; }
    public string? LocalCoverImageUrl { get; init; }
    public DateOnly? LocalReleaseDate { get; init; }
    public DateTime SyncedAtUtc { get; init; }
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
