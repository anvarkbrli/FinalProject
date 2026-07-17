namespace Ludora.Application.Common;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        new[] { Admin, User },
        StringComparer.OrdinalIgnoreCase);
}
