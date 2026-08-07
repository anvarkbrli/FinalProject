namespace Ludora.Application.Models.Steam
{
    public sealed record SteamPlayerAchievementsResultModel
    {
        public bool IsVisible { get; init; }
        public string? GameName { get; init; }
        public IReadOnlyList<SteamAchievementModel> Achievements { get; init; } = [];
    }
}
