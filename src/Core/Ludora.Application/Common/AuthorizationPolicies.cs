namespace Ludora.Application.Common
{
    public static class AuthorizationPolicies
    {
        public const string CanAccessAdminPanel = nameof(CanAccessAdminPanel);
        public const string CanModerateForum = nameof(CanModerateForum);
        public const string AdminOnly = nameof(AdminOnly);
    }
}
