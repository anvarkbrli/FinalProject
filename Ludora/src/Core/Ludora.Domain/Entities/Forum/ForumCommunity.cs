using Ludora.Domain.Entities.Base;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.Forum;

public class ForumCommunity : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? IconUrl { get; set; }
    public string? CoverImageUrl { get; set; }
    public ForumCommunityCategory Category { get; set; }
    public ForumCommunityVisibility Visibility { get; set; }

    public Guid OwnerId { get; set; }
    public AppUser Owner { get; set; } = null!;

    public ICollection<ForumCommunityMember> Members { get; set; } = [];
    public ICollection<ForumCommunityFollow> Followers { get; set; } = [];
    public ICollection<Discussion> Discussions { get; set; } = [];
}
