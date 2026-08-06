using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models;
using Ludora.Domain.Entities;
using Ludora.Domain.Entities.GameTracking;
using Ludora.Domain.Entities.IGDB;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;

using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services
{
    public sealed class PlatformService : IPlatformService
    {
        private const int MaxNameLength = 100;
        private const int MaxSearchLength = 200;

        private static readonly IReadOnlyDictionary<long, int> IgdbPlatformPriority =
            new Dictionary<long, int>
            {
                [6] = 0,   // PC (Microsoft Windows)
                [167] = 1, // PlayStation 5
                [48] = 2,  // PlayStation 4
                [169] = 3, // Xbox Series X|S
                [130] = 4  // Nintendo Switch
            };

        private readonly AppDbContext _context;
        private readonly IIgdbClient _igdbClient;

        public PlatformService(AppDbContext context, IIgdbClient igdbClient)
        {
            _context = context;
            _igdbClient = igdbClient;
        }

        public async Task<IReadOnlyList<PlatformDetailDTO>> GetAllAsync(
            string? searchName,
            CatalogSource? source = null,
            CancellationToken cancellationToken = default)
        {
            if (source.HasValue)
                source.Value.EnsureValidCatalogSource();

            var search = searchName.NormalizeOptionalText(
                "Axtarış mətni",
                MaxSearchLength);

            var result = new List<PlatformDetailDTO>();

            if (!source.HasValue || source == CatalogSource.Local)
            {
                var localQuery = _context.Platforms.AsNoTracking();

                if (search is not null)
                    localQuery = localQuery.Where(x => x.Name.Contains(search));

                result.AddRange(await localQuery
                    .Select(x => new PlatformDetailDTO
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

            var shouldQueryIgdb = source == CatalogSource.Igdb ||
                                  (!source.HasValue && _igdbClient.IsEnabled);

            if (shouldQueryIgdb)
            {
                IReadOnlyList<IgdbPlatformModel> igdbItems;

                try
                {
                    igdbItems = await _igdbClient.GetPlatformsAsync(search, cancellationToken);
                }
                catch (ExternalServiceException) when (!source.HasValue)
                {
                    igdbItems = Array.Empty<IgdbPlatformModel>();
                }

                var ids = igdbItems.Select(x => x.Id).ToArray();
                var overrides = await _context.Set<IgdbPlatformOverride>()
                    .AsNoTracking()
                    .Where(x => ids.Contains(x.IgdbPlatformId))
                    .ToDictionaryAsync(x => x.IgdbPlatformId, cancellationToken);

                foreach (var item in igdbItems)
                {
                    overrides.TryGetValue(item.Id, out var itemOverride);

                    if (itemOverride?.IsHidden == true)
                        continue;

                    result.Add(new PlatformDetailDTO
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

            return result
                .OrderBy(GetPlatformPriority)
                .ThenBy(
                    x => x.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Source)
                .ToList();
        }

        private static int GetPlatformPriority(
            PlatformDetailDTO platform)
        {
            if (platform.Source == CatalogSource.Igdb &&
                IgdbPlatformPriority.TryGetValue(platform.Id, out var priority))
            {
                return priority;
            }

            return int.MaxValue;
        }

        public async Task<IReadOnlyList<PlatformDetailDTO>> GetDeletedAsync(
            CancellationToken cancellationToken = default)
        {
            var result = await _context.Platforms
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.IsDeleted)
                .Select(x => new PlatformDetailDTO
                {
                    Source = CatalogSource.Local,
                    Id = x.Id,
                    Name = x.Name,
                    IsOverridden = false,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            var hiddenOverrides = await _context.Set<IgdbPlatformOverride>()
                .AsNoTracking()
                .Where(x => x.IsHidden)
                .ToListAsync(cancellationToken);

            if (hiddenOverrides.Count > 0)
            {
                var igdbNames = new Dictionary<long, string>();

                if (_igdbClient.IsEnabled)
                {
                    var ids = hiddenOverrides.Select(x => x.IgdbPlatformId).ToArray();

                    try
                    {
                        var igdbItems = await _igdbClient.GetPlatformsByIdsAsync(ids, cancellationToken);

                        foreach (var item in igdbItems)
                            igdbNames[item.Id] = item.Name;
                    }
                    catch (ExternalServiceException)
                    {
                        // Override adı və ya IGDB ID fallback-i istifadə olunacaq.
                    }
                }

                result.AddRange(hiddenOverrides.Select(x => new PlatformDetailDTO
                {
                    Source = CatalogSource.Igdb,
                    Id = x.IgdbPlatformId,
                    Name = x.Name ??
                           (igdbNames.TryGetValue(x.IgdbPlatformId, out var itemName)
                               ? itemName
                               : $"IGDB #{x.IgdbPlatformId}"),
                    IsOverridden = true,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                }));
            }

            return result.OrderBy(x => x.Name).ToList();
        }

        public async Task<PlatformDetailDTO> GetByIdAsync(
            CatalogSource source,
            long id,
            CancellationToken cancellationToken = default)
        {
            id.EnsureValidCatalogReference(source, "Platforma");

            if (source == CatalogSource.Local)
            {
                var local = await _context.Platforms
                    .AsNoTracking()
                    .Where(x => x.Id == (int)id)
                    .Select(x => new PlatformDetailDTO
                    {
                        Source = CatalogSource.Local,
                        Id = x.Id,
                        Name = x.Name,
                        IsOverridden = false,
                        CreatedAt = x.CreatedAt,
                        UpdatedAt = x.UpdatedAt
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                return local ?? throw new NotFoundException("Platforma tapılmadı.");
            }

            var igdbItem = await _igdbClient.GetPlatformByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("IGDB platforma tapılmadı.");

            var itemOverride = await _context.Set<IgdbPlatformOverride>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IgdbPlatformId == id, cancellationToken);

            if (itemOverride?.IsHidden == true)
                throw new NotFoundException("Platforma gizlədilib.");

            return new PlatformDetailDTO
            {
                Source = CatalogSource.Igdb,
                Id = id,
                Name = itemOverride?.Name ?? igdbItem.Name,
                IsOverridden = itemOverride is not null,
                CreatedAt = itemOverride?.CreatedAt,
                UpdatedAt = itemOverride?.UpdatedAt
            };
        }

        public async Task<long> CreateAsync(
            PlatformCreateDTO createDto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(createDto);
            var name = createDto.Name.NormalizeName(MaxNameLength, "Platforma");

            await _context.Platforms.EnsureNameIsAvailableAsync(
                name,
                "Platforma",
                cancellationToken: cancellationToken);

            var entity = new Platform
            {
                Name = name,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Platforms.AddAsync(entity, cancellationToken);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
            {
                throw new ConflictException("Bu adda platforma artıq mövcuddur.");
            }

            return entity.Id;
        }

        public async Task UpdateAsync(
            CatalogSource source,
            long id,
            PlatformUpdateDTO updateDto,
            CancellationToken cancellationToken = default)
        {
            id.EnsureValidCatalogReference(source, "Platforma");
            ArgumentNullException.ThrowIfNull(updateDto);

            if (source == CatalogSource.Local)
            {
                var entity = await _context.Platforms
                    .FirstOrDefaultAsync(x => x.Id == (int)id, cancellationToken)
                    ?? throw new NotFoundException("Platforma tapılmadı.");

                if (updateDto.Name is null)
                    return;

                var name = updateDto.Name.NormalizeName(MaxNameLength, "Platforma");

                await _context.Platforms.EnsureNameIsAvailableAsync(
                    name,
                    "Platforma",
                    entity.Id,
                    cancellationToken);

                entity.Name = name;
                entity.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _ = await _igdbClient.GetPlatformByIdAsync(id, cancellationToken)
                    ?? throw new NotFoundException("IGDB platforma tapılmadı.");

                if (updateDto.Name is null)
                    return;

                var name = updateDto.Name.NormalizeName(MaxNameLength, "Platforma");
                var itemOverride = await _context.Set<IgdbPlatformOverride>()
                    .FirstOrDefaultAsync(x => x.IgdbPlatformId == id, cancellationToken);

                if (itemOverride is null)
                {
                    itemOverride = new IgdbPlatformOverride
                    {
                        IgdbPlatformId = id,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _context.Set<IgdbPlatformOverride>().AddAsync(itemOverride, cancellationToken);
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
                    ? "Bu adda platforma artıq mövcuddur."
                    : "IGDB platforma məlumatı eyni vaxtda dəyişdirilib. Sorğunu yenidən göndərin.";

                throw new ConflictException(message);
            }
        }

        public async Task DeleteAsync(
            CatalogSource source,
            long id,
            CancellationToken cancellationToken = default)
        {
            id.EnsureValidCatalogReference(source, "Platforma");

            if (source == CatalogSource.Local)
            {
                var entity = await _context.Platforms
                    .FirstOrDefaultAsync(x => x.Id == (int)id, cancellationToken)
                    ?? throw new NotFoundException("Platforma tapılmadı.");

                await EnsureNotReferencedAsync(source, id, cancellationToken);

                entity.IsDeleted = true;
                entity.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _ = await _igdbClient.GetPlatformByIdAsync(id, cancellationToken)
                    ?? throw new NotFoundException("IGDB platforma tapılmadı.");

                await EnsureNotReferencedAsync(source, id, cancellationToken);

                var itemOverride = await _context.Set<IgdbPlatformOverride>()
                    .FirstOrDefaultAsync(x => x.IgdbPlatformId == id, cancellationToken);

                if (itemOverride is null)
                {
                    itemOverride = new IgdbPlatformOverride
                    {
                        IgdbPlatformId = id,
                        IsHidden = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _context.Set<IgdbPlatformOverride>().AddAsync(itemOverride, cancellationToken);
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
                throw new ConflictException(
                    "IGDB məlumatı eyni vaxtda dəyişdirilib. Sorğunu yenidən göndərin.");
            }
        }

        public async Task RestoreAsync(CatalogSource source, long id, CancellationToken cancellationToken = default)
        {
            id.EnsureValidCatalogReference(source, "Platforma");

            if (source == CatalogSource.Local)
            {
                var entity = await _context.Platforms
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.Id == (int)id, cancellationToken)
                    ?? throw new NotFoundException("Platforma tapılmadı.");

                if (!entity.IsDeleted) throw new ConflictException("Platforma artıq aktivdir.");

                await _context.Platforms.EnsureNameIsAvailableAsync(
                    entity.Name,
                    "Platforma",
                    entity.Id,
                    cancellationToken);

                entity.IsDeleted = false;
                entity.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _ = await _igdbClient.GetPlatformByIdAsync(id, cancellationToken)
                    ?? throw new NotFoundException("IGDB platforma tapılmadı.");

                var itemOverride = await _context.Set<IgdbPlatformOverride>()
                    .FirstOrDefaultAsync(x => x.IgdbPlatformId == id, cancellationToken)
                    ?? throw new NotFoundException("Gizlədilmiş IGDB platforma tapılmadı.");

                if (!itemOverride.IsHidden)
                    throw new ConflictException("IGDB platforma artıq aktivdir");

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
                    ? "Bu adda platforma artıq mövcuddur."
                    : "IGDB platforma məlumatı eyni vaxtda dəyişdirilib. Sorğunu yenidən göndərin.";

                throw new ConflictException(message);
            }
        }

        private async Task EnsureNotReferencedAsync(
            CatalogSource source,
            long id,
            CancellationToken cancellationToken)
        {
            var usedByLocalGame = await _context.GamePlatforms
                .AnyAsync(x =>
                    x.PlatformSource == source &&
                    x.PlatformSourceId == id,
                    cancellationToken);

            var usedByIgdbOverride = await _context.IgdbGamePlatformOverrides
                .AnyAsync(x =>
                    x.PlatformSource == source &&
                    x.PlatformSourceId == id,
                    cancellationToken);

            if (usedByLocalGame || usedByIgdbOverride)
            {
                throw new ConflictException(
                    "Bu platforma oyunlarda istifadə olunduğu üçün silinə bilməz.");
            }
        }
    }
}
