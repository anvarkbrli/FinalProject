using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Admin;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class AnnouncementService : IAnnouncementService
{
    private const int MaxSearchLength = 200;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;

    public AnnouncementService(
        AppDbContext context,
        INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<PagedResultDTO<AnnouncementDTO>> GetPublishedAsync(
        Guid? userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(pageNumber, pageSize);
        var eligibleAudiences = await GetEligibleAudiencesAsync(userId, cancellationToken);

        var query = _context.Announcements
            .AsNoTracking()
            .Where(x =>
                x.IsPublished &&
                eligibleAudiences.Contains(x.Audience));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await Project(query)
            .OrderByDescending(x => x.PublishedAtUtc ?? x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return CreatePagedResult(items, pageNumber, pageSize, totalCount);
    }

    public async Task<AnnouncementDTO> GetPublishedByIdAsync(
        Guid? userId,
        int announcementId,
        CancellationToken cancellationToken = default)
    {
        ValidateAnnouncementId(announcementId);
        var eligibleAudiences = await GetEligibleAudiencesAsync(userId, cancellationToken);

        return await Project(
                _context.Announcements
                    .AsNoTracking()
                    .Where(x =>
                        x.Id == announcementId &&
                        x.IsPublished &&
                        eligibleAudiences.Contains(x.Audience)))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Elan tapılmadı.");
    }

    public async Task MarkViewedAsync(
        Guid userId,
        int announcementId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new BadRequestException("İstifadəçi ID-si düzgün deyil.");

        ValidateAnnouncementId(announcementId);
        var eligibleAudiences = await GetEligibleAudiencesAsync(userId, cancellationToken);

        var isEligible = await _context.Announcements
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == announcementId &&
                x.IsPublished &&
                eligibleAudiences.Contains(x.Audience),
                cancellationToken);

        if (!isEligible)
            throw new NotFoundException("Elan tapılmadı.");

        var alreadyViewed = await _context.AnnouncementViews
            .AsNoTracking()
            .AnyAsync(x =>
                x.AnnouncementId == announcementId &&
                x.UserId == userId,
                cancellationToken);

        if (alreadyViewed)
            return;

        await _context.AnnouncementViews.AddAsync(
            new AnnouncementView
            {
                AnnouncementId = announcementId,
                UserId = userId,
                FirstViewedAtUtc = DateTime.UtcNow
            },
            cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation())
        {
            // Eyni elan bir neçə tabda eyni anda görünə bilər. İlk insert statistikaya kifayətdir.
        }
    }

    public async Task<PagedResultDTO<AnnouncementDTO>> GetAllForAdminAsync(
        AdminAnnouncementFilterDTO filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ValidatePagination(filter.PageNumber, filter.PageSize);

        var search = filter.Search.NormalizeOptionalText(
            "Axtarış mətni",
            MaxSearchLength);

        var query = _context.Announcements
            .IgnoreQueryFilters()
            .AsNoTracking();

        if (search is not null)
        {
            query = query.Where(x =>
                x.Title.Contains(search) ||
                x.Content.Contains(search));
        }

        if (filter.IsPublished.HasValue)
            query = query.Where(x => x.IsPublished == filter.IsPublished.Value);

        if (filter.IsDeleted.HasValue)
            query = query.Where(x => x.IsDeleted == filter.IsDeleted.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        query = ApplySorting(query, filter.SortBy);

        var items = await Project(query)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        items = await AddAudienceStatisticsAsync(items, cancellationToken);

        return CreatePagedResult(
            items,
            filter.PageNumber,
            filter.PageSize,
            totalCount);
    }

    public async Task<AnnouncementDTO> GetByIdForAdminAsync(
        int announcementId,
        CancellationToken cancellationToken = default)
    {
        ValidateAnnouncementId(announcementId);

        var item = await Project(
                _context.Announcements
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(x => x.Id == announcementId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Elan tapılmadı.");

        return (await AddAudienceStatisticsAsync([item], cancellationToken))[0];
    }

    public async Task<AnnouncementDTO> CreateAsync(
        Guid adminUserId,
        AnnouncementCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ValidateAdminUserId(adminUserId);
        ArgumentNullException.ThrowIfNull(dto);
        ValidateAudience(dto.Audience);

        var title = dto.Title.NormalizeRequiredText("Elanın başlığı", 200);
        var content = dto.Content.NormalizeRequiredText("Elanın mətni", 4000);

        var adminExists = await _context.Users
            .AnyAsync(x => x.Id == adminUserId, cancellationToken);

        if (!adminExists)
            throw new NotFoundException("Admin istifadəçi tapılmadı.");

        var now = DateTime.UtcNow;
        var announcement = new Announcement
        {
            Title = title,
            Content = content,
            CreatedByUserId = adminUserId,
            Audience = dto.Audience,
            IsPublished = dto.IsPublished,
            PublishedAtUtc = dto.IsPublished ? now : null,
            CreatedAt = now
        };

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        await _context.Announcements.AddAsync(announcement, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        if (announcement.IsPublished)
        {
            await _notificationService.QueueAnnouncementAsync(
                adminUserId,
                announcement.Id,
                announcement.Title,
                announcement.Content,
                announcement.Audience,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetByIdForAdminAsync(announcement.Id, cancellationToken);
    }

    public async Task<AnnouncementDTO> UpdateAsync(
        int announcementId,
        AnnouncementUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ValidateAnnouncementId(announcementId);
        ArgumentNullException.ThrowIfNull(dto);
        ValidateAudience(dto.Audience);

        var title = dto.Title.NormalizeRequiredText("Elanın başlığı", 200);
        var content = dto.Content.NormalizeRequiredText("Elanın mətni", 4000);

        var announcement = await _context.Announcements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == announcementId, cancellationToken)
            ?? throw new NotFoundException("Elan tapılmadı.");

        if (announcement.IsDeleted)
            throw new ConflictException("Silinmiş elanı yeniləmək olmaz. Əvvəlcə elanı bərpa edin.");

        if (announcement.IsPublished && dto.IsPublished && announcement.Audience != dto.Audience)
        {
            throw new ConflictException(
                "Yayımlanmış elanın auditoriyasını dəyişmək üçün əvvəlcə onu qaralama vəziyyətinə keçirin.");
        }

        var now = DateTime.UtcNow;
        var wasPublished = announcement.IsPublished;

        if (!announcement.IsPublished && dto.IsPublished)
            announcement.PublishedAtUtc = now;
        else if (announcement.IsPublished && !dto.IsPublished)
            announcement.PublishedAtUtc = null;

        announcement.Title = title;
        announcement.Content = content;
        announcement.Audience = dto.Audience;
        announcement.IsPublished = dto.IsPublished;
        announcement.UpdatedAt = now;

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        if (!wasPublished && announcement.IsPublished)
        {
            await _notificationService.QueueAnnouncementAsync(
                announcement.CreatedByUserId,
                announcement.Id,
                announcement.Title,
                announcement.Content,
                announcement.Audience,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetByIdForAdminAsync(announcementId, cancellationToken);
    }

    public async Task DeleteAsync(
        int announcementId,
        CancellationToken cancellationToken = default)
    {
        ValidateAnnouncementId(announcementId);

        var announcement = await _context.Announcements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == announcementId, cancellationToken)
            ?? throw new NotFoundException("Elan tapılmadı.");

        if (announcement.IsDeleted)
            return;

        announcement.IsDeleted = true;
        announcement.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AnnouncementDTO> RestoreAsync(
        int announcementId,
        CancellationToken cancellationToken = default)
    {
        ValidateAnnouncementId(announcementId);

        var announcement = await _context.Announcements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == announcementId, cancellationToken)
            ?? throw new NotFoundException("Elan tapılmadı.");

        if (announcement.IsDeleted)
        {
            announcement.IsDeleted = false;
            announcement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await GetByIdForAdminAsync(announcementId, cancellationToken);
    }

    private async Task<HashSet<AnnouncementAudience>> GetEligibleAudiencesAsync(
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var audiences = new HashSet<AnnouncementAudience>
        {
            AnnouncementAudience.Everyone
        };

        if (!userId.HasValue || userId.Value == Guid.Empty)
            return audiences;

        var user = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId.Value)
            .Select(x => new
            {
                x.PreferredLanguage,
                x.SteamId,
                x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return audiences;

        audiences.Add(string.Equals(user.PreferredLanguage, "en", StringComparison.OrdinalIgnoreCase)
            ? AnnouncementAudience.EnglishUsers
            : AnnouncementAudience.AzerbaijaniUsers);

        audiences.Add(AnnouncementAudience.RegularUsers);

        if (!string.IsNullOrWhiteSpace(user.SteamId))
            audiences.Add(AnnouncementAudience.SteamLinkedUsers);

        if (user.CreatedAt >= DateTime.UtcNow.AddDays(-30))
            audiences.Add(AnnouncementAudience.NewUsers);

        var roles = await (
                from userRole in _context.UserRoles.AsNoTracking()
                join role in _context.Roles.AsNoTracking()
                    on userRole.RoleId equals role.Id
                where userRole.UserId == userId.Value
                select role.Name)
            .Where(x => x != null)
            .ToListAsync(cancellationToken);

        if (roles.Any(x => string.Equals(x, RoleNames.Moderator, StringComparison.OrdinalIgnoreCase)))
            audiences.Add(AnnouncementAudience.Moderators);

        if (roles.Any(x => string.Equals(x, RoleNames.Admin, StringComparison.OrdinalIgnoreCase)))
            audiences.Add(AnnouncementAudience.Administrators);

        return audiences;
    }

    private async Task<List<AnnouncementDTO>> AddAudienceStatisticsAsync(
        IReadOnlyList<AnnouncementDTO> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            return [];

        var targetCounts = new Dictionary<AnnouncementAudience, int>();
        foreach (var audience in items.Select(x => x.Audience).Distinct())
        {
            targetCounts[audience] = await CountTargetUsersAsync(audience, cancellationToken);
        }

        return items.Select(item =>
        {
            var targetCount = targetCounts.GetValueOrDefault(item.Audience);
            var rate = targetCount == 0
                ? 0m
                : Math.Round(item.UniqueViewCount * 100m / targetCount, 1);

            return item with
            {
                TargetUserCount = targetCount,
                ViewRatePercent = rate
            };
        }).ToList();
    }

    private async Task<int> CountTargetUsersAsync(
        AnnouncementAudience audience,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var users = _context.Users.AsNoTracking();

        return audience switch
        {
            AnnouncementAudience.Everyone => await users.CountAsync(cancellationToken),
            AnnouncementAudience.AzerbaijaniUsers => await users.CountAsync(
                x => x.PreferredLanguage != "en", cancellationToken),
            AnnouncementAudience.EnglishUsers => await users.CountAsync(
                x => x.PreferredLanguage == "en", cancellationToken),
            AnnouncementAudience.SteamLinkedUsers => await users.CountAsync(
                x => x.SteamId != null && x.SteamId != string.Empty, cancellationToken),
            AnnouncementAudience.NewUsers => await users.CountAsync(
                x => x.CreatedAt >= now.AddDays(-30), cancellationToken),
            AnnouncementAudience.RegularUsers => await CountRoleUsersAsync(RoleNames.User, cancellationToken),
            AnnouncementAudience.Moderators => await CountRoleUsersAsync(RoleNames.Moderator, cancellationToken),
            AnnouncementAudience.Administrators => await CountRoleUsersAsync(RoleNames.Admin, cancellationToken),
            _ => 0
        };
    }

    private Task<int> CountRoleUsersAsync(string roleName, CancellationToken cancellationToken)
    {
        return (
            from userRole in _context.UserRoles.AsNoTracking()
            join role in _context.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where role.Name == roleName
            select userRole.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    private static IQueryable<AnnouncementDTO> Project(IQueryable<Announcement> query)
    {
        return query.Select(x => new AnnouncementDTO
        {
            Id = x.Id,
            Title = x.Title,
            Content = x.Content,
            CreatedByUserId = x.CreatedByUserId,
            CreatedByUserName = x.CreatedByUser.UserName ?? string.Empty,
            Audience = x.Audience,
            IsPublished = x.IsPublished,
            PublishedAtUtc = x.PublishedAtUtc,
            IsDeleted = x.IsDeleted,
            UniqueViewCount = x.Views.Count(),
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        });
    }

    private static IQueryable<Announcement> ApplySorting(
        IQueryable<Announcement> query,
        string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(x => x.CreatedAt),
            "title" => query.OrderBy(x => x.Title),
            "title_desc" => query.OrderByDescending(x => x.Title),
            "published" => query.OrderByDescending(x => x.PublishedAtUtc),
            "updated" => query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
    }

    private static PagedResultDTO<AnnouncementDTO> CreatePagedResult(
        IReadOnlyList<AnnouncementDTO> items,
        int pageNumber,
        int pageSize,
        int totalCount)
    {
        return new PagedResultDTO<AnnouncementDTO>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    private static void ValidateAdminUserId(Guid adminUserId)
    {
        if (adminUserId == Guid.Empty)
            throw new BadRequestException("Admin istifadəçi ID-si düzgün deyil.");
    }

    private static void ValidateAnnouncementId(int announcementId)
    {
        if (announcementId <= 0)
            throw new BadRequestException("Elan ID-si düzgün deyil.");
    }

    private static void ValidateAudience(AnnouncementAudience audience)
    {
        if (!Enum.IsDefined(audience))
            throw new BadRequestException("Elan auditoriyası düzgün deyil.");
    }

    private static void ValidatePagination(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (pageSize is < 1 or > MaxPageSize)
            throw new BadRequestException($"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");

        if ((long)(pageNumber - 1) * pageSize > int.MaxValue)
            throw new BadRequestException("Səhifə parametrləri həddindən artıq böyükdür.");
    }
}
