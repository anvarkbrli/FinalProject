namespace Ludora.Domain.Enums
{
    public enum SteamGameMatchMethod
    {
        None = 0,
        SteamAppId = 1,
        IgdbExternalId = 2,
        ExactName = 3,
        NormalizedEditionName = 4,
        SteamFallback = 5
    }
}
