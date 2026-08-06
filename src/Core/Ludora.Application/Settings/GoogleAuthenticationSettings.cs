namespace Ludora.Application.Settings
{
    public sealed class GoogleAuthenticationSettings
    {
        public const string SectionName = "Authentication:Google";

        public bool Enabled { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string CallbackPath { get; set; } = "/signin-google";
        public string FrontendCallbackUrl { get; set; } = "http://localhost:5173/auth/google/callback";
        public int TicketLifetimeMinutes { get; set; } = 10;
    }
}
