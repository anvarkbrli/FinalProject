using System.Security.Claims;
using Ludora.Application.Common;

namespace Ludora.API.Security;

public static class ClaimsPrincipalExtensions
{
    public static bool CanModerateForum(this ClaimsPrincipal user) =>
        HasCapability(user, CapabilityNames.ModerateForum);

    public static bool CanDeleteForumContent(this ClaimsPrincipal user) =>
        HasCapability(user, CapabilityNames.DeleteForumContent);

    private static bool HasCapability(
        ClaimsPrincipal user,
        string capability)
    {
        ArgumentNullException.ThrowIfNull(user);

        var roles = user
            .FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value);

        return RoleCapabilityMatrix.HasCapability(roles, capability);
    }
}
