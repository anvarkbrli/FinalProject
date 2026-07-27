import type { CatalogSource } from "./home";

export type ChatRequestStatus = "Pending" | "Accepted" | "Rejected";
export type ChatMessageKind = "Text" | "Image" | "Video" | "GameRecommendation";
export type ChatMessageDeliveryStatus = "Sent" | "Delivered" | "Read";
export type ChatMessageDeleteScope = "ForMe" | "ForEveryone";

export interface ChatUserDTO {
  id: string;
  userName: string;
  profileImageUrl: string | null;
}

export interface ChatMessageDTO {
  id: number;
  conversationId: number;
  senderUserId: string;
  kind: ChatMessageKind;
  text: string | null;
  mediaUrl: string | null;
  mediaContentType: string | null;
  gameSource: CatalogSource | null;
  gameId: number | null;
  gameName: string | null;
  gameCoverUrl: string | null;
  gameDetailUrl: string | null;
  sentAtUtc: string;
  deliveredAtUtc: string | null;
  readAtUtc: string | null;
  deliveryStatus: ChatMessageDeliveryStatus;
  isDeletedForEveryone: boolean;
  deletedForEveryoneAtUtc: string | null;
  isPinnedByCurrentUser: boolean;
  pinnedAtUtc: string | null;
}

export interface ChatConversationDTO {
  id: number;
  otherUser: ChatUserDTO;
  requestStatus: ChatRequestStatus;
  isIncomingRequest: boolean;
  canSendMessage: boolean;
  unreadCount: number;
  lastMessage: ChatMessageDTO | null;
  updatedAtUtc: string;
}

export interface ChatMessagePageDTO {
  items: ChatMessageDTO[];
  hasMore: boolean;
  nextBeforeMessageId: number | null;
}

export interface ChatSendMessageRequest {
  recipientUserId: string;
  text?: string;
  media?: File | null;
  game?: {
    source: CatalogSource;
    id: number;
    name: string;
    coverUrl: string | null;
  } | null;
}

export interface ChatRequestDecisionDTO {
  conversationId: number;
  status: ChatRequestStatus;
  respondedAtUtc: string;
}

export interface ChatReadReceiptDTO {
  conversationId: number;
  readByUserId: string;
  readAtUtc: string;
  messageIds: number[];
}

export interface ChatMessageDeletedDTO {
  messageId: number;
  conversationId: number;
  deletedByUserId: string;
  scope: ChatMessageDeleteScope;
  deletedAtUtc: string;
}

export interface ChatMessagePinChangedDTO {
  messageId: number;
  conversationId: number;
  userId: string;
  isPinned: boolean;
  pinnedAtUtc: string | null;
}
