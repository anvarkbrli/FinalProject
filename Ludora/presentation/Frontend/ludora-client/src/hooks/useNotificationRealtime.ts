import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import type { InfiniteData } from "@tanstack/react-query";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { notificationQueryKeys } from "../services/notificationsService";
import { useAuthStore } from "../stores/authStore";
import type {
  NotificationDTO,
  NotificationPageDTO,
} from "../types/notifications";

function getHubUrl(): string {
  const configuredOrigin = import.meta.env.VITE_API_ORIGIN;

  if (configuredOrigin) {
    return `${configuredOrigin.replace(/\/$/, "")}/hubs/notifications`;
  }

  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL;
  const url = new URL(apiBaseUrl, window.location.origin);
  return `${url.origin}/hubs/notifications`;
}

function prependNotification(
  current: InfiniteData<NotificationPageDTO> | undefined,
  notification: NotificationDTO,
): InfiniteData<NotificationPageDTO> | undefined {
  if (!current || current.pages.length === 0) {
    return current;
  }

  if (
    current.pages.some((page) =>
      page.items.some((item) => item.id === notification.id),
    )
  ) {
    return current;
  }

  const nextTotalCount = current.pages[0].totalCount + 1;

  return {
    ...current,
    pages: current.pages.map((page, pageIndex) => ({
      ...page,
      items:
        pageIndex === 0
          ? [notification, ...page.items].slice(0, page.pageSize)
          : page.items,
      totalCount: nextTotalCount,
      totalPages: Math.ceil(nextTotalCount / page.pageSize),
    })),
  };
}

export function useNotificationRealtime(enabled: boolean) {
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);
  const receivedNotificationIds = useRef(new Set<number>());

  useEffect(() => {
    if (!enabled || !accessToken) {
      receivedNotificationIds.current.clear();
      return;
    }

    let disposed = false;
    let retryTimer: number | undefined;

    const connection = new HubConnectionBuilder()
      .withUrl(getHubUrl(), {
        accessTokenFactory: () => accessToken,
      })
      .withAutomaticReconnect([0, 2_000, 10_000, 30_000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("NotificationCreated", (notification: NotificationDTO) => {
      if (receivedNotificationIds.current.has(notification.id)) {
        return;
      }

      receivedNotificationIds.current.add(notification.id);

      if (receivedNotificationIds.current.size > 500) {
        const oldestId = receivedNotificationIds.current.values().next().value;

        if (typeof oldestId === "number") {
          receivedNotificationIds.current.delete(oldestId);
        }
      }

      queryClient.setQueryData<{ count: number }>(
        notificationQueryKeys.unreadCount,
        (current) => ({ count: (current?.count ?? 0) + 1 }),
      );

      queryClient.setQueryData<InfiniteData<NotificationPageDTO>>(
        notificationQueryKeys.list,
        (current) => prependNotification(current, notification),
      );

      void queryClient.invalidateQueries({
        queryKey: notificationQueryKeys.unreadCount,
        refetchType: "active",
      });
      void queryClient.invalidateQueries({
        queryKey: notificationQueryKeys.list,
        refetchType: "active",
      });

      if (
        notification.type === "FollowRequestReceived" &&
        notification.entityType === "User" &&
        (!notification.actionUrl ||
          notification.actionUrl.includes("connections=requests"))
      ) {
        void queryClient.invalidateQueries({
          queryKey: ["follow-requests"],
          refetchType: "active",
        });
      }
    });

    connection.onreconnected(() => {
      void queryClient.invalidateQueries({
        queryKey: notificationQueryKeys.all,
        refetchType: "active",
      });
    });

    async function start() {
      if (disposed || connection.state !== HubConnectionState.Disconnected) {
        return;
      }

      try {
        await connection.start();
      } catch {
        if (!disposed) {
          retryTimer = window.setTimeout(start, 5_000);
        }
      }
    }

    void start();

    return () => {
      disposed = true;

      if (retryTimer) {
        window.clearTimeout(retryTimer);
      }

      connection.off("NotificationCreated");
      void connection.stop();
    };
  }, [accessToken, enabled, queryClient]);
}
