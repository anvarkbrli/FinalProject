namespace Ludora.Application.Models.Steam
{
    public sealed record SteamOpenIdValidationModel
    {
        public bool IsValid { get; init; }
        public string? SteamId { get; init; }
        public string? ClaimedIdentifier { get; init; }
        public string? ReturnTo { get; init; }
    }

    public sealed record SteamPlayerProfileModel
    {
        public string SteamId { get; init; } = string.Empty;
        public string PersonaName { get; init; } = string.Empty;
        public string? ProfileUrl { get; init; }
        public string? AvatarUrl { get; init; }
        public string? AvatarMediumUrl { get; init; }
        public string? AvatarFullUrl { get; init; }
        public int PersonaState { get; init; }
        public int CommunityVisibilityState { get; init; }
        public long? LastLogoffUnix { get; init; }
        public int? GameAppId { get; init; }
        public string? GameName { get; init; }
    }

    public sealed record SteamOwnedGameModel
    {
        public int AppId { get; init; }
        public string Name { get; init; } = string.Empty;
        public int PlaytimeForeverMinutes { get; init; }
        public int PlaytimeWindowsMinutes { get; init; }
        public int PlaytimeMacMinutes { get; init; }
        public int PlaytimeLinuxMinutes { get; init; }
        public int? PlaytimeTwoWeeksMinutes { get; init; }
        public long? LastPlayedUnix { get; init; }
        public string? IconHash { get; init; }
    }

    public sealed record SteamOwnedGamesResultModel
    {
        public bool IsVisible { get; init; }
        public int GameCount { get; init; }
        public IReadOnlyList<SteamOwnedGameModel> Games { get; init; } = [];
    }

    public sealed record SteamFriendModel
    {
        public string SteamId { get; init; } = string.Empty;
        public DateTime? FriendSinceUtc { get; init; }
    }

    public sealed record SteamFriendsResultModel
    {
        public bool IsVisible { get; init; }
        public IReadOnlyList<SteamFriendModel> Friends { get; init; } = [];
    }

    public sealed record SteamAchievementModel
    {
        public string ApiName { get; init; } = string.Empty;
        public bool Achieved { get; init; }
        public DateTime? UnlockedAtUtc { get; init; }
    }

    public sealed record SteamPlayerAchievementsResultModel
    {
        public bool IsVisible { get; init; }
        public string? GameName { get; init; }
        public IReadOnlyList<SteamAchievementModel> Achievements { get; init; } = [];
    }

    public sealed record SteamAchievementSchemaModel
    {
        public string ApiName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? IconUrl { get; init; }
        public string? GrayIconUrl { get; init; }
    }

    public sealed record SteamComingSoonGameModel
    {
        public int Rank { get; init; }
        public int AppId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? HeaderImageUrl { get; init; }
        public string? SmallCapsuleImageUrl { get; init; }
        public string? PortraitImageUrl { get; init; }
        public string? ReleaseDateText { get; init; }
        public DateOnly? ReleaseDate { get; init; }
        public string StoreUrl { get; init; } = string.Empty;
        public bool IsDiscounted { get; init; }
        public int DiscountPercent { get; init; }
        public long? OriginalPriceMinor { get; init; }
        public long? FinalPriceMinor { get; init; }
        public string? Currency { get; init; }
    }
}
