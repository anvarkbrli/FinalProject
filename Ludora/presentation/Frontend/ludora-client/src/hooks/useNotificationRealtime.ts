import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { useAuthStore } from "../stores/authStore";
import type { NotificationDTO } from "../types/notifications";

function getHubUrl(): string {
  const configuredOrigin = import.meta.env.VITE_API_ORIGIN;

  if (configuredOrigin) {
    return `${configuredOrigin.replace(/\/$/, "")}/hubs/notifications`;
  }

  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL;
  const url = new URL(apiBaseUrl, window.location.origin);
  return `${url.origin}/hubs/notifications`;
}

export function useNotificationRealtime(enabled: boolean) {
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);
  const receivedNotificationIds = useRef(new Set<number>());

  useEffect(() => {
    if (!enabled || !accessToken) {
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

      queryClient.setQueryData<{ count: number }>(
        ["notifications", "unread-count"],
        (current) => ({ count: (current?.count ?? 0) + 1 }),
      );

      queryClient.setQueryData<{
        items: NotificationDTO[];
        pageNumber: number;
        pageSize: number;
        totalCount: number;
        totalPages: number;
      }>(["notifications", "list", 1, 12], (current) => {
        if (!current) {
          return current;
        }

        if (current.items.some((item) => item.id === notification.id)) {
          return current;
        }

        return {
          ...current,
          items: [notification, ...current.items].slice(0, current.pageSize),
          totalCount: current.totalCount + 1,
        };
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
      void connection.stop();
    };
  }, [accessToken, enabled, queryClient]);
}
