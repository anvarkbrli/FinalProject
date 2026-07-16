using Ludora.Domain.Entities.Base;
using Ludora.Domain.Entities.User;


namespace Ludora.Domain.Entities
{
    public class Discussion : BaseEntity
    {
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string Content { get; set; } = null!;

        public Guid UserId { get; set; }
        public AppUser User { get; set; } = null!;

        public long CategoryId { get; set; }
        public ForumCategory Category { get; set; } = null!;

        public bool IsPinned { get; set; }
        public bool IsLocked { get; set; }

        public int ViewCount { get; set; }

        public ICollection<DiscussionReply> Replies { get; set; } = [];
        public ICollection<DiscussionLike> Likes { get; set; } = [];
    }
}
