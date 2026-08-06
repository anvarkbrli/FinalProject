using Ludora.Domain.Entities.Base;
using Ludora.Domain.Entities.User;

namespace Ludora.Domain.Entities.Forum
{
    public sealed class DiscussionPollVote : BaseEntity
    {
        public int PollId { get; set; }
        public DiscussionPoll Poll { get; set; } = null!;
        public int OptionId { get; set; }
        public DiscussionPollOption Option { get; set; } = null!;
        public Guid UserId { get; set; }
        public AppUser User { get; set; } = null!;
    }
}
