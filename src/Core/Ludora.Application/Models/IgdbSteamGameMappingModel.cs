namespace Ludora.Application.Models
{
    public sealed record IgdbSteamGameMappingModel
    {
        public int SteamAppId { get; init; }
        public long IgdbGameId { get; init; }
    }
}
