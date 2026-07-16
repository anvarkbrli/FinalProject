

namespace Ludora.Application.DTOs
{
    public record DiscussionListItemDTO
    {
        public long Id { get; set; }

        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;

        public string CategoryName { get; set; } = null!;
        public string CategorySlug { get; set; } = null!;

        public Guid AuthorId { get; set; }
        public string AuthorUserName { get; set; } = null!;
        public string? AuthorProfileImageUrl { get; set; }

        public int LikeCount { get; set; }
        public int ReplyCount { get; set; }
        public int ViewCount { get; set; }

        public bool IsLikedByCurrentUser { get; set; }
        public bool IsPinned { get; set; }
        public bool IsLocked { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
