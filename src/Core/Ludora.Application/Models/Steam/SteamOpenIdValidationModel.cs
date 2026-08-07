namespace Ludora.Application.Models.Steam
{
    public sealed record SteamOpenIdValidationModel
    {
        public bool IsValid { get; init; }
        public string? SteamId { get; init; }
        public string? ClaimedIdentifier { get; init; }
        public string? ReturnTo { get; init; }
    }
}
