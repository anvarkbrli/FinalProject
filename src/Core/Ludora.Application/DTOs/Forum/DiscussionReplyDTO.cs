using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record DiscussionReplyDTO
    {
        public int Id { get; init; }
        public int DiscussionId { get; init; }
        public int? ParentReplyId { get; init; }

        public Guid AuthorId { get; init; }
        public string AuthorUserName { get; init; } = string.Empty;
        public string? AuthorProfileImageUrl { get; init; }
        public ForumCommunityMemberRole? AuthorCommunityRole { get; init; }
        public string? AuthorCommunityFlairName { get; init; }
        public string? AuthorCommunityFlairBackgroundColor { get; init; }
        public string? AuthorCommunityFlairTextColor { get; init; }

        public string Content { get; init; } = string.Empty;
        public int NetScore { get; init; }
        public short CurrentUserVote { get; init; }
        public bool IsFollowedByCurrentUser { get; init; }
        public int ChildCount { get; init; }
        public IReadOnlyList<DiscussionReplyDTO> Children { get; init; } = [];
        public bool IsDeleted { get; init; }
        public bool CanEdit { get; init; }
        public bool CanDelete { get; init; }

        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
