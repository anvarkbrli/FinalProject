using Ludora.Application.DTOs;
using Ludora.Domain.Enums;

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

    Task<ForumCommunityLeaveResultDTO> LeaveAsync(
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

    Task<IReadOnlyList<ForumCommunityMemberDTO>> GetMembersAsync(
        int id,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ForumCommunityInviteCandidateDTO>> SearchInviteCandidatesAsync(
        int id,
        Guid ownerId,
        string query,
        CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(
        int id,
        int membershipId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task UpdateMemberRoleAsync(
        int id,
        int membershipId,
        Guid ownerId,
        ForumCommunityMemberRole role,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        int id,
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

    Task AcceptOwnershipOfferAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task DeclineOwnershipOfferAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default);
}
