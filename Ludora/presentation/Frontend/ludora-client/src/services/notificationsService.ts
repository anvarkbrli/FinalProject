import { api } from "../lib/api";
import type {
  NotificationAudience,
  NotificationDTO,
  NotificationPageDTO,
  NotificationPreferenceDTO,
  NotificationSettingsDTO,
  NotificationType,
  UnreadNotificationCountDTO,
} from "../types/notifications";

export const notificationQueryKeys = {
  all: ["notifications"] as const,
  list: ["notifications", "list"] as const,
  unreadCount: ["notifications", "unread-count"] as const,
  settings: ["notification-settings"] as const,
};

export async function getNotifications(
  pageNumber = 1,
  pageSize = 12,
  isRead?: boolean,
): Promise<NotificationPageDTO> {
  const response = await api.get<NotificationPageDTO>("/notifications", {
    params: { pageNumber, pageSize, isRead },
  });

  return response.data;
}

export async function getUnreadNotificationCount(): Promise<UnreadNotificationCountDTO> {
  const response = await api.get<UnreadNotificationCountDTO>(
    "/notifications/unread-count",
  );

  return response.data;
}

export async function markNotificationAsRead(
  notificationId: number,
): Promise<NotificationDTO> {
  const response = await api.patch<NotificationDTO>(
    `/notifications/${notificationId}/read`,
  );

  return response.data;
}

export async function markAllNotificationsAsRead(): Promise<void> {
  await api.patch("/notifications/read-all");
}

export async function deleteNotification(notificationId: number): Promise<void> {
  await api.delete(`/notifications/${notificationId}`);
}

export async function getNotificationSettings(): Promise<NotificationSettingsDTO> {
  const response = await api.get<NotificationSettingsDTO>(
    "/notifications/settings",
  );

  return response.data;
}

export async function updateNotificationPreference(
  type: NotificationType,
  audience: NotificationAudience,
): Promise<NotificationPreferenceDTO> {
  const response = await api.put<NotificationPreferenceDTO>(
    `/notifications/settings/${type}`,
    { audience },
  );

  return response.data;
}

export async function resetNotificationPreferences(): Promise<void> {
  await api.delete("/notifications/settings/reset");
}
