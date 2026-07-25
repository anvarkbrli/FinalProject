using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IForumCommunityService
{
    Task<IReadOnlyList<ForumCommunitySummaryDTO>> GetAllAsync(
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ForumCommunitySummaryDTO>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ForumCommunitySummaryDTO> GetBySlugAsync(
        string slug,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ForumCommunitySummaryDTO> CreateAsync(
        Guid userId,
        ForumCommunityCreateDTO dto,
        CancellationToken cancellationToken = default);

    Task JoinAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task LeaveAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task FollowAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task UnfollowAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ForumCommunityMemberDTO>> GetMembershipRequestsAsync(
        int id,
        Guid ownerId,
        CancellationToken cancellationToken = default);

    Task ApproveMembershipAsync(
        int id,
        int membershipId,
        Guid ownerId,
        CancellationToken cancellationToken = default);

    Task RejectMembershipAsync(
        int id,
        int membershipId,
        Guid ownerId,
        CancellationToken cancellationToken = default);

    Task InviteAsync(
        int id,
        Guid ownerId,
        string userName,
        CancellationToken cancellationToken = default);

    Task AcceptInviteAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default);
}
