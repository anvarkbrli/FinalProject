using System.Security.Claims;
using Ludora.Application.Common;

namespace Ludora.API.Security;

public static class ClaimsPrincipalExtensions
{
    public static bool CanModerateForum(this ClaimsPrincipal user) =>
        user.IsInRole(RoleNames.Admin) ||
        user.IsInRole(RoleNames.Moderator);
}
