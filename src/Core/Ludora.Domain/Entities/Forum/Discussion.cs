using Ludora.Domain.Entities.Base;
using Ludora.Domain.Entities.User;

namespace Ludora.Domain.Entities.Forum;

public class Discussion : BaseEntity
{
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Content { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public int CategoryId { get; set; }
    public ForumCategory Category { get; set; } = null!;

    public int? CommunityId { get; set; }
    public ForumCommunity? Community { get; set; }

    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public bool IsSpoiler { get; set; }
    public int ViewCount { get; set; }

    public ICollection<DiscussionReply> Replies { get; set; } = [];
    public ICollection<DiscussionLike> Likes { get; set; } = [];
    public ICollection<DiscussionMedia> Media { get; set; } = [];
    public ICollection<DiscussionSave> Saves { get; set; } = [];
    public ICollection<DiscussionView> Views { get; set; } = [];
    public DiscussionPoll? Poll { get; set; }
}
