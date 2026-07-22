using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services.Admin;

public sealed class AdminModerationAuditService : IAdminModerationAuditService
{
    private const int MaxReturnedItems = 500;

    private readonly AppDbContext _context;

    public AdminModerationAuditService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ModeratorActionLogDTO>> GetByReportAsync(
        long reportId,
        CancellationToken cancellationToken = default)
    {
        if (reportId <= 0)
            throw new BadRequestException("Şikayət ID-si düzgün deyil.");

        var reportExists = await _context.UserReports
            .AsNoTracking()
            .AnyAsync(x => x.Id == reportId, cancellationToken);

        if (!reportExists)
            throw new NotFoundException("Şikayət tapılmadı.");

        return await ProjectLogs()
            .Where(x => x.UserReportId == reportId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(MaxReturnedItems)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModeratorActionLogDTO>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new BadRequestException("İstifadəçi ID-si düzgün deyil.");

        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        return await ProjectLogs()
            .Where(x => x.TargetUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(MaxReturnedItems)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<ModeratorActionLogDTO> ProjectLogs()
    {
        return _context.ModeratorActionLogs
            .AsNoTracking()
            .Select(x => new ModeratorActionLogDTO
            {
                Id = x.Id,
                AdminUserId = x.AdminUserId,
                AdminUserName = x.AdminUser.UserName ?? string.Empty,
                TargetUserId = x.TargetUserId,
                TargetUserName = x.TargetUser.UserName ?? string.Empty,
                UserReportId = x.UserReportId,
                UserWarningId = x.UserWarningId,
                UserBanHistoryId = x.UserBanHistoryId,
                ActionType = x.ActionType,
                PreviousReportStatus = x.PreviousReportStatus,
                NewReportStatus = x.NewReportStatus,
                Reason = x.Reason,
                CreatedAtUtc = x.CreatedAtUtc
            });
    }
}
