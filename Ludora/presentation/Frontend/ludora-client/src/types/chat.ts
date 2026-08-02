import type { CatalogSource } from "./home";

export type ChatRequestStatus = "Pending" | "Accepted" | "Rejected";
export type ChatMessageKind = "Text" | "Image" | "Video" | "GameRecommendation";
export type ChatMessageDeliveryStatus = "Sent" | "Delivered" | "Read";
export type ChatMessageDeleteScope = "ForMe" | "ForEveryone";
export type ChatReactionType =
  | "Like"
  | "Love"
  | "Laugh"
  | "Surprise"
  | "Sad"
  | "Fire";

export interface ChatUserDTO {
  id: string;
  userName: string;
  profileImageUrl: string | null;
  isOnline: boolean;
  lastSeenAtUtc: string | null;
}

export interface ChatPresenceChangedDTO {
  userId: string;
  isOnline: boolean;
  lastSeenAtUtc: string | null;
}

export interface ChatReplyPreviewDTO {
  messageId: number;
  senderUserId: string;
  senderUserName: string;
  kind: ChatMessageKind;
  text: string | null;
  gameName: string | null;
  isDeleted: boolean;
}

export interface ChatReactionSummaryDTO {
  reactionType: ChatReactionType;
  count: number;
  reactedByCurrentUser: boolean;
}

export interface ChatStreakDTO {
  currentCount: number;
  longestCount: number;
  lastCompletedDate: string | null;
  isCompletedToday: boolean;
  isActive: boolean;
}

export interface ChatStreakUpdatedDTO {
  conversationId: number;
  streak: ChatStreakDTO;
  wasIncremented: boolean;
}

export interface ChatMessageMediaDTO {
  id: number;
  url: string;
  contentType: string;
  fileName: string;
  length: number;
  sortOrder: number;
}

export interface ChatMessageDTO {
  id: number;
  conversationId: number;
  senderUserId: string;
  kind: ChatMessageKind;
  text: string | null;
  mediaUrl: string | null;
  mediaContentType: string | null;
  mediaItems: ChatMessageMediaDTO[];
  gameSource: CatalogSource | null;
  gameId: number | null;
  gameName: string | null;
  gameCoverUrl: string | null;
  gameDetailUrl: string | null;
  replyTo: ChatReplyPreviewDTO | null;
  isForwarded: boolean;
  reactions: ChatReactionSummaryDTO[];
  sentAtUtc: string;
  deliveredAtUtc: string | null;
  readAtUtc: string | null;
  deliveryStatus: ChatMessageDeliveryStatus;
  isDeletedForEveryone: boolean;
  deletedForEveryoneAtUtc: string | null;
  isPinnedByCurrentUser: boolean;
  pinnedAtUtc: string | null;
}

export interface ChatSendMessageResultDTO {
  message: ChatMessageDTO;
  streakUpdate: ChatStreakUpdatedDTO | null;
}

export interface ChatForwardedMessageDTO {
  conversationId: number;
  recipientUserId: string;
  message: ChatMessageDTO;
  streakUpdate: ChatStreakUpdatedDTO | null;
}

export interface ChatForwardMessagesResultDTO {
  items: ChatForwardedMessageDTO[];
}

export interface ChatConversationDTO {
  id: number;
  otherUser: ChatUserDTO;
  requestStatus: ChatRequestStatus;
  isIncomingRequest: boolean;
  canSendMessage: boolean;
  isArchived: boolean;
  unreadCount: number;
  lastMessage: ChatMessageDTO | null;
  streak: ChatStreakDTO;
  updatedAtUtc: string;
}

export interface ChatMessagePageDTO {
  items: ChatMessageDTO[];
  hasMore: boolean;
  nextBeforeMessageId: number | null;
}

export interface ChatMessageSearchPageDTO {
  items: ChatMessageDTO[];
  page: number;
  pageSize: number;
  totalCount: number;
  hasMore: boolean;
}

export interface ChatSendMessageRequest {
  recipientUserId: string;
  text?: string;
  media?: File[];
  replyToMessageId?: number | null;
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

export interface ChatMessageReactionChangedDTO {
  messageId: number;
  conversationId: number;
  userId: string;
  reactionType: ChatReactionType | null;
  reactions: ChatReactionSummaryDTO[];
}

export interface ChatConversationArchiveChangedDTO {
  conversationId: number;
  userId: string;
  isArchived: boolean;
  archivedAtUtc: string | null;
}

export interface ChatTypingDTO {
  conversationId: number;
  userId: string;
  isTyping: boolean;
  expiresAtUtc: string;
}
