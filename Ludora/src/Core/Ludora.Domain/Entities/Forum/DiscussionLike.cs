using Ludora.Domain.Entities.User;

namespace Ludora.Domain.Entities.Forum;

public class DiscussionLike
{
    public int DiscussionId { get; set; }
    public Discussion Discussion { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
