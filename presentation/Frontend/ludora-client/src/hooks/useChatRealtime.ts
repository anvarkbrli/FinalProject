import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useRef, useState } from "react";
import { chatQueryKeys } from "../services/chatService";
import { useAuthStore } from "../stores/authStore";
import type {
  ChatConversationArchiveChangedDTO,
  ChatConversationDeletedDTO,
  ChatMessageDTO,
  ChatMessageDeletedDTO,
  ChatMessagePinChangedDTO,
  ChatMessageReactionChangedDTO,
  ChatPresenceChangedDTO,
  ChatConversationDTO,
  ChatReadReceiptDTO,
  ChatRequestDecisionDTO,
  ChatStreakUpdatedDTO,
  ChatTypingDTO,
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
  const connectionRef = useRef<HubConnection | null>(null);
  const typingTimersRef = useRef(new Map<number, number>());
  const streakTimerRef = useRef<number | null>(null);
  const [typingByConversation, setTypingByConversation] = useState<
    Record<number, ChatTypingDTO | undefined>
  >({});
  const [latestStreakEvent, setLatestStreakEvent] =
    useState<ChatStreakUpdatedDTO | null>(null);

  const joinConversation = useCallback(async (conversationId: number) => {
    const connection = connectionRef.current;
    if (!connection || connection.state !== HubConnectionState.Connected) return;
    try {
      await connection.invoke("JoinConversation", conversationId);
    } catch {
      // Reconnect will subscribe the user again.
    }
  }, []);

  const sendTyping = useCallback(
    async (conversationId: number, isTyping: boolean) => {
      const connection = connectionRef.current;
      if (!connection || connection.state !== HubConnectionState.Connected) return;
      try {
        await connection.invoke("SetTyping", conversationId, isTyping);
      } catch {
        // Typing is ephemeral; failure should not interrupt messaging.
      }
    },
    [],
  );

  const clearLatestStreakEvent = useCallback(() => {
    setLatestStreakEvent(null);
  }, []);

  useEffect(() => {
    if (!enabled || !accessToken) return;

    let disposed = false;
    let retryTimer: number | undefined;
    const typingTimers = typingTimersRef.current;
    const connection = new HubConnectionBuilder()
      .withUrl(getChatHubUrl(), {
        accessTokenFactory: () => accessToken,
      })
      .withAutomaticReconnect([0, 2_000, 10_000, 30_000])
      .configureLogging(LogLevel.Warning)
      .build();
    connectionRef.current = connection;

    const refreshConversation = (conversationId?: number) => {
      void queryClient.invalidateQueries({
        queryKey: chatQueryKeys.conversationLists,
      });
      void queryClient.invalidateQueries({ queryKey: chatQueryKeys.requestLists });
      if (conversationId) {
        void queryClient.invalidateQueries({
          queryKey: chatQueryKeys.messages(conversationId),
        });
      }
    };

    connection.on("MessageCreated", (message: ChatMessageDTO) => {
      refreshConversation(message.conversationId);
      void connection
        .invoke("JoinConversation", message.conversationId)
        .catch(() => undefined);
    });
    connection.on("MessageDeleted", (event: ChatMessageDeletedDTO) => {
      refreshConversation(event.conversationId);
      void queryClient.invalidateQueries({
        queryKey: chatQueryKeys.pinnedMessages(event.conversationId),
      });
    });
    connection.on("MessagePinChanged", (event: ChatMessagePinChangedDTO) => {
      refreshConversation(event.conversationId);
      void queryClient.invalidateQueries({
        queryKey: chatQueryKeys.pinnedMessages(event.conversationId),
      });
    });
    connection.on(
      "MessageReactionChanged",
      (event: ChatMessageReactionChangedDTO) => {
        refreshConversation(event.conversationId);
        void queryClient.invalidateQueries({
          queryKey: ["chat", "message-search", event.conversationId],
        });
      },
    );
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
    connection.on(
      "ConversationArchiveChanged",
      (event: ChatConversationArchiveChangedDTO) => {
        refreshConversation(event.conversationId);
      },
    );
    connection.on(
      "ConversationDeleted",
      (event: ChatConversationDeletedDTO) => {
        refreshConversation(event.conversationId);
        void queryClient.removeQueries({
          queryKey: chatQueryKeys.messages(event.conversationId),
        });
        void queryClient.removeQueries({
          queryKey: chatQueryKeys.pinnedMessages(event.conversationId),
        });
      },
    );
    connection.on("ChatStreakUpdated", (event: ChatStreakUpdatedDTO) => {
      setLatestStreakEvent(event);
      if (streakTimerRef.current) window.clearTimeout(streakTimerRef.current);
      streakTimerRef.current = window.setTimeout(() => {
        setLatestStreakEvent(null);
        streakTimerRef.current = null;
      }, 3_200);
      refreshConversation(event.conversationId);
    });
    connection.on("UserPresenceChanged", (event: ChatPresenceChangedDTO) => {
      const updateConversations = (current: ChatConversationDTO[] | undefined) =>
        current?.map((conversation) =>
          conversation.otherUser.id === event.userId
            ? {
                ...conversation,
                otherUser: {
                  ...conversation.otherUser,
                  isOnline: event.isOnline,
                  lastSeenAtUtc: event.lastSeenAtUtc,
                },
              }
            : conversation,
        );

      queryClient.setQueriesData<ChatConversationDTO[]>(
        { queryKey: chatQueryKeys.conversationLists },
        updateConversations,
      );
      queryClient.setQueriesData<ChatConversationDTO[]>(
        { queryKey: chatQueryKeys.requestLists },
        updateConversations,
      );
    });
    connection.on("ChatTyping", (event: ChatTypingDTO) => {
      const previousTimer = typingTimersRef.current.get(event.conversationId);
      if (previousTimer) window.clearTimeout(previousTimer);

      setTypingByConversation((current) => ({
        ...current,
        [event.conversationId]: event.isTyping ? event : undefined,
      }));

      if (event.isTyping) {
        const delay = Math.max(
          250,
          new Date(event.expiresAtUtc).getTime() - Date.now(),
        );
        const timer = window.setTimeout(() => {
          setTypingByConversation((current) => ({
            ...current,
            [event.conversationId]: undefined,
          }));
          typingTimersRef.current.delete(event.conversationId);
        }, delay);
        typingTimersRef.current.set(event.conversationId, timer);
      }
    });
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
      connectionRef.current = null;
      if (retryTimer) window.clearTimeout(retryTimer);
      for (const timer of typingTimers.values()) {
        window.clearTimeout(timer);
      }
      typingTimers.clear();
      if (streakTimerRef.current) window.clearTimeout(streakTimerRef.current);
      connection.off("MessageCreated");
      connection.off("MessageDeleted");
      connection.off("MessagePinChanged");
      connection.off("MessageReactionChanged");
      connection.off("MessagesRead");
      connection.off("MessageDeliveryChanged");
      connection.off("MessageRequestUpdated");
      connection.off("ConversationArchiveChanged");
      connection.off("ConversationDeleted");
      connection.off("ChatStreakUpdated");
      connection.off("UserPresenceChanged");
      connection.off("ChatTyping");
      void connection.stop();
    };
  }, [accessToken, enabled, queryClient]);

  return {
    typingByConversation,
    latestStreakEvent,
    clearLatestStreakEvent,
    joinConversation,
    sendTyping,
  };
}
