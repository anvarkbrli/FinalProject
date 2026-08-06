namespace Ludora.API.Configuration
{
    public sealed class ForwardedHeadersSettings
    {
        public const string SectionName = "ForwardedHeaders";

        public bool Enabled { get; set; }
        public int ForwardLimit { get; set; } = 1;
        public string[] KnownProxies { get; set; } = [];
        public string[] KnownNetworks { get; set; } = [];
        public string[] AllowedHosts { get; set; } = [];
    }
}
