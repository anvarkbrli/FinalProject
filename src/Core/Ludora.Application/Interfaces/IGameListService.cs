using Ludora.Application.DTOs;
using Ludora.Domain.Enums;

namespace Ludora.Application.Interfaces
{
    public interface IGameListService
    {
        Task<PagedResultDTO<GameListSummaryDTO>> GetUserListsAsync(
            Guid userId,
            Guid? currentUserId,
            PaginationFilterDTO pagination,
            CancellationToken cancellationToken = default);

        Task<PagedResultDTO<GameListSummaryDTO>> GetCollaboratingListsAsync(
            Guid userId,
            PaginationFilterDTO pagination,
            CancellationToken cancellationToken = default);

        Task<GameListDetailDTO> GetByIdAsync(
            int id,
            Guid? currentUserId,
            CancellationToken cancellationToken = default);

        Task<GameListPlayedProgressDTO> GetPlayedProgressAsync(
            int id,
            Guid currentUserId,
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

        Task ReorderGamesAsync(
            int listId,
            Guid userId,
            GameListReorderDTO dto,
            CancellationToken cancellationToken = default);

        Task<int> CloneAsync(
            int sourceListId,
            Guid userId,
            GameListCloneDTO dto,
            CancellationToken cancellationToken = default);

        Task InviteCollaboratorAsync(
            int listId,
            Guid ownerUserId,
            GameListCollaboratorInviteDTO dto,
            CancellationToken cancellationToken = default);

        Task AcceptCollaborationAsync(
            int listId,
            Guid userId,
            CancellationToken cancellationToken = default);

        Task DeclineCollaborationAsync(
            int listId,
            Guid userId,
            CancellationToken cancellationToken = default);

        Task RemoveCollaboratorAsync(
            int listId,
            Guid actingUserId,
            Guid collaboratorUserId,
            CancellationToken cancellationToken = default);
    }
}
