using Ludora.Domain.Entities.Base;

namespace Ludora.Domain.Entities.Forum
{
    public sealed class DiscussionPollOption : BaseEntity
    {
        public int PollId { get; set; }
        public DiscussionPoll Poll { get; set; } = null!;
        public string Text { get; set; } = null!;
        public int DisplayOrder { get; set; }
        public ICollection<DiscussionPollVote> Votes { get; set; } = [];
    }
}
