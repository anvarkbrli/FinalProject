using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services.Admin;

public sealed class AdminModerationAuditService : IAdminModerationAuditService
{
    private const int MaxReturnedItems = 500;
    private const int MaxPageSize = 100;
    private const int MaxSearchLength = 200;

    private readonly AppDbContext _context;

    public AdminModerationAuditService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDTO<ModeratorActionLogDTO>> GetAllAsync(
        AdminModerationAuditFilterDTO filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.PageNumber < 1)
            throw new BadRequestException(
                "Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (filter.PageSize is < 1 or > MaxPageSize)
            throw new BadRequestException(
                $"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");

        if (filter.FromUtc.HasValue &&
            filter.ToUtc.HasValue &&
            filter.FromUtc.Value > filter.ToUtc.Value)
        {
            throw new BadRequestException(
                "Başlanğıc tarixi bitiş tarixindən sonra ola bilməz.");
        }

        var search = string.IsNullOrWhiteSpace(filter.Search)
            ? null
            : filter.Search.Trim();

        if (search?.Length > MaxSearchLength)
            throw new BadRequestException(
                $"Axtarış mətni maksimum {MaxSearchLength} simvol ola bilər.");

        var query = ProjectLogs();

        if (filter.AdminUserId.HasValue)
            query = query.Where(x => x.AdminUserId == filter.AdminUserId.Value);

        if (filter.TargetUserId.HasValue)
            query = query.Where(x => x.TargetUserId == filter.TargetUserId.Value);

        if (filter.UserReportId.HasValue)
            query = query.Where(x => x.UserReportId == filter.UserReportId.Value);

        if (filter.ActionType.HasValue)
            query = query.Where(x => x.ActionType == filter.ActionType.Value);

        if (filter.FromUtc.HasValue)
            query = query.Where(x => x.CreatedAtUtc >= filter.FromUtc.Value);

        if (filter.ToUtc.HasValue)
            query = query.Where(x => x.CreatedAtUtc <= filter.ToUtc.Value);

        if (search is not null)
        {
            query = query.Where(x =>
                x.AdminUserName.Contains(search) ||
                x.TargetUserName.Contains(search) ||
                (x.Operation != null && x.Operation.Contains(search)) ||
                (x.ResourceType != null && x.ResourceType.Contains(search)) ||
                (x.ResourceId != null && x.ResourceId.Contains(search)) ||
                (x.ResourceName != null && x.ResourceName.Contains(search)) ||
                (x.Reason != null && x.Reason.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDTO<ModeratorActionLogDTO>
        {
            Items = items,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)filter.PageSize)
        };
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
                TargetUserName = x.TargetUser != null
                    ? x.TargetUser.UserName ?? string.Empty
                    : string.Empty,
                UserReportId = x.UserReportId,
                UserWarningId = x.UserWarningId,
                UserBanHistoryId = x.UserBanHistoryId,
                ActionType = x.ActionType,
                PreviousReportStatus = x.PreviousReportStatus,
                NewReportStatus = x.NewReportStatus,
                Operation = x.Operation,
                ResourceType = x.ResourceType,
                ResourceId = x.ResourceId,
                ResourceName = x.ResourceName,
                Reason = x.Reason,
                CreatedAtUtc = x.CreatedAtUtc
            });
    }
}
