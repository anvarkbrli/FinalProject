using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services.Admin;

public sealed class AdminPostService : IAdminPostService
{
    private const int MaxSearchLength = 200;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _context;

    public AdminPostService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDTO<AdminPostDTO>> GetAllAsync(
        AdminPostFilterDTO filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ValidatePagination(filter.PageNumber, filter.PageSize);

        var search = filter.Search.NormalizeOptionalText("Axtarış mətni", MaxSearchLength);

        var query = _context.UserPosts
            .IgnoreQueryFilters()
            .AsNoTracking();

        if (search is not null)
        {
            query = query.Where(x =>
                x.Content.Contains(search) ||
                (x.User.UserName != null && x.User.UserName.Contains(search)));
        }

        if (filter.UserId.HasValue)
            query = query.Where(x => x.UserId == filter.UserId.Value);

        if (filter.IsDeleted.HasValue)
            query = query.Where(x => x.IsDeleted == filter.IsDeleted.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySorting(query, filter.SortBy);

        var items = await Project(query)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDTO<AdminPostDTO>
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

    public async Task<AdminPostDTO> GetByIdAsync(
        long postId,
        CancellationToken cancellationToken = default)
    {
        if (postId <= 0)
            throw new BadRequestException("Post ID-si düzgün deyil.");

        return await Project(
                _context.UserPosts
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(x => x.Id == postId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Post tapılmadı.");
    }

    public async Task DeleteAsync(
        Guid adminUserId,
        long postId,
        ModeratePostDTO dto,
        CancellationToken cancellationToken = default)
    {
        if (adminUserId == Guid.Empty)
            throw new BadRequestException("Admin istifadəçi ID-si düzgün deyil.");

        if (postId <= 0)
            throw new BadRequestException("Post ID-si düzgün deyil.");

        ArgumentNullException.ThrowIfNull(dto);

        var post = await _context.UserPosts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == postId, cancellationToken)
            ?? throw new NotFoundException("Post tapılmadı.");

        if (post.IsDeleted)
            return;

        post.IsDeleted = true;
        post.DeletedAtUtc = DateTime.UtcNow;
        post.DeletedByUserId = adminUserId;
        post.DeleteReason = dto.Reason.NormalizeOptionalText("Silmə səbəbi", 500);
        post.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminPostDTO> RestoreAsync(
        long postId,
        CancellationToken cancellationToken = default)
    {
        if (postId <= 0)
            throw new BadRequestException("Post ID-si düzgün deyil.");

        var post = await _context.UserPosts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == postId, cancellationToken)
            ?? throw new NotFoundException("Post tapılmadı.");

        if (post.IsDeleted)
        {
            post.IsDeleted = false;
            post.DeletedAtUtc = null;
            post.DeletedByUserId = null;
            post.DeleteReason = null;
            post.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
        }

        return await GetByIdAsync(postId, cancellationToken);
    }

    private static IQueryable<AdminPostDTO> Project(IQueryable<UserPost> query)
    {
        return query.Select(x => new AdminPostDTO
        {
            Id = x.Id,
            UserId = x.UserId,
            UserName = x.User.UserName ?? string.Empty,
            ProfileImageUrl = x.User.ProfileImageUrl,
            Content = x.Content,
            IsDeleted = x.IsDeleted,
            DeletedAtUtc = x.DeletedAtUtc,
            DeletedByUserId = x.DeletedByUserId,
            DeleteReason = x.DeleteReason,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        });
    }

    private static IQueryable<UserPost> ApplySorting(
        IQueryable<UserPost> query,
        string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(x => x.CreatedAt),
            "updated" => query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
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
