using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface ICompanyService
    {
        Task<List<CompanyDetailDTO>> GetAllAsync(string? searchName);
        Task<List<CompanyDetailDTO>> GetDeletedAsync(string? searchName);
        Task<CompanyDetailDTO> GetByIdAsync(int id);
        Task<int> CreateAsync(CompanyCreateDTO createDto);
        Task UpdateAsync(int id, CompanyUpdateDTO updateDto);
        Task DeleteAsync(int id);
        Task RestoreAsync(int id);
    }
}
