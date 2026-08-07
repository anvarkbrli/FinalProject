namespace Ludora.Application.Models.Steam
{
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
}
