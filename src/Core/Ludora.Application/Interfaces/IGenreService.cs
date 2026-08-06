using Ludora.Application.DTOs;
using Ludora.Domain.Enums;

namespace Ludora.Application.Interfaces
{
    public interface IGenreService
    {
        Task<IReadOnlyList<GenreDetailDTO>> GetAllAsync(
            string? searchName,
            CatalogSource? source = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<GenreDetailDTO>> GetDeletedAsync(
            CancellationToken cancellationToken = default);

        Task<GenreDetailDTO> GetByIdAsync(
            CatalogSource source,
            long id,
            CancellationToken cancellationToken = default);

        Task<long> CreateAsync(
            GenreCreateDTO createDto,
            CancellationToken cancellationToken = default);

        Task UpdateAsync(
            CatalogSource source,
            long id,
            GenreUpdateDTO updateDto,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            CatalogSource source,
            long id,
            CancellationToken cancellationToken = default);

        Task RestoreAsync(
            CatalogSource source,
            long id,
            CancellationToken cancellationToken = default);
    }
}
