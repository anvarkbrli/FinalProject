using Ludora.Application.DTOs;
using Ludora.Application.DTOs.GameList;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public class GameService : IGameService
{
    private readonly AppDbContext _context;
    private readonly IIgdbClient _igdbClient;

    public GameService(AppDbContext context, IIgdbClient igdbClient)
    {
        _context = context;
        _igdbClient = igdbClient;
    }

    public async Task<GameDetailDTO> GetByIdAsync(int id)
    {
        if (id < 1)
            throw new ArgumentException("Oyun ID-si düzgün deyil.");

        var game = await _context.Games
            .AsNoTracking()
            .Where(g => g.Id == id && !g.IsDeleted)
            .Select(g => new GameDetailDTO
            {
                Id = g.Id,
                Title = g.Name,
                Description = g.Description,
                ReleaseDate = g.ReleaseDate,
                CoverImageUrl = g.CoverImageUrl,
                DeveloperCompanyName = g.DeveloperCompany.Name,
                PlayCount = g.PlayCount,
                AverageRating = g.AverageRating,
                Genres = g.GameGenres
                    .Where(gg => !gg.Genre.IsDeleted)
                    .Select(gg => gg.Genre.Name)
                    .OrderBy(name => name)
                    .ToList(),
                Platforms = g.GamePlatforms
                    .Where(gp => !gp.Platform.IsDeleted)
                    .Select(gp => gp.Platform.Name)
                    .OrderBy(name => name)
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (game == null)
            throw new KeyNotFoundException("Oyun tapılmadı.");

        return game;
    }

    public async Task CreateAsync(GameCreateDTO createDto)
    {
        ArgumentNullException.ThrowIfNull(createDto);

        var title = NormalizeRequiredText(createDto.Title, "Oyunun adı", 200);
        var description = NormalizeRequiredText(createDto.Description, "Oyunun təsviri", 4000);
        var coverImageUrl = NormalizeRequiredText(createDto.CoverImageUrl, "Cover şəkil linki", 2048);

        if (createDto.ReleaseDate == default)
            throw new ArgumentException("Oyunun çıxış tarixi düzgün daxil edilməyib.");

        await ValidateCompanyAsync(createDto.DeveloperCompanyId);

        var genreIds = NormalizeIds(createDto.GenreIds, "Janr");
        var platformIds = NormalizeIds(createDto.PlatformIds, "Platforma");

        await ValidateGenreIdsAsync(genreIds);
        await ValidatePlatformIdsAsync(platformIds);

        var game = new Game
        {
            Name = title,
            Description = description,
            ReleaseDate = createDto.ReleaseDate,
            CoverImageUrl = coverImageUrl,
            DeveloperCompanyId = createDto.DeveloperCompanyId,
            GameGenres = genreIds.Select(genreId => new GameGenre
            {
                GenreId = genreId
            }).ToList(),
            GamePlatforms = platformIds.Select(platformId => new GamePlatform
            {
                PlatformId = platformId
            }).ToList()
        };

        await _context.Games.AddAsync(game);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(int id, GameUpdateDTO updateDto)
    {
        if (id < 1) throw new ArgumentException("Oyun ID-si düzgün deyil.");

        ArgumentNullException.ThrowIfNull(updateDto);

        var game = await _context.Games
            .Include(g => g.GameGenres)
            .Include(g => g.GamePlatforms)
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

        if (game == null) throw new KeyNotFoundException("Oyun tapılmadı.");

        var hasChanges =
            updateDto.Title != null ||
            updateDto.Description != null ||
            updateDto.ReleaseDate.HasValue ||
            updateDto.CoverImageUrl != null ||
            updateDto.DeveloperCompanyId.HasValue ||
            updateDto.GenreIds != null ||
            updateDto.PlatformIds != null;

        if (!hasChanges)
            return;

        if (updateDto.Title != null)
            game.Name = NormalizeRequiredText(updateDto.Title, "Oyunun adı", 200);

        if (updateDto.Description != null)
            game.Description = NormalizeRequiredText(updateDto.Description, "Oyunun təsviri", 4000);

        if (updateDto.CoverImageUrl != null)
            game.CoverImageUrl = NormalizeRequiredText(updateDto.CoverImageUrl, "Cover şəkil linki", 2048);

        if (updateDto.ReleaseDate.HasValue)
        {
            if (updateDto.ReleaseDate.Value == default)
                throw new ArgumentException("Oyunun çıxış tarixi düzgün deyil.");

            game.ReleaseDate = updateDto.ReleaseDate.Value;
        }

        if (updateDto.DeveloperCompanyId.HasValue)
        {
            await ValidateCompanyAsync(updateDto.DeveloperCompanyId.Value);
            game.DeveloperCompanyId = updateDto.DeveloperCompanyId.Value;
        }

        if (updateDto.GenreIds != null)
        {
            var genreIds = NormalizeIds(updateDto.GenreIds, "Janr");

            await ValidateGenreIdsAsync(genreIds);
            UpdateGameGenres(game, genreIds);
        }

        if (updateDto.PlatformIds != null)
        {
            var platformIds = NormalizeIds(updateDto.PlatformIds, "Platforma");

            await ValidatePlatformIdsAsync(platformIds);
            UpdateGamePlatforms(game, platformIds);
        }

        game.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        if (id < 1)
            throw new ArgumentException("Oyun ID-si düzgün deyil.");

        var game = await _context.Games
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

        if (game == null)
            throw new KeyNotFoundException("Oyun tapılmadı və ya artıq silinib.");

        game.IsDeleted = true;
        game.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<List<LookupDTO>> GetCompaniesLookupAsync()
    {
        return await _context.Companies
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new LookupDTO
            {
                Id = c.Id,
                Name = c.Name
            })
            .ToListAsync();
    }

    public async Task<List<LookupDTO>> GetGenresLookupAsync()
    {
        return await _context.Genres
            .AsNoTracking()
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.Name)
            .Select(g => new LookupDTO
            {
                Id = g.Id,
                Name = g.Name
            })
            .ToListAsync();
    }

    public async Task<List<LookupDTO>> GetPlatformsLookupAsync()
    {
        return await _context.Platforms
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .Select(p => new LookupDTO
            {
                Id = p.Id,
                Name = p.Name
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<GameListItemDTO>> GetFilteredGamesAsync(GameFilterDTO filterDto)
    {
        ArgumentNullException.ThrowIfNull(filterDto);

        ValidateFilterIds(filterDto);

        var query = _context.Games
            .AsNoTracking()
            .Where(g => !g.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filterDto.SearchName))
        {
            var searchName = filterDto.SearchName.Trim();
            query = query.Where(g => g.Name.Contains(searchName));
        }

        if (filterDto.CompanyId.HasValue)
            query = query.Where(g => g.DeveloperCompanyId == filterDto.CompanyId.Value);

        if (filterDto.GenreId.HasValue)
        {
            query = query.Where(g => g.GameGenres.Any(gg =>
                gg.GenreId == filterDto.GenreId.Value &&
                !gg.Genre.IsDeleted));
        }

        if (filterDto.PlatformId.HasValue)
        {
            query = query.Where(g => g.GamePlatforms.Any(gp =>
                gp.PlatformId == filterDto.PlatformId.Value &&
                !gp.Platform.IsDeleted));
        }

        if (filterDto.ReleaseYear.HasValue)
            query = query.Where(g => g.ReleaseDate.Year == filterDto.ReleaseYear.Value.Year);

        query = filterDto.SortBy?.Trim().ToLowerInvariant() switch
        {
            "name_asc" => query.OrderBy(g => g.Name),
            "name_desc" => query.OrderByDescending(g => g.Name),
            "rating_asc" => query.OrderBy(g => g.AverageRating),
            "rating_desc" => query.OrderByDescending(g => g.AverageRating),
            "play_asc" => query.OrderBy(g => g.PlayCount),
            "play_desc" => query.OrderByDescending(g => g.PlayCount),
            "year_asc" => query.OrderBy(g => g.ReleaseDate),
            "year_desc" => query.OrderByDescending(g => g.ReleaseDate),
            "company_asc" => query.OrderBy(g => g.DeveloperCompany.Name),
            "company_desc" => query.OrderByDescending(g => g.DeveloperCompany.Name),
            _ => query.OrderByDescending(g => g.Id)
        };

        var pageNumber = filterDto.PageNumber < 1 ? 1 : filterDto.PageNumber;

        var pageSize = filterDto.PageSize switch
        {
            < 1 => 10,
            > 100 => 100,
            _ => filterDto.PageSize
        };

        var result = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GameListItemDTO
            {
                Id = g.Id,
                Title = g.Name,
                CoverImageUrl = g.CoverImageUrl,
                DeveloperCompanyName = g.DeveloperCompany.Name,
                PlayCount = g.PlayCount,
                AverageRating = g.AverageRating,
                ReleaseDate = g.ReleaseDate,
                Genres = g.GameGenres
                    .Where(gg => !gg.Genre.IsDeleted)
                    .Select(gg => gg.Genre.Name)
                    .OrderBy(name => name)
                    .ToList(),
                Platforms = g.GamePlatforms
                    .Where(gp => !gp.Platform.IsDeleted)
                    .Select(gp => gp.Platform.Name)
                    .OrderBy(name => name)
                    .ToList()
            })
            .ToListAsync();

        return result;
    }

    private async Task ValidateCompanyAsync(int companyId)
    {
        if (companyId < 1)
            throw new ArgumentException("Şirkət ID-si düzgün deyil.");

        var companyExists = await _context.Companies
            .AnyAsync(c => c.Id == companyId && !c.IsDeleted);

        if (!companyExists)
            throw new ArgumentException($"ID-si {companyId} olan şirkət tapılmadı.");
    }

    private async Task ValidateGenreIdsAsync(IReadOnlyCollection<int> genreIds)
    {
        if (genreIds.Count == 0)
            return;

        var existingGenreIds = await _context.Genres
            .Where(g => genreIds.Contains(g.Id) && !g.IsDeleted)
            .Select(g => g.Id)
            .ToListAsync();

        var missingGenreIds = genreIds
            .Except(existingGenreIds)
            .OrderBy(id => id)
            .ToList();

        if (missingGenreIds.Count > 0)
            throw new ArgumentException("Bu janr ID-ləri tapılmadı: " + string.Join(", ", missingGenreIds));
    }

    private async Task ValidatePlatformIdsAsync(IReadOnlyCollection<int> platformIds)
    {
        if (platformIds.Count == 0)
            return;

        var existingPlatformIds = await _context.Platforms
            .Where(p => platformIds.Contains(p.Id) && !p.IsDeleted)
            .Select(p => p.Id)
            .ToListAsync();

        var missingPlatformIds = platformIds
            .Except(existingPlatformIds)
            .OrderBy(id => id)
            .ToList();

        if (missingPlatformIds.Count > 0)
            throw new ArgumentException("Bu platforma ID-ləri tapılmadı: " + string.Join(", ", missingPlatformIds));
    }

    private void UpdateGameGenres(Game game, IReadOnlyCollection<int> requestedGenreIds)
    {
        var requestedIds = requestedGenreIds.ToHashSet();

        var relationsToRemove = game.GameGenres
            .Where(gg => !requestedIds.Contains(gg.GenreId))
            .ToList();

        _context.GameGenres.RemoveRange(relationsToRemove);

        foreach (var relation in relationsToRemove)
            game.GameGenres.Remove(relation);

        var currentGenreIds = game.GameGenres
            .Select(gg => gg.GenreId)
            .ToHashSet();

        var genreIdsToAdd = requestedIds.Except(currentGenreIds);

        foreach (var genreId in genreIdsToAdd)
        {
            game.GameGenres.Add(new GameGenre
            {
                GameId = game.Id,
                GenreId = genreId
            });
        }
    }

    private void UpdateGamePlatforms(Game game, IReadOnlyCollection<int> requestedPlatformIds)
    {
        var requestedIds = requestedPlatformIds.ToHashSet();

        var relationsToRemove = game.GamePlatforms
            .Where(gp => !requestedIds.Contains(gp.PlatformId))
            .ToList();

        _context.GamePlatforms.RemoveRange(relationsToRemove);

        foreach (var relation in relationsToRemove)
            game.GamePlatforms.Remove(relation);

        var currentPlatformIds = game.GamePlatforms
            .Select(gp => gp.PlatformId)
            .ToHashSet();

        var platformIdsToAdd = requestedIds.Except(currentPlatformIds);

        foreach (var platformId in platformIdsToAdd)
        {
            game.GamePlatforms.Add(new GamePlatform
            {
                GameId = game.Id,
                PlatformId = platformId
            });
        }
    }

    private static List<int> NormalizeIds(IEnumerable<int>? ids, string fieldName)
    {
        if (ids == null)
            return new List<int>();

        var idList = ids.ToList();

        if (idList.Any(id => id < 1))
            throw new ArgumentException($"{fieldName} ID-si 1-dən kiçik ola bilməz.");

        return idList.Distinct().ToList();
    }

    private static string NormalizeRequiredText(string? value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{fieldName} boş ola bilməz.");

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > maximumLength)
            throw new ArgumentException($"{fieldName} maksimum {maximumLength} simvol ola bilər.");

        return normalizedValue;
    }

    private static void ValidateFilterIds(GameFilterDTO filterDto)
    {
        if (filterDto.CompanyId.HasValue && filterDto.CompanyId.Value < 1)
            throw new ArgumentException("Şirkət ID-si düzgün deyil.");

        if (filterDto.GenreId.HasValue && filterDto.GenreId.Value < 1)
            throw new ArgumentException("Janr ID-si düzgün deyil.");

        if (filterDto.PlatformId.HasValue && filterDto.PlatformId.Value < 1)
            throw new ArgumentException("Platforma ID-si düzgün deyil.");
    }
}