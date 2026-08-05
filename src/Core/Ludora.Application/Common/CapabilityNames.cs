namespace Ludora.Application.Common;

public static class CapabilityNames
{
    public const string AccessAdminPanel = "admin.access";
    public const string ViewDashboard = "admin.dashboard.view";

    public const string ViewUsers = "admin.users.view";
    public const string WarnUsers = "admin.users.warn";
    public const string TemporarilyBanUsers = "admin.users.ban.temporary";
    public const string PermanentlyBanUsers = "admin.users.ban.permanent";
    public const string UnbanTemporaryUsers = "admin.users.unban.temporary";
    public const string UnbanPermanentUsers = "admin.users.unban.permanent";
    public const string ManageRoles = "admin.users.roles.manage";

    public const string ViewReports = "admin.reports.view";
    public const string ManageReports = "admin.reports.manage";

    public const string ViewPosts = "admin.posts.view";
    public const string DeletePosts = "admin.posts.delete";
    public const string RestorePosts = "admin.posts.restore";

    public const string ViewAnnouncements = "admin.announcements.view";
    public const string ManageAnnouncements = "admin.announcements.manage";

    public const string ViewContactMessages = "admin.contact-messages.view";
    public const string ManageContactMessages = "admin.contact-messages.manage";

    public const string ViewModerationAudit = "admin.audit.view";

    public const string ViewForumModeration = "admin.forum.view";
    public const string ModerateForum = "admin.forum.moderate";
    public const string DeleteForumContent = "admin.forum.delete";
    public const string RestoreForumContent = "admin.forum.restore";
    public const string ManageForumCategories = "admin.forum.categories.manage";

    public const string ViewBadges = "admin.badges.view";
    public const string ManageBadges = "admin.badges.manage";
    public const string AssignBadges = "admin.badges.assign";

    public const string ViewCatalog = "admin.catalog.view";
    public const string ManageCatalog = "admin.catalog.manage";
    public const string DeleteCatalog = "admin.catalog.delete";
    public const string RestoreCatalog = "admin.catalog.restore";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        new[]
        {
            AccessAdminPanel,
            ViewDashboard,
            ViewUsers,
            WarnUsers,
            TemporarilyBanUsers,
            PermanentlyBanUsers,
            UnbanTemporaryUsers,
            UnbanPermanentUsers,
            ManageRoles,
            ViewReports,
            ManageReports,
            ViewPosts,
            DeletePosts,
            RestorePosts,
            ViewAnnouncements,
            ManageAnnouncements,
            ViewContactMessages,
            ManageContactMessages,
            ViewModerationAudit,
            ViewForumModeration,
            ModerateForum,
            DeleteForumContent,
            RestoreForumContent,
            ManageForumCategories,
            ViewBadges,
            ManageBadges,
            AssignBadges,
            ViewCatalog,
            ManageCatalog,
            DeleteCatalog,
            RestoreCatalog
        },
        StringComparer.Ordinal);
}
