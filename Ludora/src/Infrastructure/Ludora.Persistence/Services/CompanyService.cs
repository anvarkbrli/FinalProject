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
    public sealed class CompanyService : ICompanyService
    {
        private const int MaxNameLength = 200;
        private readonly AppDbContext _context;

        public CompanyService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<CompanyDetailDTO>> GetAllAsync(string? searchName)
        {
            IQueryable<Company> query = _context.Companies.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                var search = searchName.Trim();
                query = query.Where(c => c.Name.Contains(search));
            }

            return await query
                .OrderBy(c => c.Name)
                .Select(c => new CompanyDetailDTO
                {
                    Id = c.Id,
                    Name = c.Name,
                    ActiveGameCount = c.DevelopedGames.Count(g => !g.IsDeleted),
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<List<CompanyDetailDTO>> GetDeletedAsync(string? searchName)
        {
            IQueryable<Company> query = _context.Companies
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.IsDeleted);

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                var search = searchName.Trim();
                query = query.Where(c => c.Name.Contains(search));
            }

            return await query
                .OrderBy(c => c.Name)
                .Select(c => new CompanyDetailDTO
                {
                    Id = c.Id,
                    Name = c.Name,
                    ActiveGameCount = c.DevelopedGames.Count(g => !g.IsDeleted),
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<CompanyDetailDTO> GetByIdAsync(int id)
        {
            id.EnsureValidId();

            var company = await _context.Companies
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new CompanyDetailDTO
                {
                    Id = c.Id,
                    Name = c.Name,
                    ActiveGameCount = c.DevelopedGames.Count(g => !g.IsDeleted),
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return company ?? throw new KeyNotFoundException("Şirkət tapılmadı.");
        }

        public async Task<int> CreateAsync(CompanyCreateDTO createDto)
        {
            var normalizedName = createDto.Name.NormalizeName(MaxNameLength);
            await _context.Companies.EnsureNameIsAvailableAsync(normalizedName, "Şirkət");

            var company = new Company
            {
                Name = normalizedName,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
                
            };

            await _context.Companies.AddAsync(company);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
            {
                throw new ConflictException("Bu adda şirkət artıq mövcuddur.");
            }

            return company.Id;
        }

        public async Task UpdateAsync(int id, CompanyUpdateDTO updateDto)
        {
            id.EnsureValidId();

            if (updateDto.Name is null)
                throw new ArgumentException("Yenilənəcək şirkət adı göndərilməyib.");

            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == id);

            if (company is null)
                throw new KeyNotFoundException("Şirkət tapılmadı.");

            var normalizedName = updateDto.Name.NormalizeName(MaxNameLength);

            if (company.Name == normalizedName)
                return;

            await _context.Companies.EnsureNameIsAvailableAsync(normalizedName, "Şirkət", id);

            company.Name = normalizedName;
            company.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
            {
                throw new ConflictException("Bu adda şirkət artıq mövcuddur.");
            }
        }

        public async Task DeleteAsync(int id)
        {
            id.EnsureValidId();

            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == id);

            if (company is null)
                throw new KeyNotFoundException("Şirkət tapılmadı.");

            var hasActiveGames = await _context.Games
                .AnyAsync(g => g.DeveloperCompanyId == id);

            if (hasActiveGames)
            {
                throw new ConflictException("Bu şirkət aktiv oyunlarda istifadə olunur. Əvvəl həmin oyunların şirkətini dəyişin və ya oyunları silin.");
            }

            company.IsDeleted = true;
            company.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task RestoreAsync(int id)
        {
            id.EnsureValidId();

            var company = await _context.Companies
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (company is null)
                throw new KeyNotFoundException("Şirkət tapılmadı.");

            if (!company.IsDeleted)
                throw new ConflictException("Şirkət artıq aktivdir.");

            await _context.Companies.EnsureNameIsAvailableAsync(company.Name, "Şirkət", id);

            company.IsDeleted = false;
            company.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
            {
                throw new ConflictException("Eyni adda aktiv şirkət olduğu üçün bu şirkət bərpa edilə bilmir.");
            }
        }

     

       
    }
}
