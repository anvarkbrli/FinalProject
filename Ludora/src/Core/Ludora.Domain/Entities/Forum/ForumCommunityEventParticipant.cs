using Ludora.Domain.Entities.User;

namespace Ludora.Domain.Entities.Forum;

public sealed class ForumCommunityEventParticipant
{
    public int EventId { get; set; }
    public ForumCommunityEvent Event { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}
