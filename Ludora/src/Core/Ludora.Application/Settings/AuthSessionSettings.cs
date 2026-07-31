namespace Ludora.Application.Settings;

public sealed class AuthSessionSettings
{
    public const string SectionName = "AuthSession";

    public string RefreshCookieName { get; set; } = "wellplayd.refresh";
    public int RefreshTokenLifetimeDays { get; set; } = 30;
    public int MaxActiveSessionsPerUser { get; set; } = 10;
}
