using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IGameListInteractionService
    {
        Task LikeAsync(int gameListId, Guid userId, CancellationToken cancellationToken = default);
        Task UnlikeAsync(int gameListId, Guid userId, CancellationToken cancellationToken = default);
        Task<PagedResultDTO<InteractionLikeUserDTO>> GetLikesAsync(int gameListId, int pageNumber, int pageSize, Guid? currentUserId, CancellationToken cancellationToken = default);
        Task<GameListCommentDTO> CreateCommentAsync(int gameListId, Guid userId, InteractionCommentCreateDTO dto, CancellationToken cancellationToken = default);
        Task<PagedResultDTO<GameListCommentDTO>> GetCommentsAsync(int gameListId, InteractionCommentFilterDTO filter, Guid? currentUserId, CancellationToken cancellationToken = default);
        Task<GameListCommentDTO> UpdateCommentAsync(long commentId, Guid userId, InteractionCommentUpdateDTO dto, CancellationToken cancellationToken = default);
        Task DeleteCommentAsync(long commentId, Guid userId, CancellationToken cancellationToken = default);
    }
}
