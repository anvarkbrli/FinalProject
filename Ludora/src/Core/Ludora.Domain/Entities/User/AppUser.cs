using Ludora.Domain.Entities.Forum;
using Microsoft.AspNetCore.Identity;

namespace Ludora.Domain.Entities.User;

public class AppUser : IdentityUser<Guid>
{
    public string? Bio { get; set; }
    public string? ProfileImageUrl { get; set; }
    public bool IsPrivate { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool IsPermanentlyBanned { get; set; }
    public DateTime? BannedUntilUtc { get; set; }
    public string? BanReason { get; set; }
    public DateTime? BannedAtUtc { get; set; }
    public Guid? BannedByUserId { get; set; }

    public ICollection<GameList> GameLists { get; set; } = new List<GameList>();
    public ICollection<UserFollow> Followers { get; set; } = new List<UserFollow>();
    public ICollection<UserFollow> Following { get; set; } = new List<UserFollow>();
    public ICollection<UserBlock> BlockedUsers { get; set; } = new List<UserBlock>();
    public ICollection<UserBlock> BlockedByUsers { get; set; } = new List<UserBlock>();
    public ICollection<UserGame> UserGames { get; set; } = new HashSet<UserGame>();
    public ICollection<UserPost> Posts { get; set; } = new List<UserPost>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<ReviewLike> ReviewLikes { get; set; } = new List<ReviewLike>();
    public ICollection<ReviewComment> ReviewComments { get; set; } = new List<ReviewComment>();
    public ICollection<UserActivity> Activities { get; set; } = new List<UserActivity>();
    public ICollection<Discussion> Discussions { get; set; } = new List<Discussion>();
    public ICollection<DiscussionReply> DiscussionReplies { get; set; } = new List<DiscussionReply>();
    public ICollection<DiscussionLike> DiscussionLikes { get; set; } = new List<DiscussionLike>();
    public ICollection<DiscussionReplyLike> DiscussionReplyLikes { get; set; } = new List<DiscussionReplyLike>();
    public ICollection<UserWarning> ReceivedWarnings { get; set; } = new List<UserWarning>();
    public ICollection<UserWarning> SentWarnings { get; set; } = new List<UserWarning>();
    public ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();
}

