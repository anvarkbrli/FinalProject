namespace Ludora.Application.Models.Steam
{
    public sealed record SteamFriendModel
    {
        public string SteamId { get; init; } = string.Empty;
        public DateTime? FriendSinceUtc { get; init; }
    }
}
