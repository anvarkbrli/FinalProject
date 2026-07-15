using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models;
using Ludora.Domain.Entities;
using Ludora.Domain.Entities.IGDB;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class GenreService : IGenreService
{
    private const int MaxNameLength = 100;
    private const int MaxSearchLength = 200;
    private readonly AppDbContext _context;
    private readonly IIgdbClient _igdbClient;

    public GenreService(AppDbContext context, IIgdbClient igdbClient)
    {
        _context = context;
        _igdbClient = igdbClient;
    }

    public async Task<IReadOnlyList<GenreDetailDTO>> GetAllAsync(string? searchName, CatalogSource? source = null, CancellationToken cancellationToken = default)
    {
        if (source.HasValue)
        {
            source.Value.EnsureValidCatalogSource();
        }


        var search = searchName.NormalizeOptionalText("Axtarış mətni",MaxSearchLength);

        var result = new List<GenreDetailDTO>();

        if (!source.HasValue || source == CatalogSource.Local)
        {
            var localQuery = _context.Genres.AsNoTracking();

            if (search is not null)
                localQuery = localQuery.Where(x => x.Name.Contains(search));

            result.AddRange(await localQuery
                .Select(x => new GenreDetailDTO
                {
                    Source = CatalogSource.Local,
                    Id = x.Id,
                    Name = x.Name,
                    IsOverridden = false,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync(cancellationToken));
        }

        var shouldQueryIgdb = source == CatalogSource.Igdb || (!source.HasValue && _igdbClient.IsEnabled);

        if (shouldQueryIgdb)
        {
            IReadOnlyList<IgdbGenreModel> igdbItems;

            try
            {
                igdbItems = await _igdbClient.GetGenresAsync(search, cancellationToken);
            }
            catch (ExternalServiceException) when (!source.HasValue)
            {
                igdbItems = Array.Empty<IgdbGenreModel>();
            }

            var ids = igdbItems.Select(x => x.Id).ToArray();
            var overrides = await _context.Set<IgdbGenreOverride>()
                .AsNoTracking()
                .Where(x => ids.Contains(x.IgdbGenreId))
                .ToDictionaryAsync(x => x.IgdbGenreId, cancellationToken);

            foreach (var item in igdbItems)
            {
                overrides.TryGetValue(item.Id, out var itemOverride);

                if (itemOverride?.IsHidden == true)
                    continue;

                result.Add(new GenreDetailDTO
                {
                    Source = CatalogSource.Igdb,
                    Id = item.Id,
                    Name = itemOverride?.Name ?? item.Name,
                    IsOverridden = itemOverride is not null,
                    CreatedAt = itemOverride?.CreatedAt,
                    UpdatedAt = itemOverride?.UpdatedAt
                });
            }
        }

        return result.OrderBy(x => x.Name).ThenBy(x => x.Source).ToList();
    }

    public async Task<IReadOnlyList<GenreDetailDTO>> GetDeletedAsync(CancellationToken cancellationToken = default)
    {
        var result = await _context.Genres
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.IsDeleted)
            .Select(x => new GenreDetailDTO
            {
                Source = CatalogSource.Local,
                Id = x.Id,
                Name = x.Name,
                IsOverridden = false,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var hiddenOverrides = await _context.Set<IgdbGenreOverride>()
            .AsNoTracking()
            .Where(x => x.IsHidden)
            .ToListAsync(cancellationToken);

        if (hiddenOverrides.Count > 0)
        {
            var igdbNames = new Dictionary<long, string>();

            if (_igdbClient.IsEnabled)
            {
                var ids = hiddenOverrides.Select(x => x.IgdbGenreId).ToArray();

                try
                {
                    var igdbItems = await _igdbClient.GetGenresByIdsAsync(ids, cancellationToken);

                    foreach (var item in igdbItems)
                        igdbNames[item.Id] = item.Name;
                }
                catch (ExternalServiceException)
                {
                    // Override adı və ya IGDB ID fallback-i istifadə olunacaq.
                }
            }

            result.AddRange(hiddenOverrides.Select(x => new GenreDetailDTO
            {
                Source = CatalogSource.Igdb,
                Id = x.IgdbGenreId,
                Name = x.Name ??
                       (igdbNames.TryGetValue(x.IgdbGenreId, out var itemName)
                           ? itemName
                           : $"IGDB #{x.IgdbGenreId}"),
                IsOverridden = true,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }));
        }

        return result.OrderBy(x => x.Name).ToList();
    }

    public async Task<GenreDetailDTO> GetByIdAsync(CatalogSource source, long id, CancellationToken cancellationToken = default)
    {
        id.EnsureValidCatalogReference(source, "Janr");

        if (source == CatalogSource.Local)
        {
            var local = await _context.Genres
                .AsNoTracking()
                .Where(x => x.Id == (int)id)
                .Select(x => new GenreDetailDTO
                {
                    Source = CatalogSource.Local,
                    Id = x.Id,
                    Name = x.Name,
                    IsOverridden = false,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            return local ?? throw new NotFoundException("Janr tapılmadı.");
        }

        var igdbItem = await _igdbClient.GetGenreByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("IGDB janr tapılmadı.");

        var itemOverride = await _context.Set<IgdbGenreOverride>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IgdbGenreId == id, cancellationToken);

        if (itemOverride?.IsHidden == true) throw new NotFoundException("Janr gizlədilib.");

        return new GenreDetailDTO
        {
            Source = CatalogSource.Igdb,
            Id = id,
            Name = itemOverride?.Name ?? igdbItem.Name,
            IsOverridden = itemOverride is not null,
            CreatedAt = itemOverride?.CreatedAt,
            UpdatedAt = itemOverride?.UpdatedAt
        };
    }

    public async Task<long> CreateAsync(GenreCreateDTO createDto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(createDto);
        var name = createDto.Name.NormalizeName(MaxNameLength, "Janr");

        await _context.Genres.EnsureNameIsAvailableAsync(
            name,
            "Janr",
            cancellationToken: cancellationToken);

        var entity = new Genre
        {
            Name = name,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Genres.AddAsync(entity, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
        {
            throw new ConflictException("Bu adda janr artıq mövcuddur.");
        }

        return entity.Id;
    }

    public async Task UpdateAsync(CatalogSource source, long id, GenreUpdateDTO updateDto, CancellationToken cancellationToken = default)
    {
        id.EnsureValidCatalogReference(source, "Janr");
        ArgumentNullException.ThrowIfNull(updateDto);

        if (source == CatalogSource.Local)
        {
            var entity = await _context.Genres
                .FirstOrDefaultAsync(x => x.Id == (int)id, cancellationToken)
                ?? throw new NotFoundException("Janr tapılmadı.");

            if (updateDto.Name is null) return;

            var name = updateDto.Name.NormalizeName(MaxNameLength, "Janr");

            await _context.Genres.EnsureNameIsAvailableAsync(
                name,
                "Janr",
                entity.Id,
                cancellationToken);

            entity.Name = name;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _ = await _igdbClient.GetGenreByIdAsync(id, cancellationToken) ?? throw new NotFoundException("IGDB janr tapılmadı.");

            if (updateDto.Name is null)
                return;

            var name = updateDto.Name.NormalizeName(MaxNameLength, "Janr");
            var itemOverride = await _context.Set<IgdbGenreOverride>().FirstOrDefaultAsync(x => x.IgdbGenreId == id, cancellationToken);

            if (itemOverride is null)
            {
                itemOverride = new IgdbGenreOverride
                {
                    IgdbGenreId = id,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.Set<IgdbGenreOverride>().AddAsync(itemOverride, cancellationToken);
            }

            itemOverride.Name = name;
            itemOverride.IsHidden = false;
            itemOverride.UpdatedAt = DateTime.UtcNow;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
        {
            var message = source == CatalogSource.Local
                ? "Bu adda janr artıq mövcuddur."
                : "IGDB janr məlumatı eyni vaxtda dəyişdirilib. Sorğunu yenidən göndərin.";

            throw new ConflictException(message);
        }
    }

    public async Task DeleteAsync(CatalogSource source, long id, CancellationToken cancellationToken = default)
    {
        id.EnsureValidCatalogReference(source, "Janr");

        if (source == CatalogSource.Local)
        {
            var entity = await _context.Genres
                .FirstOrDefaultAsync(x => x.Id == (int)id, cancellationToken)
                ?? throw new NotFoundException("Janr tapılmadı.");

            await EnsureNotReferencedAsync(source, id, cancellationToken);

            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _ = await _igdbClient.GetGenreByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("IGDB janr tapılmadı.");

            await EnsureNotReferencedAsync(source, id, cancellationToken);

            var itemOverride = await _context.Set<IgdbGenreOverride>().FirstOrDefaultAsync(x => x.IgdbGenreId == id, cancellationToken);

            if (itemOverride is null)
            {
                itemOverride = new IgdbGenreOverride
                {
                    IgdbGenreId = id,
                    IsHidden = true,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.Set<IgdbGenreOverride>().AddAsync(itemOverride, cancellationToken);
            }
            else
            {
                itemOverride.IsHidden = true;
                itemOverride.UpdatedAt = DateTime.UtcNow;
            }
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
        {
            throw new ConflictException("IGDB məlumatı eyni vaxtda dəyişdirilib. Sorğunu yenidən göndərin.");
        }
    }

    public async Task RestoreAsync(CatalogSource source, long id, CancellationToken cancellationToken = default)
    {
        id.EnsureValidCatalogReference(source, "Janr");

        if (source == CatalogSource.Local)
        {
            var genre = await _context.Genres
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    x => x.Id == (int)id,
                    cancellationToken)
                ?? throw new NotFoundException("Janr tapılmadı.");

            if (!genre.IsDeleted)
            {
                throw new ConflictException("Janr onsuz da aktivdir.");
            }

            await _context.Genres.EnsureNameIsAvailableAsync(
                genre.Name,
                "Janr",
                genre.Id,
                cancellationToken);

            genre.IsDeleted = false;
            genre.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _ = await _igdbClient.GetGenreByIdAsync(
                    id,
                    cancellationToken)
                ?? throw new NotFoundException("IGDB janr tapılmadı.");

            var genreOverride = await _context
                .Set<IgdbGenreOverride>()
                .FirstOrDefaultAsync(
                    x => x.IgdbGenreId == id,
                    cancellationToken)
                ?? throw new NotFoundException("Gizlədilmiş IGDB janr tapılmadı.");

            if (!genreOverride.IsHidden)
            {
                throw new ConflictException("IGDB janr artıq aktivdir.");
            }

            genreOverride.IsHidden = false;
            genreOverride.UpdatedAt = DateTime.UtcNow;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation())
        {
            var message = source == CatalogSource.Local
                ? "Bu adda janr artıq mövcuddur."
                : "IGDB janr məlumatı eyni vaxtda dəyişdirilib. Sorğunu yenidən göndərin.";

            throw new ConflictException(message);
        }
    }


    private async Task EnsureNotReferencedAsync(CatalogSource source, long id, CancellationToken cancellationToken)
    {
        var usedByLocalGame = await _context.GameGenres
            .AnyAsync(x =>
                x.GenreSource == source &&
                x.GenreSourceId == id,
                cancellationToken);

        var usedByIgdbOverride = await _context.IgdbGameGenreOverrides
            .AnyAsync(x =>
                x.GenreSource == source &&
                x.GenreSourceId == id,
                cancellationToken);

        if (usedByLocalGame || usedByIgdbOverride)
        {
            throw new ConflictException(
                "Bu janr oyunlarda istifadə olunduğu üçün silinə bilməz.");
        }
    }
}
