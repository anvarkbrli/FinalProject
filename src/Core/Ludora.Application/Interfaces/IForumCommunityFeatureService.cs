using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IForumCommunityFeatureService
    {
        Task<IReadOnlyList<ForumCommunityRuleDTO>> GetRulesAsync(int communityId, Guid? currentUserId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ForumCommunityRuleDTO>> ReplaceRulesAsync(int communityId, Guid actorUserId, ForumCommunityRulesUpdateDTO dto, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ForumCommunityMemberSearchDTO>> GetModeratorsAsync(int communityId, Guid? currentUserId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ForumCommunityFlairDTO>> GetFlairsAsync(int communityId, Guid? currentUserId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ForumCommunityFlairDTO>> ReplaceFlairsAsync(int communityId, Guid actorUserId, ForumCommunityFlairsUpdateDTO dto, CancellationToken cancellationToken = default);
        Task SetMyFlairAsync(int communityId, Guid userId, ForumCommunitySetFlairDTO dto, CancellationToken cancellationToken = default);
        Task<PagedResultDTO<ForumCommunityMemberSearchDTO>> SearchMembersAsync(int communityId, Guid currentUserId, string? query, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResultDTO<ForumCommunityEventDTO>> GetEventsAsync(int communityId, Guid? currentUserId, bool past, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<ForumCommunityEventDTO> CreateEventAsync(int communityId, Guid actorUserId, ForumCommunityEventUpsertDTO dto, CancellationToken cancellationToken = default);
        Task<ForumCommunityEventDTO> UpdateEventAsync(int communityId, int eventId, Guid actorUserId, ForumCommunityEventUpsertDTO dto, CancellationToken cancellationToken = default);
        Task CancelEventAsync(int communityId, int eventId, Guid actorUserId, CancellationToken cancellationToken = default);
        Task JoinEventAsync(int communityId, int eventId, Guid userId, CancellationToken cancellationToken = default);
        Task LeaveEventAsync(int communityId, int eventId, Guid userId, CancellationToken cancellationToken = default);
    }
}
