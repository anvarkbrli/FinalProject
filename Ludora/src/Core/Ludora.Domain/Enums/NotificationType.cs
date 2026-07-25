namespace Ludora.Domain.Enums
{
    public enum NotificationType
    {
        AnnouncementPublished = 1,
        WarningIssued = 2,

        FollowingPostCreated = 10,
        FollowingReviewCreated = 11,
        FollowingGameStatusChanged = 12,
        FollowingPublicListCreated = 13,
        FollowingDiscussionCreated = 14,
        CommunityDiscussionCreated = 15,

        PostLiked = 20,
        PostCommented = 21,
        ReviewLiked = 22,
        ReviewCommented = 23,
        GameListLiked = 24,
        GameListCommented = 25,
        DiscussionLiked = 26,
        DiscussionReplied = 27,
        DiscussionReplyLiked = 28,
        FollowedDiscussionReplyActivity = 29,

        FollowRequestReceived = 30,
        UserFollowed = 31,
        FollowRequestAccepted = 32
    }
}

