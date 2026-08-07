namespace Ludora.Application.Models.Steam
{
    public sealed record SteamAchievementModel
    {
        public string ApiName { get; init; } = string.Empty;
        public bool Achieved { get; init; }
        public DateTime? UnlockedAtUtc { get; init; }
    }
}
