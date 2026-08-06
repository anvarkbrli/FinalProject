namespace Ludora.API.Security
{
    public static class RateLimitPolicyNames
    {
        public const string Auth = "Auth";
        public const string EmailAuth = "EmailAuth";
        public const string AccountSensitive = "AccountSensitive";
        public const string Uploads = "Uploads";
        public const string ForumWrite = "ForumWrite";
        public const string SocialWrite = "SocialWrite";
        public const string Moderation = "Moderation";
        public const string UserReports = "UserReports";
        public const string FpsEstimator = "FpsEstimator";
        public const string SteamSync = "SteamSync";
        public const string Search = "Search";
        public const string Chat = "Chat";
        public const string Contact = "Contact";
    }
}
