
using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities;
using Ludora.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services
{
    public sealed class PlatformService : IPlatformService
    {
        private const int MaxNameLength = 100;
        private readonly AppDbContext _context;

        public PlatformService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PlatformDetailDTO>> GetAllAsync(string? searchName)
        {
            IQueryable<Platform> query = _context.Platforms.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                var search = searchName.Trim();
                query = query.Where(p => p.Name.Contains(search));
            }

            return await query
                .OrderBy(p => p.Name)
                .Select(p => new PlatformDetailDTO
                {
                    Id = p.Id,
                    Name = p.Name,
                    ActiveGameCount = p.GamePlatforms.Count(gp => !gp.Game.IsDeleted),
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<List<PlatformDetailDTO>> GetDeletedAsync(string? searchName)
        {
            IQueryable<Platform> query = _context.Platforms
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.IsDeleted);

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                var search = searchName.Trim();
                query = query.Where(p => p.Name.Contains(search));
            }

            return await query
                .OrderBy(p => p.Name)
                .Select(p => new PlatformDetailDTO
                {
                    Id = p.Id,
                    Name = p.Name,
                    ActiveGameCount = p.GamePlatforms.Count(gp => !gp.Game.IsDeleted),
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<PlatformDetailDTO> GetByIdAsync(int id)
        {
            id.EnsureValidId();

            var platform = await _context.Platforms
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new PlatformDetailDTO
                {
                    Id = p.Id,
                    Name = p.Name,
                    ActiveGameCount = p.GamePlatforms.Count(gp => !gp.Game.IsDeleted),
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return platform ?? throw new KeyNotFoundException("Platforma tapılmadı.");
        }

        public async Task<int> CreateAsync(PlatformCreateDTO createDto)
        {
            var normalizedName = createDto.Name.NormalizeName(MaxNameLength);
            await EnsureNameIsAvailableAsync(normalizedName);

            var platform = new Platform
            {
                Name = normalizedName
            };

            await _context.Platforms.AddAsync(platform);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                throw new ConflictException("Bu adda platforma artıq mövcuddur.");
            }

            return platform.Id;
        }

        public async Task UpdateAsync(int id, PlatformUpdateDTO updateDto)
        {
            id.EnsureValidId();

            if (updateDto.Name is null)
                throw new ArgumentException("Yenilənəcək platforma adı göndərilməyib.");

            var platform = await _context.Platforms.FirstOrDefaultAsync(p => p.Id == id);

            if (platform is null)
                throw new KeyNotFoundException("Platforma tapılmadı.");

            var normalizedName = updateDto.Name.NormalizeName(MaxNameLength);

            if (platform.Name == normalizedName)
                return;

            await EnsureNameIsAvailableAsync(normalizedName, id);

            platform.Name = normalizedName;
            platform.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                throw new ConflictException("Bu adda platforma artıq mövcuddur.");
            }
        }

        public async Task DeleteAsync(int id)
        {
            id.EnsureValidId();

            var platform = await _context.Platforms.FirstOrDefaultAsync(p => p.Id == id);

            if (platform is null)
                throw new KeyNotFoundException("Platforma tapılmadı.");

            var hasActiveGames = await _context.GamePlatforms
                .AnyAsync(gp => gp.PlatformId == id && !gp.Game.IsDeleted);

            if (hasActiveGames)
            {
                throw new ConflictException(
                    "Bu platforma aktiv oyunlarda istifadə olunur. Əvvəl platformanı həmin oyunlardan çıxarın və ya oyunları silin.");
            }

            platform.IsDeleted = true;
            platform.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task RestoreAsync(int id)
        {
            id.EnsureValidId();

            var platform = await _context.Platforms
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (platform is null)
                throw new KeyNotFoundException("Platforma tapılmadı.");

            if (!platform.IsDeleted)
                throw new ConflictException("Platforma artıq aktivdir.");

            await EnsureNameIsAvailableAsync(platform.Name, id);

            platform.IsDeleted = false;
            platform.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                throw new ConflictException(
                    "Eyni adda aktiv platforma olduğu üçün bu platforma bərpa edilə bilmir.");
            }
        }

        private async Task EnsureNameIsAvailableAsync(string name, int? excludedId = null)
        {
            var duplicate = await _context.Platforms
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => (!excludedId.HasValue || p.Id != excludedId.Value) && p.Name == name)
                .OrderBy(p => p.IsDeleted)
                .Select(p => new { p.IsDeleted })
                .FirstOrDefaultAsync();

            if (duplicate is null)
                return;

            if (duplicate.IsDeleted)
            {
                throw new ConflictException(
                    "Bu adda silinmiş platforma mövcuddur. Yeni qeyd yaratmaq əvəzinə onu bərpa edin.");
            }

            throw new ConflictException("Bu adda platforma artıq mövcuddur.");
        }

        

        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            return exception.InnerException is SqlException sqlException
                && (sqlException.Number == 2601 || sqlException.Number == 2627);
        }

   
    }
}
