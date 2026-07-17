using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Admin;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class AnnouncementService : IAnnouncementService
{
    private const int MaxSearchLength = 200;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _context;

    public AnnouncementService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDTO<AnnouncementDTO>> GetPublishedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(pageNumber, pageSize);

        var query = _context.Announcements
            .AsNoTracking()
            .Where(x => x.IsPublished);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await Project(query)
            .OrderByDescending(x => x.PublishedAtUtc ?? x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return CreatePagedResult(items, pageNumber, pageSize, totalCount);
    }

    public async Task<AnnouncementDTO> GetPublishedByIdAsync(
        int announcementId,
        CancellationToken cancellationToken = default)
    {
        ValidateAnnouncementId(announcementId);

        return await Project(
                _context.Announcements
                    .AsNoTracking()
                    .Where(x =>
                        x.Id == announcementId &&
                        x.IsPublished))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Elan tapılmadı.");
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

        return await Project(
                _context.Announcements
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(x => x.Id == announcementId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Elan tapılmadı.");
    }

    public async Task<AnnouncementDTO> CreateAsync(
        Guid adminUserId,
        AnnouncementCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ValidateAdminUserId(adminUserId);
        ArgumentNullException.ThrowIfNull(dto);

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
            IsPublished = dto.IsPublished,
            PublishedAtUtc = dto.IsPublished ? now : null,
            CreatedAt = now
        };

        await _context.Announcements.AddAsync(
            announcement,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdForAdminAsync(
            announcement.Id,
            cancellationToken);
    }

    public async Task<AnnouncementDTO> UpdateAsync(
        int announcementId,
        AnnouncementUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ValidateAnnouncementId(announcementId);
        ArgumentNullException.ThrowIfNull(dto);

        var title = dto.Title.NormalizeRequiredText("Elanın başlığı", 200);
        var content = dto.Content.NormalizeRequiredText("Elanın mətni", 4000);

        var announcement = await _context.Announcements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.Id == announcementId,
                cancellationToken)
            ?? throw new NotFoundException("Elan tapılmadı.");

        if (announcement.IsDeleted)
            throw new ConflictException("Silinmiş elanı yeniləmək olmaz. Əvvəlcə elanı bərpa edin.");

        var now = DateTime.UtcNow;

        if (!announcement.IsPublished && dto.IsPublished)
            announcement.PublishedAtUtc = now;
        else if (announcement.IsPublished && !dto.IsPublished)
            announcement.PublishedAtUtc = null;

        announcement.Title = title;
        announcement.Content = content;
        announcement.IsPublished = dto.IsPublished;
        announcement.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdForAdminAsync(
            announcementId,
            cancellationToken);
    }

    public async Task DeleteAsync(
        int announcementId,
        CancellationToken cancellationToken = default)
    {
        ValidateAnnouncementId(announcementId);

        var announcement = await _context.Announcements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.Id == announcementId,
                cancellationToken)
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
            .FirstOrDefaultAsync(
                x => x.Id == announcementId,
                cancellationToken)
            ?? throw new NotFoundException("Elan tapılmadı.");

        if (announcement.IsDeleted)
        {
            announcement.IsDeleted = false;
            announcement.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
        }

        return await GetByIdForAdminAsync(
            announcementId,
            cancellationToken);
    }

    private static IQueryable<AnnouncementDTO> Project(
        IQueryable<Announcement> query)
    {
        return query.Select(x => new AnnouncementDTO
        {
            Id = x.Id,
            Title = x.Title,
            Content = x.Content,
            CreatedByUserId = x.CreatedByUserId,
            CreatedByUserName = x.CreatedByUser.UserName ?? string.Empty,
            IsPublished = x.IsPublished,
            PublishedAtUtc = x.PublishedAtUtc,
            IsDeleted = x.IsDeleted,
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
