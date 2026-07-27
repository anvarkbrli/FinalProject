import type {
  UserReportCategory,
  UserReportContentType,
  UserReportStatus,
} from "./userReports";

export type {
  UserReportCategory,
  UserReportContentType,
  UserReportStatus,
} from "./userReports";

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface AdminDashboard {
  totalUsers: number;
  newUsersLast30Days: number;
  bannedUsers: number;
  totalPosts: number;
  deletedPosts: number;
  totalDiscussions: number;
  deletedDiscussions: number;
  totalReplies: number;
  deletedReplies: number;
  totalGames: number;
  totalWarnings: number;
  unreadWarnings: number;
  totalAnnouncements: number;
  publishedAnnouncements: number;
  deletedAnnouncements: number;
}

export interface AdminUserFilter {
  search?: string;
  role?: string;
  isBanned?: boolean;
  sortBy?: string;
  pageNumber?: number;
  pageSize?: number;
}

export interface AdminUserListItem {
  id: string;
  userName: string;
  email: string;
  profileImageUrl: string | null;
  role: string;
  isPrivate: boolean;
  isOnline: boolean;
  isBanned: boolean;
  isPermanentlyBanned: boolean;
  bannedUntilUtc: string | null;
  warningCount: number;
  reportCount: number;
  pendingReportCount: number;
  createdAt: string;
}

export interface AdminUserDetail {
  id: string;
  userName: string;
  email: string;
  bio: string | null;
  profileImageUrl: string | null;
  role: string;
  isPrivate: boolean;
  isOnline: boolean;
  isBanned: boolean;
  isPermanentlyBanned: boolean;
  bannedUntilUtc: string | null;
  banReason: string | null;
  bannedAtUtc: string | null;
  bannedByUserId: string | null;
  followerCount: number;
  followingCount: number;
  postCount: number;
  deletedPostCount: number;
  discussionCount: number;
  replyCount: number;
  warningCount: number;
  unreadWarningCount: number;
  reportCount: number;
  pendingReportCount: number;
  createdAt: string;
}

export interface UserWarning {
  id: number;
  userId: string;
  userName: string;
  warnedByUserId: string;
  warnedByUserName: string;
  message: string;
  createdAtUtc: string;
  isRead: boolean;
  readAtUtc: string | null;
}

export interface AdminUserWarningHistory {
  userId: string;
  userName: string;
  totalWarningCount: number;
  readWarningCount: number;
  unreadWarningCount: number;
  items: UserWarning[];
}

export type BanType = 1 | 2;

export interface AdminUserBanHistoryItem {
  id: number;
  type: BanType;
  reason: string;
  bannedAtUtc: string;
  bannedUntilUtc: string | null;
  bannedByUserId: string;
  bannedByUserName: string;
  isActive: boolean;
  isExpired: boolean;
  wasManuallyUnbanned: boolean;
  unbannedAtUtc: string | null;
  unbannedByUserId: string | null;
  unbannedByUserName: string | null;
}

export interface AdminUserBanHistory {
  userId: string;
  userName: string;
  totalBanCount: number;
  temporaryBanCount: number;
  permanentBanCount: number;
  items: AdminUserBanHistoryItem[];
}

export type ModeratorActionType =
  | 1
  | 2
  | 3
  | 4
  | 5
  | "ReportStatusChanged"
  | "WarningIssued"
  | "TemporaryBanIssued"
  | "PermanentBanIssued"
  | "UserUnbanned";

export interface UserReportCategoryCount {
  category: UserReportCategory;
  count: number;
}

export interface AdminReportedUserListItem {
  userId: string;
  userName: string;
  email: string | null;
  profileImageUrl: string | null;
  totalReportCount: number;
  pendingReportCount: number;
  warningCount: number;
  isBanned: boolean;
  isPermanentlyBanned: boolean;
  bannedUntilUtc: string | null;
  lastReportedAtUtc: string;
  categories: UserReportCategoryCount[];
}

export interface AdminUserReportItem {
  id: number;
  reporterUserId: string;
  reporterUserName: string;
  reporterProfileImageUrl: string | null;
  category: UserReportCategory;
  description: string | null;
  evidenceImageUrl: string | null;
  evidenceImageUrls: string[];
  evidenceImageCount: number;
  reportedContentType: UserReportContentType;
  reportedContentId: number | null;
  reportedContentSnapshot: string | null;
  reportedContentUrl: string | null;
  status: UserReportStatus;
  createdAtUtc: string;
  reviewedByUserId: string | null;
  reviewedByUserName: string | null;
  reviewedAtUtc: string | null;
  adminNote: string | null;
}

export interface AdminReportedUserDetail
  extends Omit<AdminReportedUserListItem, "lastReportedAtUtc"> {
  banReason: string | null;
  reports: AdminUserReportItem[];
}

export interface ModeratorActionLog {
  id: number;
  adminUserId: string;
  adminUserName: string;
  targetUserId: string;
  targetUserName: string;
  userReportId: number | null;
  userWarningId: number | null;
  userBanHistoryId: number | null;
  actionType: ModeratorActionType;
  previousReportStatus: UserReportStatus | null;
  newReportStatus: UserReportStatus | null;
  reason: string | null;
  createdAtUtc: string;
}

export interface AdminPost {
  id: number;
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  content: string | null;
  imageUrl: string | null;
  isDeleted: boolean;
  deletedAtUtc: string | null;
  deletedByUserId: string | null;
  deleteReason: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface Announcement {
  id: number;
  title: string;
  content: string;
  createdByUserId: string;
  createdByUserName: string;
  isPublished: boolean;
  publishedAtUtc: string | null;
  isDeleted: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface ForumCategory {
  id: number;
  name: string;
  slug: string;
  description: string | null;
  discussionCount: number;
  isDeleted: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface DiscussionListItem {
  id: number;
  title: string;
  slug: string;
  categoryId: number;
  categoryName: string;
  categorySlug: string;
  authorId: string;
  authorUserName: string;
  authorProfileImageUrl: string | null;
  likeCount: number;
  replyCount: number;
  viewCount: number;
  isPinned: boolean;
  isLocked: boolean;
  isDeleted: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface DiscussionReply {
  id: number;
  discussionId: number;
  authorId: string;
  authorUserName: string;
  authorProfileImageUrl: string | null;
  content: string;
  likeCount: number;
  isDeleted: boolean;
  createdAt: string;
  updatedAt: string | null;
}
