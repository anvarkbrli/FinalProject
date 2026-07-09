using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IPlatformService
    {
        Task<List<PlatformDetailDTO>> GetAllAsync(string? searchName);
        Task<List<PlatformDetailDTO>> GetDeletedAsync(string? searchName);
        Task<PlatformDetailDTO> GetByIdAsync(int id);
        Task<int> CreateAsync(PlatformCreateDTO createDto);
        Task UpdateAsync(int id, PlatformUpdateDTO updateDto);
        Task DeleteAsync(int id);
        Task RestoreAsync(int id);
    }
}
