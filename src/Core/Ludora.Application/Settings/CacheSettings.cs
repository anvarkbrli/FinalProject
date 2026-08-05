namespace Ludora.Application.Settings;

public sealed class CacheSettings
{
    public const string SectionName = "Cache";

    public bool RedisEnabled { get; set; }
    public string RedisConnectionString { get; set; } = string.Empty;
    public string InstanceName { get; set; } = "wellplayd:";
    public int LocalFallbackSeconds { get; set; } = 30;
}
