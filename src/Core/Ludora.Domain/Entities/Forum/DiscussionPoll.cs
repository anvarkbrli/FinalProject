using Ludora.Domain.Entities.Base;
using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.Forum
{
    public sealed class DiscussionPoll : BaseEntity
    {
        public int DiscussionId { get; set; }
        public Discussion Discussion { get; set; } = null!;
        public string Question { get; set; } = null!;
        public DateTime EndsAtUtc { get; set; }
        public PollResultsVisibility ResultsVisibility { get; set; }
        public bool AllowVoteChange { get; set; }
        public bool IsClosed { get; set; }
        public ICollection<DiscussionPollOption> Options { get; set; } = [];
        public ICollection<DiscussionPollVote> Votes { get; set; } = [];
    }
}
