namespace Ludora.API.Configuration
{
    public sealed class PresenceSettings
    {
        public const string SectionName = "Presence";

        public bool UseDistributedStore { get; set; } = true;
        public int HeartbeatSeconds { get; set; } = 20;
        public int OfflineAfterSeconds { get; set; } = 90;
    }
}
