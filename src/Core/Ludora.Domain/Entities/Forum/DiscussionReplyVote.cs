using Ludora.Domain.Entities.User;

namespace Ludora.Domain.Entities.Forum;

public sealed class DiscussionReplyVote
{
    public int ReplyId { get; set; }
    public DiscussionReply Reply { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public short Value { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
