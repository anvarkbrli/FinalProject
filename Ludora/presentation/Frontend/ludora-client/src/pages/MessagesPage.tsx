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
  MessageCircle,
  Search,
  Send,
  ShieldQuestion,
  Video,
  X,
} from "lucide-react";
import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
  type FormEvent,
} from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { useChatRealtime } from "../hooks/useChatRealtime";
import { getApiErrorMessage } from "../lib/api";
import { resolveAssetUrl } from "../lib/helpers";
import {
  acceptMessageRequest,
  chatQueryKeys,
  getChatConversations,
  getChatMediaBlob,
  getChatMessages,
  getMessageRequests,
  markConversationRead,
  rejectMessageRequest,
  sendChatMessage,
} from "../services/chatService";
import { globalSearch } from "../services/searchService";
import { useAuthStore } from "../stores/authStore";
import type {
  ChatConversationDTO,
  ChatMessageDTO,
  ChatMessageDeliveryStatus,
} from "../types/chat";

function formatConversationTime(value: string): string {
  const date = new Date(value);
  const today = new Date();
  if (date.toDateString() === today.toDateString()) {
    return new Intl.DateTimeFormat("az-AZ", {
      hour: "2-digit",
      minute: "2-digit",
    }).format(date);
  }
  return new Intl.DateTimeFormat("az-AZ", {
    day: "2-digit",
    month: "short",
  }).format(date);
}

function formatMessageTime(value: string): string {
  return new Intl.DateTimeFormat("az-AZ", {
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

  if (date.toDateString() === today.toDateString()) return "Bu gün";
  if (date.toDateString() === yesterday.toDateString()) return "Dünən";
  return new Intl.DateTimeFormat("az-AZ", {
    day: "numeric",
    month: "long",
    year: date.getFullYear() === today.getFullYear() ? undefined : "numeric",
  }).format(date);
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

  return message.kind === "Video" ? (
    <video
      src={objectUrl}
      controls
      preload="metadata"
      className="max-h-80 w-full max-w-md rounded-xl bg-black object-contain"
    />
  ) : (
    <img
      src={objectUrl}
      alt="Mesaj şəkli"
      className="max-h-80 w-full max-w-md rounded-xl object-cover"
    />
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
  const preview =
    conversation.lastMessage?.kind === "GameRecommendation"
      ? `🎮 ${conversation.lastMessage.gameName ?? "Oyun tövsiyəsi"}`
      : conversation.lastMessage?.kind === "Image"
        ? "📷 Şəkil"
        : conversation.lastMessage?.kind === "Video"
          ? "🎥 Video"
          : conversation.lastMessage?.text ?? "Yeni söhbət";

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
  const messageListRef = useRef<HTMLDivElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

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
    enabled: selectedConversationId !== null,
  });
  const messages = useMemo(
    () =>
      (messagesQuery.data?.pages ?? [])
        .slice()
        .reverse()
        .flatMap((page) => page.items),
    [messagesQuery.data?.pages],
  );

  useEffect(() => {
    if (!selectedConversationId) return;
    void markConversationRead(selectedConversationId).then(() => {
      void queryClient.invalidateQueries({ queryKey: chatQueryKeys.conversations });
    });
  }, [messages.length, queryClient, selectedConversationId]);

  useEffect(() => {
    if (messagesQuery.isFetchingNextPage) return;
    const element = messageListRef.current;
    if (element) element.scrollTop = element.scrollHeight;
  }, [messages.length, messagesQuery.isFetchingNextPage, selectedConversationId]);

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
      setMessageText("");
      setMediaFile(null);
      setValidationMessage(null);
      setSelectedConversationId(message.conversationId);
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
      setSelectedConversationId(null);
      await refreshChat();
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
    <div className="min-h-screen wellplayd-page-bg text-zinc-100">
      <Navbar />
      <main className="mx-auto max-w-7xl px-4 py-6 lg:px-8">
        <div className="grid min-h-[calc(100vh-150px)] overflow-hidden rounded-3xl border border-white/[0.08] bg-[#0d0d12] shadow-2xl shadow-black/30 md:grid-cols-[340px_minmax(0,1fr)]">
          <aside
            className={`border-r border-white/[0.07] p-3 ${
              selectedConversationId || initialRecipientId ? "hidden md:block" : "block"
            }`}
          >
            <div className="flex items-center justify-between px-3 py-3">
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.16em] text-violet-300">Ludora chat</p>
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
                    onSelect={() => setSelectedConversationId(request.id)}
                  />
                ))}
              </section>
            )}

            <div className="max-h-[calc(100vh-260px)] space-y-1 overflow-y-auto">
              {conversations.map((conversation) => (
                <ConversationItem
                  key={conversation.id}
                  conversation={conversation}
                  selected={selectedConversationId === conversation.id}
                  onSelect={() => setSelectedConversationId(conversation.id)}
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
            className={`min-w-0 flex-col ${
              selectedConversationId || initialRecipientId ? "flex" : "hidden md:flex"
            }`}
          >
            {recipientId ? (
              <>
                <header className="flex h-18 items-center gap-3 border-b border-white/[0.07] px-4 py-3 sm:px-5">
                  <button
                    type="button"
                    onClick={() => {
                      setSelectedConversationId(null);
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

                <div
                  ref={messageListRef}
                  onScroll={(event) => {
                    if (
                      event.currentTarget.scrollTop < 80 &&
                      messagesQuery.hasNextPage &&
                      !messagesQuery.isFetchingNextPage
                    ) {
                      void messagesQuery.fetchNextPage();
                    }
                  }}
                  className="min-h-0 flex-1 space-y-2 overflow-y-auto bg-[radial-gradient(circle_at_top,_rgba(124,58,237,0.07),_transparent_45%)] px-4 py-5 sm:px-6"
                >
                  {messagesQuery.isFetchingNextPage && (
                    <LoaderCircle size={18} className="mx-auto animate-spin text-zinc-600" />
                  )}
                  {messages.map((message, index) => {
                    const previous = messages[index - 1];
                    const showDate =
                      !previous ||
                      new Date(previous.sentAtUtc).toDateString() !==
                        new Date(message.sentAtUtc).toDateString();
                    const mine = message.senderUserId === currentUser?.id;
                    const gameDetailUrl =
                      message.gameSource && message.gameId
                        ? `/games/${message.gameSource}/${message.gameId}`
                        : message.gameDetailUrl ?? "/games";

                    return (
                      <div key={message.id}>
                        {showDate && (
                          <div className="my-5 flex items-center gap-3">
                            <span className="h-px flex-1 bg-white/[0.06]" />
                            <span className="rounded-full border border-white/[0.07] bg-[#15151b] px-3 py-1 text-[10px] font-semibold text-zinc-500">
                              {formatDateSeparator(message.sentAtUtc)}
                            </span>
                            <span className="h-px flex-1 bg-white/[0.06]" />
                          </div>
                        )}
                        <div className={`flex ${mine ? "justify-end" : "justify-start"}`}>
                          <article
                            className={`max-w-[85%] rounded-2xl px-3.5 py-2.5 shadow-sm sm:max-w-[70%] ${
                              mine
                                ? "rounded-br-md bg-violet-600 text-white"
                                : "rounded-bl-md border border-white/[0.07] bg-[#17171d] text-zinc-200"
                            }`}
                          >
                            {message.mediaUrl && <AuthenticatedMedia message={message} />}
                            {message.kind === "GameRecommendation" && (
                              <Link
                                to={gameDetailUrl}
                                className="block min-w-56 overflow-hidden rounded-xl bg-black/20"
                              >
                                {message.gameCoverUrl ? (
                                  <img
                                    src={resolveAssetUrl(message.gameCoverUrl) ?? message.gameCoverUrl}
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
                            <div className={`mt-1 flex items-center justify-end gap-1 text-[10px] ${mine ? "text-violet-100/70" : "text-zinc-600"}`}>
                              {formatMessageTime(message.sentAtUtc)}
                              {mine && deliveryIcon(message.deliveryStatus)}
                            </div>
                          </article>
                        </div>
                      </div>
                    );
                  })}
                </div>

                {selectedConversation?.isIncomingRequest ? (
                  <div className="border-t border-white/[0.07] bg-[#111116] p-4">
                    <p className="mb-3 text-center text-xs text-zinc-500">
                      Cavab yazmaq üçün mesaj istəyini qəbul et.
                    </p>
                    <div className="flex justify-center gap-3">
                      <button
                        type="button"
                        onClick={() => rejectMutation.mutate(selectedConversation.id)}
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
                    className="border-t border-amber-400/10 bg-[#111116] px-5 py-5 text-center"
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
                  <form onSubmit={handleSubmit} className="border-t border-white/[0.07] bg-[#111116] p-3 sm:p-4">
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
      <Footer />

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
    </div>
  );
}
