namespace Ludora.Application.Common;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string User = "User";
    public const string AdminOrModerator = Admin + "," + Moderator;

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        new[] { Admin, Moderator, User },
        StringComparer.OrdinalIgnoreCase);
}
