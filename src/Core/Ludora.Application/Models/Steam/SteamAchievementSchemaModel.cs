namespace Ludora.Application.Models.Steam
{
    public sealed record SteamAchievementSchemaModel
    {
        public string ApiName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? IconUrl { get; init; }
        public string? GrayIconUrl { get; init; }
    }
}
