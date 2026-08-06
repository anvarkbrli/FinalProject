namespace Ludora.API.Configuration
{
    public sealed class DataProtectionSettings
    {
        public const string SectionName = "DataProtection";

        public string ApplicationName { get; set; } = "WellPlayd";
        public string KeyRingPath { get; set; } = "data-protection-keys";
    }
}
