using Ludora.Domain.Entities.User;

namespace Ludora.Domain.Entities.Forum
{
    public class DiscussionReplyLike
    {
        public int ReplyId { get; set; }
        public DiscussionReply Reply { get; set; } = null!;

        public Guid UserId { get; set; }
        public AppUser User { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}
