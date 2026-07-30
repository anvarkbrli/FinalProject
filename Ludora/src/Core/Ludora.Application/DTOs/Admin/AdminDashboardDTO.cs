namespace Ludora.Application.DTOs;

public record AdminDashboardDTO
{
    public int TotalUsers { get; init; }
    public int NewUsersLast30Days { get; init; }
    public int BannedUsers { get; init; }
    public int TotalPosts { get; init; }
    public int DeletedPosts { get; init; }
    public int TotalDiscussions { get; init; }
    public int DeletedDiscussions { get; init; }
    public int TotalReplies { get; init; }
    public int DeletedReplies { get; init; }
    public int TotalGames { get; init; }
    public int TotalWarnings { get; init; }
    public int UnreadWarnings { get; init; }
    public int TotalAnnouncements { get; init; }
    public int PublishedAnnouncements { get; init; }
    public int DeletedAnnouncements { get; init; }
    public int TotalReports { get; init; }
    public int PendingReports { get; init; }
    public int ModerationActionsLast30Days { get; init; }
    public int ActiveBadges { get; init; }
}
