using Ludora.Domain.Entities.Base;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.Forum;

public class ForumCommunityMember : BaseEntity
{
    public int CommunityId { get; set; }
    public ForumCommunity Community { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public ForumCommunityMemberRole Role { get; set; }
    public ForumCommunityMembershipStatus Status { get; set; }
}
