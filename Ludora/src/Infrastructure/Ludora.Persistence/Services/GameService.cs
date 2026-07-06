using AutoMapper;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

public class GameService : IGameService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public GameService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<GameDetailDTO> GetByIdAsync(int id)
    {
        var game = await _context.Games
            .Include(g => g.DeveloperCompany)
            .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
            .Include(g => g.GamePlatforms).ThenInclude(gp => gp.Platform)
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

        if (game == null) throw new KeyNotFoundException("Oyun tapılmadı.");

        return _mapper.Map<GameDetailDTO>(game);
    }

    public async Task<List<GameListItemDTO>> GetFilteredGamesAsync(GameFilterDTO filter)
    {
        var query = _context.Games
            .Include(g => g.DeveloperCompany)
            .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
            .Include(g => g.GamePlatforms).ThenInclude(gp => gp.Platform)
            .Where(g => !g.IsDeleted);

        // 1. Dinamik Filtrləmə
        if (!string.IsNullOrWhiteSpace(filter.SearchName))
            query = query.Where(g => g.Name.ToLower().Contains(filter.SearchName.ToLower()));

        if (filter.CompanyId.HasValue)
            query = query.Where(g => g.DeveloperCompanyId == filter.CompanyId.Value);

        if (filter.GenreId.HasValue)
            query = query.Where(g => g.GameGenres.Any(gg => gg.GenreId == filter.GenreId.Value));

        if (filter.PlatformId.HasValue)
            query = query.Where(g => g.GamePlatforms.Any(gp => gp.PlatformId == filter.PlatformId.Value));

        if (filter.ReleaseYear.HasValue)
            query = query.Where(g => g.ReleaseDate.Year == filter.ReleaseYear.Value);

        // 2. Dinamik Sıralama (Sorting)
        query = filter.SortBy?.ToLower() switch
        {
            "name_asc" => query.OrderBy(g => g.Name),
            "name_desc" => query.OrderByDescending(g => g.Name),
            "rating_desc" => query.OrderByDescending(g => g.AverageRating),
            "play_desc" => query.OrderByDescending(g => g.PlayCount),
            "year_desc" => query.OrderByDescending(g => g.ReleaseDate),
            _ => query.OrderByDescending(g => g.CreatedAt) // Default olaraq ən yeni əlavə edilənlər
        };

        // 3. Səhifələmə (Pagination)
        var pagedGames = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return _mapper.Map<List<GameListItemDTO>>(pagedGames);
    }

    public async Task CreateAsync(GameCreateDTO createDto)
    {
        var game = _mapper.Map<Game>(createDto);

        if (createDto.GenreIds != null)
            game.GameGenres = createDto.GenreIds.Select(id => new GameGenre { GenreId = id }).ToList();

        if (createDto.PlatformIds != null)
            game.GamePlatforms = createDto.PlatformIds.Select(id => new GamePlatform { PlatformId = id }).ToList();

        await _context.Games.AddAsync(game);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(int id, GameUpdateDTO updateDto)
    {
        var game = await _context.Games
            .Include(g => g.GameGenres)
            .Include(g => g.GamePlatforms)
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

        if (game == null) throw new KeyNotFoundException("Oyun tapılmadı.");

        // Sahələrin Null-Check yoxlaması ilə yenilənməsi
        if (!string.IsNullOrWhiteSpace(updateDto.Title)) game.Name = updateDto.Title;
        if (!string.IsNullOrWhiteSpace(updateDto.Description)) game.Description = updateDto.Description;
        if (updateDto.ReleaseDate.HasValue) game.ReleaseDate = updateDto.ReleaseDate.Value ;
        if (!string.IsNullOrWhiteSpace(updateDto.CoverImageUrl)) game.CoverImageUrl = updateDto.CoverImageUrl;
        if (updateDto.DeveloperCompanyId.HasValue) game.DeveloperCompanyId = updateDto.DeveloperCompanyId.Value;

        // Genre yenilənməsi (Many-to-Many sıfırlayıb yenidən yazmaq)
        if (updateDto.GenreIds != null)
        {
            game.GameGenres.Clear();
            game.GameGenres = updateDto.GenreIds.Select(gId => new GameGenre { GameId = id, GenreId = gId }).ToList();
        }

        // Platform yenilənməsi
        if (updateDto.PlatformIds != null)
        {
            game.GamePlatforms.Clear();
            game.GamePlatforms = updateDto.PlatformIds.Select(pId => new GamePlatform { GameId = id, PlatformId = pId }).ToList();
        }

        game.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);
        if (game == null) throw new KeyNotFoundException("Oyun tapılmadı.");

        game.IsDeleted = true; // Soft Delete
        game.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    // Front-end Dropdown-ları üçün Lookup Data Metodları
    public async Task<List<LookupDTO>> GetCompaniesLookupAsync() =>
        await _context.Companies.Where(c => !c.IsDeleted).Select(c => new LookupDTO { Id = c.Id, Name = c.Name }).ToListAsync();

    public async Task<List<LookupDTO>> GetGenresLookupAsync() =>
        await _context.Genres.Where(g => !g.IsDeleted).Select(g => new LookupDTO { Id = g.Id, Name = g.Name }).ToListAsync();

    public async Task<List<LookupDTO>> GetPlatformsLookupAsync() =>
        await _context.Platforms.Where(p => !p.IsDeleted).Select(p => new LookupDTO { Id = p.Id, Name = p.Name }).ToListAsync();

    public async Task<IEnumerable<GameListItemDTO>> GetFilteredGamesAsync(GameFilterDTO filterDto)
    {
        // Sorğunu IQueryable olaraq başladırıq ki, filtrlər bazaya getmədən əvvəl tətbiq olunsun
        var query = _context.Games
            .Include(g => g.GameGenres)     // Əgər oyuna aid janr məlumatı lazımdırsa bura include edirik
            .Include(g => g.GamePlatforms)  // Platforma məlumatı üçün
            .Include(g => g.DeveloperCompany)   // Şirkət məlumatı üçün
            .AsQueryable();

        // 1. Oyunun adına görə filtr (Axtarış)
        if (!string.IsNullOrWhiteSpace(filterDto.SearchName))
        {
            query = query.Where(g => g.Name.Contains(filterDto.SearchName));
        }

        // 2. Janra görə filtr
        if (filterDto.GenreId.HasValue)
        {
            query = query.Where(g => g. == filterDto.GenreId.Value);
        }

        // 3. Platformaya görə filtr
        if (filterDto.PlatformId.HasValue)
        {
            query = query.Where(g => g.PlatformId == filterDto.PlatformId.Value);
        }

        // 4. Şirkətə (Company) görə filtr
        if (filterDto.CompanyId.HasValue)
        {
            query = query.Where(g => g.CompanyId == filterDto.CompanyId.Value);
        }

        // Filtrlənmiş datanı bazadan asinxron olaraq çəkirik
        var filteredGames = await query.ToListAsync();

        // AutoMapper vasitəsilə tapılan Game entity-lərini GameListItemDTO-ya çeviririk
        var gameListItems = _mapper.Map<IEnumerable<GameListItemDTO>>(filteredGames);

        return gameListItems;
    }
}