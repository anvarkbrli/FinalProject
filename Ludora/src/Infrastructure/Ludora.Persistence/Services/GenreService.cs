
using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services
{
    public sealed class GenreService : IGenreService
    {
        private const int MaxNameLength = 100;
        private readonly AppDbContext _context;

        public GenreService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<GenreDetailDTO>> GetAllAsync(string? searchName)
        {
            IQueryable<Genre> query = _context.Genres.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                var search = searchName.Trim();
                query = query.Where(g => g.Name.Contains(search));
            }

            return await query
                .OrderBy(g => g.Name)
                .Select(g => new GenreDetailDTO
                {
                    Id = g.Id,
                    Name = g.Name,
                    ActiveGameCount = g.GameGenres.Count(gg => !gg.Game.IsDeleted),
                    CreatedAt = g.CreatedAt,
                    UpdatedAt = g.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<List<GenreDetailDTO>> GetDeletedAsync(string? searchName)
        {
            IQueryable<Genre> query = _context.Genres
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(g => g.IsDeleted);

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                var search = searchName.Trim();
                query = query.Where(g => g.Name.Contains(search));
            }

            return await query
                .OrderBy(g => g.Name)
                .Select(g => new GenreDetailDTO
                {
                    Id = g.Id,
                    Name = g.Name,
                    ActiveGameCount = g.GameGenres.Count(gg => !gg.Game.IsDeleted),
                    CreatedAt = g.CreatedAt,
                    UpdatedAt = g.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<GenreDetailDTO> GetByIdAsync(int id)
        {
            id.EnsureValidId();

            var genre = await _context.Genres
                .AsNoTracking()
                .Where(g => g.Id == id)
                .Select(g => new GenreDetailDTO
                {
                    Id = g.Id,
                    Name = g.Name,
                    ActiveGameCount = g.GameGenres.Count(gg => !gg.Game.IsDeleted),
                    CreatedAt = g.CreatedAt,
                    UpdatedAt = g.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return genre ?? throw new KeyNotFoundException("Janr tapılmadı.");
        }

        public async Task<int> CreateAsync(GenreCreateDTO createDto)
        {
            var normalizedName = createDto.Name.NormalizeName(MaxNameLength);
            await _context.Genres.EnsureNameIsAvailableAsync(normalizedName,"Janr");

            var genre = new Genre
            {
                Name = normalizedName
            };

            await _context.Genres.AddAsync(genre);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
            {
                throw new ConflictException("Bu adda janr artıq mövcuddur.");
            }

            return genre.Id;
        }

        public async Task UpdateAsync(int id, GenreUpdateDTO updateDto)
        {
            id.EnsureValidId();

            if (updateDto.Name is null)
                throw new ArgumentException("Yenilənəcək janr adı göndərilməyib.");

            var genre = await _context.Genres.FirstOrDefaultAsync(g => g.Id == id);

            if (genre is null)
                throw new KeyNotFoundException("Janr tapılmadı.");

            var normalizedName = updateDto.Name.NormalizeName(MaxNameLength);

            if (genre.Name == normalizedName)
                return;

            await _context.Genres.EnsureNameIsAvailableAsync(normalizedName, "Janr", id);

            genre.Name = normalizedName;
            genre.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
            {
                throw new ConflictException("Bu adda janr artıq mövcuddur.");
            }
        }

        public async Task DeleteAsync(int id)
        {
            id.EnsureValidId();

            var genre = await _context.Genres.FirstOrDefaultAsync(g => g.Id == id);

            if (genre is null)
                throw new KeyNotFoundException("Janr tapılmadı.");

            var hasActiveGames = await _context.GameGenres
                .AnyAsync(gg => gg.GenreId == id && !gg.Game.IsDeleted);

            if (hasActiveGames)
            {
                throw new ConflictException(
                    "Bu janr aktiv oyunlarda istifadə olunur. Əvvəl janrı həmin oyunlardan çıxarın və ya oyunları silin.");
            }

            genre.IsDeleted = true;
            genre.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task RestoreAsync(int id)
        {
            id.EnsureValidId();

            var genre = await _context.Genres
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(g => g.Id == id);

            if (genre is null)
                throw new KeyNotFoundException("Janr tapılmadı.");

            if (!genre.IsDeleted)
                throw new ConflictException("Janr artıq aktivdir.");

            await _context.Genres.EnsureNameIsAvailableAsync(genre.Name, "Janr", id);

            genre.IsDeleted = false;
            genre.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
            {
                throw new ConflictException(
                    "Eyni adda aktiv janr olduğu üçün bu janr bərpa edilə bilmir.");
            }
        }

     
      
    }
}
