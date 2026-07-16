using Ludora.Domain.Entities.Base;
using Ludora.Domain.Entities.User;


namespace Ludora.Domain.Entities
{
    public class DiscussionReply : BaseEntity
    {
        public string Content { get; set; } = null!;

        public long DiscussionId { get; set; }
        public Discussion Discussion { get; set; } = null!;

        public Guid UserId { get; set; }
        public AppUser User { get; set; } = null!;

        public ICollection<DiscussionReplyLike> Likes { get; set; } = [];
    }
}
