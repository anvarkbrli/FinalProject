using Ludora.Domain.Entities.Forum;
using Ludora.Domain.Entities.Steam;
using Microsoft.AspNetCore.Identity;

namespace Ludora.Domain.Entities.User;

public class AppUser : IdentityUser<Guid>
{
    public string? Bio { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? SocialLinksJson { get; set; }
    public bool IsPrivate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AccountDeletionRequestedAtUtc { get; set; }

    public string? SteamId { get; set; }
    public string? SteamDisplayName { get; set; }
    public string? SteamAvatarUrl { get; set; }
    public string? SteamProfileUrl { get; set; }
    public DateTime? SteamLinkedAtUtc { get; set; }
    public DateTime? SteamLastSyncedAtUtc { get; set; }

    public string? FpsGpuName { get; set; }
    public string? FpsCpuName { get; set; }
    public int? FpsRamGb { get; set; }
    public int? FpsVramGb { get; set; }
    public DateTime? FpsHardwareUpdatedAtUtc { get; set; }

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
    public ICollection<PostLike> PostLikes { get; set; } = new List<PostLike>();
    public ICollection<PostComment> PostComments { get; set; } = new List<PostComment>();
    public ICollection<GameListLike> GameListLikes { get; set; } = new List<GameListLike>();
    public ICollection<GameListComment> GameListComments { get; set; } = new List<GameListComment>();
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
    public ICollection<UserReport> SubmittedReports { get; set; } = new List<UserReport>();
    public ICollection<UserReport> ReceivedReports { get; set; } = new List<UserReport>();
    public ICollection<UserReport> ReviewedReports { get; set; } = new List<UserReport>();
    public ICollection<ModeratorActionLog> PerformedModeratorActions { get; set; } = new List<ModeratorActionLog>();
    public ICollection<ModeratorActionLog> ReceivedModeratorActions { get; set; } = new List<ModeratorActionLog>();
    public ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();
    public ICollection<Notification> ReceivedNotifications { get; set; } = new List<Notification>();
    public ICollection<Notification> SentNotifications { get; set; } = new List<Notification>();
    public ICollection<NotificationPreference> NotificationPreferences { get; set; } = new List<NotificationPreference>();
    public ICollection<SteamLinkChallenge> SteamLinkChallenges { get; set; } = new List<SteamLinkChallenge>();
    public ICollection<SteamOwnedGame> SteamOwnedGames { get; set; } = new List<SteamOwnedGame>();
    public ICollection<UserGamePlayLog> SteamPlayLogs { get; set; } = new List<UserGamePlayLog>();
    public ICollection<SteamFriendSnapshot> SteamFriendSnapshots { get; set; } = new List<SteamFriendSnapshot>();
    public ICollection<ChatConversation> ChatConversationsAsUserOne { get; set; } = [];
    public ICollection<ChatConversation> ChatConversationsAsUserTwo { get; set; } = [];
    public ICollection<ChatConversation> InitiatedChatConversations { get; set; } = [];
    public ICollection<ChatMessage> SentChatMessages { get; set; } = [];
    public ICollection<ChatMessageUserState> ChatMessageStates { get; set; } = [];
    public ICollection<UserBadge> Badges { get; set; } = [];
}
