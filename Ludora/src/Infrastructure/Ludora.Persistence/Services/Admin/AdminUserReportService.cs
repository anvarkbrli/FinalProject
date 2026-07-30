using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services.Admin;

public sealed class AdminUserReportService : IAdminUserReportService
{
    private const int MaxSearchLength = 200;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    public AdminUserReportService(
        AppDbContext context,
        IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    public async Task<PagedResultDTO<AdminReportedUserListItemDTO>> GetReportedUsersAsync(
        AdminUserReportFilterDTO filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ValidatePagination(filter.PageNumber, filter.PageSize);

        var search = filter.Search.NormalizeOptionalText(
            "Axtarış mətni",
            MaxSearchLength);
        var now = DateTime.UtcNow;

        var reportQuery = _context.UserReports.AsNoTracking();

        if (search is not null)
        {
            reportQuery = reportQuery.Where(x =>
                (x.ReportedUser.UserName != null &&
                 x.ReportedUser.UserName.Contains(search)) ||
                (x.ReportedUser.Email != null &&
                 x.ReportedUser.Email.Contains(search)));
        }

        if (filter.Category.HasValue)
            reportQuery = reportQuery.Where(x => x.Category == filter.Category.Value);

        if (filter.Status.HasValue)
            reportQuery = reportQuery.Where(x => x.Status == filter.Status.Value);

        if (filter.ContentType.HasValue)
        {
            reportQuery = reportQuery.Where(
                x => x.ReportedContentType == filter.ContentType.Value);
        }

        if (filter.FromUtc.HasValue)
            reportQuery = reportQuery.Where(x => x.CreatedAtUtc >= filter.FromUtc.Value);

        if (filter.ToUtc.HasValue)
            reportQuery = reportQuery.Where(x => x.CreatedAtUtc <= filter.ToUtc.Value);

        if (filter.FromUtc.HasValue &&
            filter.ToUtc.HasValue &&
            filter.FromUtc.Value > filter.ToUtc.Value)
        {
            throw new BadRequestException(
                "Başlanğıc tarixi bitiş tarixindən sonra ola bilməz.");
        }

        if (filter.IsBanned.HasValue)
        {
            reportQuery = filter.IsBanned.Value
                ? reportQuery.Where(x =>
                    x.ReportedUser.IsPermanentlyBanned ||
                    (x.ReportedUser.BannedUntilUtc.HasValue &&
                     x.ReportedUser.BannedUntilUtc > now))
                : reportQuery.Where(x =>
                    !x.ReportedUser.IsPermanentlyBanned &&
                    (!x.ReportedUser.BannedUntilUtc.HasValue ||
                     x.ReportedUser.BannedUntilUtc <= now));
        }

        var groupedQuery = reportQuery
            .GroupBy(x => new
            {
                x.ReportedUserId,
                x.ReportedUser.UserName,
                x.ReportedUser.Email,
                x.ReportedUser.ProfileImageUrl,
                x.ReportedUser.IsPermanentlyBanned,
                x.ReportedUser.BannedUntilUtc
            })
            .Select(group => new ReportedUserSummary
            {
                UserId = group.Key.ReportedUserId,
                UserName = group.Key.UserName ?? string.Empty,
                Email = group.Key.Email,
                ProfileImageUrl = group.Key.ProfileImageUrl,
                TotalReportCount = group.Count(),
                PendingReportCount = group.Count(x =>
                    x.Status == UserReportStatus.Pending ||
                    x.Status == UserReportStatus.UnderReview),
                IsPermanentlyBanned = group.Key.IsPermanentlyBanned,
                BannedUntilUtc = group.Key.BannedUntilUtc,
                LastReportedAtUtc = group.Max(x => x.CreatedAtUtc)
            });

        var totalCount = await groupedQuery.CountAsync(cancellationToken);
        groupedQuery = ApplySorting(groupedQuery, filter.SortBy);

        var summaries = await groupedQuery
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = summaries.Select(x => x.UserId).ToArray();

        var categoryRows = userIds.Length == 0
            ? new List<CategorySummary>()
            : await _context.UserReports
                .AsNoTracking()
                .Where(x => userIds.Contains(x.ReportedUserId))
                .GroupBy(x => new
                {
                    x.ReportedUserId,
                    x.Category
                })
                .Select(group => new CategorySummary
                {
                    UserId = group.Key.ReportedUserId,
                    Category = group.Key.Category,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

        var warningCounts = userIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await _context.UserWarnings
                .AsNoTracking()
                .Where(x => userIds.Contains(x.UserId))
                .GroupBy(x => x.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    Count = group.Count()
                })
                .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var items = summaries.Select(summary =>
        {
            var categories = categoryRows
                .Where(x => x.UserId == summary.UserId)
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Category)
                .Select(x => new UserReportCategoryCountDTO
                {
                    Category = x.Category,
                    Count = x.Count
                })
                .ToArray();

            return new AdminReportedUserListItemDTO
            {
                UserId = summary.UserId,
                UserName = summary.UserName,
                Email = summary.Email,
                ProfileImageUrl = summary.ProfileImageUrl,
                TotalReportCount = summary.TotalReportCount,
                PendingReportCount = summary.PendingReportCount,
                WarningCount = warningCounts.GetValueOrDefault(summary.UserId),
                IsBanned = summary.IsPermanentlyBanned ||
                           (summary.BannedUntilUtc.HasValue &&
                            summary.BannedUntilUtc > now),
                IsPermanentlyBanned = summary.IsPermanentlyBanned,
                BannedUntilUtc = summary.BannedUntilUtc,
                LastReportedAtUtc = summary.LastReportedAtUtc,
                Categories = categories
            };
        }).ToArray();

        return new PagedResultDTO<AdminReportedUserListItemDTO>
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

    public async Task<AdminReportedUserDetailDTO> GetReportedUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new BadRequestException("İstifadəçi ID-si düzgün deyil.");

        var user = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Id,
                UserName = x.UserName ?? string.Empty,
                x.Email,
                x.ProfileImageUrl,
                x.IsPermanentlyBanned,
                x.BannedUntilUtc,
                x.BanReason
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        var reportRows = await ProjectReports(
                _context.UserReports
                    .AsNoTracking()
                    .Where(x => x.ReportedUserId == userId))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var reports = reportRows
            .Select(MapEvidenceUrl)
            .ToList();

        if (reports.Count == 0)
            throw new NotFoundException("Bu istifadəçi üçün şikayət tapılmadı.");

        var categories = await _context.UserReports
            .AsNoTracking()
            .Where(x => x.ReportedUserId == userId)
            .GroupBy(x => x.Category)
            .Select(group => new UserReportCategoryCountDTO
            {
                Category = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Category)
            .ToListAsync(cancellationToken);

        var warningCount = await _context.UserWarnings
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId, cancellationToken);

        var now = DateTime.UtcNow;

        return new AdminReportedUserDetailDTO
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            ProfileImageUrl = user.ProfileImageUrl,
            TotalReportCount = reports.Count,
            PendingReportCount = reports.Count(x =>
                x.Status == UserReportStatus.Pending ||
                x.Status == UserReportStatus.UnderReview),
            WarningCount = warningCount,
            IsBanned = user.IsPermanentlyBanned ||
                       (user.BannedUntilUtc.HasValue &&
                        user.BannedUntilUtc > now),
            IsPermanentlyBanned = user.IsPermanentlyBanned,
            BannedUntilUtc = user.BannedUntilUtc,
            BanReason = user.BanReason,
            Categories = categories,
            Reports = reports
        };
    }

    public async Task<AdminUserReportItemDTO> UpdateStatusAsync(
        Guid adminUserId,
        long reportId,
        AdminUserReportStatusUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        if (adminUserId == Guid.Empty)
            throw new BadRequestException("Admin istifadəçi ID-si düzgün deyil.");

        if (reportId <= 0)
            throw new BadRequestException("Şikayət ID-si düzgün deyil.");

        ArgumentNullException.ThrowIfNull(dto);

        if (!Enum.IsDefined(dto.Status))
            throw new BadRequestException("Şikayət statusu düzgün deyil.");

        var adminNote = dto.AdminNote.NormalizeOptionalText(
            "Admin qeydi",
            1000);

        var report = await _context.UserReports
            .FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken)
            ?? throw new NotFoundException("Şikayət tapılmadı.");

        var previousStatus = report.Status;
        var now = DateTime.UtcNow;

        report.Status = dto.Status;
        report.AdminNote = adminNote;

        if (dto.Status == UserReportStatus.Pending)
        {
            report.ReviewedByUserId = null;
            report.ReviewedAtUtc = null;
        }
        else
        {
            report.ReviewedByUserId = adminUserId;
            report.ReviewedAtUtc = now;
        }

        var auditLog = new ModeratorActionLog
        {
            AdminUserId = adminUserId,
            TargetUserId = report.ReportedUserId,
            UserReportId = report.Id,
            ActionType = ModeratorActionType.ReportStatusChanged,
            PreviousReportStatus = previousStatus,
            NewReportStatus = dto.Status,
            Reason = adminNote,
            CreatedAtUtc = now
        };

        await _context.ModeratorActionLogs.AddAsync(
            auditLog,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var result = await ProjectReports(
                _context.UserReports
                    .AsNoTracking()
                    .Where(x => x.Id == reportId))
            .SingleAsync(cancellationToken);

        return MapEvidenceUrl(result);
    }

    public async Task<StoredFileDTO> GetEvidenceAsync(
        long reportId,
        int index,
        CancellationToken cancellationToken = default)
    {
        if (index < 0 || index >= 5)
            throw new BadRequestException("Sübut faylı indeksi düzgün deyil.");

        var multiStorageKey = await _context.UserReportEvidenceImages
            .AsNoTracking()
            .Where(x => x.UserReportId == reportId)
            .OrderBy(x => x.DisplayOrder)
            .Skip(index)
            .Select(x => x.StorageKey)
            .FirstOrDefaultAsync(cancellationToken);

        if (multiStorageKey is not null)
        {
            return await _fileStorageService.ReadReportEvidenceImageAsync(
                multiStorageKey,
                cancellationToken);
        }

        if (index > 0)
            throw new NotFoundException("Bu şikayət üçün həmin sübut faylı yoxdur.");

        if (reportId <= 0)
            throw new BadRequestException("Şikayət ID-si düzgün deyil.");

        var storageKey = await _context.UserReports
            .AsNoTracking()
            .Where(x => x.Id == reportId)
            .Select(x => x.EvidenceImageStorageKey)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Bu şikayət üçün media sübutu yoxdur.");

        return await _fileStorageService
            .ReadReportEvidenceImageAsync(
                storageKey,
                cancellationToken);
    }

    private static AdminUserReportItemDTO MapEvidenceUrl(
        AdminUserReportItemDTO report)
    {
        return report with
        {
            EvidenceImageUrl = report.EvidenceImageCount == 0
                ? null
                : $"/api/admin/user-reports/{report.Id}/evidence?index=0",
            EvidenceImageUrls = Enumerable.Range(0, report.EvidenceImageCount)
                .Select(index =>
                    $"/api/admin/user-reports/{report.Id}/evidence?index={index}")
                .ToList()
        };
    }

    private static IQueryable<AdminUserReportItemDTO> ProjectReports(
        IQueryable<Ludora.Domain.Entities.User.UserReport> query)
    {
        return query.Select(x => new AdminUserReportItemDTO
        {
            Id = x.Id,
            ReporterUserId = x.ReporterUserId,
            ReporterUserName = x.ReporterUser.UserName ?? string.Empty,
            ReporterProfileImageUrl = x.ReporterUser.ProfileImageUrl,
            Category = x.Category,
            Description = x.Description,
            EvidenceImageUrl = x.EvidenceImageStorageKey,
            EvidenceImageCount = x.EvidenceImages.Count +
                (x.EvidenceImageStorageKey == null ? 0 : 1),
            ReportedContentType = x.ReportedContentType,
            ReportedContentId = x.ReportedContentId,
            ReportedContentSnapshot = x.ReportedContentSnapshot,
            ReportedContentUrl = x.ReportedContentUrl,
            Status = x.Status,
            CreatedAtUtc = x.CreatedAtUtc,
            ReviewedByUserId = x.ReviewedByUserId,
            ReviewedByUserName = x.ReviewedByUser == null
                ? null
                : x.ReviewedByUser.UserName,
            ReviewedAtUtc = x.ReviewedAtUtc,
            AdminNote = x.AdminNote
        });
    }

    private static IQueryable<ReportedUserSummary> ApplySorting(
        IQueryable<ReportedUserSummary> query,
        string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "newest" => query
                .OrderByDescending(x => x.LastReportedAtUtc),
            "oldest" => query
                .OrderBy(x => x.LastReportedAtUtc),
            "mostpending" => query
                .OrderByDescending(x => x.PendingReportCount)
                .ThenByDescending(x => x.TotalReportCount),
            "username" => query
                .OrderBy(x => x.UserName),
            _ => query
                .OrderByDescending(x => x.TotalReportCount)
                .ThenByDescending(x => x.PendingReportCount)
                .ThenByDescending(x => x.LastReportedAtUtc)
        };
    }

    private static void ValidatePagination(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (pageSize is < 1 or > MaxPageSize)
        {
            throw new BadRequestException(
                $"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");
        }

        if ((long)(pageNumber - 1) * pageSize > int.MaxValue)
            throw new BadRequestException("Səhifə parametrləri həddindən artıq böyükdür.");
    }

    private sealed class ReportedUserSummary
    {
        public Guid UserId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string? Email { get; init; }
        public string? ProfileImageUrl { get; init; }
        public int TotalReportCount { get; init; }
        public int PendingReportCount { get; init; }
        public bool IsPermanentlyBanned { get; init; }
        public DateTime? BannedUntilUtc { get; init; }
        public DateTime LastReportedAtUtc { get; init; }
    }

    private sealed class CategorySummary
    {
        public Guid UserId { get; init; }
        public UserReportCategory Category { get; init; }
        public int Count { get; init; }
    }
}
