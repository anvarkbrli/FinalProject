import { api } from "../lib/api";
import type {
  AdminDashboard,
  AdminBadge,
  AdminBadgeUpsert,
  AdminModerationAuditFilter,
  AdminPost,
  AdminReportedUserDetail,
  AdminReportedUserListItem,
  AdminUserBanHistory,
  AdminUserBadge,
  AdminUserDetail,
  AdminUserFilter,
  AdminUserListItem,
  AdminUserReportItem,
  AdminUserWarningHistory,
  Announcement,
  BanType,
  DiscussionListItem,
  DiscussionReply,
  ForumCategory,
  ModeratorActionLog,
  PagedResult,
  UserReportCategory,
  UserReportContentType,
  UserReportStatus,
  UserWarning,
} from "../types/admin";

export async function getAdminDashboard(
  signal?: AbortSignal,
): Promise<AdminDashboard> {
  const response = await api.get<AdminDashboard>(
    "/admin/dashboard",
    { signal },
  );
  return response.data;
}

export async function getAdminUsers(
  filter: AdminUserFilter,
  signal?: AbortSignal,
): Promise<PagedResult<AdminUserListItem>> {
  const response = await api.get<PagedResult<AdminUserListItem>>(
    "/admin/users",
    { params: filter, signal },
  );
  return response.data;
}

export async function getAdminUser(
  userId: string,
  signal?: AbortSignal,
): Promise<AdminUserDetail> {
  const response = await api.get<AdminUserDetail>(
    `/admin/users/${userId}`,
    { signal },
  );
  return response.data;
}

export async function getAdminUserWarnings(
  userId: string,
  signal?: AbortSignal,
): Promise<AdminUserWarningHistory> {
  const response = await api.get<AdminUserWarningHistory>(
    `/admin/users/${userId}/warnings`,
    { signal },
  );
  return response.data;
}

export async function getAdminUserBanHistory(
  userId: string,
  signal?: AbortSignal,
): Promise<AdminUserBanHistory> {
  const response = await api.get<AdminUserBanHistory>(
    `/admin/users/${userId}/ban-history`,
    { signal },
  );
  return response.data;
}

export async function warnAdminUser(
  userId: string,
  message: string,
): Promise<UserWarning> {
  const response = await api.post<UserWarning>(
    `/admin/users/${userId}/warnings`,
    { message },
  );
  return response.data;
}

export async function banAdminUser(
  userId: string,
  request: {
    type: BanType;
    durationMinutes?: number;
    reason: string;
  },
): Promise<AdminUserDetail> {
  const response = await api.post<AdminUserDetail>(
    `/admin/users/${userId}/ban`,
    request,
  );
  return response.data;
}

export async function unbanAdminUser(
  userId: string,
): Promise<AdminUserDetail> {
  const response = await api.post<AdminUserDetail>(
    `/admin/users/${userId}/unban`,
  );
  return response.data;
}

export async function changeAdminUserRole(
  userId: string,
  role: string,
): Promise<AdminUserDetail> {
  const response = await api.put<AdminUserDetail>(
    `/admin/users/${userId}/role`,
    { role },
  );
  return response.data;
}

export async function getReportedUsers(
  filter: {
    search?: string;
    category?: UserReportCategory;
    status?: UserReportStatus;
    contentType?: UserReportContentType;
    isBanned?: boolean;
    fromUtc?: string;
    toUtc?: string;
    sortBy?: string;
    pageNumber?: number;
    pageSize?: number;
  },
  signal?: AbortSignal,
): Promise<PagedResult<AdminReportedUserListItem>> {
  const response = await api.get<PagedResult<AdminReportedUserListItem>>(
    "/admin/user-reports",
    { params: filter, signal },
  );
  return response.data;
}

export async function getReportedUser(
  userId: string,
  signal?: AbortSignal,
): Promise<AdminReportedUserDetail> {
  const response = await api.get<AdminReportedUserDetail>(
    `/admin/user-reports/users/${userId}`,
    { signal },
  );
  return response.data;
}

export async function getReportEvidence(
  reportId: number,
  index: number,
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await api.get<Blob>(
    `/admin/user-reports/${reportId}/evidence`,
    {
      params: { index },
      responseType: "blob",
      signal,
      timeout: 60_000,
    },
  );
  return response.data;
}

export async function updateReportStatus(
  reportId: number,
  request: {
    status: UserReportStatus;
    adminNote?: string;
  },
): Promise<AdminUserReportItem> {
  const response = await api.patch<AdminUserReportItem>(
    `/admin/user-reports/${reportId}/status`,
    request,
  );
  return response.data;
}

export async function getAdminPosts(
  filter: {
    search?: string;
    userId?: string;
    isDeleted?: boolean;
    sortBy?: string;
    pageNumber?: number;
    pageSize?: number;
  },
  signal?: AbortSignal,
): Promise<PagedResult<AdminPost>> {
  const response = await api.get<PagedResult<AdminPost>>(
    "/admin/posts",
    { params: filter, signal },
  );
  return response.data;
}

export async function getAdminPost(
  postId: number,
  signal?: AbortSignal,
): Promise<AdminPost> {
  const response = await api.get<AdminPost>(
    `/admin/posts/${postId}`,
    { signal },
  );
  return response.data;
}

export async function deleteAdminPost(
  postId: number,
  reason: string,
): Promise<void> {
  await api.delete(`/admin/posts/${postId}`, {
    data: { reason },
  });
}

export async function restoreAdminPost(
  postId: number,
): Promise<AdminPost> {
  const response = await api.post<AdminPost>(
    `/admin/posts/${postId}/restore`,
  );
  return response.data;
}

export async function getAdminAnnouncements(
  filter: {
    search?: string;
    isPublished?: boolean;
    isDeleted?: boolean;
    sortBy?: string;
    pageNumber?: number;
    pageSize?: number;
  },
  signal?: AbortSignal,
): Promise<PagedResult<Announcement>> {
  const response = await api.get<PagedResult<Announcement>>(
    "/admin/announcements",
    { params: filter, signal },
  );
  return response.data;
}

export async function getAdminAnnouncement(
  announcementId: number,
  signal?: AbortSignal,
): Promise<Announcement> {
  const response = await api.get<Announcement>(
    `/admin/announcements/${announcementId}`,
    { signal },
  );
  return response.data;
}

export async function createAdminAnnouncement(
  request: {
    title: string;
    content: string;
    isPublished: boolean;
  },
): Promise<Announcement> {
  const response = await api.post<Announcement>(
    "/admin/announcements",
    request,
  );
  return response.data;
}

export async function updateAdminAnnouncement(
  announcementId: number,
  request: {
    title: string;
    content: string;
    isPublished: boolean;
  },
): Promise<Announcement> {
  const response = await api.put<Announcement>(
    `/admin/announcements/${announcementId}`,
    request,
  );
  return response.data;
}

export async function deleteAdminAnnouncement(
  announcementId: number,
): Promise<void> {
  await api.delete(`/admin/announcements/${announcementId}`);
}

export async function restoreAdminAnnouncement(
  announcementId: number,
): Promise<Announcement> {
  const response = await api.post<Announcement>(
    `/admin/announcements/${announcementId}/restore`,
  );
  return response.data;
}

export async function getForumCategories(
  includeDeleted = false,
  signal?: AbortSignal,
): Promise<ForumCategory[]> {
  const path = includeDeleted
    ? "/forum/categories/deleted"
    : "/forum/categories";
  const response = await api.get<ForumCategory[]>(path, { signal });
  return response.data;
}

export async function createForumCategory(
  request: { name: string; description?: string },
): Promise<ForumCategory> {
  const response = await api.post<ForumCategory>(
    "/forum/categories",
    request,
  );
  return response.data;
}

export async function updateForumCategory(
  categoryId: number,
  request: { name: string; description?: string },
): Promise<ForumCategory> {
  const response = await api.put<ForumCategory>(
    `/forum/categories/${categoryId}`,
    request,
  );
  return response.data;
}

export async function deleteForumCategory(
  categoryId: number,
): Promise<void> {
  await api.delete(`/forum/categories/${categoryId}`);
}

export async function restoreForumCategory(
  categoryId: number,
): Promise<ForumCategory> {
  const response = await api.post<ForumCategory>(
    `/forum/categories/${categoryId}/restore`,
  );
  return response.data;
}

export async function getDeletedDiscussions(
  filter: {
    search?: string;
    categoryId?: number;
    sortBy?: string;
    pageNumber?: number;
    pageSize?: number;
  },
  signal?: AbortSignal,
): Promise<PagedResult<DiscussionListItem>> {
  const response = await api.get<PagedResult<DiscussionListItem>>(
    "/admin/forum/discussions/deleted",
    { params: filter, signal },
  );
  return response.data;
}

export async function setDiscussionState(
  discussionId: number,
  action: "pin" | "unpin" | "lock" | "unlock" | "restore",
): Promise<void> {
  await api.post(`/admin/forum/discussions/${discussionId}/${action}`);
}

export async function deleteDiscussion(
  discussionId: number,
): Promise<void> {
  await api.delete(`/admin/forum/discussions/${discussionId}`);
}

export async function getDeletedDiscussionReplies(
  filter: {
    discussionId?: number;
    pageNumber?: number;
    pageSize?: number;
  },
  signal?: AbortSignal,
): Promise<PagedResult<DiscussionReply>> {
  const response = await api.get<PagedResult<DiscussionReply>>(
    "/admin/forum/replies/deleted",
    { params: filter, signal },
  );
  return response.data;
}

export async function deleteDiscussionReply(
  replyId: number,
): Promise<void> {
  await api.delete(`/admin/forum/replies/${replyId}`);
}

export async function restoreDiscussionReply(
  replyId: number,
): Promise<DiscussionReply> {
  const response = await api.post<DiscussionReply>(
    `/admin/forum/replies/${replyId}/restore`,
  );
  return response.data;
}

export async function getModerationAuditByUser(
  userId: string,
  signal?: AbortSignal,
): Promise<ModeratorActionLog[]> {
  const response = await api.get<ModeratorActionLog[]>(
    `/admin/moderation-audit/users/${userId}`,
    { signal },
  );
  return response.data;
}

export async function getModerationAuditByReport(
  reportId: number,
  signal?: AbortSignal,
): Promise<ModeratorActionLog[]> {
  const response = await api.get<ModeratorActionLog[]>(
    `/admin/moderation-audit/reports/${reportId}`,
    { signal },
  );
  return response.data;
}

export async function getModerationAudit(
  filter: AdminModerationAuditFilter,
  signal?: AbortSignal,
): Promise<PagedResult<ModeratorActionLog>> {
  const response = await api.get<PagedResult<ModeratorActionLog>>(
    "/admin/moderation-audit",
    { params: filter, signal },
  );
  return response.data;
}

export async function getAdminBadges(
  signal?: AbortSignal,
): Promise<AdminBadge[]> {
  const response = await api.get<AdminBadge[]>("/admin/badges", { signal });
  return response.data;
}

export async function createAdminBadge(
  request: AdminBadgeUpsert,
): Promise<AdminBadge> {
  const response = await api.post<AdminBadge>("/admin/badges", request);
  return response.data;
}

export async function updateAdminBadge(
  badgeId: number,
  request: AdminBadgeUpsert,
): Promise<AdminBadge> {
  const response = await api.put<AdminBadge>(
    `/admin/badges/${badgeId}`,
    request,
  );
  return response.data;
}

export async function archiveAdminBadge(badgeId: number): Promise<void> {
  await api.delete(`/admin/badges/${badgeId}`);
}

export async function restoreAdminBadge(
  badgeId: number,
): Promise<AdminBadge> {
  const response = await api.post<AdminBadge>(
    `/admin/badges/${badgeId}/restore`,
  );
  return response.data;
}

export async function evaluateAdminBadgesForUser(
  userId: string,
): Promise<number> {
  const response = await api.post<{ awardedCount: number }>(
    `/admin/badges/evaluate/users/${userId}`,
  );
  return response.data.awardedCount;
}

export async function uploadAdminBadgeIcon(
  badgeId: number,
  image: File,
): Promise<AdminBadge> {
  const formData = new FormData();
  formData.append("image", image);
  const response = await api.put<AdminBadge>(
    `/admin/badges/${badgeId}/icon`,
    formData,
    {
      headers: {
        "Content-Type": "multipart/form-data",
      },
    },
  );
  return response.data;
}

export async function getAdminUserBadges(
  userId: string,
  signal?: AbortSignal,
): Promise<AdminUserBadge[]> {
  const response = await api.get<AdminUserBadge[]>(
    `/badges/users/${userId}`,
    { signal },
  );
  return response.data;
}

export async function assignAdminBadgeToUser(
  userId: string,
  badgeId: number,
): Promise<AdminUserBadge> {
  const response = await api.post<AdminUserBadge>(
    `/admin/badges/${badgeId}/users/${userId}`,
  );
  return response.data;
}

export async function revokeAdminBadgeFromUser(
  userId: string,
  badgeId: number,
): Promise<void> {
  await api.delete(`/admin/badges/${badgeId}/users/${userId}`);
}
