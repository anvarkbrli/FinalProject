import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  Check,
  CheckCheck,
  ChevronLeft,
  Gamepad2,
  ImagePlus,
  LoaderCircle,
  Maximize2,
  MessageCircle,
  MoreVertical,
  Pin,
  PinOff,
  Search,
  Send,
  ShieldQuestion,
  Trash2,
  Video,
  X,
} from "lucide-react";
import {
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
  type FormEvent,
} from "react";
import { createPortal } from "react-dom";
import { Link, useSearchParams } from "react-router-dom";
import { Navbar } from "../components/layout/Navbar";
import { useChatRealtime } from "../hooks/useChatRealtime";
import {
  getCurrentLanguage,
  getCurrentLocale,
} from "../i18n/localization";
import { useI18n } from "../i18n/useI18n";
import { getApiErrorMessage } from "../lib/api";
import { useConfirmation } from "../lib/confirmation";
import { resolveAssetUrl } from "../lib/helpers";
import {
  acceptMessageRequest,
  chatQueryKeys,
  deleteChatMessage,
  getChatConversations,
  getChatMediaBlob,
  getChatMessages,
  getMessageRequests,
  getPinnedChatMessages,
  markConversationRead,
  rejectMessageRequest,
  sendChatMessage,
  setChatMessagePinned,
} from "../services/chatService";
import { globalSearch } from "../services/searchService";
import { useAuthStore } from "../stores/authStore";
import type {
  ChatConversationDTO,
  ChatMessageDeleteScope,
  ChatMessageDTO,
  ChatMessageDeliveryStatus,
} from "../types/chat";

const AZERBAIJANI_MONTH_NAMES = [
  "yanvar",
  "fevral",
  "mart",
  "aprel",
  "may",
  "iyun",
  "iyul",
  "avqust",
  "sentyabr",
  "oktyabr",
  "noyabr",
  "dekabr",
] as const;

function formatLocalizedDate(
  date: Date,
  includeYear = false,
): string {
  if (getCurrentLanguage() === "az") {
    const day = date.getDate();
    const month = AZERBAIJANI_MONTH_NAMES[date.getMonth()];
    return includeYear
      ? `${day} ${month} ${date.getFullYear()}`
      : `${day} ${month}`;
  }

  return new Intl.DateTimeFormat(getCurrentLocale(), {
    day: "numeric",
    month: "long",
    ...(includeYear ? { year: "numeric" as const } : {}),
  }).format(date);
}

function formatConversationTime(value: string): string {
  const date = new Date(value);
  const today = new Date();
  if (date.toDateString() === today.toDateString()) {
    return new Intl.DateTimeFormat(getCurrentLocale(), {
      hour: "2-digit",
      minute: "2-digit",
    }).format(date);
  }
  return formatLocalizedDate(
    date,
    date.getFullYear() !== today.getFullYear(),
  );
}

function formatMessageTime(value: string): string {
  return new Intl.DateTimeFormat(getCurrentLocale(), {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).format(new Date(value));
}

function formatDateSeparator(value: string): string {
  const date = new Date(value);
  const today = new Date();
  const yesterday = new Date();
  yesterday.setDate(today.getDate() - 1);

  if (date.toDateString() === today.toDateString()) {
    return getCurrentLanguage() === "en" ? "Today" : "Bu gün";
  }
  if (date.toDateString() === yesterday.toDateString()) {
    return getCurrentLanguage() === "en" ? "Yesterday" : "Dünən";
  }
  return formatLocalizedDate(
    date,
    date.getFullYear() !== today.getFullYear(),
  );
}

function deliveryIcon(status: ChatMessageDeliveryStatus) {
  if (status === "Sent") return <Check size={14} className="text-zinc-500" />;
  return (
    <CheckCheck
      size={15}
      className={status === "Read" ? "text-sky-400" : "text-zinc-400"}
    />
  );
}

function getMessagePreview(message: ChatMessageDTO | null): string {
  if (!message) return "Yeni söhbət";
  if (message.isDeletedForEveryone) return "Bu mesaj silindi";
  if (message.kind === "GameRecommendation") {
    return `🎮 ${message.gameName ?? "Oyun tövsiyəsi"}`;
  }
  if (message.kind === "Image") return "📷 Şəkil";
  if (message.kind === "Video") return "🎥 Video";
  return message.text ?? "Mesaj";
}

function UserAvatar({
  userName,
  profileImageUrl,
  className = "h-11 w-11",
}: {
  userName: string;
  profileImageUrl: string | null;
  className?: string;
}) {
  const imageUrl = resolveAssetUrl(profileImageUrl);
  return (
    <span
      className={`flex shrink-0 items-center justify-center overflow-hidden rounded-xl bg-violet-500/15 text-sm font-bold text-violet-200 ${className}`}
    >
      {imageUrl ? (
        <img src={imageUrl} alt="" className="h-full w-full object-cover" />
      ) : (
        userName.charAt(0).toUpperCase()
      )}
    </span>
  );
}

function AuthenticatedMedia({ message }: { message: ChatMessageDTO }) {
  const [isOpen, setIsOpen] = useState(false);
  const mediaQuery = useQuery({
    queryKey: ["chat", "media", message.id],
    queryFn: () => getChatMediaBlob(message.mediaUrl!),
    enabled: Boolean(message.mediaUrl),
    staleTime: Infinity,
  });
  const objectUrl = useMemo(
    () => (mediaQuery.data ? URL.createObjectURL(mediaQuery.data) : null),
    [mediaQuery.data],
  );

  useEffect(
    () => () => {
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    },
    [objectUrl],
  );

  useEffect(() => {
    if (!isOpen) return;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setIsOpen(false);
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen]);

  if (mediaQuery.isLoading) {
    return (
      <div className="flex h-40 w-64 items-center justify-center rounded-xl bg-black/20">
        <LoaderCircle size={20} className="animate-spin text-zinc-500" />
      </div>
    );
  }
  if (mediaQuery.isError) {
    return (
      <button
        type="button"
        onClick={() => void mediaQuery.refetch()}
        className="flex min-h-24 w-64 flex-col items-center justify-center rounded-xl border border-red-400/15 bg-red-500/[0.07] px-4 text-center text-xs text-red-200 transition hover:bg-red-500/[0.12]"
      >
        <span>Media yüklənmədi</span>
        <span className="mt-1 text-red-200/60">
          Yenidən yoxlamaq üçün kliklə
        </span>
      </button>
    );
  }
  if (!objectUrl) return null;

  const isVideo = message.kind === "Video";
  return (
    <>
      <button
        type="button"
        onClick={() => setIsOpen(true)}
        className="group/media relative block max-w-md overflow-hidden rounded-xl bg-black text-left"
        aria-label={isVideo ? "Videonu böyüt" : "Şəkli böyüt"}
      >
        {isVideo ? (
          <video
            src={objectUrl}
            muted
            preload="metadata"
            className="pointer-events-none max-h-80 w-full object-contain"
          />
        ) : (
          <img
            src={objectUrl}
            alt="Mesaj şəkli"
            className="max-h-80 w-full object-cover"
          />
        )}
        <span className="absolute right-2 top-2 flex h-8 w-8 items-center justify-center rounded-lg bg-black/65 text-white opacity-80 transition group-hover/media:opacity-100">
          <Maximize2 size={16} />
        </span>
        {isVideo && (
          <span className="absolute inset-0 flex items-center justify-center bg-black/10 text-sm font-semibold text-white">
            Videonu aç
          </span>
        )}
      </button>

      {isOpen &&
        createPortal(
          <div
            className="fixed inset-0 z-[300] flex items-center justify-center bg-black/80 p-4 backdrop-blur-xl"
            role="dialog"
            aria-modal="true"
            onClick={() => setIsOpen(false)}
          >
            <button
              type="button"
              onClick={() => setIsOpen(false)}
              className="absolute right-5 top-5 flex h-11 w-11 items-center justify-center rounded-2xl border border-white/10 bg-black/55 text-white transition hover:bg-black/80"
              aria-label="Media önizləməsini bağla"
            >
              <X size={22} />
            </button>
            <div
              className="flex max-h-[90vh] max-w-[94vw] items-center justify-center"
              onClick={(event) => event.stopPropagation()}
            >
              {isVideo ? (
                <video
                  src={objectUrl}
                  controls
                  autoPlay
                  className="max-h-[88vh] max-w-[92vw] rounded-2xl bg-black object-contain shadow-2xl"
                />
              ) : (
                <img
                  src={objectUrl}
                  alt="Böyüdülmüş mesaj şəkli"
                  className="max-h-[88vh] max-w-[92vw] rounded-2xl object-contain shadow-2xl"
                />
              )}
            </div>
          </div>,
          document.body,
        )}
    </>
  );
}

function ConversationItem({
  conversation,
  selected,
  onSelect,
}: {
  conversation: ChatConversationDTO;
  selected: boolean;
  onSelect: () => void;
}) {
  const preview = getMessagePreview(conversation.lastMessage);

  return (
    <button
      type="button"
      onClick={onSelect}
      className={`flex w-full items-center gap-3 rounded-2xl px-3 py-3 text-left transition ${
        selected
          ? "bg-violet-500/12 ring-1 ring-violet-400/20"
          : "hover:bg-white/[0.045]"
      }`}
    >
      <UserAvatar
        userName={conversation.otherUser.userName}
        profileImageUrl={conversation.otherUser.profileImageUrl}
      />
      <span className="min-w-0 flex-1">
        <span className="flex items-center justify-between gap-2">
          <strong className="truncate text-sm text-zinc-100">
            {conversation.otherUser.userName}
          </strong>
          <span className="shrink-0 text-[10px] text-zinc-600">
            {formatConversationTime(conversation.updatedAtUtc)}
          </span>
        </span>
        <span className="mt-1 flex items-center justify-between gap-2">
          <span className="truncate text-xs text-zinc-500">{preview}</span>
          {conversation.unreadCount > 0 && (
            <span className="flex h-5 min-w-5 items-center justify-center rounded-full bg-violet-500 px-1.5 text-[10px] font-bold text-white">
              {Math.min(conversation.unreadCount, 99)}
            </span>
          )}
        </span>
      </span>
    </button>
  );
}

export function MessagesPage() {
  const { language } = useI18n();
  const requestConfirmation = useConfirmation();
  const queryClient = useQueryClient();
  const currentUser = useAuthStore((state) => state.user);
  const [searchParams, setSearchParams] = useSearchParams();
  const initialRecipientId = searchParams.get("userId");
  const initialRecipientName = searchParams.get("userName") ?? "İstifadəçi";
  const [selectedConversationChoice, setSelectedConversationId] =
    useState<number | null | undefined>(undefined);
  const [messageText, setMessageText] = useState("");
  const [mediaFile, setMediaFile] = useState<File | null>(null);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);
  const [gamePickerOpen, setGamePickerOpen] = useState(false);
  const [gameSearch, setGameSearch] = useState("");
  const [openMessageMenuId, setOpenMessageMenuId] = useState<number | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<{
    message: ChatMessageDTO;
    scope: ChatMessageDeleteScope;
  } | null>(null);
  const [deleteChoiceTarget, setDeleteChoiceTarget] =
    useState<ChatMessageDTO | null>(null);
  const [pinnedMessageIndex, setPinnedMessageIndex] = useState(0);
  const [focusedPinnedMessageId, setFocusedPinnedMessageId] =
    useState<number | null>(null);
  const [revealedPinnedMessageIds, setRevealedPinnedMessageIds] =
    useState<number[]>([]);

  const selectConversation = (conversationId: number | null) => {
    setOpenMessageMenuId(null);
    setDeleteTarget(null);
    setDeleteChoiceTarget(null);
    setPinnedMessageIndex(0);
    setFocusedPinnedMessageId(null);
    setRevealedPinnedMessageIds([]);
    setSelectedConversationId(conversationId);
  };

  const messageListRef = useRef<HTMLDivElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const pendingPrependHeightRef = useRef<number | null>(null);
  const initialScrolledConversationRef = useRef<number | null>(null);
  const wasNearBottomRef = useRef(true);
  const lastMarkedReadRef = useRef<string | null>(null);

  useChatRealtime(Boolean(currentUser));

  const conversationsQuery = useQuery({
    queryKey: chatQueryKeys.conversations,
    queryFn: getChatConversations,
  });
  const requestsQuery = useQuery({
    queryKey: chatQueryKeys.requests,
    queryFn: getMessageRequests,
  });
  const conversations = useMemo(
    () => conversationsQuery.data ?? [],
    [conversationsQuery.data],
  );
  const requests = useMemo(
    () => requestsQuery.data ?? [],
    [requestsQuery.data],
  );
  const defaultConversationId =
    conversations.find((item) => item.otherUser.id === initialRecipientId)?.id ??
    (!initialRecipientId ? conversations[0]?.id : undefined) ??
    null;
  const selectedConversationId =
    selectedConversationChoice === undefined
      ? defaultConversationId
      : selectedConversationChoice;


  const selectedConversation = useMemo(
    () =>
      [...conversations, ...requests].find(
        (item) => item.id === selectedConversationId,
      ) ?? null,
    [conversations, requests, selectedConversationId],
  );
  const recipientId =
    selectedConversation?.otherUser.id ?? initialRecipientId ?? null;
  const recipientName =
    selectedConversation?.otherUser.userName ?? initialRecipientName;
  const isOutgoingRequestAwaitingDecision =
    selectedConversation?.requestStatus === "Pending" &&
    !selectedConversation.isIncomingRequest &&
    selectedConversation.canSendMessage === false;

  const messagesQuery = useInfiniteQuery({
    queryKey: selectedConversationId
      ? chatQueryKeys.messages(selectedConversationId)
      : ["chat", "messages", "new"],
    queryFn: ({ pageParam }) =>
      getChatMessages(selectedConversationId!, pageParam),
    initialPageParam: undefined as number | undefined,
    getNextPageParam: (lastPage) =>
      lastPage.hasMore ? lastPage.nextBeforeMessageId ?? undefined : undefined,
    enabled: typeof selectedConversationId === "number",
  });
  const messages = useMemo(
    () =>
      (messagesQuery.data?.pages ?? [])
        .slice()
        .reverse()
        .flatMap((page) => page.items),
    [messagesQuery.data?.pages],
  );

  const pinnedMessagesQuery = useQuery({
    queryKey: selectedConversationId
      ? chatQueryKeys.pinnedMessages(selectedConversationId)
      : ["chat", "pinned-messages", "none"],
    queryFn: () => getPinnedChatMessages(selectedConversationId!),
    enabled: typeof selectedConversationId === "number",
  });
  const pinnedMessages = useMemo(
    () => pinnedMessagesQuery.data ?? [],
    [pinnedMessagesQuery.data],
  );
  const pinnedMessageToOpen =
    pinnedMessages[pinnedMessageIndex % Math.max(pinnedMessages.length, 1)] ??
    null;

  const displayedMessages = useMemo(() => {
    const loadedMessageIds = new Set(messages.map((message) => message.id));
    const revealedMessages = pinnedMessages.filter(
      (message) =>
        revealedPinnedMessageIds.includes(message.id) &&
        !loadedMessageIds.has(message.id),
    );

    if (revealedMessages.length === 0) return messages;

    return [...messages, ...revealedMessages].sort((first, second) => {
      const dateDifference =
        new Date(first.sentAtUtc).getTime() -
        new Date(second.sentAtUtc).getTime();
      return dateDifference !== 0 ? dateDifference : first.id - second.id;
    });
  }, [messages, pinnedMessages, revealedPinnedMessageIds]);

  const latestIncomingMessageId = useMemo(
    () =>
      [...messages]
        .reverse()
        .find((message) => message.senderUserId !== currentUser?.id)?.id ?? null,
    [currentUser?.id, messages],
  );

  const scrollToPinnedMessage = () => {
    if (!pinnedMessageToOpen || pinnedMessages.length === 0) return;

    const messageId = pinnedMessageToOpen.id;
    setRevealedPinnedMessageIds((current) =>
      current.includes(messageId) ? current : [...current, messageId],
    );
    setFocusedPinnedMessageId(messageId);
    setPinnedMessageIndex(
      (current) => (current + 1) % pinnedMessages.length,
    );

    window.requestAnimationFrame(() => {
      window.requestAnimationFrame(() => {
        document
          .getElementById(`chat-message-${messageId}`)
          ?.scrollIntoView({ behavior: "smooth", block: "center" });
      });
    });
  };

  useEffect(() => {
    if (!focusedPinnedMessageId) return;
    const timeoutId = window.setTimeout(
      () => setFocusedPinnedMessageId(null),
      2_500,
    );
    return () => window.clearTimeout(timeoutId);
  }, [focusedPinnedMessageId]);

  useEffect(() => {
    if (
      !selectedConversationId ||
      !latestIncomingMessageId ||
      (selectedConversation?.unreadCount ?? 0) <= 0
    ) {
      return;
    }

    const marker = `${selectedConversationId}:${latestIncomingMessageId}`;
    if (lastMarkedReadRef.current === marker) return;
    lastMarkedReadRef.current = marker;

    void markConversationRead(selectedConversationId)
      .then(() => {
        queryClient.setQueryData<ChatConversationDTO[]>(
          chatQueryKeys.conversations,
          (current) =>
            current?.map((conversation) =>
              conversation.id === selectedConversationId
                ? { ...conversation, unreadCount: 0 }
                : conversation,
            ) ?? [],
        );
      })
      .catch(() => {
        if (lastMarkedReadRef.current === marker) {
          lastMarkedReadRef.current = null;
        }
      });
  }, [
    latestIncomingMessageId,
    queryClient,
    selectedConversation?.unreadCount,
    selectedConversationId,
  ]);

  useLayoutEffect(() => {
    const element = messageListRef.current;
    if (!element || messagesQuery.isFetchingNextPage) return;

    if (pendingPrependHeightRef.current !== null) {
      const previousHeight = pendingPrependHeightRef.current;
      pendingPrependHeightRef.current = null;
      element.scrollTop += element.scrollHeight - previousHeight;
      return;
    }

    if (initialScrolledConversationRef.current !== selectedConversationId) {
      initialScrolledConversationRef.current = selectedConversationId;
      element.scrollTop = element.scrollHeight;
      wasNearBottomRef.current = true;
      return;
    }

    if (wasNearBottomRef.current) {
      element.scrollTop = element.scrollHeight;
    }
  }, [
    displayedMessages.length,
    messagesQuery.isFetchingNextPage,
    selectedConversationId,
  ]);

  const gameSearchQuery = useQuery({
    queryKey: ["chat", "game-search", gameSearch.trim()],
    queryFn: () => globalSearch(gameSearch.trim(), 12),
    enabled: gamePickerOpen && gameSearch.trim().length >= 2,
    staleTime: 30_000,
  });

  const refreshChat = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: chatQueryKeys.conversations }),
      queryClient.invalidateQueries({ queryKey: chatQueryKeys.requests }),
    ]);
  };

  const sendMutation = useMutation({
    mutationFn: sendChatMessage,
    onSuccess: async (message) => {
      wasNearBottomRef.current = true;
      setMessageText("");
      setMediaFile(null);
      setValidationMessage(null);
      selectConversation(message.conversationId);
      setSearchParams({});
      await refreshChat();
      await queryClient.invalidateQueries({
        queryKey: chatQueryKeys.messages(message.conversationId),
      });
    },
  });
  const acceptMutation = useMutation({
    mutationFn: acceptMessageRequest,
    onSuccess: refreshChat,
  });
  const rejectMutation = useMutation({
    mutationFn: rejectMessageRequest,
    onSuccess: async () => {
      selectConversation(null);
      await refreshChat();
    },
  });

  const deleteMessageMutation = useMutation({
    mutationFn: ({
      messageId,
      scope,
    }: {
      messageId: number;
      scope: ChatMessageDeleteScope;
    }) => deleteChatMessage(messageId, scope),
    onSuccess: async (event) => {
      setDeleteTarget(null);
      setDeleteChoiceTarget(null);
      setOpenMessageMenuId(null);
      await Promise.all([
        refreshChat(),
        queryClient.invalidateQueries({
          queryKey: chatQueryKeys.messages(event.conversationId),
        }),
        queryClient.invalidateQueries({
          queryKey: chatQueryKeys.pinnedMessages(event.conversationId),
        }),
      ]);
    },
  });
  const pinMessageMutation = useMutation({
    mutationFn: ({ messageId, isPinned }: { messageId: number; isPinned: boolean }) =>
      setChatMessagePinned(messageId, isPinned),
    onSuccess: async (event) => {
      setOpenMessageMenuId(null);
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: chatQueryKeys.messages(event.conversationId),
        }),
        queryClient.invalidateQueries({
          queryKey: chatQueryKeys.pinnedMessages(event.conversationId),
        }),
      ]);
    },
  });

  function handleMediaChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0] ?? null;
    event.target.value = "";
    if (!file) return;
    const isImage = file.type.startsWith("image/");
    const isVideo = file.type === "video/mp4" || file.type === "video/webm";
    if (!isImage && !isVideo) {
      setValidationMessage("Yalnız şəkil, MP4 və WEBM faylları göndərilə bilər.");
      return;
    }
    const maxSize = isVideo ? 50 * 1024 * 1024 : 10 * 1024 * 1024;
    if (file.size > maxSize) {
      setValidationMessage(isVideo ? "Video maksimum 50 MB ola bilər." : "Şəkil maksimum 10 MB ola bilər.");
      return;
    }
    setValidationMessage(null);
    setMediaFile(file);
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!recipientId || (!messageText.trim() && !mediaFile)) return;
    sendMutation.mutate({
      recipientUserId: recipientId,
      text: messageText,
      media: mediaFile,
    });
  }

  const mutationError = sendMutation.isError
    ? getApiErrorMessage(sendMutation.error, "Mesaj göndərilmədi.")
    : null;

  return (
    <div className="flex h-[100dvh] flex-col overflow-hidden wellplayd-page-bg text-zinc-100">
      <Navbar />
      <main className="mx-auto flex min-h-0 w-full max-w-7xl flex-1 px-4 py-4 sm:py-5 lg:px-8">
        <div className="grid h-full min-h-0 w-full overflow-hidden rounded-3xl border border-white/[0.08] bg-[#0d0d12] shadow-2xl shadow-black/30 md:grid-cols-[340px_minmax(0,1fr)]">
          <aside
            className={`min-h-0 flex-col overflow-hidden border-r border-white/[0.07] p-3 ${
              selectedConversationId || initialRecipientId ? "hidden md:flex" : "flex"
            }`}
          >
            <div className="flex items-center justify-between px-3 py-3">
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.16em] text-violet-300">WellPlayd chat</p>
                <h1 className="mt-1 text-xl font-bold text-white">Mesajlar</h1>
              </div>
              <MessageCircle size={22} className="text-violet-300" />
            </div>

            {requests.length > 0 && (
              <section className="mb-3 rounded-2xl border border-amber-400/15 bg-amber-500/[0.045] p-2">
                <div className="flex items-center gap-2 px-2 py-2 text-xs font-bold text-amber-200">
                  <ShieldQuestion size={15} />
                  Mesaj istəkləri
                  <span className="ml-auto rounded-full bg-amber-400/15 px-2 py-0.5">{requests.length}</span>
                </div>
                {requests.map((request) => (
                  <ConversationItem
                    key={request.id}
                    conversation={request}
                    selected={selectedConversationId === request.id}
                    onSelect={() => selectConversation(request.id)}
                  />
                ))}
              </section>
            )}

            <div className="min-h-0 flex-1 space-y-1 overflow-y-auto">
              {conversations.map((conversation) => (
                <ConversationItem
                  key={conversation.id}
                  conversation={conversation}
                  selected={selectedConversationId === conversation.id}
                  onSelect={() => selectConversation(conversation.id)}
                />
              ))}
              {!conversationsQuery.isLoading && conversations.length === 0 && (
                <p className="px-4 py-12 text-center text-sm leading-6 text-zinc-600">
                  Hələ söhbətin yoxdur. Profil menyusundan “Mesaj yaz” seçərək başla.
                </p>
              )}
            </div>
          </aside>

          <section
            className={`min-h-0 min-w-0 flex-col overflow-hidden ${
              selectedConversationId || initialRecipientId ? "flex" : "hidden md:flex"
            }`}
          >
            {recipientId ? (
              <>
                <header className="flex shrink-0 items-center gap-3 border-b border-white/[0.07] px-4 py-3 sm:px-5">
                  <button
                    type="button"
                    onClick={() => {
                      selectConversation(null);
                      setSearchParams({});
                    }}
                    className="flex h-9 w-9 items-center justify-center rounded-xl text-zinc-400 hover:bg-white/[0.06] md:hidden"
                    aria-label="Söhbətlərə qayıt"
                  >
                    <ChevronLeft size={20} />
                  </button>
                  <UserAvatar
                    userName={recipientName}
                    profileImageUrl={selectedConversation?.otherUser.profileImageUrl ?? null}
                    className="h-10 w-10"
                  />
                  <div className="min-w-0">
                    <Link
                      to={`/users/${encodeURIComponent(recipientName)}`}
                      className="truncate text-sm font-bold text-white hover:text-violet-200"
                    >
                      {recipientName}
                    </Link>
                    <p className="mt-0.5 text-[11px] text-zinc-600">
                      {selectedConversation?.requestStatus === "Pending"
                        ? "Mesaj istəyi"
                        : "Real-vaxtlı söhbət"}
                    </p>
                  </div>
                </header>

                {pinnedMessageToOpen && (
                  <button
                    type="button"
                    onClick={scrollToPinnedMessage}
                    className="flex shrink-0 items-center gap-3 border-b border-white/[0.07] bg-violet-500/[0.06] px-4 py-2.5 text-left transition hover:bg-violet-500/[0.1] sm:px-5"
                    aria-label={
                      language === "en"
                        ? "Go to the next pinned message"
                        : "Növbəti pinlənmiş mesaja get"
                    }
                  >
                    <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-violet-500/15 text-violet-300">
                      <Pin size={15} />
                    </span>
                    <span className="min-w-0 flex-1">
                      <strong className="block text-xs text-violet-200">
                        {pinnedMessages.length === 1
                          ? language === "en"
                            ? "Pinned message"
                            : "Pinlənmiş mesaj"
                          : language === "en"
                            ? `${pinnedMessages.length} pinned messages`
                            : `${pinnedMessages.length} pinlənmiş mesaj`}
                      </strong>
                      <span className="mt-0.5 block truncate text-xs text-zinc-500">
                        {getMessagePreview(pinnedMessageToOpen)}
                      </span>
                    </span>
                  </button>
                )}

                <div
                  ref={messageListRef}
                  onScroll={(event) => {
                    const element = event.currentTarget;
                    wasNearBottomRef.current =
                      element.scrollHeight -
                        element.scrollTop -
                        element.clientHeight <
                      120;

                    if (
                      element.scrollTop < 80 &&
                      messagesQuery.hasNextPage &&
                      !messagesQuery.isFetchingNextPage
                    ) {
                      pendingPrependHeightRef.current = element.scrollHeight;
                      void messagesQuery.fetchNextPage().catch(() => {
                        pendingPrependHeightRef.current = null;
                      });
                    }
                  }}
                  className="min-h-0 flex-1 space-y-2 overflow-y-auto bg-[radial-gradient(circle_at_top,_rgba(124,58,237,0.07),_transparent_45%)] px-4 py-5 sm:px-6"
                >
                  {messagesQuery.isFetchingNextPage && (
                    <LoaderCircle size={18} className="mx-auto animate-spin text-zinc-600" />
                  )}
                  {displayedMessages.map((message, index) => {
                    const previous = displayedMessages[index - 1];
                    const showDate =
                      !previous ||
                      new Date(previous.sentAtUtc).toDateString() !==
                        new Date(message.sentAtUtc).toDateString();
                    const mine = message.senderUserId === currentUser?.id;
                    const gameDetailUrl =
                      message.gameSource && message.gameId
                        ? `/games/${message.gameSource}/${message.gameId}`
                        : message.gameDetailUrl ?? "/games";
                    const menuOpen = openMessageMenuId === message.id;

                    const messageMenu = (
                      <div className="relative mt-1 shrink-0">
                        <button
                          type="button"
                          onClick={() =>
                            setOpenMessageMenuId((current) =>
                              current === message.id ? null : message.id,
                            )
                          }
                          className="flex h-8 w-8 items-center justify-center rounded-lg text-zinc-600 opacity-70 transition hover:bg-white/[0.06] hover:text-zinc-200 hover:opacity-100"
                          aria-label={
                            language === "en"
                              ? "Message actions"
                              : "Mesaj əməliyyatları"
                          }
                        >
                          <MoreVertical size={16} />
                        </button>
                        {menuOpen && (
                          <div
                            className={`absolute top-9 z-40 w-48 overflow-hidden rounded-2xl border border-white/[0.09] bg-[#18181f] p-1.5 shadow-2xl shadow-black/50 ${
                              mine ? "right-0" : "left-0"
                            }`}
                          >
                            {!message.isDeletedForEveryone && (
                              <button
                                type="button"
                                onClick={() =>
                                  pinMessageMutation.mutate({
                                    messageId: message.id,
                                    isPinned: !message.isPinnedByCurrentUser,
                                  })
                                }
                                className="flex w-full items-center gap-2.5 rounded-xl px-3 py-2.5 text-left text-sm text-zinc-300 transition hover:bg-white/[0.06]"
                              >
                                {message.isPinnedByCurrentUser ? (
                                  <PinOff size={16} />
                                ) : (
                                  <Pin size={16} />
                                )}
                                {message.isPinnedByCurrentUser
                                  ? language === "en"
                                    ? "Unpin"
                                    : "Pindən çıxar"
                                  : language === "en"
                                    ? "Pin message"
                                    : "Mesajı pinlə"}
                              </button>
                            )}
                            <button
                              type="button"
                              onClick={() => {
                                deleteMessageMutation.reset();
                                if (mine) {
                                  setDeleteChoiceTarget(message);
                                } else {
                                  setDeleteTarget({
                                    message,
                                    scope: "ForMe",
                                  });
                                }
                                setOpenMessageMenuId(null);
                              }}
                              className={`flex w-full items-center gap-2.5 rounded-xl px-3 py-2.5 text-left text-sm transition ${
                                mine
                                  ? "text-red-300 hover:bg-red-500/[0.09]"
                                  : "text-zinc-300 hover:bg-white/[0.06]"
                              }`}
                            >
                              <Trash2 size={16} />
                              {mine
                                ? language === "en"
                                  ? "Delete"
                                  : "Sil"
                                : language === "en"
                                  ? "Delete for me"
                                  : "Məndən sil"}
                            </button>
                          </div>
                        )}
                      </div>
                    );

                    return (
                      <div
                        key={message.id}
                        id={`chat-message-${message.id}`}
                        className={`scroll-mt-28 rounded-2xl transition ${
                          focusedPinnedMessageId === message.id
                            ? "bg-violet-500/[0.08] ring-2 ring-violet-400/30"
                            : ""
                        }`}
                      >
                        {showDate && (
                          <div className="my-5 flex items-center gap-3">
                            <span className="h-px flex-1 bg-white/[0.06]" />
                            <span className="rounded-full border border-white/[0.07] bg-[#15151b] px-3 py-1 text-[10px] font-semibold text-zinc-500">
                              {formatDateSeparator(message.sentAtUtc)}
                            </span>
                            <span className="h-px flex-1 bg-white/[0.06]" />
                          </div>
                        )}
                        <div
                          className={`flex items-start gap-1 ${
                            mine ? "justify-end" : "justify-start"
                          }`}
                        >
                          {mine && messageMenu}
                          <article
                            className={`max-w-[85%] rounded-2xl px-3.5 py-2.5 shadow-sm sm:max-w-[70%] ${
                              message.isDeletedForEveryone
                                ? "border border-white/[0.08] bg-white/[0.035] text-zinc-500"
                                : mine
                                  ? "rounded-br-md bg-violet-600 text-white"
                                  : "rounded-bl-md border border-white/[0.07] bg-[#17171d] text-zinc-200"
                            }`}
                          >
                            {message.isDeletedForEveryone ? (
                              <p className="text-sm italic text-zinc-500">
                                Bu mesaj silindi
                              </p>
                            ) : (
                              <>
                                {message.mediaUrl && (
                                  <AuthenticatedMedia message={message} />
                                )}
                                {message.kind === "GameRecommendation" && (
                                  <Link
                                    to={gameDetailUrl}
                                    className="block min-w-56 overflow-hidden rounded-xl bg-black/20"
                                  >
                                    {message.gameCoverUrl ? (
                                      <img
                                        src={
                                          resolveAssetUrl(message.gameCoverUrl) ??
                                          message.gameCoverUrl
                                        }
                                        alt={message.gameName ?? "Oyun"}
                                        className="aspect-[16/9] w-full object-cover"
                                      />
                                    ) : (
                                      <div className="flex aspect-[16/9] items-center justify-center">
                                        <Gamepad2 size={28} />
                                      </div>
                                    )}
                                    <strong className="block px-3 py-2.5 text-sm">
                                      {message.gameName}
                                    </strong>
                                  </Link>
                                )}
                                {message.text && (
                                  <p className="whitespace-pre-wrap break-words text-sm leading-6">
                                    {message.text}
                                  </p>
                                )}
                              </>
                            )}
                            <div
                              className={`mt-1 flex items-center justify-end gap-1 text-[10px] ${
                                mine ? "text-violet-100/70" : "text-zinc-600"
                              }`}
                            >
                              {message.isPinnedByCurrentUser && (
                                <Pin size={11} aria-label="Pinlənib" />
                              )}
                              {formatMessageTime(message.sentAtUtc)}
                              {mine && deliveryIcon(message.deliveryStatus)}
                            </div>
                          </article>
                          {!mine && messageMenu}
                        </div>
                      </div>
                    );
                  })}
                </div>

                {selectedConversation?.isIncomingRequest ? (
                  <div className="shrink-0 border-t border-white/[0.07] bg-[#111116] p-4">
                    <p className="mb-3 text-center text-xs text-zinc-500">
                      Cavab yazmaq üçün mesaj istəyini qəbul et.
                    </p>
                    <div className="flex justify-center gap-3">
                      <button
                        type="button"
                        onClick={async () => {
                          const confirmed =
                            await requestConfirmation({
                              title: `${selectedConversation.otherUser.userName} istifadəçisinin mesaj istəyi rədd edilsin?`,
                              description:
                                "Bu söhbət istəyi bağlanacaq və gözləyən istəklər siyahından çıxacaq.",
                              detail:
                                "İstifadəçi sonradan yeni mesaj istəyi göndərə bilər.",
                              confirmLabel: "İstəyi rədd et",
                              kind: "remove-user",
                              tone: "warning",
                            });

                          if (confirmed) {
                            rejectMutation.mutate(
                              selectedConversation.id,
                            );
                          }
                        }}
                        className="h-10 rounded-xl border border-red-400/15 bg-red-500/[0.07] px-5 text-sm font-semibold text-red-300"
                      >
                        Rədd et
                      </button>
                      <button
                        type="button"
                        onClick={() => acceptMutation.mutate(selectedConversation.id)}
                        className="h-10 rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white"
                      >
                        Qəbul et
                      </button>
                    </div>
                  </div>
                ) : isOutgoingRequestAwaitingDecision ? (
                  <div
                    className="shrink-0 border-t border-amber-400/10 bg-[#111116] px-5 py-5 text-center"
                    role="status"
                  >
                    <ShieldQuestion
                      size={22}
                      className="mx-auto text-amber-300"
                    />
                    <p className="mt-2 text-sm font-semibold text-zinc-200">
                      Mesaj istəyin göndərildi
                    </p>
                    <p className="mx-auto mt-1 max-w-md text-xs leading-5 text-zinc-500">
                      Qarşı tərəf istəyini qəbul edənədək əlavə mesaj,
                      foto, video və ya oyun təklifi göndərə bilməzsən.
                    </p>
                  </div>
                ) : (
                  <form onSubmit={handleSubmit} className="shrink-0 border-t border-white/[0.07] bg-[#111116] p-3 sm:p-4">
                    {(validationMessage || mutationError) && (
                      <p className="mb-2 rounded-xl bg-red-500/10 px-3 py-2 text-xs text-red-200">
                        {validationMessage ?? mutationError}
                      </p>
                    )}
                    {mediaFile && (
                      <div className="mb-2 flex items-center gap-2 rounded-xl border border-white/[0.07] bg-white/[0.04] px-3 py-2 text-xs text-zinc-400">
                        {mediaFile.type.startsWith("video/") ? <Video size={15} /> : <ImagePlus size={15} />}
                        <span className="min-w-0 flex-1 truncate">{mediaFile.name}</span>
                        <button type="button" onClick={() => setMediaFile(null)}><X size={15} /></button>
                      </div>
                    )}
                    <div className="flex items-end gap-2">
                      <input
                        ref={fileInputRef}
                        type="file"
                        accept="image/jpeg,image/png,image/webp,video/mp4,video/webm"
                        onChange={handleMediaChange}
                        className="sr-only"
                      />
                      <button
                        type="button"
                        onClick={() => fileInputRef.current?.click()}
                        className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl text-zinc-400 transition hover:bg-white/[0.06] hover:text-white"
                        aria-label="Foto və ya video əlavə et"
                      >
                        <ImagePlus size={20} />
                      </button>
                      <button
                        type="button"
                        onClick={() => setGamePickerOpen(true)}
                        className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl text-zinc-400 transition hover:bg-white/[0.06] hover:text-violet-200"
                        aria-label="Oyun təklif et"
                      >
                        <Gamepad2 size={20} />
                      </button>
                      <textarea
                        value={messageText}
                        onChange={(event) => setMessageText(event.target.value)}
                        onKeyDown={(event) => {
                          if (event.key === "Enter" && !event.shiftKey) {
                            event.preventDefault();
                            event.currentTarget.form?.requestSubmit();
                          }
                        }}
                        rows={1}
                        maxLength={4000}
                        placeholder={
                          selectedConversation?.requestStatus === "Pending"
                            ? "Bir mesaj istəyi yaza bilərsən..."
                            : "Mesaj yaz..."
                        }
                        disabled={selectedConversation?.canSendMessage === false || sendMutation.isPending}
                        className="max-h-32 min-h-11 flex-1 resize-none rounded-2xl border border-white/[0.09] bg-white/[0.04] px-4 py-3 text-sm text-zinc-100 outline-none placeholder:text-zinc-700 focus:border-violet-400/30"
                      />
                      <button
                        type="submit"
                        disabled={
                          sendMutation.isPending ||
                          !recipientId ||
                          (!messageText.trim() && !mediaFile) ||
                          selectedConversation?.canSendMessage === false
                        }
                        className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-violet-600 text-white transition hover:bg-violet-500 disabled:opacity-40"
                        aria-label="Mesajı göndər"
                      >
                        {sendMutation.isPending ? <LoaderCircle size={18} className="animate-spin" /> : <Send size={18} />}
                      </button>
                    </div>
                  </form>
                )}
              </>
            ) : (
              <div className="flex flex-1 flex-col items-center justify-center px-6 text-center">
                <MessageCircle size={42} className="text-zinc-700" />
                <h2 className="mt-5 text-xl font-bold text-white">Söhbət seç</h2>
                <p className="mt-2 max-w-sm text-sm leading-6 text-zinc-600">
                  Mesaj tarixçən solda görünəcək. Yeni söhbət üçün istifadəçi profilindən “Mesaj yaz” seç.
                </p>
              </div>
            )}
          </section>
        </div>
      </main>

      {gamePickerOpen && (
        <div className="fixed inset-0 z-[120] flex items-center justify-center bg-black/75 p-4 backdrop-blur-sm">
          <div className="w-full max-w-2xl rounded-3xl border border-white/[0.09] bg-[#111116] p-5 shadow-2xl">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.15em] text-violet-300">Oyun təklif et</p>
                <h2 className="mt-1 text-xl font-bold text-white">Oyunu axtar</h2>
              </div>
              <button type="button" onClick={() => setGamePickerOpen(false)} className="flex h-9 w-9 items-center justify-center rounded-xl text-zinc-500 hover:bg-white/[0.06]">
                <X size={18} />
              </button>
            </div>
            <label className="mt-5 flex h-12 items-center gap-3 rounded-2xl border border-white/[0.09] bg-white/[0.04] px-4">
              <Search size={18} className="text-zinc-600" />
              <input
                autoFocus
                value={gameSearch}
                onChange={(event) => setGameSearch(event.target.value)}
                placeholder="Ən azı 2 simvol yaz..."
                className="min-w-0 flex-1 bg-transparent text-sm text-white outline-none"
              />
            </label>
            <div className="mt-4 max-h-[55vh] space-y-2 overflow-y-auto">
              {gameSearchQuery.data?.games.map((game) => (
                <button
                  key={`${game.source}-${game.id}`}
                  type="button"
                  onClick={() => {
                    if (!recipientId) return;
                    sendMutation.mutate({
                      recipientUserId: recipientId,
                      game: {
                        source: game.source,
                        id: game.id,
                        name: game.title,
                        coverUrl: game.coverImageUrl,
                      },
                    });
                    setGamePickerOpen(false);
                    setGameSearch("");
                  }}
                  className="flex w-full items-center gap-3 rounded-2xl p-3 text-left transition hover:bg-white/[0.05]"
                >
                  <span className="flex h-16 w-12 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-white/[0.04]">
                    {game.coverImageUrl ? (
                      <img src={resolveAssetUrl(game.coverImageUrl) ?? game.coverImageUrl} alt="" className="h-full w-full object-cover" />
                    ) : (
                      <Gamepad2 size={20} className="text-zinc-700" />
                    )}
                  </span>
                  <span>
                    <strong className="block text-sm text-zinc-100">{game.title}</strong>
                    <span className="mt-1 block text-xs text-zinc-600">Kliklə və birbaşa göndər</span>
                  </span>
                </button>
              ))}
              {gameSearchQuery.isLoading && <LoaderCircle size={20} className="mx-auto animate-spin text-zinc-600" />}
            </div>
          </div>
        </div>
      )}

      {deleteChoiceTarget &&
        createPortal(
          <div
            className="fixed inset-0 z-[310] grid place-items-center bg-black/75 p-4 backdrop-blur-md"
            role="dialog"
            aria-modal="true"
            aria-labelledby="delete-message-options-title"
            onMouseDown={(event) => {
              if (
                event.target === event.currentTarget &&
                !deleteMessageMutation.isPending
              ) {
                setDeleteChoiceTarget(null);
              }
            }}
          >
            <section className="relative w-full max-w-sm rounded-[1.75rem] border border-white/[0.09] bg-[#15151c] p-6 shadow-2xl shadow-black/65">
              <button
                type="button"
                onClick={() => setDeleteChoiceTarget(null)}
                disabled={deleteMessageMutation.isPending}
                className="absolute right-4 top-4 flex h-9 w-9 items-center justify-center rounded-xl text-zinc-600 transition hover:bg-white/[0.06] hover:text-white disabled:opacity-40"
                aria-label={language === "en" ? "Close" : "Bağla"}
              >
                <X size={18} />
              </button>

              <span className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-red-500/12 text-red-300">
                <Trash2 size={21} />
              </span>
              <h2
                id="delete-message-options-title"
                className="mt-4 text-center text-xl font-bold text-white"
              >
                {language === "en" ? "Delete message" : "Mesajı sil"}
              </h2>
              <p className="mx-auto mt-2 max-w-xs text-center text-sm leading-6 text-zinc-500">
                {language === "en"
                  ? "Choose who this message should be deleted for."
                  : "Mesajın kim üçün silinəcəyini seç."}
              </p>

              {deleteMessageMutation.isError && (
                <p className="mt-4 rounded-xl bg-red-500/10 px-3 py-2 text-center text-xs text-red-200">
                  {getApiErrorMessage(
                    deleteMessageMutation.error,
                    language === "en"
                      ? "The message could not be deleted."
                      : "Mesaj silinmədi.",
                  )}
                </p>
              )}

              <div className="mt-6 flex flex-col gap-3">
                <button
                  type="button"
                  onClick={() =>
                    deleteMessageMutation.mutate({
                      messageId: deleteChoiceTarget.id,
                      scope: "ForMe",
                    })
                  }
                  disabled={deleteMessageMutation.isPending}
                  className="flex min-h-16 w-full items-center gap-3 rounded-2xl border border-white/[0.08] bg-white/[0.035] px-4 py-3 text-left transition hover:border-white/[0.14] hover:bg-white/[0.065] disabled:opacity-50"
                >
                  <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-white/[0.06] text-zinc-300">
                    {deleteMessageMutation.isPending ? (
                      <LoaderCircle size={18} className="animate-spin" />
                    ) : (
                      <Trash2 size={18} />
                    )}
                  </span>
                  <span>
                    <strong className="block text-sm text-zinc-100">
                      {language === "en"
                        ? "Delete for me"
                        : "Məndən sil"}
                    </strong>
                    <span className="mt-0.5 block text-xs leading-5 text-zinc-600">
                      {language === "en"
                        ? "The other person will still see it."
                        : "Qarşı tərəf mesajı görməyə davam edəcək."}
                    </span>
                  </span>
                </button>

                <button
                  type="button"
                  onClick={() => {
                    setDeleteTarget({
                      message: deleteChoiceTarget,
                      scope: "ForEveryone",
                    });
                    setDeleteChoiceTarget(null);
                    deleteMessageMutation.reset();
                  }}
                  disabled={deleteMessageMutation.isPending}
                  className="flex min-h-16 w-full items-center gap-3 rounded-2xl border border-red-400/15 bg-red-500/[0.07] px-4 py-3 text-left transition hover:border-red-300/25 hover:bg-red-500/[0.12] disabled:opacity-50"
                >
                  <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-red-500/12 text-red-300">
                    <Trash2 size={18} />
                  </span>
                  <span>
                    <strong className="block text-sm text-red-200">
                      {language === "en"
                        ? "Delete for everyone"
                        : "Hər kəsdən sil"}
                    </strong>
                    <span className="mt-0.5 block text-xs leading-5 text-red-200/45">
                      {language === "en"
                        ? "It will be removed from both sides."
                        : "Mesaj söhbətin hər iki tərəfindən silinəcək."}
                    </span>
                  </span>
                </button>
              </div>
            </section>
          </div>,
          document.body,
        )}

      {deleteTarget &&
        createPortal(
          <div
            className="fixed inset-0 z-[315] grid place-items-center bg-black/75 p-4 backdrop-blur-md"
            role="dialog"
            aria-modal="true"
            aria-labelledby="delete-message-confirmation-title"
            onMouseDown={(event) => {
              if (
                event.target === event.currentTarget &&
                !deleteMessageMutation.isPending
              ) {
                setDeleteTarget(null);
              }
            }}
          >
            <section className="w-full max-w-md rounded-[1.9rem] border border-white/[0.09] bg-[#15151c] p-6 text-center shadow-2xl shadow-black/65 sm:p-7">
              <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-red-500/12 text-red-300">
                <Trash2 size={23} />
              </span>
              <h2
                id="delete-message-confirmation-title"
                className="mt-5 text-xl font-bold text-white"
              >
                {deleteTarget.scope === "ForEveryone"
                  ? language === "en"
                    ? "Delete message for everyone?"
                    : "Mesaj hər kəsdən silinsin?"
                  : language === "en"
                    ? "Delete message for you?"
                    : "Mesaj səndən silinsin?"}
              </h2>
              <p className="mx-auto mt-2 max-w-sm text-sm leading-6 text-zinc-500">
                {deleteTarget.scope === "ForEveryone"
                  ? language === "en"
                    ? "The message text and media will be removed from both sides of the conversation. This action cannot be undone."
                    : "Mesajın mətni və mediası söhbətin hər iki tərəfindən silinəcək. Bu əməliyyatı geri qaytarmaq mümkün deyil."
                  : language === "en"
                    ? "The message will disappear only from your chat history. The other person will still be able to see it."
                    : "Mesaj yalnız sənin söhbət tarixçəndən yox olacaq. Qarşı tərəf onu görməyə davam edəcək."}
              </p>
              {deleteMessageMutation.isError && (
                <p className="mt-5 rounded-xl bg-red-500/10 px-3 py-2 text-xs text-red-200">
                  {getApiErrorMessage(
                    deleteMessageMutation.error,
                    language === "en"
                      ? "The message could not be deleted."
                      : "Mesaj silinmədi.",
                  )}
                </p>
              )}
              <div className="mt-7 grid grid-cols-2 gap-3">
                <button
                  type="button"
                  onClick={() => setDeleteTarget(null)}
                  disabled={deleteMessageMutation.isPending}
                  className="h-11 rounded-xl border border-white/[0.09] bg-white/[0.025] px-4 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.06]"
                >
                  {language === "en" ? "Cancel" : "Ləğv et"}
                </button>
                <button
                  type="button"
                  onClick={() =>
                    deleteMessageMutation.mutate({
                      messageId: deleteTarget.message.id,
                      scope: deleteTarget.scope,
                    })
                  }
                  disabled={deleteMessageMutation.isPending}
                  className="flex h-11 items-center justify-center gap-2 rounded-xl bg-red-600 px-4 text-sm font-semibold text-white transition hover:bg-red-500 disabled:opacity-50"
                >
                  {deleteMessageMutation.isPending && (
                    <LoaderCircle size={15} className="animate-spin" />
                  )}
                  {language === "en" ? "Delete" : "Sil"}
                </button>
              </div>
            </section>
          </div>,
          document.body,
        )}

    </div>
  );
}
