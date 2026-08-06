using Ludora.Application.DTOs;
using Ludora.Domain.Enums;

namespace Ludora.Application.Interfaces
{
    public interface IPlatformService
    {
        Task<IReadOnlyList<PlatformDetailDTO>> GetAllAsync(
            string? searchName,
            CatalogSource? source = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PlatformDetailDTO>> GetDeletedAsync(
            CancellationToken cancellationToken = default);

        Task<PlatformDetailDTO> GetByIdAsync(
            CatalogSource source,
            long id,
            CancellationToken cancellationToken = default);

        Task<long> CreateAsync(
            PlatformCreateDTO createDto,
            CancellationToken cancellationToken = default);

        Task UpdateAsync(
            CatalogSource source,
            long id,
            PlatformUpdateDTO updateDto,
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
