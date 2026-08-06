using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IDiscussionReplyService
    {
        Task<DiscussionReplyDTO> GetByIdAsync(
            int replyId,
            Guid? currentUserId,
            bool canModerate,
            CancellationToken cancellationToken = default);

        Task<PagedResultDTO<DiscussionReplyDTO>> GetByDiscussionAsync(
            int discussionId,
            int pageNumber,
            int pageSize,
            Guid? currentUserId,
            bool canModerate,
            CancellationToken cancellationToken = default);

        Task<PagedResultDTO<DiscussionReplyDTO>> GetDeletedAsync(
            int? discussionId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<DiscussionReplyDTO> CreateAsync(
            int discussionId,
            Guid userId,
            DiscussionReplyCreateDTO dto,
            CancellationToken cancellationToken = default);

        Task<DiscussionReplyDTO> UpdateAsync(
            int replyId,
            Guid userId,
            bool canModerate,
            DiscussionReplyUpdateDTO dto,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            int replyId,
            Guid userId,
            bool canModerate,
            CancellationToken cancellationToken = default);

        Task LikeAsync(int replyId, Guid userId, CancellationToken cancellationToken = default);
        Task UnlikeAsync(int replyId, Guid userId, CancellationToken cancellationToken = default);
        Task<DiscussionReplyDTO> SetVoteAsync(
            int replyId,
            Guid userId,
            short value,
            bool canModerate,
            CancellationToken cancellationToken = default);
        Task FollowAsync(
            int replyId,
            Guid userId,
            CancellationToken cancellationToken = default);
        Task UnfollowAsync(
            int replyId,
            Guid userId,
            CancellationToken cancellationToken = default);
        Task<DiscussionReplyDTO> RestoreAsync(int replyId, CancellationToken cancellationToken = default);
    }
}
