import { api } from "../lib/api";
import type {
  ChatConversationDTO,
  ChatMessageDTO,
  ChatMessagePageDTO,
  ChatMessageDeletedDTO,
  ChatMessageDeleteScope,
  ChatMessagePinChangedDTO,
  ChatRequestDecisionDTO,
  ChatSendMessageRequest,
} from "../types/chat";

export const chatQueryKeys = {
  all: ["chat"] as const,
  conversations: ["chat", "conversations"] as const,
  requests: ["chat", "requests"] as const,
  messages: (conversationId: number) =>
    ["chat", "messages", conversationId] as const,
  pinnedMessages: (conversationId: number) =>
    ["chat", "pinned-messages", conversationId] as const,
};

export async function getChatConversations(): Promise<ChatConversationDTO[]> {
  const response = await api.get<ChatConversationDTO[]>("/chat/conversations");
  return response.data;
}

export async function getMessageRequests(): Promise<ChatConversationDTO[]> {
  const response = await api.get<ChatConversationDTO[]>("/chat/requests");
  return response.data;
}

export async function getChatMessages(
  conversationId: number,
  beforeMessageId?: number,
): Promise<ChatMessagePageDTO> {
  const response = await api.get<ChatMessagePageDTO>(
    `/chat/conversations/${conversationId}/messages`,
    {
      params: {
        beforeMessageId,
        pageSize: 60,
      },
    },
  );
  return response.data;
}

export async function sendChatMessage(
  request: ChatSendMessageRequest,
): Promise<ChatMessageDTO> {
  const formData = new FormData();
  formData.append("recipientUserId", request.recipientUserId);

  if (request.text?.trim()) {
    formData.append("text", request.text.trim());
  }
  if (request.media) {
    formData.append("media", request.media);
  }
  if (request.game) {
    formData.append("gameSource", request.game.source);
    formData.append("gameId", request.game.id.toString());
    formData.append("gameName", request.game.name);
    if (request.game.coverUrl) {
      formData.append("gameCoverUrl", request.game.coverUrl);
    }
  }

  const response = await api.post<ChatMessageDTO>(
    "/chat/messages",
    formData,
    {
      headers: { "Content-Type": "multipart/form-data" },
      timeout: request.media ? 90_000 : 15_000,
    },
  );
  return response.data;
}

export async function acceptMessageRequest(
  conversationId: number,
): Promise<ChatRequestDecisionDTO> {
  const response = await api.post<ChatRequestDecisionDTO>(
    `/chat/requests/${conversationId}/accept`,
  );
  return response.data;
}

export async function rejectMessageRequest(
  conversationId: number,
): Promise<ChatRequestDecisionDTO> {
  const response = await api.post<ChatRequestDecisionDTO>(
    `/chat/requests/${conversationId}/reject`,
  );
  return response.data;
}

export async function markConversationRead(
  conversationId: number,
): Promise<void> {
  await api.post(`/chat/conversations/${conversationId}/read`);
}

export async function getChatMediaBlob(mediaUrl: string): Promise<Blob> {
  const normalizedUrl = mediaUrl.startsWith("/api/")
    ? mediaUrl.slice(4)
    : mediaUrl;
  const response = await api.get<Blob>(normalizedUrl, {
    responseType: "blob",
    timeout: 120_000,
  });
  return response.data;
}

export async function getPinnedChatMessages(
  conversationId: number,
): Promise<ChatMessageDTO[]> {
  const response = await api.get<ChatMessageDTO[]>(
    `/chat/conversations/${conversationId}/pinned-messages`,
  );
  return response.data;
}

export async function deleteChatMessage(
  messageId: number,
  scope: ChatMessageDeleteScope,
): Promise<ChatMessageDeletedDTO> {
  const response = await api.delete<ChatMessageDeletedDTO>(
    `/chat/messages/${messageId}`,
    { params: { scope } },
  );
  return response.data;
}

export async function setChatMessagePinned(
  messageId: number,
  isPinned: boolean,
): Promise<ChatMessagePinChangedDTO> {
  const response = isPinned
    ? await api.post<ChatMessagePinChangedDTO>(`/chat/messages/${messageId}/pin`)
    : await api.delete<ChatMessagePinChangedDTO>(`/chat/messages/${messageId}/pin`);
  return response.data;
}
