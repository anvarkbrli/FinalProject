import { api } from "../lib/api";
import type {
  ChatConversationArchiveChangedDTO,
  ChatConversationDeletedDTO,
  ChatConversationDTO,
  ChatForwardMessagesResultDTO,
  ChatGroupChangedDTO,
  ChatGroupDTO,
  ChatBulkDeleteResultDTO,
  ChatMessageDTO,
  ChatMessageDeletedDTO,
  ChatMessageDeleteScope,
  ChatMessagePageDTO,
  ChatMessagePinChangedDTO,
  ChatMessageReactionChangedDTO,
  ChatMessageSearchPageDTO,
  ChatReactionType,
  ChatRequestDecisionDTO,
  ChatSendMessageRequest,
  ChatSendMessageResultDTO,
} from "../types/chat";

export const chatQueryKeys = {
  all: ["chat"] as const,
  conversationLists: ["chat", "conversations"] as const,
  conversations: (archived = false, search = "") =>
    [
      "chat",
      "conversations",
      archived ? "archived" : "active",
      search.trim().toLocaleLowerCase(),
    ] as const,
  requestLists: ["chat", "requests"] as const,
  requests: (search = "") =>
    ["chat", "requests", search.trim().toLocaleLowerCase()] as const,
  messages: (conversationId: number) =>
    ["chat", "messages", conversationId] as const,
  pinnedMessages: (conversationId: number) =>
    ["chat", "pinned-messages", conversationId] as const,
  messageSearch: (conversationId: number, query: string, page: number) =>
    ["chat", "message-search", conversationId, query, page] as const,
};

export async function getChatConversations(
  archived = false,
  search = "",
): Promise<ChatConversationDTO[]> {
  const response = await api.get<ChatConversationDTO[]>("/chat/conversations", {
    params: { archived, search: search.trim() || undefined },
  });
  return response.data;
}

export async function getMessageRequests(
  search = "",
): Promise<ChatConversationDTO[]> {
  const response = await api.get<ChatConversationDTO[]>("/chat/requests", {
    params: { search: search.trim() || undefined },
  });
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

export async function getChatMessage(
  conversationId: number,
  messageId: number,
): Promise<ChatMessageDTO> {
  const response = await api.get<ChatMessageDTO>(
    `/chat/conversations/${conversationId}/messages/${messageId}`,
  );
  return response.data;
}

export async function searchChatMessages(
  conversationId: number,
  query: string,
  page = 1,
): Promise<ChatMessageSearchPageDTO> {
  const response = await api.get<ChatMessageSearchPageDTO>(
    `/chat/conversations/${conversationId}/messages/search`,
    { params: { query, page, pageSize: 20 } },
  );
  return response.data;
}

export async function sendChatMessage(
  request: ChatSendMessageRequest,
): Promise<ChatSendMessageResultDTO> {
  const formData = new FormData();
  if (request.recipientUserId) {
    formData.append("recipientUserId", request.recipientUserId);
  }
  if (request.conversationId) {
    formData.append("conversationId", request.conversationId.toString());
  }

  if (request.text?.trim()) {
    formData.append("text", request.text.trim());
  }
  for (const mediaFile of request.media ?? []) {
    formData.append("media", mediaFile);
  }
  if (request.replyToMessageId) {
    formData.append("replyToMessageId", request.replyToMessageId.toString());
  }
  if (request.game) {
    formData.append("gameSource", request.game.source);
    formData.append("gameId", request.game.id.toString());
    formData.append("gameName", request.game.name);
    if (request.game.coverUrl) {
      formData.append("gameCoverUrl", request.game.coverUrl);
    }
  }

  const response = await api.post<ChatSendMessageResultDTO>(
    "/chat/messages",
    formData,
    {
      headers: { "Content-Type": "multipart/form-data" },
      timeout: request.media?.length ? 180_000 : 15_000,
    },
  );
  return response.data;
}

export async function forwardChatMessage(
  messageId: number,
  targetConversationIds: number[],
): Promise<ChatForwardMessagesResultDTO> {
  const response = await api.post<ChatForwardMessagesResultDTO>(
    `/chat/messages/${messageId}/forward`,
    { targetConversationIds },
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

export async function setConversationArchived(
  conversationId: number,
  isArchived: boolean,
): Promise<ChatConversationArchiveChangedDTO> {
  const response = isArchived
    ? await api.post<ChatConversationArchiveChangedDTO>(
        `/chat/conversations/${conversationId}/archive`,
      )
    : await api.delete<ChatConversationArchiveChangedDTO>(
        `/chat/conversations/${conversationId}/archive`,
      );
  return response.data;
}

export async function deleteChatConversation(
  conversationId: number,
): Promise<ChatConversationDeletedDTO> {
  const response = await api.delete<ChatConversationDeletedDTO>(
    `/chat/conversations/${conversationId}`,
  );
  return response.data;
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

export async function setChatMessageReaction(
  messageId: number,
  reactionType: ChatReactionType | null,
): Promise<ChatMessageReactionChangedDTO> {
  const response = reactionType
    ? await api.put<ChatMessageReactionChangedDTO>(
        `/chat/messages/${messageId}/reaction`,
        { reactionType },
      )
    : await api.delete<ChatMessageReactionChangedDTO>(
        `/chat/messages/${messageId}/reaction`,
      );
  return response.data;
}


export interface ChatGroupWriteRequest {
  name: string;
  description?: string;
  image?: File | null;
  removeImage?: boolean;
  memberUserIdsToAdd?: string[];
}

export async function createChatGroup(
  request: ChatGroupWriteRequest & { memberUserIds: string[] },
): Promise<ChatGroupDTO> {
  const formData = new FormData();
  formData.append("name", request.name.trim());
  if (request.description?.trim()) formData.append("description", request.description.trim());
  for (const userId of request.memberUserIds) formData.append("memberUserIds", userId);
  if (request.image) formData.append("image", request.image);

  const response = await api.post<ChatGroupDTO>("/chat/groups", formData, {
    headers: { "Content-Type": "multipart/form-data" },
    timeout: 60_000,
  });
  return response.data;
}

export async function getChatGroup(conversationId: number): Promise<ChatGroupDTO> {
  const response = await api.get<ChatGroupDTO>(`/chat/groups/${conversationId}`);
  return response.data;
}

export async function updateChatGroup(
  conversationId: number,
  request: ChatGroupWriteRequest,
): Promise<ChatGroupDTO> {
  const formData = new FormData();
  formData.append("name", request.name.trim());
  if (request.description?.trim()) formData.append("description", request.description.trim());
  formData.append("removeImage", request.removeImage ? "true" : "false");
  for (const userId of request.memberUserIdsToAdd ?? []) {
    formData.append("memberUserIdsToAdd", userId);
  }
  if (request.image) formData.append("image", request.image);

  const response = await api.put<ChatGroupDTO>(
    `/chat/groups/${conversationId}`,
    formData,
    { headers: { "Content-Type": "multipart/form-data" }, timeout: 60_000 },
  );
  return response.data;
}

export async function addChatGroupMembers(
  conversationId: number,
  userIds: string[],
): Promise<ChatGroupChangedDTO> {
  const response = await api.post<ChatGroupChangedDTO>(
    `/chat/groups/${conversationId}/members`,
    { userIds },
  );
  return response.data;
}

export async function removeChatGroupMember(
  conversationId: number,
  userId: string,
): Promise<ChatGroupChangedDTO> {
  const response = await api.delete<ChatGroupChangedDTO>(
    `/chat/groups/${conversationId}/members/${userId}`,
  );
  return response.data;
}

export async function leaveChatGroup(
  conversationId: number,
): Promise<ChatGroupChangedDTO> {
  const response = await api.post<ChatGroupChangedDTO>(
    `/chat/groups/${conversationId}/leave`,
  );
  return response.data;
}

export async function deleteChatMessages(
  messageIds: number[],
  scope: ChatMessageDeleteScope,
): Promise<ChatBulkDeleteResultDTO> {
  const response = await api.post<ChatBulkDeleteResultDTO>(
    "/chat/messages/bulk-delete",
    { messageIds, scope },
  );
  return response.data;
}
