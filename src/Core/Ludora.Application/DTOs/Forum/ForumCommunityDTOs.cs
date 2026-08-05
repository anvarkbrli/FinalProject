using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record ForumCommunityCreateDTO
{
    public string Name { get; init; } = null!;
    public string Description { get; init; } = null!;
    public ForumCommunityCategory Category { get; init; }
    public ForumCommunityVisibility Visibility { get; init; }
    public ProfileImageUploadDTO? Icon { get; init; }
    public ProfileImageUploadDTO? CoverImage { get; init; }
}

public record ForumCommunitySummaryDTO
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? IconUrl { get; init; }
    public string? CoverImageUrl { get; init; }
    public ForumCommunityCategory Category { get; init; }
    public ForumCommunityVisibility Visibility { get; init; }
    public Guid OwnerId { get; init; }
    public string OwnerUserName { get; init; } = string.Empty;
    public int MemberCount { get; init; }
    public int FollowerCount { get; init; }
    public int DiscussionCount { get; init; }
    public ForumCommunityMembershipStatus? CurrentUserMembershipStatus { get; init; }
    public ForumCommunityMemberRole? CurrentUserRole { get; init; }
    public bool IsFollowedByCurrentUser { get; init; }
    public bool CanManage { get; init; }
    public bool CanModerate { get; init; }
    public bool HasOwnershipOfferForCurrentUser { get; init; }
    public bool HasPendingOwnershipTransfer { get; init; }
    public string? PendingOwnerCandidateUserName { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record ForumCommunityMemberDTO
{
    public int MembershipId { get; init; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? ProfileImageUrl { get; init; }
    public ForumCommunityMemberRole Role { get; init; }
    public ForumCommunityMembershipStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record ForumCommunityInviteDTO
{
    public string UserName { get; init; } = null!;
}

public record ForumCommunityInviteCandidateDTO
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? ProfileImageUrl { get; init; }
    public ForumCommunityMembershipStatus? MembershipStatus { get; init; }
}

public record ForumCommunityMemberRoleUpdateDTO
{
    public ForumCommunityMemberRole Role { get; init; }
}

public record ForumCommunityLeaveResultDTO
{
    public bool LeftCommunity { get; init; }
    public bool OwnershipOfferCreated { get; init; }
    public string? CandidateUserName { get; init; }
}
