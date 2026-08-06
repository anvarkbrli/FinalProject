using Ludora.Domain.Entities.Base;
using Ludora.Domain.Entities.User;

namespace Ludora.Domain.Entities.Forum
{
    public class DiscussionReply : BaseEntity
    {
        public string Content { get; set; } = null!;

        public int DiscussionId { get; set; }
        public Discussion Discussion { get; set; } = null!;

        public Guid UserId { get; set; }
        public AppUser User { get; set; } = null!;

        public int? ParentReplyId { get; set; }
        public DiscussionReply? ParentReply { get; set; }

        public ICollection<DiscussionReply> ChildReplies { get; set; } = [];
        public ICollection<DiscussionReplyLike> Likes { get; set; } = [];
        public ICollection<DiscussionReplyVote> Votes { get; set; } = [];
        public ICollection<DiscussionReplyFollow> Followers { get; set; } = [];
    }
}
