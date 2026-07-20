using Ludora.Application.DTOs;
using Ludora.Domain.Enums;

namespace Ludora.Application.Interfaces;

public interface IGameListService
{
    Task<IReadOnlyList<GameListSummaryDTO>> GetUserListsAsync(
        Guid userId,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<GameListDetailDTO> GetByIdAsync(
        int id,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<int> CreateAsync(
        Guid userId,
        GameListCreateDTO dto,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        int id,
        Guid userId,
        GameListUpdateDTO dto,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task RestoreAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddGameAsync(
        int listId,
        CatalogSource source,
        long gameId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task RemoveGameAsync(
        int listId,
        CatalogSource source,
        long gameId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task UpdateGamePositionAsync(
        int listId,
        CatalogSource source,
        long gameId,
        int position,
        Guid userId,
        CancellationToken cancellationToken = default);
}
