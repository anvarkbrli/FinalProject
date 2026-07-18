using Ludora.Application.DTOs;
using Ludora.Application.DTOs.GameList;
using Ludora.Domain.Enums;

namespace Ludora.Application.Interfaces;

public interface IGameService
{
    Task<PagedResultDTO<GameListItemDTO>> GetFilteredGamesAsync(
        GameFilterDTO filter,
        CancellationToken cancellationToken = default);

    Task<GameDetailDTO> GetByIdAsync(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken = default);

    Task<long> CreateAsync(
        GameCreateDTO dto,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        CatalogSource source,
        long id,
        GameUpdateDTO dto,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken = default);

    Task RestoreAsync(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken = default);

    Task<int> AddRelationAsync(
        CatalogSource baseSource,
        long baseId,
        GameRelationCreateDTO dto,
        CancellationToken cancellationToken = default);

    Task RemoveRelationAsync(
        CatalogSource baseSource,
        long baseId,
        CatalogSource relatedSource,
        long relatedId,
        GameType relationType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(CatalogSource Source, long Id), GameDetailDTO>> GetByReferencesAsync(
        IReadOnlyCollection<CatalogReferenceDTO> references,
        CancellationToken cancellationToken = default);
}
