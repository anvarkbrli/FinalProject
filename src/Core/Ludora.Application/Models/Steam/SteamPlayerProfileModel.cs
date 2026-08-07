namespace Ludora.Application.Models.Steam
{
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
}
