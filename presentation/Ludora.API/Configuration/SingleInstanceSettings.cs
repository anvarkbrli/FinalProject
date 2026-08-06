namespace Ludora.API.Configuration
{
    public sealed class SingleInstanceSettings
    {
        public const string SectionName = "Deployment:SingleInstance";

        public bool Enabled { get; set; }
        public string LockResource { get; set; } = "WellPlayd.API.Production";
        public int AcquireTimeoutSeconds { get; set; }
    }
}
