export type NotificationType =
  | "AnnouncementPublished"
  | "WarningIssued"
  | "FollowingPostCreated"
  | "FollowingReviewCreated"
  | "FollowingGameStatusChanged"
  | "FollowingPublicListCreated"
  | "FollowingDiscussionCreated"
  | "PostLiked"
  | "PostCommented"
  | "ReviewLiked"
  | "ReviewCommented"
  | "GameListLiked"
  | "GameListCommented"
  | "DiscussionLiked"
  | "DiscussionReplied"
  | "DiscussionReplyLiked"
  | "FollowedDiscussionReplyActivity"
  | "FollowRequestReceived"
  | "UserFollowed"
  | "FollowRequestAccepted";

export type NotificationPriority = "Low" | "Normal" | "High" | "Critical";
export type NotificationAudience = "Everyone" | "ConnectionsOnly" | "Never";
export type NotificationEntityType =
  | "None"
  | "Announcement"
  | "Warning"
  | "User"
  | "Post"
  | "Review"
  | "Game"
  | "GameList"
  | "Discussion"
  | "DiscussionReply";

export interface NotificationDTO {
  id: number;
  type: NotificationType;
  priority: NotificationPriority;
  entityType: NotificationEntityType;
  entityId: string | null;
  actorUserId: string | null;
  actorUserName: string | null;
  actorProfileImageUrl: string | null;
  title: string;
  message: string;
  actionUrl: string | null;
  dataJson: string | null;
  isAdminNotification: boolean;
  isRead: boolean;
  readAtUtc: string | null;
  createdAtUtc: string;
}

export interface NotificationPageDTO {
  items: NotificationDTO[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface UnreadNotificationCountDTO {
  count: number;
}

export interface NotificationPreferenceDTO {
  type: NotificationType;
  audience: NotificationAudience;
  isOverridden: boolean;
}

export interface NotificationSettingsDTO {
  items: NotificationPreferenceDTO[];
}
