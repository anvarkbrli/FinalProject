using Ludora.Domain.Entities.User;

namespace Ludora.Domain.Entities.Forum;

public class ForumCommunityFollow
{
    public int CommunityId { get; set; }
    public ForumCommunity Community { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
