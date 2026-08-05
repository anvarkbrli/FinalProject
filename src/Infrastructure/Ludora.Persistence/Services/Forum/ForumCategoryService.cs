using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Helpers;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.Forum;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class ForumCategoryService : IForumCategoryService
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 1000;

    private readonly AppDbContext _context;

    public ForumCategoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ForumCategoryDTO>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.ForumCategories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ForumCategoryDTO
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                Description = x.Description,
                DiscussionCount = x.Discussions.Count,
                IsDeleted = false,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ForumCategoryDTO>> GetDeletedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.ForumCategories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.IsDeleted)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => new ForumCategoryDTO
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                Description = x.Description,
                DiscussionCount = x.Discussions.Count(discussion => !discussion.IsDeleted),
                IsDeleted = true,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ForumCategoryDTO> GetBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug.NormalizeRequiredText("Kateqoriya slug-u", 120)
            .ToLowerInvariant();

        return await _context.ForumCategories
            .AsNoTracking()
            .Where(x => x.Slug == normalizedSlug)
            .Select(x => new ForumCategoryDTO
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                Description = x.Description,
                DiscussionCount = x.Discussions.Count,
                IsDeleted = false,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Forum kateqoriyasÄ± tapÄ±lmadÄ±.");
    }

    public async Task<ForumCategoryDTO> CreateAsync(
        ForumCategoryCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var name = dto.Name.NormalizeRequiredText("Kateqoriya adÄ±", MaxNameLength);
        var description = dto.Description.NormalizeOptionalText(
            "Kateqoriya aÃ§Ä±qlamasÄ±",
            MaxDescriptionLength);

        await _context.ForumCategories.EnsureNameIsAvailableAsync(
            name,
            "Forum kateqoriyasÄ±",
            cancellationToken: cancellationToken);

        var slug = await CreateUniqueSlugAsync(name, null, cancellationToken);

        var category = new ForumCategory
        {
            Name = name,
            Slug = slug,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        await _context.ForumCategories.AddAsync(category, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return await MapCategoryAsync(category.Id, false, cancellationToken);
    }

    public async Task<ForumCategoryDTO> UpdateAsync(
        int id,
        ForumCategoryUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();
        ArgumentNullException.ThrowIfNull(dto);

        var category = await _context.ForumCategories
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Forum kateqoriyasÄ± tapÄ±lmadÄ±.");

        var name = dto.Name.NormalizeRequiredText("Kateqoriya adÄ±", MaxNameLength);
        var description = dto.Description.NormalizeOptionalText(
            "Kateqoriya aÃ§Ä±qlamasÄ±",
            MaxDescriptionLength);

        await _context.ForumCategories.EnsureNameIsAvailableAsync(
            name,
            "Forum kateqoriyasÄ±",
            id,
            cancellationToken);

        category.Name = name;
        category.Slug = await CreateUniqueSlugAsync(name, id, cancellationToken);
        category.Description = description;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return await MapCategoryAsync(category.Id, false, cancellationToken);
    }

    public async Task DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();

        var category = await _context.ForumCategories
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Forum kateqoriyasÄ± tapÄ±lmadÄ±.");

        var hasDiscussions = await _context.Discussions
            .AnyAsync(x => x.CategoryId == id, cancellationToken);

        if (hasDiscussions)
        {
            throw new ConflictException(
                "Bu kateqoriyada mÃ¶vzular var. KateqoriyanÄ± silmÉ™zdÉ™n É™vvÉ™l mÃ¶vzularÄ± baÅŸqa kateqoriyaya kÃ¶Ã§Ã¼rÃ¼n vÉ™ ya silin.");
        }

        category.IsDeleted = true;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ForumCategoryDTO> RestoreAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();

        var category = await _context.ForumCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Forum kateqoriyasÄ± tapÄ±lmadÄ±.");

        if (!category.IsDeleted)
            return await MapCategoryAsync(category.Id, false, cancellationToken);

        var hasConflict = await _context.ForumCategories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(x =>
                    x.Id != category.Id &&
                    !x.IsDeleted &&
                    (x.Name == category.Name || x.Slug == category.Slug),
                cancellationToken);

        if (hasConflict)
        {
            throw new ConflictException(
                "Eyni ad vÉ™ ya slug ilÉ™ aktiv forum kateqoriyasÄ± mÃ¶vcuddur.");
        }

        category.IsDeleted = false;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return await MapCategoryAsync(category.Id, false, cancellationToken);
    }

    private async Task<string> CreateUniqueSlugAsync(
        string name,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var baseSlug = SlugHelper.Generate(name, "category");
        var slug = baseSlug;
        var suffix = 2;

        while (await _context.ForumCategories
                   .IgnoreQueryFilters()
                   .AsNoTracking()
                   .AnyAsync(x =>
                           (!excludedId.HasValue || x.Id != excludedId.Value) &&
                           x.Slug == slug,
                       cancellationToken))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    private async Task<ForumCategoryDTO> MapCategoryAsync(
        int id,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var query = includeDeleted
            ? _context.ForumCategories.IgnoreQueryFilters()
            : _context.ForumCategories;

        return await query
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ForumCategoryDTO
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                Description = x.Description,
                DiscussionCount = x.Discussions.Count(discussion => !discussion.IsDeleted),
                IsDeleted = x.IsDeleted,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .FirstAsync(cancellationToken);
    }
}
