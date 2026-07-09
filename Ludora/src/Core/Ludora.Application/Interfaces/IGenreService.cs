using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IGenreService
    {
        Task<List<GenreDetailDTO>> GetAllAsync(string? searchName);
        Task<List<GenreDetailDTO>> GetDeletedAsync(string? searchName);
        Task<GenreDetailDTO> GetByIdAsync(int id);
        Task<int> CreateAsync(GenreCreateDTO createDto);
        Task UpdateAsync(int id, GenreUpdateDTO updateDto);
        Task DeleteAsync(int id);
        Task RestoreAsync(int id);
    }
}
