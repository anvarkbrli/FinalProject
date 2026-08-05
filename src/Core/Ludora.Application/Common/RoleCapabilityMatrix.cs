namespace Ludora.Application.Common;

public static class RoleCapabilityMatrix
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> CapabilitiesByRole =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [RoleNames.Admin] = new HashSet<string>(CapabilityNames.All, StringComparer.Ordinal),
            [RoleNames.Moderator] = new HashSet<string>(
                new[]
                {
                    CapabilityNames.AccessAdminPanel,
                    CapabilityNames.ViewDashboard,
                    CapabilityNames.ViewUsers,
                    CapabilityNames.WarnUsers,
                    CapabilityNames.TemporarilyBanUsers,
                    CapabilityNames.UnbanTemporaryUsers,
                    CapabilityNames.ViewReports,
                    CapabilityNames.ManageReports,
                    CapabilityNames.ViewPosts,
                    CapabilityNames.ViewAnnouncements,
                    CapabilityNames.ManageAnnouncements,
                    CapabilityNames.ViewContactMessages,
                    CapabilityNames.ManageContactMessages,
                    CapabilityNames.ViewModerationAudit,
                    CapabilityNames.ViewForumModeration,
                    CapabilityNames.ModerateForum
                },
                StringComparer.Ordinal)
        };

    public static IReadOnlyList<string> GetCapabilities(IEnumerable<string>? roles)
    {
        if (roles is null)
            return [];

        var capabilities = new HashSet<string>(StringComparer.Ordinal);

        foreach (var role in roles)
        {
            if (string.IsNullOrWhiteSpace(role) ||
                !CapabilitiesByRole.TryGetValue(role.Trim(), out var roleCapabilities))
            {
                continue;
            }

            capabilities.UnionWith(roleCapabilities);
        }

        return capabilities.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    public static bool HasCapability(IEnumerable<string>? roles, string capability)
    {
        if (string.IsNullOrWhiteSpace(capability) || !CapabilityNames.All.Contains(capability))
            return false;

        if (roles is null)
            return false;

        foreach (var role in roles)
        {
            if (!string.IsNullOrWhiteSpace(role) &&
                CapabilitiesByRole.TryGetValue(role.Trim(), out var roleCapabilities) &&
                roleCapabilities.Contains(capability))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<string> GetRolesWithCapability(string capability)
    {
        if (string.IsNullOrWhiteSpace(capability) || !CapabilityNames.All.Contains(capability))
            return [];

        return CapabilitiesByRole
            .Where(pair => pair.Value.Contains(capability))
            .Select(pair => pair.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }
}
