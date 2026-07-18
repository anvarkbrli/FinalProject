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

namespace Ludora.Persistence.Services;

public sealed class CompanyService : ICompanyService
{
    private const int MaxNameLength = 200;
    private const int MaxSearchLength = 200;
    private readonly AppDbContext _context;
    private readonly IIgdbClient _igdbClient;

    public CompanyService(AppDbContext context, IIgdbClient igdbClient)
    {
        _context = context;
        _igdbClient = igdbClient;
    }

    public async Task<IReadOnlyList<CompanyDetailDTO>> GetAllAsync(
        string? searchName,
        CatalogSource? source = null,
        CancellationToken cancellationToken = default)
    {
        if (source.HasValue)
            source.Value.EnsureValidCatalogSource();

        var search = searchName.NormalizeOptionalText(
            "Axtarış mətni",
            MaxSearchLength);

        var result = new List<CompanyDetailDTO>();

        if (!source.HasValue || source == CatalogSource.Local)
        {
            var localQuery = _context.Companies.AsNoTracking();

            if (search is not null)
                localQuery = localQuery.Where(x => x.Name.Contains(search));

            result.AddRange(await localQuery
                .Select(x => new CompanyDetailDTO
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
            IReadOnlyList<IgdbCompanyModel> igdbItems;

            try
            {
                igdbItems = await _igdbClient.GetCompaniesAsync(search, cancellationToken);
            }
            catch (ExternalServiceException) when (!source.HasValue)
            {
                igdbItems = Array.Empty<IgdbCompanyModel>();
            }

            var ids = igdbItems.Select(x => x.Id).ToArray();
            var overrides = await _context.Set<IgdbCompanyOverride>()
                .AsNoTracking()
                .Where(x => ids.Contains(x.IgdbCompanyId))
                .ToDictionaryAsync(x => x.IgdbCompanyId, cancellationToken);

            foreach (var item in igdbItems)
            {
                overrides.TryGetValue(item.Id, out var itemOverride);

                if (itemOverride?.IsHidden == true)
                    continue;

                result.Add(new CompanyDetailDTO
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
            .OrderBy(
                x => x.Name,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Source)
            .ToList();
    }

    public async Task<IReadOnlyList<CompanyDetailDTO>> GetDeletedAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await _context.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.IsDeleted)
            .Select(x => new CompanyDetailDTO
            {
                Source = CatalogSource.Local,
                Id = x.Id,
                Name = x.Name,
                IsOverridden = false,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var hiddenOverrides = await _context.Set<IgdbCompanyOverride>()
            .AsNoTracking()
            .Where(x => x.IsHidden)
            .ToListAsync(cancellationToken);

        if (hiddenOverrides.Count > 0)
        {
            var igdbNames = new Dictionary<long, string>();

            if (_igdbClient.IsEnabled)
            {
                var ids = hiddenOverrides.Select(x => x.IgdbCompanyId).ToArray();

                try
                {
                    var igdbItems = await _igdbClient.GetCompaniesByIdsAsync(ids, cancellationToken);

                    foreach (var item in igdbItems)
                        igdbNames[item.Id] = item.Name;
                }
                catch (ExternalServiceException)
                {
                    // Override adı və ya IGDB ID fallback-i istifadə olunacaq.
                }
            }

            result.AddRange(hiddenOverrides.Select(x => new CompanyDetailDTO
            {
                Source = CatalogSource.Igdb,
                Id = x.IgdbCompanyId,
                Name = x.Name ??
                       (igdbNames.TryGetValue(x.IgdbCompanyId, out var itemName)
                           ? itemName
                           : $"IGDB #{x.IgdbCompanyId}"),
                IsOverridden = true,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }));
        }

        return result.OrderBy(x => x.Name).ToList();
    }

    public async Task<CompanyDetailDTO> GetByIdAsync(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidCatalogReference(source, "Şirkət");

        if (source == CatalogSource.Local)
        {
            var local = await _context.Companies
                .AsNoTracking()
                .Where(x => x.Id == (int)id)
                .Select(x => new CompanyDetailDTO
                {
                    Source = CatalogSource.Local,
                    Id = x.Id,
                    Name = x.Name,
                    IsOverridden = false,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            return local ?? throw new NotFoundException("Şirkət tapılmadı.");
        }

        var igdbItem = await _igdbClient.GetCompanyByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("IGDB şirkət tapılmadı.");

        var itemOverride = await _context.Set<IgdbCompanyOverride>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IgdbCompanyId == id, cancellationToken);

        if (itemOverride?.IsHidden == true)
            throw new NotFoundException("Şirkət gizlədilib.");

        return new CompanyDetailDTO
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
        CompanyCreateDTO createDto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(createDto);
        var name = createDto.Name.NormalizeName(MaxNameLength, "Şirkət");

        await _context.Companies.EnsureNameIsAvailableAsync(
            name,
            "Şirkət",
            cancellationToken: cancellationToken);

        var entity = new Company
        {
            Name = name,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Companies.AddAsync(entity, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
        {
            throw new ConflictException("Bu adda şirkət artıq mövcuddur.");
        }

        return entity.Id;
    }

    public async Task UpdateAsync(
        CatalogSource source,
        long id,
        CompanyUpdateDTO updateDto,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidCatalogReference(source, "Şirkət");
        ArgumentNullException.ThrowIfNull(updateDto);

        if (source == CatalogSource.Local)
        {
            var entity = await _context.Companies
                .FirstOrDefaultAsync(x => x.Id == (int)id, cancellationToken)
                ?? throw new NotFoundException("Şirkət tapılmadı.");

            if (updateDto.Name is null)
                return;

            var name = updateDto.Name.NormalizeName(MaxNameLength, "Şirkət");

            await _context.Companies.EnsureNameIsAvailableAsync(
                name,
                "Şirkət",
                entity.Id,
                cancellationToken);

            entity.Name = name;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _ = await _igdbClient.GetCompanyByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("IGDB şirkət tapılmadı.");

            if (updateDto.Name is null)
                return;

            var name = updateDto.Name.NormalizeName(MaxNameLength, "Şirkət");
            var itemOverride = await _context.Set<IgdbCompanyOverride>()
                .FirstOrDefaultAsync(x => x.IgdbCompanyId == id, cancellationToken);

            if (itemOverride is null)
            {
                itemOverride = new IgdbCompanyOverride
                {
                    IgdbCompanyId = id,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.Set<IgdbCompanyOverride>().AddAsync(itemOverride, cancellationToken);
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
                ? "Bu adda şirkət artıq mövcuddur."
                : "IGDB şirkət məlumatı eyni vaxtda dəyişdirilib. Sorğunu yenidən göndərin.";

            throw new ConflictException(message);
        }
    }

    public async Task DeleteAsync(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidCatalogReference(source, "Şirkət");

        if (source == CatalogSource.Local)
        {
            var entity = await _context.Companies
                .FirstOrDefaultAsync(x => x.Id == (int)id, cancellationToken)
                ?? throw new NotFoundException("Şirkət tapılmadı.");

            await EnsureNotReferencedAsync(source, id, cancellationToken);

            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _ = await _igdbClient.GetCompanyByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("IGDB şirkət tapılmadı.");

            await EnsureNotReferencedAsync(source, id, cancellationToken);

            var itemOverride = await _context.Set<IgdbCompanyOverride>()
                .FirstOrDefaultAsync(x => x.IgdbCompanyId == id, cancellationToken);

            if (itemOverride is null)
            {
                itemOverride = new IgdbCompanyOverride
                {
                    IgdbCompanyId = id,
                    IsHidden = true,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.Set<IgdbCompanyOverride>().AddAsync(itemOverride, cancellationToken);
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
        id.EnsureValidCatalogReference(source, "Şirkət");

        if (source == CatalogSource.Local)
        {
            var entity = await _context.Companies
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == (int)id, cancellationToken)
                ?? throw new NotFoundException("Şirkət tapılmadı.");

            if (!entity.IsDeleted)
            {
                throw new ConflictException("Şirkət onsuz da aktivdir.");
            }

            await _context.Companies.EnsureNameIsAvailableAsync(
                entity.Name,
                "Şirkət",
                entity.Id,
                cancellationToken);

            entity.IsDeleted = false;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _ = await _igdbClient.GetCompanyByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("IGDB şirkət tapılmadı.");

            var itemOverride = await _context.Set<IgdbCompanyOverride>()
                .FirstOrDefaultAsync(x => x.IgdbCompanyId == id, cancellationToken)
                ?? throw new NotFoundException("Gizlədilmiş IGDB şirkət tapılmadı.");

            if (!itemOverride.IsHidden)
                throw new ConflictException("IGDB şirkət artıq aktivdir");

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
                ? "Bu adda şirkət artıq mövcuddur."
                : "IGDB şirkət məlumatı eyni vaxtda dəyişdirilib. Sorğunu yenidən göndərin.";

            throw new ConflictException(message);
        }
    }

    private async Task EnsureNotReferencedAsync(CatalogSource source, long id, CancellationToken cancellationToken)
    {
        var usedByLocalGame = await _context.GameCompanies
            .AnyAsync(x =>
                x.CompanySource == source &&
                x.CompanySourceId == id,
                cancellationToken);

        var usedByIgdbOverride = await _context.IgdbGameCompanyOverrides
            .AnyAsync(x =>
                x.CompanySource == source &&
                x.CompanySourceId == id,
                cancellationToken);

        if (usedByLocalGame || usedByIgdbOverride)
        {
            throw new ConflictException("Bu şirkət oyunlarda istifadə olunduğu üçün silinə bilməz.");
        }
    }
}
