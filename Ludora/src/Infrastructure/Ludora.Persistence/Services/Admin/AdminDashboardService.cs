using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services.Admin;

public sealed class AdminDashboardService : IAdminDashboardService
{
    private readonly AppDbContext _context;

    public AdminDashboardService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AdminDashboardDTO> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);

        return new AdminDashboardDTO
        {
            TotalUsers = await _context.Users.CountAsync(cancellationToken),
            NewUsersLast30Days = await _context.Users
                .CountAsync(x => x.CreatedAt >= thirtyDaysAgo, cancellationToken),
            BannedUsers = await _context.Users.CountAsync(x =>
                    x.IsPermanentlyBanned ||
                    (x.BannedUntilUtc.HasValue && x.BannedUntilUtc > now),
                cancellationToken),
            TotalPosts = await _context.UserPosts.CountAsync(cancellationToken),
            DeletedPosts = await _context.UserPosts
                .IgnoreQueryFilters()
                .CountAsync(x => x.IsDeleted, cancellationToken),
            TotalDiscussions = await _context.Discussions.CountAsync(cancellationToken),
            DeletedDiscussions = await _context.Discussions
                .IgnoreQueryFilters()
                .CountAsync(x => x.IsDeleted, cancellationToken),
            TotalReplies = await _context.DiscussionReplies.CountAsync(cancellationToken),
            DeletedReplies = await _context.DiscussionReplies
                .IgnoreQueryFilters()
                .CountAsync(x => x.IsDeleted, cancellationToken),
            TotalGames = await _context.Games.CountAsync(cancellationToken),
            TotalWarnings = await _context.UserWarnings.CountAsync(cancellationToken),
            UnreadWarnings = await _context.UserWarnings
                .CountAsync(x => !x.IsRead, cancellationToken),
            TotalAnnouncements = await _context.Announcements
                .CountAsync(cancellationToken),
            PublishedAnnouncements = await _context.Announcements
                .CountAsync(x => x.IsPublished, cancellationToken),
            DeletedAnnouncements = await _context.Announcements
                .IgnoreQueryFilters()
                .CountAsync(x => x.IsDeleted, cancellationToken)
        };
    }
}
