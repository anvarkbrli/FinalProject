namespace Ludora.Domain.Enums
{
    public enum SteamPlaytimeResolution
    {
        Unresolved = 0,
        SteamOnly = 1,
        PendingUserDecision = 2,
        SteamIncludesManual = 3,
        SeparateFromSteam = 4,
        ConservativeMaximum = 5,
        SeparatePlatformAutomatic = 6
    }
}
