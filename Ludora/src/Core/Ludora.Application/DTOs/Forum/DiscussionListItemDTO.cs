using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record DiscussionListItemDTO
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string ContentPreview { get; init; } = string.Empty;
    public IReadOnlyList<DiscussionMediaDTO> Media { get; init; } = [];

    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string CategorySlug { get; init; } = string.Empty;

    public int? CommunityId { get; init; }
    public string? CommunityName { get; init; }
    public string? CommunitySlug { get; init; }
    public string? CommunityIconUrl { get; init; }

    public Guid AuthorId { get; init; }
    public string AuthorUserName { get; init; } = string.Empty;
    public string? AuthorProfileImageUrl { get; init; }
    public ForumCommunityMemberRole? AuthorCommunityRole { get; init; }

    public int LikeCount { get; init; }
    public int ReplyCount { get; init; }
    public int ViewCount { get; init; }

    public bool IsLikedByCurrentUser { get; init; }
    public bool IsSavedByCurrentUser { get; init; }
    public bool IsPinned { get; init; }
    public bool IsLocked { get; init; }
    public bool IsDeleted { get; init; }
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
