import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { chatQueryKeys } from "../services/chatService";
import { useAuthStore } from "../stores/authStore";
import type {
  ChatMessageDTO,
  ChatReadReceiptDTO,
  ChatRequestDecisionDTO,
} from "../types/chat";

function getChatHubUrl(): string {
  const configuredOrigin = import.meta.env.VITE_API_ORIGIN;
  if (configuredOrigin) {
    return `${configuredOrigin.replace(/\/$/, "")}/hubs/chat`;
  }

  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL;
  const url = new URL(apiBaseUrl, window.location.origin);
  return `${url.origin}/hubs/chat`;
}

export function useChatRealtime(enabled: boolean) {
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);

  useEffect(() => {
    if (!enabled || !accessToken) return;

    let disposed = false;
    let retryTimer: number | undefined;
    const connection = new HubConnectionBuilder()
      .withUrl(getChatHubUrl(), {
        accessTokenFactory: () => accessToken,
      })
      .withAutomaticReconnect([0, 2_000, 10_000, 30_000])
      .configureLogging(LogLevel.Warning)
      .build();

    const refreshConversation = (conversationId?: number) => {
      void queryClient.invalidateQueries({ queryKey: chatQueryKeys.conversations });
      void queryClient.invalidateQueries({ queryKey: chatQueryKeys.requests });
      if (conversationId) {
        void queryClient.invalidateQueries({
          queryKey: chatQueryKeys.messages(conversationId),
        });
      }
    };

    connection.on("MessageCreated", (message: ChatMessageDTO) => {
      refreshConversation(message.conversationId);
    });
    connection.on("MessagesRead", (receipt: ChatReadReceiptDTO) => {
      refreshConversation(receipt.conversationId);
    });
    connection.on("MessageDeliveryChanged", (receipt: ChatReadReceiptDTO) => {
      refreshConversation(receipt.conversationId);
    });
    connection.on(
      "MessageRequestUpdated",
      (decision: ChatRequestDecisionDTO) => {
        refreshConversation(decision.conversationId);
      },
    );
    connection.onreconnected(() => refreshConversation());

    async function start() {
      if (disposed || connection.state !== HubConnectionState.Disconnected) return;
      try {
        await connection.start();
      } catch {
        if (!disposed) retryTimer = window.setTimeout(start, 5_000);
      }
    }

    void start();
    return () => {
      disposed = true;
      if (retryTimer) window.clearTimeout(retryTimer);
      connection.off("MessageCreated");
      connection.off("MessagesRead");
      connection.off("MessageDeliveryChanged");
      connection.off("MessageRequestUpdated");
      void connection.stop();
    };
  }, [accessToken, enabled, queryClient]);
}
