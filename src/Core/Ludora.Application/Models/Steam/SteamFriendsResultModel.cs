namespace Ludora.Application.Models.Steam
{
    public sealed record SteamFriendsResultModel
    {
        public bool IsVisible { get; init; }
        public IReadOnlyList<SteamFriendModel> Friends { get; init; } = [];
    }
}
