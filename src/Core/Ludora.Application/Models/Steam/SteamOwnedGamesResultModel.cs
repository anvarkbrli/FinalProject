namespace Ludora.Application.Models.Steam
{
    public sealed record SteamOwnedGamesResultModel
    {
        public bool IsVisible { get; init; }
        public int GameCount { get; init; }
        public IReadOnlyList<SteamOwnedGameModel> Games { get; init; } = [];
    }
}
