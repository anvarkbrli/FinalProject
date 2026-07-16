using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IForumCategoryService
{
    Task<IReadOnlyList<ForumCategoryDTO>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ForumCategoryDTO>> GetDeletedAsync(CancellationToken cancellationToken = default);
    Task<ForumCategoryDTO> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<ForumCategoryDTO> CreateAsync(ForumCategoryCreateDTO dto, CancellationToken cancellationToken = default);
    Task<ForumCategoryDTO> UpdateAsync(int id, ForumCategoryUpdateDTO dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<ForumCategoryDTO> RestoreAsync(int id, CancellationToken cancellationToken = default);
}
