import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  Archive,
  Check,
  CheckCheck,
  ChevronLeft,
  ChevronRight,
  Flame,
  Forward,
  Gamepad2,
  ImagePlus,
  Inbox,
  LoaderCircle,
  Maximize2,
  MessageCircle,
  MoreVertical,
  Pin,
  PinOff,
  Play,
  Plus,
  Reply,
  Search,
  Send,
  ShieldQuestion,
  Trash2,
  Undo2,
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
  type MouseEvent as ReactMouseEvent,
} from "react";
import { createPortal } from "react-dom";
import { Link, useSearchParams } from "react-router-dom";
import { ChatEmojiPicker } from "../components/chat/ChatEmojiPicker";
import { Navbar } from "../components/layout/Navbar";
import { useChatRealtime } from "../hooks/useChatRealtime";
import {
  getCurrentLanguage,
  getCurrentLocale,
  t as translateText,
} from "../i18n/localization";
import { useI18n } from "../i18n/useI18n";
import { getApiErrorMessage } from "../lib/api";
import { useConfirmation } from "../lib/confirmation";
import { resolveAssetUrl } from "../lib/helpers";
import {
  acceptMessageRequest,
  chatQueryKeys,
  deleteChatConversation,
  deleteChatMessage,
  forwardChatMessage,
  getChatConversations,
  getChatMediaBlob,
  getChatMessage,
  getChatMessages,
  getMessageRequests,
  getPinnedChatMessages,
  markConversationRead,
  rejectMessageRequest,
  searchChatMessages,
  sendChatMessage,
  setChatMessagePinned,
  setChatMessageReaction,
  setConversationArchived,
} from "../services/chatService";
import { globalSearch } from "../services/searchService";
import { useAuthStore } from "../stores/authStore";
import type {
  ChatConversationDTO,
  ChatMessageDTO,
  ChatMessageDeleteScope,
  ChatMessageMediaDTO,
  ChatMessageDeliveryStatus,
  ChatReactionType,
  ChatStreakUpdatedDTO,
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

const MAX_CHAT_MEDIA_FILES = 10;
const MAX_CHAT_MEDIA_TOTAL_BYTES = 150 * 1024 * 1024;

const REACTION_OPTIONS: ReadonlyArray<{
  type: ChatReactionType;
  emoji: string;
  labelAz: string;
  labelEn: string;
}> = [
  { type: "Like", emoji: "👍", labelAz: "Bəyən", labelEn: "Like" },
  { type: "Love", emoji: "❤️", labelAz: "Sevgi", labelEn: "Love" },
  { type: "Laugh", emoji: "😂", labelAz: "Gülüş", labelEn: "Laugh" },
  { type: "Surprise", emoji: "😮", labelAz: "Təəccüb", labelEn: "Wow" },
  { type: "Sad", emoji: "😢", labelAz: "Kədər", labelEn: "Sad" },
  { type: "Fire", emoji: "🔥", labelAz: "Alov", labelEn: "Fire" },
];

function formatLocalizedDate(date: Date, includeYear = false): string {
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
  return formatLocalizedDate(date, date.getFullYear() !== today.getFullYear());
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
  return formatLocalizedDate(date, date.getFullYear() !== today.getFullYear());
}

function formatPresenceStatus(
  isOnline: boolean,
  lastSeenAtUtc: string | null,
): string {
  if (isOnline) {
    return translateText("messages:header.online", undefined, "onlayn");
  }
  if (!lastSeenAtUtc) {
    return translateText(
      "messages:header.lastSeenUnknown",
      undefined,
      "son onlayn vaxtı məlum deyil",
    );
  }

  const elapsedMs = Math.max(0, Date.now() - new Date(lastSeenAtUtc).getTime());
  const elapsedMinutes = Math.floor(elapsedMs / 60_000);
  if (elapsedMinutes < 1) {
    return translateText(
      "messages:header.lastSeenJustNow",
      undefined,
      "indicə onlayn idi",
    );
  }
  if (elapsedMinutes < 60) {
    return translateText(
      elapsedMinutes === 1
        ? "messages:header.lastSeenMinute"
        : "messages:header.lastSeenMinutes",
      { count: elapsedMinutes },
      `${elapsedMinutes} dəqiqə əvvəl onlayn idi`,
    );
  }

  const elapsedHours = Math.floor(elapsedMinutes / 60);
  if (elapsedHours < 24) {
    return translateText(
      elapsedHours === 1
        ? "messages:header.lastSeenHour"
        : "messages:header.lastSeenHours",
      { count: elapsedHours },
      `${elapsedHours} saat əvvəl onlayn idi`,
    );
  }

  const elapsedDays = Math.floor(elapsedHours / 24);
  return translateText(
    elapsedDays === 1
      ? "messages:header.lastSeenDay"
      : "messages:header.lastSeenDays",
    { count: elapsedDays },
    `${elapsedDays} gün əvvəl onlayn idi`,
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
  if (!message) return translateText("messages:preview.newConversation", undefined, "Yeni söhbət");
  if (message.isDeletedForEveryone) return translateText("messages:preview.deleted", undefined, "Bu mesaj silindi");
  if (message.kind === "GameRecommendation") {
    return `🎮 ${message.gameName ?? translateText("messages:preview.gameRecommendation", undefined, "Oyun tövsiyəsi")}`;
  }
  if (message.kind === "Image") return `📷 ${translateText("messages:preview.image", undefined, "Şəkil")}`;
  if (message.kind === "Video") return `🎥 ${translateText("messages:preview.video", undefined, "Video")}`;
  return message.text ?? translateText("messages:preview.message", undefined, "Mesaj");
}

function getReplyPreview(message: ChatMessageDTO): string {
  if (message.isDeletedForEveryone) return translateText("messages:preview.deleted", undefined, "Bu mesaj silindi");
  if (message.kind === "GameRecommendation") {
    return `🎮 ${message.gameName ?? translateText("messages:preview.gameRecommendation", undefined, "Oyun tövsiyəsi")}`;
  }
  if (message.kind === "Image") return `📷 ${translateText("messages:preview.image", undefined, "Şəkil")}`;
  if (message.kind === "Video") return `🎥 ${translateText("messages:preview.video", undefined, "Video")}`;
  return message.text ?? translateText("messages:preview.message", undefined, "Mesaj");
}

function getReplyReferencePreview(message: ChatMessageDTO["replyTo"]): string {
  if (!message || message.isDeleted) return translateText("messages:preview.deleted", undefined, "Bu mesaj silindi");
  if (message.kind === "GameRecommendation") {
    return `🎮 ${message.gameName ?? translateText("messages:preview.gameRecommendation", undefined, "Oyun tövsiyəsi")}`;
  }
  if (message.kind === "Image") return `📷 ${translateText("messages:preview.image", undefined, "Şəkil")}`;
  if (message.kind === "Video") return `🎥 ${translateText("messages:preview.video", undefined, "Video")}`;
  return message.text ?? translateText("messages:preview.message", undefined, "Mesaj");
}

function reactionEmoji(type: ChatReactionType): string {
  return REACTION_OPTIONS.find((item) => item.type === type)?.emoji ?? "👍";
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

function getMediaItems(message: ChatMessageDTO): ChatMessageMediaDTO[] {
  if (message.mediaItems?.length) {
    return [...message.mediaItems].sort((left, right) => left.sortOrder - right.sortOrder);
  }

  if (!message.mediaUrl) return [];
  return [
    {
      id: 0,
      url: message.mediaUrl,
      contentType: message.mediaContentType ?? "application/octet-stream",
      fileName: "media",
      length: 0,
      sortOrder: 0,
    },
  ];
}

interface ObjectUrlCacheEntry {
  url: string;
  subscribers: number;
  revokeTimer: number | null;
}

const objectUrlCache = new Map<Blob, ObjectUrlCacheEntry>();

function getOrCreateObjectUrl(source: Blob): string {
  const cached = objectUrlCache.get(source);
  if (cached) {
    return cached.url;
  }

  const url = URL.createObjectURL(source);
  objectUrlCache.set(source, {
    url,
    subscribers: 0,
    revokeTimer: null,
  });
  return url;
}

function useStableObjectUrl(source: Blob | File | null | undefined): string | null {
  const objectUrl = useMemo(
    () => (source ? getOrCreateObjectUrl(source) : null),
    [source],
  );

  useEffect(() => {
    if (!source || !objectUrl) {
      return;
    }

    const entry = objectUrlCache.get(source);
    if (!entry) {
      return;
    }

    if (entry.revokeTimer !== null) {
      window.clearTimeout(entry.revokeTimer);
      entry.revokeTimer = null;
    }
    entry.subscribers += 1;

    return () => {
      entry.subscribers = Math.max(0, entry.subscribers - 1);
      if (entry.subscribers > 0) {
        return;
      }

      entry.revokeTimer = window.setTimeout(() => {
        if (entry.subscribers === 0) {
          URL.revokeObjectURL(entry.url);
          objectUrlCache.delete(source);
        }
      }, 1_000);
    };
  }, [objectUrl, source]);

  return objectUrl;
}

function ChatMediaTile({
  messageId,
  item,
  className,
  onClick,
  remainingCount = 0,
  fit = "cover",
}: {
  messageId: number;
  item: ChatMessageMediaDTO;
  className: string;
  onClick: () => void;
  remainingCount?: number;
  fit?: "cover" | "contain";
}) {
  const { t } = useI18n();
  const mediaQuery = useQuery({
    queryKey: ["chat", "media", messageId, item.id, item.url],
    queryFn: () => getChatMediaBlob(item.url),
    staleTime: Infinity,
  });
  const objectUrl = useStableObjectUrl(mediaQuery.data);

  if (mediaQuery.isLoading || (mediaQuery.data && !objectUrl)) {
    return (
      <div className={`${className} flex items-center justify-center bg-black/25`}>
        <LoaderCircle size={20} className="animate-spin text-zinc-500" />
      </div>
    );
  }

  if (mediaQuery.isError || !objectUrl) {
    return (
      <button
        type="button"
        onClick={() => void mediaQuery.refetch()}
        className={`${className} flex flex-col items-center justify-center border border-red-400/15 bg-red-500/[0.07] px-3 text-center text-xs text-red-200`}
      >
        <span>{t("messages:media.loadFailed", undefined, "Media yüklənmədi")}</span>
        <span className="mt-1 text-red-200/60">
          {t("messages:media.retry", undefined, "Yenidən yoxlamaq üçün kliklə")}
        </span>
      </button>
    );
  }

  const isVideo = (item.contentType ?? "").startsWith("video/");
  return (
    <button
      type="button"
      onClick={onClick}
      className={`${className} group/media relative overflow-hidden bg-black text-left ${fit === "contain" ? "flex min-h-32 items-center justify-center" : ""}`}
      aria-label={
        isVideo
          ? t("messages:media.expandVideo", undefined, "Videonu böyüt")
          : t("messages:media.expandImage", undefined, "Şəkli böyüt")
      }
    >
      {isVideo ? (
        <video
          src={objectUrl}
          muted
          preload="metadata"
          className={
            fit === "contain"
              ? "pointer-events-none block h-auto max-h-[30rem] w-full object-contain"
              : "pointer-events-none h-full w-full object-cover"
          }
        />
      ) : (
        <img
          src={objectUrl}
          alt={t("messages:media.messageImageAlt", undefined, "Mesaj şəkli")}
          className={
            fit === "contain"
              ? "block h-auto max-h-[30rem] w-full object-contain"
              : "h-full w-full object-cover transition duration-300 group-hover/media:scale-[1.02]"
          }
        />
      )}
      {isVideo && (
        <span className="absolute inset-0 flex items-center justify-center bg-black/10">
          <span className="flex h-10 w-10 items-center justify-center rounded-full bg-black/60 text-white backdrop-blur-sm">
            <Play size={18} className="ml-0.5 fill-current" />
          </span>
        </span>
      )}
      {remainingCount > 0 ? (
        <span className="absolute inset-0 flex items-center justify-center bg-black/65 text-3xl font-black text-white backdrop-blur-[2px]">
          +{remainingCount}
        </span>
      ) : (
        <span className="absolute right-2 top-2 flex h-8 w-8 items-center justify-center rounded-lg bg-black/65 text-white opacity-0 transition group-hover/media:opacity-100">
          <Maximize2 size={16} />
        </span>
      )}
    </button>
  );
}

function ChatMediaLightbox({
  messageId,
  items,
  initialIndex,
  onClose,
}: {
  messageId: number;
  items: ChatMessageMediaDTO[];
  initialIndex: number;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const [currentIndex, setCurrentIndex] = useState(initialIndex);
  const safeCurrentIndex = Math.min(
    Math.max(currentIndex, 0),
    Math.max(items.length - 1, 0),
  );
  const currentItem = items[safeCurrentIndex];
  const mediaQuery = useQuery({
    queryKey: [
      "chat",
      "media",
      messageId,
      currentItem?.id ?? 0,
      currentItem?.url ?? "missing",
    ],
    queryFn: () => getChatMediaBlob(currentItem!.url),
    enabled: Boolean(currentItem?.url),
    staleTime: Infinity,
  });
  const objectUrl = useStableObjectUrl(mediaQuery.data);

  useEffect(() => {
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
      if (event.key === "ArrowLeft" && items.length > 1) {
        setCurrentIndex((index) => (index - 1 + items.length) % items.length);
      }
      if (event.key === "ArrowRight" && items.length > 1) {
        setCurrentIndex((index) => (index + 1) % items.length);
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [items.length, onClose]);

  const isVideo = (currentItem?.contentType ?? "").startsWith("video/");
  return createPortal(
    <div
      className="fixed inset-0 z-[400] flex items-center justify-center bg-[#050507]/75 p-4 backdrop-blur-2xl"
      role="dialog"
      aria-modal="true"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <button
        type="button"
        onClick={onClose}
        className="absolute right-5 top-5 flex h-11 w-11 items-center justify-center rounded-2xl border border-white/10 bg-black/55 text-white transition hover:bg-white/10"
        aria-label={t("messages:media.closePreview", undefined, "Media önizləməsini bağla")}
      >
        <X size={22} />
      </button>

      {items.length > 1 && (
        <>
          <button
            type="button"
            onClick={() => setCurrentIndex((index) => (index - 1 + items.length) % items.length)}
            className="absolute left-4 top-1/2 flex h-12 w-12 -translate-y-1/2 items-center justify-center rounded-2xl border border-white/10 bg-black/50 text-white transition hover:bg-white/10 sm:left-6"
            aria-label={t("messages:media.previous", undefined, "Əvvəlki media")}
          >
            <ChevronLeft size={24} />
          </button>
          <button
            type="button"
            onClick={() => setCurrentIndex((index) => (index + 1) % items.length)}
            className="absolute right-4 top-1/2 flex h-12 w-12 -translate-y-1/2 items-center justify-center rounded-2xl border border-white/10 bg-black/50 text-white transition hover:bg-white/10 sm:right-6"
            aria-label={t("messages:media.next", undefined, "Növbəti media")}
          >
            <ChevronRight size={24} />
          </button>
        </>
      )}

      <div className="flex max-h-[92vh] max-w-[92vw] flex-col items-center gap-3" onMouseDown={(event) => event.stopPropagation()}>
        {mediaQuery.isLoading || (mediaQuery.data && !objectUrl) ? (
          <div className="flex h-64 w-64 items-center justify-center">
            <LoaderCircle size={28} className="animate-spin text-zinc-400" />
          </div>
        ) : mediaQuery.isError || !objectUrl ? (
          <button
            type="button"
            onClick={() => void mediaQuery.refetch()}
            className="rounded-2xl border border-red-400/20 bg-red-500/10 px-6 py-5 text-sm text-red-200"
          >
            {t("messages:media.retry", undefined, "Yenidən yoxlamaq üçün kliklə")}
          </button>
        ) : isVideo ? (
          <video
            key={objectUrl}
            src={objectUrl}
            controls
            autoPlay
            className="max-h-[84vh] max-w-[88vw] rounded-2xl bg-black object-contain shadow-2xl"
          />
        ) : (
          <img
            key={objectUrl}
            src={objectUrl}
            alt={t("messages:media.expandedImageAlt", undefined, "Böyüdülmüş mesaj şəkli")}
            className="max-h-[84vh] max-w-[88vw] rounded-2xl object-contain shadow-2xl"
          />
        )}
        {items.length > 1 && (
          <span className="rounded-full bg-black/55 px-3 py-1 text-xs font-bold text-zinc-200">
            {safeCurrentIndex + 1} / {items.length}
          </span>
        )}
      </div>
    </div>,
    document.body,
  );
}

function AuthenticatedMedia({ message }: { message: ChatMessageDTO }) {
  const items = getMediaItems(message);
  const [expanded, setExpanded] = useState(false);
  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null);

  if (items.length === 0) return null;

  const collapsedItems = items.slice(0, 3);
  const remainingCount = Math.max(0, items.length - 3);

  return (
    <>
      {expanded ? (
        <div className="w-full max-w-md space-y-2">
          {items.map((item, index) => (
            <ChatMediaTile
              key={item.id || `${message.id}-${index}`}
              messageId={message.id}
              item={item}
              className="block min-h-32 w-full rounded-xl"
              fit="contain"
              onClick={() => setLightboxIndex(index)}
            />
          ))}
          {items.length > 3 && (
            <button
              type="button"
              onClick={() => setExpanded(false)}
              className="w-full rounded-xl border border-white/[0.08] bg-white/[0.035] px-3 py-2 text-xs font-bold text-zinc-400 transition hover:bg-white/[0.07] hover:text-white"
            >
              {translateText("messages:media.collapse", undefined, "Kolleksiyanı yığ")}
            </button>
          )}
        </div>
      ) : items.length === 1 ? (
        <ChatMediaTile
          messageId={message.id}
          item={items[0]}
          className="block min-h-32 w-full max-w-md rounded-xl"
          fit="contain"
          onClick={() => setLightboxIndex(0)}
        />
      ) : items.length === 2 ? (
        <div className="grid h-64 w-full max-w-md grid-cols-2 gap-1 overflow-hidden rounded-xl">
          {collapsedItems.map((item, index) => (
            <ChatMediaTile
              key={item.id || `${message.id}-${index}`}
              messageId={message.id}
              item={item}
              className="h-full min-w-0"
              onClick={() => setLightboxIndex(index)}
            />
          ))}
        </div>
      ) : (
        <div className="grid h-72 w-full max-w-md grid-cols-2 grid-rows-2 gap-1 overflow-hidden rounded-xl">
          {collapsedItems.map((item, index) => (
            <ChatMediaTile
              key={item.id || `${message.id}-${index}`}
              messageId={message.id}
              item={item}
              className={index === 0 ? "row-span-2 h-full min-w-0" : "h-full min-w-0"}
              remainingCount={index === 2 ? remainingCount : 0}
              onClick={() => {
                if (index === 2 && remainingCount > 0) {
                  setExpanded(true);
                } else {
                  setLightboxIndex(index);
                }
              }}
            />
          ))}
        </div>
      )}

      {lightboxIndex !== null && (
        <ChatMediaLightbox
          messageId={message.id}
          items={items}
          initialIndex={lightboxIndex}
          onClose={() => setLightboxIndex(null)}
        />
      )}
    </>
  );
}

function LocalFilePreview({
  file,
  className,
  loadingClassName,
  controls = false,
  muted = false,
  alt = "",
}: {
  file: File;
  className: string;
  loadingClassName: string;
  controls?: boolean;
  muted?: boolean;
  alt?: string;
}) {
  const previewUrl = useStableObjectUrl(file);

  if (!previewUrl) {
    return (
      <span className={`${loadingClassName} flex items-center justify-center`}>
        <LoaderCircle size={18} className="animate-spin text-zinc-500" />
      </span>
    );
  }

  return (file.type ?? "").startsWith("video/") ? (
    <video
      key={previewUrl}
      src={previewUrl}
      controls={controls}
      muted={muted}
      preload="auto"
      className={className}
    />
  ) : (
    <img
      key={previewUrl}
      src={previewUrl}
      alt={alt}
      className={className}
    />
  );
}

function MediaComposerPreviewModal({
  files,
  selectedIndex,
  messageText,
  isSending,
  onSelect,
  onMessageChange,
  onRemove,
  onAdd,
  onClose,
  onSend,
}: {
  files: File[];
  selectedIndex: number;
  messageText: string;
  isSending: boolean;
  onSelect: (index: number) => void;
  onMessageChange: (value: string) => void;
  onRemove: (index: number) => void;
  onAdd: () => void;
  onClose: () => void;
  onSend: () => void;
}) {
  const { t } = useI18n();

  useEffect(() => {
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [onClose]);

  if (files.length === 0) return null;
  const safeIndex = Math.min(selectedIndex, files.length - 1);
  const selectedFile = files[safeIndex];
  const isVideo = (selectedFile.type ?? "").startsWith("video/");

  return createPortal(
    <div
      className="fixed inset-0 z-[350] grid place-items-center bg-[#050507]/75 p-3 text-zinc-100 backdrop-blur-xl sm:p-6"
      role="dialog"
      aria-modal="true"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <section
        className="flex max-h-[min(680px,calc(100vh-2rem))] w-full max-w-3xl flex-col overflow-hidden rounded-[24px] border border-white/[0.09] bg-[#0f1015] shadow-[0_30px_100px_rgba(0,0,0,0.72)]"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <header className="flex h-14 shrink-0 items-center justify-between border-b border-white/[0.07] px-3 sm:px-4">
          <button
            type="button"
            onClick={onClose}
            className="flex h-9 w-9 items-center justify-center rounded-xl text-zinc-400 transition hover:bg-white/[0.06] hover:text-white"
            aria-label={t("messages:media.closePreview", undefined, "Media önizləməsini bağla")}
          >
            <X size={20} />
          </button>
          <p className="text-xs font-black text-zinc-300 sm:text-sm">
            {translateText("messages:composer.selectedMedia", { count: files.length }, `${files.length} media seçilib`)}
          </p>
          <button
            type="button"
            onClick={() => onRemove(safeIndex)}
            className="flex h-9 w-9 items-center justify-center rounded-xl text-red-300 transition hover:bg-red-500/10 hover:text-red-200"
            aria-label={translateText("messages:composer.removeSelected", undefined, "Seçilmiş medianı sil")}
          >
            <Trash2 size={18} />
          </button>
        </header>

        <main className="flex min-h-0 flex-1 items-center justify-center overflow-hidden bg-black/20 p-3 sm:p-5">
          <LocalFilePreview
            file={selectedFile}
            controls={isVideo}
            alt={selectedFile.name}
            className="block h-auto max-h-[46vh] max-w-full rounded-2xl bg-black object-contain shadow-2xl"
            loadingClassName="h-44 w-44 rounded-2xl border border-white/[0.07] bg-black/20"
          />
        </main>

        <div className="shrink-0 border-t border-white/[0.07] bg-[#111218] px-3 py-3 sm:px-4">
          <div className="flex items-center gap-2">
            <textarea
              value={messageText}
              onChange={(event) => onMessageChange(event.target.value)}
              rows={1}
              maxLength={4000}
              placeholder={t("messages:composer.messagePlaceholder", undefined, "Mesaj yaz...")}
              className="max-h-24 min-h-11 flex-1 resize-none rounded-2xl border border-white/[0.09] bg-white/[0.045] px-4 py-3 text-sm text-zinc-100 outline-none placeholder:text-zinc-600 focus:border-violet-400/30"
            />
            <button
              type="button"
              onClick={onSend}
              disabled={isSending}
              className="relative flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-violet-600 text-white shadow-lg shadow-violet-950/30 transition hover:bg-violet-500 disabled:opacity-45"
              aria-label={t("messages:composer.send", undefined, "Mesajı göndər")}
            >
              {isSending ? <LoaderCircle size={19} className="animate-spin" /> : <Send size={20} />}
              <span className="absolute -right-1 -top-1 flex h-5 min-w-5 items-center justify-center rounded-full border-2 border-[#111218] bg-white px-1 text-[10px] font-black text-violet-700">
                {files.length}
              </span>
            </button>
          </div>

          <div className="mt-2.5 flex items-center gap-2 overflow-x-auto pb-0.5">
            {files.map((file, index) => (
              <button
                key={`${file.name}-${file.size}-${file.lastModified}-${index}`}
                type="button"
                onClick={() => onSelect(index)}
                className={`relative flex h-14 w-14 shrink-0 items-center justify-center overflow-hidden rounded-xl border-2 bg-black/30 transition ${
                  index === safeIndex ? "border-violet-400" : "border-transparent opacity-70 hover:opacity-100"
                }`}
              >
                <LocalFilePreview
                  file={file}
                  muted
                  className="h-full w-full object-cover"
                  loadingClassName="h-full w-full bg-black/30"
                />
                {(file.type ?? "").startsWith("video/") && (
                  <span className="absolute inset-0 flex items-center justify-center bg-black/20 text-white">
                    <Play size={14} className="fill-current" />
                  </span>
                )}
              </button>
            ))}
            <button
              type="button"
              onClick={onAdd}
              className="flex h-14 w-14 shrink-0 items-center justify-center rounded-xl border border-dashed border-white/20 text-zinc-400 transition hover:border-violet-400/50 hover:bg-violet-500/10 hover:text-violet-200"
              aria-label={t("messages:composer.addMedia", undefined, "Foto və ya video əlavə et")}
            >
              <Plus size={22} />
            </button>
          </div>
        </div>
      </section>
    </div>,
    document.body,
  );
}


function ConversationItem({
  conversation,
  selected,
  onSelect,
  onContextMenu,
}: {
  conversation: ChatConversationDTO;
  selected: boolean;
  onSelect: () => void;
  onContextMenu?: (event: ReactMouseEvent<HTMLDivElement>) => void;
}) {
  const preview = getMessagePreview(conversation.lastMessage);

  return (
    <div
      className="group/conversation relative"
      onContextMenu={onContextMenu}
    >
      <button
        type="button"
        onClick={onSelect}
        className={`chat-conversation-row flex w-full items-center gap-3 rounded-2xl px-3 py-3 text-left transition ${
          selected ? "is-selected" : ""
        }`}
      >
        <span className="relative shrink-0">
          <UserAvatar
            userName={conversation.otherUser.userName}
            profileImageUrl={conversation.otherUser.profileImageUrl}
          />
          {conversation.otherUser.isOnline && (
            <span
              className="chat-presence-indicator absolute -bottom-0.5 -right-0.5 h-3.5 w-3.5 rounded-full border-2"
              aria-label={translateText("messages:header.online", undefined, "onlayn")}
            />
          )}
        </span>
        <span className="min-w-0 flex-1">
          <span className="flex items-center justify-between gap-2">
            <strong className="chat-conversation-name truncate text-sm">
              {conversation.otherUser.userName}
            </strong>
            <span className="flex shrink-0 items-center gap-1.5">
              {conversation.streak.currentCount > 0 && (
                <span className="chat-conversation-streak flex items-center gap-0.5 rounded-full px-1.5 py-0.5 text-[10px] font-bold">
                  <Flame size={11} fill="currentColor" />
                  {conversation.streak.currentCount}
                </span>
              )}
              <span className="chat-conversation-time text-[10px]">
                {formatConversationTime(conversation.updatedAtUtc)}
              </span>
            </span>
          </span>
          <span className="mt-1 flex items-center justify-between gap-2">
            <span className="chat-conversation-preview truncate text-xs">{preview}</span>
            {conversation.unreadCount > 0 && (
              <span className="flex h-5 min-w-5 items-center justify-center rounded-full bg-violet-500 px-1.5 text-[10px] font-bold text-white">
                {Math.min(conversation.unreadCount, 99)}
              </span>
            )}
          </span>
        </span>
      </button>

    </div>
  );
}

function StreakCelebration({
  event,
  otherUserName,
}: {
  event: ChatStreakUpdatedDTO;
  otherUserName: string;
}) {
  const { t } = useI18n();
  const longestKey =
    event.streak.longestCount === 1
      ? "messages:streak.longestOne"
      : "messages:streak.longestMany";

  return (
    <div className="pointer-events-none absolute inset-0 z-[90] grid place-items-center overflow-hidden bg-black/35 backdrop-blur-[2px]">
      <div className="chat-streak-celebration relative flex w-[min(90%,360px)] flex-col items-center rounded-[2rem] border border-orange-300/25 bg-[#171116]/95 px-8 py-9 text-center shadow-2xl shadow-orange-950/50">
        <span className="chat-streak-fire flex h-24 w-24 items-center justify-center rounded-full bg-gradient-to-b from-amber-300/25 to-orange-600/10 text-orange-300">
          <Flame size={58} fill="currentColor" />
        </span>
        <strong className="mt-5 text-4xl font-black text-white">
          {t(
            "messages:streak.celebrationTitle",
            { count: event.streak.currentCount },
            `${event.streak.currentCount} günlük streak!`,
          )}
        </strong>
        <p className="mt-2 text-sm leading-6 text-zinc-400">
          {t(
            "messages:streak.celebrationDescription",
            { userName: otherUserName },
            `Sən və ${otherUserName} bu gün də qarşılıqlı mesajlaşdınız.`,
          )}
        </p>
        <div className="mt-5 flex items-center gap-2 rounded-full border border-orange-300/15 bg-orange-500/10 px-4 py-2 text-sm font-bold text-orange-200">
          <Flame size={17} fill="currentColor" />
          {t(
            longestKey,
            { count: event.streak.longestCount },
            `Ən uzun: ${event.streak.longestCount} gün`,
          )}
        </div>
      </div>
    </div>
  );
}

export function MessagesPage() {
  const { language, t } = useI18n();
  const requestConfirmation = useConfirmation();
  const queryClient = useQueryClient();
  const currentUser = useAuthStore((state) => state.user);
  const [searchParams, setSearchParams] = useSearchParams();
  const initialRecipientId = searchParams.get("userId");
  const initialRecipientName =
    searchParams.get("userName") ??
    t("messages:userFallback", undefined, "İstifadəçi");

  const [showArchived, setShowArchived] = useState(false);
  const [conversationSearchText, setConversationSearchText] = useState("");
  const [debouncedConversationSearch, setDebouncedConversationSearch] =
    useState("");
  const [, setPresenceClock] = useState(0);
  const [selectedConversationChoice, setSelectedConversationId] =
    useState<number | null | undefined>(undefined);
  const [messageText, setMessageText] = useState("");
  const [mediaFiles, setMediaFiles] = useState<File[]>([]);
  const [mediaPreviewOpen, setMediaPreviewOpen] = useState(false);
  const [mediaPreviewIndex, setMediaPreviewIndex] = useState(0);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);
  const [gamePickerOpen, setGamePickerOpen] = useState(false);
  const [gameSearch, setGameSearch] = useState("");
  const [openMessageMenuId, setOpenMessageMenuId] = useState<number | null>(null);
  const [replyTarget, setReplyTarget] = useState<ChatMessageDTO | null>(null);
  const [deleteChoiceMessage, setDeleteChoiceMessage] =
    useState<ChatMessageDTO | null>(null);
  const [forwardMessage, setForwardMessage] = useState<ChatMessageDTO | null>(null);
  const [forwardSearchText, setForwardSearchText] = useState("");
  const [forwardTargetConversationIds, setForwardTargetConversationIds] =
    useState<number[]>([]);
  const [pinnedMessageIndex, setPinnedMessageIndex] = useState(0);
  const [focusedMessageId, setFocusedMessageId] = useState<number | null>(null);
  const [revealedPinnedMessageIds, setRevealedPinnedMessageIds] = useState<number[]>([]);
  const [revealedSearchMessages, setRevealedSearchMessages] = useState<ChatMessageDTO[]>([]);
  const [messageSearchOpen, setMessageSearchOpen] = useState(false);
  const [messageSearchText, setMessageSearchText] = useState("");
  const [debouncedMessageSearch, setDebouncedMessageSearch] = useState("");
  const [messageSearchPage, setMessageSearchPage] = useState(1);
  const [conversationContextMenu, setConversationContextMenu] = useState<{
    conversationId: number;
    x: number;
    y: number;
  } | null>(null);

  const messageListRef = useRef<HTMLDivElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const messageInputRef = useRef<HTMLTextAreaElement>(null);
  const pendingPrependHeightRef = useRef<number | null>(null);
  const initialScrolledConversationRef = useRef<number | null>(null);
  const wasNearBottomRef = useRef(true);
  const lastMarkedReadRef = useRef<string | null>(null);
  const typingStopTimerRef = useRef<number | null>(null);
  const typingActiveRef = useRef(false);

  const {
    typingByConversation,
    latestStreakEvent,
    joinConversation,
    sendTyping,
  } = useChatRealtime(Boolean(currentUser));

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setDebouncedConversationSearch(conversationSearchText.trim());
    }, 250);
    return () => window.clearTimeout(timeout);
  }, [conversationSearchText]);

  useEffect(() => {
    const interval = window.setInterval(() => {
      setPresenceClock((current) => current + 1);
    }, 60_000);
    return () => window.clearInterval(interval);
  }, []);

  useEffect(() => {
    if (!conversationContextMenu) return;

    const closeMenu = () => setConversationContextMenu(null);
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") closeMenu();
    };

    document.addEventListener("pointerdown", closeMenu);
    document.addEventListener("keydown", handleKeyDown);
    window.addEventListener("resize", closeMenu);
    window.addEventListener("scroll", closeMenu, true);
    return () => {
      document.removeEventListener("pointerdown", closeMenu);
      document.removeEventListener("keydown", handleKeyDown);
      window.removeEventListener("resize", closeMenu);
      window.removeEventListener("scroll", closeMenu, true);
    };
  }, [conversationContextMenu]);

  const activeConversationsQuery = useQuery({
    queryKey: chatQueryKeys.conversations(false, debouncedConversationSearch),
    queryFn: () =>
      getChatConversations(false, debouncedConversationSearch),
  });
  const archivedConversationsQuery = useQuery({
    queryKey: chatQueryKeys.conversations(true, debouncedConversationSearch),
    queryFn: () =>
      getChatConversations(true, debouncedConversationSearch),
  });
  const requestsQuery = useQuery({
    queryKey: chatQueryKeys.requests(debouncedConversationSearch),
    queryFn: () => getMessageRequests(debouncedConversationSearch),
  });

  const activeConversations = useMemo(
    () => activeConversationsQuery.data ?? [],
    [activeConversationsQuery.data],
  );
  const archivedConversations = useMemo(
    () => archivedConversationsQuery.data ?? [],
    [archivedConversationsQuery.data],
  );
  const conversations = showArchived ? archivedConversations : activeConversations;
  const requests = useMemo(() => requestsQuery.data ?? [], [requestsQuery.data]);
  const allConversations = useMemo(
    () => [...activeConversations, ...archivedConversations, ...requests],
    [activeConversations, archivedConversations, requests],
  );
  const forwardCandidates = useMemo(() => {
    const search = forwardSearchText.trim().toLocaleLowerCase();
    const unique = new Map<number, ChatConversationDTO>();
    for (const conversation of allConversations) {
      if (
        conversation.requestStatus !== "Accepted" ||
        conversation.id === forwardMessage?.conversationId
      ) {
        continue;
      }
      if (
        search &&
        !conversation.otherUser.userName.toLocaleLowerCase().includes(search)
      ) {
        continue;
      }
      unique.set(conversation.id, conversation);
    }
    return [...unique.values()].sort((left, right) =>
      left.otherUser.userName.localeCompare(right.otherUser.userName),
    );
  }, [allConversations, forwardMessage?.conversationId, forwardSearchText]);

  const defaultConversationId =
    allConversations.find((item) => item.otherUser.id === initialRecipientId)?.id ??
    (!initialRecipientId ? conversations[0]?.id : undefined) ??
    null;
  const selectedConversationId =
    selectedConversationChoice === undefined
      ? defaultConversationId
      : selectedConversationChoice;
  const selectedConversation = useMemo(
    () =>
      allConversations.find((item) => item.id === selectedConversationId) ?? null,
    [allConversations, selectedConversationId],
  );
  const recipientId = selectedConversation?.otherUser.id ?? initialRecipientId ?? null;
  const recipientName = selectedConversation?.otherUser.userName ?? initialRecipientName;
  const isOutgoingRequestAwaitingDecision =
    selectedConversation?.requestStatus === "Pending" &&
    !selectedConversation.isIncomingRequest &&
    selectedConversation.canSendMessage === false;

  const selectConversation = (conversationId: number | null) => {
    setConversationContextMenu(null);
    setOpenMessageMenuId(null);
    setReplyTarget(null);
    setDeleteChoiceMessage(null);
    setForwardMessage(null);
    setForwardSearchText("");
    setForwardTargetConversationIds([]);
    setPinnedMessageIndex(0);
    setFocusedMessageId(null);
    setRevealedPinnedMessageIds([]);
    setRevealedSearchMessages([]);
    setMessageSearchOpen(false);
    setMessageSearchText("");
    setDebouncedMessageSearch("");
    setMessageSearchPage(1);
    setSelectedConversationId(conversationId);
  };

  const messagesQuery = useInfiniteQuery({
    queryKey: selectedConversationId
      ? chatQueryKeys.messages(selectedConversationId)
      : ["chat", "messages", "new"],
    queryFn: ({ pageParam }) => getChatMessages(selectedConversationId!, pageParam),
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
    pinnedMessages[pinnedMessageIndex % Math.max(pinnedMessages.length, 1)] ?? null;

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setDebouncedMessageSearch(messageSearchText.trim());
      setMessageSearchPage(1);
    }, 350);
    return () => window.clearTimeout(timeout);
  }, [messageSearchText]);

  const messageSearchQuery = useQuery({
    queryKey:
      selectedConversationId && debouncedMessageSearch.length >= 2
        ? chatQueryKeys.messageSearch(
            selectedConversationId,
            debouncedMessageSearch,
            messageSearchPage,
          )
        : ["chat", "message-search", "idle"],
    queryFn: () =>
      searchChatMessages(
        selectedConversationId!,
        debouncedMessageSearch,
        messageSearchPage,
      ),
    enabled:
      messageSearchOpen &&
      typeof selectedConversationId === "number" &&
      debouncedMessageSearch.length >= 2,
  });

  const displayedMessages = useMemo(() => {
    const loadedMessageIds = new Set(messages.map((message) => message.id));
    const revealedPinned = pinnedMessages.filter(
      (message) =>
        revealedPinnedMessageIds.includes(message.id) &&
        !loadedMessageIds.has(message.id),
    );
    const extras = [...revealedPinned, ...revealedSearchMessages].filter(
      (message, index, array) =>
        !loadedMessageIds.has(message.id) &&
        array.findIndex((item) => item.id === message.id) === index,
    );

    if (extras.length === 0) return messages;
    return [...messages, ...extras].sort((first, second) => {
      const difference =
        new Date(first.sentAtUtc).getTime() - new Date(second.sentAtUtc).getTime();
      return difference !== 0 ? difference : first.id - second.id;
    });
  }, [messages, pinnedMessages, revealedPinnedMessageIds, revealedSearchMessages]);

  const latestIncomingMessageId = useMemo(
    () =>
      [...messages]
        .reverse()
        .find((message) => message.senderUserId !== currentUser?.id)?.id ?? null,
    [currentUser?.id, messages],
  );

  const typingEvent = selectedConversationId
    ? typingByConversation[selectedConversationId]
    : undefined;
  const otherUserIsTyping =
    Boolean(typingEvent?.isTyping) && typingEvent?.userId !== currentUser?.id;

  useEffect(() => {
    if (!selectedConversationId) return;
    void joinConversation(selectedConversationId);
  }, [joinConversation, selectedConversationId]);

  useEffect(() => {
    return () => {
      if (typingStopTimerRef.current) window.clearTimeout(typingStopTimerRef.current);
      if (typingActiveRef.current && selectedConversationId) {
        void sendTyping(selectedConversationId, false);
      }
    };
  }, [sendTyping, selectedConversationId]);

  useEffect(() => {
    if (!focusedMessageId) return;
    const timeout = window.setTimeout(() => setFocusedMessageId(null), 2_500);
    return () => window.clearTimeout(timeout);
  }, [focusedMessageId]);

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
          chatQueryKeys.conversations(false),
          (current) =>
            current?.map((conversation) =>
              conversation.id === selectedConversationId
                ? { ...conversation, unreadCount: 0 }
                : conversation,
            ) ?? [],
        );
      })
      .catch(() => {
        if (lastMarkedReadRef.current === marker) lastMarkedReadRef.current = null;
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

    if (wasNearBottomRef.current) element.scrollTop = element.scrollHeight;
  }, [displayedMessages.length, messagesQuery.isFetchingNextPage, selectedConversationId]);

  const gameSearchQuery = useQuery({
    queryKey: ["chat", "game-search", gameSearch.trim()],
    queryFn: () => globalSearch(gameSearch.trim(), 12),
    enabled: gamePickerOpen && gameSearch.trim().length >= 2,
    staleTime: 30_000,
  });

  const refreshChat = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: chatQueryKeys.conversationLists }),
      queryClient.invalidateQueries({ queryKey: chatQueryKeys.requestLists }),
    ]);
  };

  const sendMutation = useMutation({
    mutationFn: sendChatMessage,
    onSuccess: async (result) => {
      const message = result.message;
      wasNearBottomRef.current = true;
      setMessageText("");
      setMediaFiles([]);
      setMediaPreviewOpen(false);
      setMediaPreviewIndex(0);
      setReplyTarget(null);
      setValidationMessage(null);
      setShowArchived(false);
      selectConversation(message.conversationId);
      setSearchParams({});
      if (typingActiveRef.current && selectedConversationId) {
        typingActiveRef.current = false;
        void sendTyping(selectedConversationId, false);
      }
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
  const archiveMutation = useMutation({
    mutationFn: ({ conversationId, archived }: { conversationId: number; archived: boolean }) =>
      setConversationArchived(conversationId, archived),
    onSuccess: async (event) => {
      selectConversation(null);
      setShowArchived(!event.isArchived);
      await refreshChat();
    },
  });
  const deleteConversationMutation = useMutation({
    mutationFn: deleteChatConversation,
    onSuccess: async (event) => {
      setConversationContextMenu(null);
      if (selectedConversationId === event.conversationId) {
        selectConversation(null);
        setSearchParams({});
      }
      queryClient.removeQueries({
        queryKey: chatQueryKeys.messages(event.conversationId),
      });
      queryClient.removeQueries({
        queryKey: chatQueryKeys.pinnedMessages(event.conversationId),
      });
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
  const reactionMutation = useMutation({
    mutationFn: ({
      messageId,
      reactionType,
    }: {
      messageId: number;
      reactionType: ChatReactionType | null;
    }) => setChatMessageReaction(messageId, reactionType),
    onSuccess: async (event) => {
      setOpenMessageMenuId(null);
      await queryClient.invalidateQueries({
        queryKey: chatQueryKeys.messages(event.conversationId),
      });
    },
  });
  const forwardMessageMutation = useMutation({
    mutationFn: ({
      messageId,
      targetConversationIds,
    }: {
      messageId: number;
      targetConversationIds: number[];
    }) => forwardChatMessage(messageId, targetConversationIds),
    onSuccess: async (result) => {
      setOpenMessageMenuId(null);
      setForwardMessage(null);
      setForwardSearchText("");
      setForwardTargetConversationIds([]);
      await Promise.all([
        refreshChat(),
        ...result.items.map((item) =>
          queryClient.invalidateQueries({
            queryKey: chatQueryKeys.messages(item.conversationId),
          }),
        ),
      ]);
    },
  });

  function revealAndScroll(message: ChatMessageDTO) {
    setRevealedSearchMessages((current) =>
      current.some((item) => item.id === message.id) ? current : [...current, message],
    );
    setFocusedMessageId(message.id);
    window.requestAnimationFrame(() => {
      window.requestAnimationFrame(() => {
        document
          .getElementById(`chat-message-${message.id}`)
          ?.scrollIntoView({ behavior: "smooth", block: "center" });
      });
    });
  }

  async function revealMessageById(messageId: number) {
    const loaded = displayedMessages.find((message) => message.id === messageId);
    if (loaded) {
      revealAndScroll(loaded);
      return;
    }
    if (!selectedConversationId) return;

    try {
      const message = await queryClient.fetchQuery({
        queryKey: ["chat", "message", selectedConversationId, messageId],
        queryFn: () => getChatMessage(selectedConversationId, messageId),
        staleTime: 30_000,
      });
      revealAndScroll(message);
    } catch {
      setValidationMessage(
        t(
          "messages:reply.unavailable",
          undefined,
          "Cavab verilən mesaj artıq əlçatan deyil.",
        ),
      );
    }
  }

  function scrollToPinnedMessage() {
    if (!pinnedMessageToOpen || pinnedMessages.length === 0) return;
    setRevealedPinnedMessageIds((current) =>
      current.includes(pinnedMessageToOpen.id)
        ? current
        : [...current, pinnedMessageToOpen.id],
    );
    revealAndScroll(pinnedMessageToOpen);
    setPinnedMessageIndex((current) => (current + 1) % pinnedMessages.length);
  }

  function handleTyping(nextText: string) {
    setMessageText(nextText);
    if (!selectedConversationId || selectedConversation?.canSendMessage === false) return;

    if (nextText.trim() && !typingActiveRef.current) {
      typingActiveRef.current = true;
      void sendTyping(selectedConversationId, true);
    }
    if (!nextText.trim() && typingActiveRef.current) {
      typingActiveRef.current = false;
      void sendTyping(selectedConversationId, false);
    }

    if (typingStopTimerRef.current) window.clearTimeout(typingStopTimerRef.current);
    typingStopTimerRef.current = window.setTimeout(() => {
      if (typingActiveRef.current && selectedConversationId) {
        typingActiveRef.current = false;
        void sendTyping(selectedConversationId, false);
      }
    }, 1_400);
  }

  function handleMediaChange(event: ChangeEvent<HTMLInputElement>) {
    const selectedFiles = Array.from(event.target.files ?? []);
    event.target.value = "";
    if (selectedFiles.length === 0) return;

    const nextFiles = [...mediaFiles, ...selectedFiles];
    if (nextFiles.length > MAX_CHAT_MEDIA_FILES) {
      setValidationMessage(
        t(
          "messages:validation.tooManyMedia",
          { count: MAX_CHAT_MEDIA_FILES },
          `Bir mesajda maksimum ${MAX_CHAT_MEDIA_FILES} media faylı göndərilə bilər.`,
        ),
      );
      return;
    }

    for (const file of selectedFiles) {
      const isImage = ["image/jpeg", "image/png", "image/webp"].includes(file.type);
      const isVideo = file.type === "video/mp4" || file.type === "video/webm";
      if (!isImage && !isVideo) {
        setValidationMessage(
          t(
            "messages:validation.unsupportedMedia",
            undefined,
            "Yalnız şəkil, MP4 və WEBM faylları göndərilə bilər.",
          ),
        );
        return;
      }

      const maxSize = isVideo ? 50 * 1024 * 1024 : 10 * 1024 * 1024;
      if (file.size > maxSize) {
        setValidationMessage(
          isVideo
            ? t(
                "messages:validation.videoTooLarge",
                undefined,
                "Video maksimum 50 MB ola bilər.",
              )
            : t(
                "messages:validation.imageTooLarge",
                undefined,
                "Şəkil maksimum 10 MB ola bilər.",
              ),
        );
        return;
      }
    }

    const totalBytes = nextFiles.reduce((sum, file) => sum + file.size, 0);
    if (totalBytes > MAX_CHAT_MEDIA_TOTAL_BYTES) {
      setValidationMessage(
        t(
          "messages:validation.mediaTotalTooLarge",
          undefined,
          "Media fayllarının ümumi ölçüsü maksimum 150 MB ola bilər.",
        ),
      );
      return;
    }

    setValidationMessage(null);
    setMediaFiles(nextFiles);
    setMediaPreviewIndex(mediaFiles.length);
    setMediaPreviewOpen(true);
  }

  function removeMediaFile(index: number) {
    setMediaFiles((current) => {
      const next = current.filter((_, itemIndex) => itemIndex !== index);
      if (next.length === 0) {
        setMediaPreviewOpen(false);
        setMediaPreviewIndex(0);
      } else {
        setMediaPreviewIndex((currentIndex) =>
          Math.min(currentIndex > index ? currentIndex - 1 : currentIndex, next.length - 1),
        );
      }
      return next;
    });
  }

  function submitCurrentMessage() {
    if (!recipientId || (!messageText.trim() && mediaFiles.length === 0)) return;
    sendMutation.mutate({
      recipientUserId: recipientId,
      text: messageText,
      media: mediaFiles,
      replyToMessageId: replyTarget?.id ?? null,
    });
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    submitCurrentMessage();
  }

  function openForwardDialog(message: ChatMessageDTO) {
    setOpenMessageMenuId(null);
    setForwardMessage(message);
    setForwardSearchText("");
    setForwardTargetConversationIds([]);
  }

  function toggleForwardTarget(conversationId: number) {
    setForwardTargetConversationIds((current) =>
      current.includes(conversationId)
        ? current.filter((id) => id !== conversationId)
        : [...current, conversationId],
    );
  }

  function submitForwardMessage() {
    if (!forwardMessage || forwardTargetConversationIds.length === 0) return;
    forwardMessageMutation.mutate({
      messageId: forwardMessage.id,
      targetConversationIds: forwardTargetConversationIds,
    });
  }

  function openDeleteFlow(message: ChatMessageDTO) {
    setOpenMessageMenuId(null);
    const mine = message.senderUserId === currentUser?.id;
    if (mine && !message.isDeletedForEveryone) {
      setDeleteChoiceMessage(message);
      return;
    }
    void requestDelete(message, "ForMe");
  }

  async function chooseDeleteScope(scope: ChatMessageDeleteScope) {
    const message = deleteChoiceMessage;
    if (!message) return;
    setDeleteChoiceMessage(null);
    await requestDelete(message, scope);
  }

  async function requestDelete(message: ChatMessageDTO, scope: ChatMessageDeleteScope) {
    const mine = message.senderUserId === currentUser?.id;
    const confirmed = await requestConfirmation({
      title:
        scope === "ForEveryone"
          ? t(
              "messages:delete.forEveryoneTitle",
              undefined,
              "Mesaj hər kəsdən silinsin?",
            )
          : t(
              "messages:delete.forMeTitle",
              undefined,
              "Mesaj səndən silinsin?",
            ),
      description:
        scope === "ForEveryone"
          ? t(
              "messages:delete.forEveryoneDescription",
              undefined,
              "Mesaj söhbətin hər iki tərəfindən silinəcək.",
            )
          : t(
              "messages:delete.forMeDescription",
              undefined,
              "Mesaj yalnız sənin söhbət tarixçəndən silinəcək.",
            ),
      detail:
        scope === "ForEveryone" && mine
          ? t(
              "messages:delete.forEveryoneDetail",
              undefined,
              "Bu əməliyyatı geri qaytarmaq mümkün deyil.",
            )
          : t(
              "messages:delete.forMeDetail",
              undefined,
              "Qarşı tərəfin tarixçəsi dəyişməyəcək.",
            ),
      confirmLabel: language === "en" ? "Delete" : "Sil",
      kind: "remove-user",
      tone: "danger",
    });
    if (confirmed) deleteMessageMutation.mutate({ messageId: message.id, scope });
  }

  function openConversationActions(
    event: ReactMouseEvent<HTMLElement>,
    conversationId: number,
  ) {
    event.preventDefault();
    event.stopPropagation();
    const menuWidth = 208;
    const menuHeight = 112;
    const anchorRect = event.currentTarget.getBoundingClientRect();
    const requestedX = event.clientX || anchorRect.right;
    const requestedY = event.clientY || anchorRect.bottom;
    setConversationContextMenu({
      conversationId,
      x: Math.min(
        Math.max(12, requestedX),
        window.innerWidth - menuWidth - 12,
      ),
      y: Math.min(
        Math.max(12, requestedY),
        window.innerHeight - menuHeight - 12,
      ),
    });
  }

  async function requestConversationDelete(conversation: ChatConversationDTO) {
    setConversationContextMenu(null);
    const confirmed = await requestConfirmation({
      title: t(
        "messages:conversationActions.deleteTitle",
        undefined,
        language === "en"
          ? `Delete chat with ${conversation.otherUser.userName}?`
          : `${conversation.otherUser.userName} ilə söhbət silinsin?`,
      ),
      description: t(
        "messages:conversationActions.deleteDescription",
        undefined,
        language === "en"
          ? "This conversation and its current message history will be removed only from your account."
          : "Bu söhbət və hazırkı mesaj tarixçəsi yalnız sənin hesabından silinəcək.",
      ),
      detail: t(
        "messages:conversationActions.deleteDetail",
        undefined,
        language === "en"
          ? "The other person keeps their copy. A future message will open a fresh conversation without restoring your deleted history."
          : "Qarşı tərəfin tarixçəsi qalacaq. Gələcək yeni mesaj silinmiş tarixçəni qaytarmadan söhbəti yenidən açacaq.",
      ),
      confirmLabel: t(
        "messages:conversationActions.deleteConfirm",
        undefined,
        language === "en" ? "Delete chat" : "Söhbəti sil",
      ),
      kind: "delete",
      tone: "danger",
    });

    if (confirmed) deleteConversationMutation.mutate(conversation.id);
  }

  function insertEmoji(emoji: string) {
    const input = messageInputRef.current;
    const selectionStart = input?.selectionStart ?? messageText.length;
    const selectionEnd = input?.selectionEnd ?? selectionStart;
    const nextText = `${messageText.slice(0, selectionStart)}${emoji}${messageText.slice(selectionEnd)}`.slice(0, 4000);
    handleTyping(nextText);

    window.requestAnimationFrame(() => {
      const nextCursor = Math.min(selectionStart + emoji.length, nextText.length);
      messageInputRef.current?.focus();
      messageInputRef.current?.setSelectionRange(nextCursor, nextCursor);
    });
  }

  const contextConversation = conversationContextMenu
    ? allConversations.find(
        (conversation) => conversation.id === conversationContextMenu.conversationId,
      ) ?? null
    : null;

  const mutationError = sendMutation.isError
    ? getApiErrorMessage(
        sendMutation.error,
        t("messages:validation.sendFailed", undefined, "Mesaj göndərilmədi."),
      )
    : null;
  const streakCelebration =
    latestStreakEvent?.wasIncremented &&
    latestStreakEvent.conversationId === selectedConversationId
      ? latestStreakEvent
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
                <p className="text-xs font-bold uppercase tracking-[0.16em] text-violet-300">
                  WellPlayd chat
                </p>
                <h1 className="mt-1 text-xl font-bold text-white">
                  {t("messages:title", undefined, "Mesajlar")}
                </h1>
              </div>
              <MessageCircle size={22} className="text-violet-300" />
            </div>

            <div className="relative mb-3 px-1">
              <Search
                size={15}
                className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600"
              />
              <input
                type="search"
                value={conversationSearchText}
                onChange={(event) => setConversationSearchText(event.target.value)}
                placeholder={t(
                  "messages:conversationSearch.placeholder",
                  undefined,
                  "Söhbətlərdə axtar...",
                )}
                className="h-10 w-full rounded-xl border border-white/[0.07] bg-white/[0.035] pl-9 pr-9 text-sm text-zinc-200 outline-none placeholder:text-zinc-600 focus:border-violet-400/35 focus:bg-white/[0.05]"
              />
              {conversationSearchText && (
                <button
                  type="button"
                  onClick={() => setConversationSearchText("")}
                  className="absolute right-3 top-1/2 flex h-7 w-7 -translate-y-1/2 items-center justify-center rounded-lg text-zinc-600 hover:bg-white/[0.06] hover:text-zinc-300"
                  aria-label={t(
                    "messages:conversationSearch.clear",
                    undefined,
                    "Söhbət axtarışını təmizlə",
                  )}
                >
                  <X size={14} />
                </button>
              )}
            </div>

            <div className="mb-3 grid grid-cols-2 gap-1 rounded-2xl border border-white/[0.06] bg-white/[0.025] p-1">
              <button
                type="button"
                onClick={() => {
                  setShowArchived(false);
                  selectConversation(null);
                }}
                className={`flex h-9 items-center justify-center gap-2 rounded-xl text-xs font-semibold transition ${
                  !showArchived
                    ? "bg-violet-500/15 text-violet-200"
                    : "text-zinc-500 hover:text-zinc-300"
                }`}
              >
                <Inbox size={14} /> {t("messages:tabs.active", undefined, "Aktiv")}
              </button>
              <button
                type="button"
                onClick={() => {
                  setShowArchived(true);
                  selectConversation(null);
                }}
                className={`flex h-9 items-center justify-center gap-2 rounded-xl text-xs font-semibold transition ${
                  showArchived
                    ? "bg-violet-500/15 text-violet-200"
                    : "text-zinc-500 hover:text-zinc-300"
                }`}
              >
                <Archive size={14} />{
                  t(
                    "messages:tabs.archive",
                    { count: archivedConversations.length },
                    `Arxiv (${archivedConversations.length})`,
                  )
                }
              </button>
            </div>

            {!showArchived && requests.length > 0 && (
              <section className="mb-3 rounded-2xl border border-amber-400/15 bg-amber-500/[0.045] p-2">
                <div className="flex items-center gap-2 px-2 py-2 text-xs font-bold text-amber-200">
                  <ShieldQuestion size={15} />
                  {t("messages:requests.title", undefined, "Mesaj istəkləri")}
                  <span className="ml-auto rounded-full bg-amber-400/15 px-2 py-0.5">
                    {requests.length}
                  </span>
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
                  onContextMenu={(event) =>
                    openConversationActions(event, conversation.id)
                  }
                />
              ))}
              {!(showArchived
                ? archivedConversationsQuery.isLoading
                : activeConversationsQuery.isLoading) && conversations.length === 0 && (
                <p className="px-4 py-12 text-center text-sm leading-6 text-zinc-600">
                  {debouncedConversationSearch
                    ? t(
                        "messages:conversationSearch.empty",
                        undefined,
                        "Uyğun söhbət tapılmadı.",
                      )
                    : showArchived
                      ? t(
                          "messages:empty.archive",
                          undefined,
                          "Arxivlənmiş söhbətin yoxdur.",
                        )
                      : t(
                          "messages:empty.conversations",
                          undefined,
                          "Hələ söhbətin yoxdur. Profil menyusundan “Mesaj yaz” seçərək başla.",
                        )}
                </p>
              )}
            </div>
          </aside>

          <section
            className={`relative min-h-0 min-w-0 flex-col overflow-hidden ${
              selectedConversationId || initialRecipientId ? "flex" : "hidden md:flex"
            }`}
          >
            {recipientId ? (
              <>
                <header className="chat-conversation-header relative z-30 flex shrink-0 items-center gap-3 border-b px-4 py-3 sm:px-5">
                  <button
                    type="button"
                    onClick={() => {
                      selectConversation(null);
                      setSearchParams({});
                    }}
                    className="flex h-9 w-9 items-center justify-center rounded-xl text-zinc-400 hover:bg-white/[0.06] md:hidden"
                    aria-label={t(
                      "messages:header.backToConversations",
                      undefined,
                      "Söhbətlərə qayıt",
                    )}
                  >
                    <ChevronLeft size={20} />
                  </button>
                  <span className="relative shrink-0">
                    <UserAvatar
                      userName={recipientName}
                      profileImageUrl={selectedConversation?.otherUser.profileImageUrl ?? null}
                      className="h-10 w-10"
                    />
                    {selectedConversation?.otherUser.isOnline && (
                      <span
                        className="chat-presence-indicator absolute -bottom-0.5 -right-0.5 h-3.5 w-3.5 rounded-full border-2"
                        aria-hidden="true"
                      />
                    )}
                  </span>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <Link
                        to={`/users/${encodeURIComponent(recipientName)}`}
                        className="chat-conversation-title truncate text-sm font-bold transition hover:text-violet-200"
                      >
                        {recipientName}
                      </Link>
                      {(selectedConversation?.streak.currentCount ?? 0) > 0 && (
                        <span
                          className={`chat-conversation-streak flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-extrabold ${
                            selectedConversation?.streak.isCompletedToday
                              ? "is-complete"
                              : ""
                          }`}
                          title={t(
                            (selectedConversation?.streak.longestCount ?? 0) === 1
                              ? "messages:streak.longestTitleOne"
                              : "messages:streak.longestTitleMany",
                            {
                              count: selectedConversation?.streak.longestCount ?? 0,
                            },
                            `Ən uzun streak: ${selectedConversation?.streak.longestCount ?? 0} gün`,
                          )}
                        >
                          <Flame size={13} fill="currentColor" />
                          {selectedConversation?.streak.currentCount}
                        </span>
                      )}
                    </div>
                    <p className="chat-conversation-presence mt-0.5 text-[11px]">
                      {otherUserIsTyping
                        ? t("messages:header.typing", undefined, "yazır...")
                        : selectedConversation?.requestStatus === "Pending"
                          ? t("messages:requests.status", undefined, "Mesaj istəyi")
                          : selectedConversation
                            ? formatPresenceStatus(
                                selectedConversation.otherUser.isOnline,
                                selectedConversation.otherUser.lastSeenAtUtc,
                              )
                            : t(
                                "messages:header.realtime",
                                undefined,
                                "Real-vaxtlı söhbət",
                              )}
                    </p>
                  </div>
                  {selectedConversationId && (
                    <>
                      <button
                        type="button"
                        onClick={() => setMessageSearchOpen((current) => !current)}
                        className={`flex h-9 w-9 items-center justify-center rounded-xl transition ${
                          messageSearchOpen
                            ? "bg-violet-500/15 text-violet-200"
                            : "text-zinc-500 hover:bg-white/[0.06] hover:text-white"
                        }`}
                        aria-label={t(
                          "messages:header.searchMessages",
                          undefined,
                          "Mesajlarda axtar",
                        )}
                      >
                        <Search size={18} />
                      </button>
                      {selectedConversation?.requestStatus === "Accepted" && (
                        <button
                          type="button"
                          onClick={() =>
                            archiveMutation.mutate({
                              conversationId: selectedConversationId,
                              archived: !selectedConversation.isArchived,
                            })
                          }
                          disabled={archiveMutation.isPending}
                          className="flex h-9 w-9 items-center justify-center rounded-xl text-zinc-500 transition hover:bg-white/[0.06] hover:text-white disabled:opacity-50"
                          aria-label={
                            selectedConversation.isArchived
                              ? t("messages:archive.restore", undefined, "Arxivdən çıxar")
                              : t("messages:archive.archive", undefined, "Söhbəti arxivlə")
                          }
                        >
                          {selectedConversation.isArchived ? (
                            <Undo2 size={18} />
                          ) : (
                            <Archive size={18} />
                          )}
                        </button>
                      )}
                    </>
                  )}

                  {messageSearchOpen && selectedConversationId && (
                    <div className="absolute left-3 right-3 top-[calc(100%+8px)] z-50 rounded-2xl border border-white/[0.09] bg-[#15151c] p-3 shadow-2xl shadow-black/60 sm:left-auto sm:right-5 sm:w-[420px]">
                      <label className="flex h-11 items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.035] px-3">
                        <Search size={16} className="text-zinc-600" />
                        <input
                          autoFocus
                          value={messageSearchText}
                          onChange={(event) => setMessageSearchText(event.target.value)}
                          placeholder={t("messages:search.placeholder", undefined, "Mesajlarda axtar...")}
                          className="min-w-0 flex-1 bg-transparent text-sm text-white outline-none placeholder:text-zinc-700"
                        />
                        <button
                          type="button"
                          onClick={() => {
                            setMessageSearchOpen(false);
                            setMessageSearchText("");
                          }}
                          className="text-zinc-600 hover:text-white"
                          aria-label={t("messages:search.close", undefined, "Axtarışı bağla")}
                        >
                          <X size={16} />
                        </button>
                      </label>
                      <div className="mt-2 max-h-80 overflow-y-auto">
                        {debouncedMessageSearch.length < 2 ? (
                          <p className="px-3 py-8 text-center text-xs text-zinc-600">
                            {t(
                              "messages:search.minimumCharacters",
                              undefined,
                              "Ən azı 2 simvol yaz.",
                            )}
                          </p>
                        ) : messageSearchQuery.isLoading ? (
                          <LoaderCircle size={20} className="mx-auto my-8 animate-spin text-zinc-600" />
                        ) : messageSearchQuery.isError ? (
                          <p className="px-3 py-8 text-center text-xs text-red-300">
                            {t(
                              "messages:search.loadFailed",
                              undefined,
                              "Axtarış nəticələri yüklənmədi.",
                            )}
                          </p>
                        ) : messageSearchQuery.data?.items.length ? (
                          <>
                            {messageSearchQuery.data.items.map((message) => (
                              <button
                                key={message.id}
                                type="button"
                                onClick={() => {
                                  revealAndScroll(message);
                                  setMessageSearchOpen(false);
                                }}
                                className="block w-full rounded-xl px-3 py-2.5 text-left transition hover:bg-white/[0.05]"
                              >
                                <span className="block truncate text-xs text-zinc-300">
                                  {getMessagePreview(message)}
                                </span>
                                <span className="mt-1 block text-[10px] text-zinc-600">
                                  {formatLocalizedDate(new Date(message.sentAtUtc), true)} · {formatMessageTime(message.sentAtUtc)}
                                </span>
                              </button>
                            ))}
                            <div className="mt-2 flex items-center justify-between border-t border-white/[0.06] px-2 pt-2">
                              <button
                                type="button"
                                disabled={messageSearchPage <= 1}
                                onClick={() => setMessageSearchPage((page) => Math.max(1, page - 1))}
                                className="rounded-lg px-2 py-1 text-xs text-zinc-500 disabled:opacity-30"
                              >
                                {t("messages:search.previous", undefined, "Əvvəlki")}
                              </button>
                              <span className="text-[10px] text-zinc-600">
                                {t(
                                  messageSearchQuery.data.totalCount === 1
                                    ? "messages:search.resultOne"
                                    : "messages:search.resultMany",
                                  { count: messageSearchQuery.data.totalCount },
                                  `${messageSearchQuery.data.totalCount} nəticə`,
                                )}
                              </span>
                              <button
                                type="button"
                                disabled={!messageSearchQuery.data.hasMore}
                                onClick={() => setMessageSearchPage((page) => page + 1)}
                                className="rounded-lg px-2 py-1 text-xs text-zinc-500 disabled:opacity-30"
                              >
                                {t("messages:search.next", undefined, "Növbəti")}
                              </button>
                            </div>
                          </>
                        ) : (
                          <p className="px-3 py-8 text-center text-xs text-zinc-600">
                            {t("messages:search.empty", undefined, "Uyğun mesaj tapılmadı.")}
                          </p>
                        )}
                      </div>
                    </div>
                  )}
                </header>

                {pinnedMessageToOpen && (
                  <button
                    type="button"
                    onClick={scrollToPinnedMessage}
                    className="flex shrink-0 items-center gap-3 border-b border-white/[0.07] bg-violet-500/[0.06] px-4 py-2.5 text-left transition hover:bg-violet-500/[0.1] sm:px-5"
                  >
                    <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-violet-500/15 text-violet-300">
                      <Pin size={15} />
                    </span>
                    <span className="min-w-0 flex-1">
                      <strong className="block text-xs text-violet-200">
                        {pinnedMessages.length === 1
                          ? t("messages:pinned.single", undefined, "Bərkidilmiş mesaj")
                          : t(
                              "messages:pinned.multiple",
                              { count: pinnedMessages.length },
                              `${pinnedMessages.length} bərkidilmiş mesaj`,
                            )}
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
                      element.scrollHeight - element.scrollTop - element.clientHeight < 120;

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
                    const currentReaction = message.reactions.find(
                      (reaction) => reaction.reactedByCurrentUser,
                    )?.reactionType;

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
                          aria-label={t("messages:actions.menu", undefined, "Mesaj əməliyyatları")}
                        >
                          <MoreVertical size={16} />
                        </button>
                        {menuOpen && (
                          <div
                            className={`absolute top-9 z-40 w-56 overflow-hidden rounded-2xl border border-white/[0.09] bg-[#18181f] p-1.5 shadow-2xl shadow-black/50 ${
                              mine ? "right-0" : "left-0"
                            }`}
                          >
                            {!message.isDeletedForEveryone && (
                              <>
                                <div className="grid grid-cols-6 gap-1 border-b border-white/[0.06] p-1 pb-2">
                                  {REACTION_OPTIONS.map((reaction) => (
                                    <button
                                      key={reaction.type}
                                      type="button"
                                      title={
                                        language === "en"
                                          ? reaction.labelEn
                                          : reaction.labelAz
                                      }
                                      onClick={() =>
                                        reactionMutation.mutate({
                                          messageId: message.id,
                                          reactionType:
                                            currentReaction === reaction.type
                                              ? null
                                              : reaction.type,
                                        })
                                      }
                                      className={`flex h-8 items-center justify-center rounded-lg text-lg transition hover:bg-white/[0.08] ${
                                        currentReaction === reaction.type
                                          ? "bg-violet-500/20 ring-1 ring-violet-400/30"
                                          : ""
                                      }`}
                                    >
                                      {reaction.emoji}
                                    </button>
                                  ))}
                                </div>
                                <button
                                  type="button"
                                  onClick={() => {
                                    setReplyTarget(message);
                                    setOpenMessageMenuId(null);
                                  }}
                                  className="mt-1 flex w-full items-center gap-2.5 rounded-xl px-3 py-2.5 text-left text-sm text-zinc-300 transition hover:bg-white/[0.06]"
                                >
                                  <Reply size={16} /> {t("messages:actions.reply", undefined, "Cavab ver")}
                                </button>
                                <button
                                  type="button"
                                  onClick={() => openForwardDialog(message)}
                                  className="flex w-full items-center gap-2.5 rounded-xl px-3 py-2.5 text-left text-sm text-zinc-300 transition hover:bg-white/[0.06]"
                                >
                                  <Forward size={16} />
                                  {t("messages:actions.forward", undefined, "Yönləndir")}
                                </button>
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
                                    ? t("messages:pinned.unpin", undefined, "Bərkitməni ləğv et")
                                    : t("messages:pinned.pin", undefined, "Mesajı bərkid")}
                                </button>
                              </>
                            )}
                            <button
                              type="button"
                              onClick={() => openDeleteFlow(message)}
                              className="flex w-full items-center gap-2.5 rounded-xl px-3 py-2.5 text-left text-sm text-red-300 transition hover:bg-red-500/[0.09]"
                            >
                              <Trash2 size={16} />
                              {t("messages:actions.delete", undefined, "Sil")}
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
                          focusedMessageId === message.id
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
                          <div className="max-w-[85%] sm:max-w-[70%]">
                            <article
                              className={`chat-message-bubble rounded-2xl px-3.5 py-2.5 shadow-sm ${
                                message.isDeletedForEveryone
                                  ? "chat-message-bubble-deleted border"
                                  : mine
                                    ? "chat-message-bubble-sent rounded-br-md text-white"
                                    : "chat-message-bubble-received rounded-bl-md border"
                              }`}
                            >
                              {message.isForwarded && !message.isDeletedForEveryone && (
                                <div
                                  className={`mb-2 flex items-center gap-1.5 text-[11px] font-semibold ${
                                    mine ? "text-violet-100/75" : "chat-message-muted"
                                  }`}
                                >
                                  <Forward size={12} />
                                  {t(
                                    "messages:forward.forwardedLabel",
                                    undefined,
                                    "Yönləndirildi",
                                  )}
                                </div>
                              )}
                              {message.replyTo && (
                                <button
                                  type="button"
                                  onClick={() =>
                                    void revealMessageById(message.replyTo!.messageId)
                                  }
                                  className={`mb-2 block w-full rounded-xl border-l-2 px-3 py-2 text-left ${
                                    mine
                                      ? "border-violet-100/60 bg-black/15"
                                      : "border-violet-400/60 bg-violet-500/[0.08]"
                                  }`}
                                >
                                  <strong className={`block truncate text-[11px] ${mine ? "text-violet-100" : "chat-message-reply-author"}`}>
                                    {message.replyTo.senderUserName}
                                  </strong>
                                  <span className="mt-0.5 block truncate text-xs opacity-70">
                                    {getReplyReferencePreview(message.replyTo)}
                                  </span>
                                </button>
                              )}
                              {message.isDeletedForEveryone ? (
                                <p className="text-sm italic text-zinc-500">
                                  {t("messages:deleted", undefined, "Bu mesaj silindi")}
                                </p>
                              ) : (
                                <>
                                  {getMediaItems(message).length > 0 && <AuthenticatedMedia message={message} />}
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
                                          alt={
                                            message.gameName ??
                                            t("messages:game.fallbackName", undefined, "Oyun")
                                          }
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
                                    <p
                                      data-user-content
                                      className="whitespace-pre-wrap break-words text-sm leading-6"
                                    >
                                      {message.text}
                                    </p>
                                  )}
                                </>
                              )}
                              <div
                                className={`mt-1 flex items-center justify-end gap-1 text-[10px] ${
                                  mine ? "text-violet-100/70" : "chat-message-time-received"
                                }`}
                              >
                                {message.isPinnedByCurrentUser && <Pin size={11} />}
                                {formatMessageTime(message.sentAtUtc)}
                                {mine && deliveryIcon(message.deliveryStatus)}
                              </div>
                            </article>
                            {message.reactions.length > 0 && (
                              <div
                                className={`mt-1 flex flex-wrap gap-1 ${
                                  mine ? "justify-end" : "justify-start"
                                }`}
                              >
                                {message.reactions.map((reaction) => (
                                  <button
                                    key={reaction.reactionType}
                                    type="button"
                                    onClick={() =>
                                      reactionMutation.mutate({
                                        messageId: message.id,
                                        reactionType: reaction.reactedByCurrentUser
                                          ? null
                                          : reaction.reactionType,
                                      })
                                    }
                                    className={`flex h-7 items-center gap-1 rounded-full border px-2 text-xs transition ${
                                      reaction.reactedByCurrentUser
                                        ? "border-violet-400/30 bg-violet-500/15 text-violet-100"
                                        : "border-white/[0.08] bg-[#17171d] text-zinc-400 hover:bg-white/[0.06]"
                                    }`}
                                  >
                                    <span>{reactionEmoji(reaction.reactionType)}</span>
                                    <span>{reaction.count}</span>
                                  </button>
                                ))}
                              </div>
                            )}
                          </div>
                          {!mine && messageMenu}
                        </div>
                      </div>
                    );
                  })}
                  {otherUserIsTyping && (
                    <div className="flex items-center gap-2 px-1 py-2 text-xs text-zinc-500">
                      <span className="flex gap-1 rounded-full bg-white/[0.05] px-3 py-2">
                        <i className="chat-typing-dot h-1.5 w-1.5 rounded-full bg-violet-300" />
                        <i className="chat-typing-dot h-1.5 w-1.5 rounded-full bg-violet-300" />
                        <i className="chat-typing-dot h-1.5 w-1.5 rounded-full bg-violet-300" />
                      </span>
                      {t(
                        "messages:header.typingWithName",
                        { userName: recipientName },
                        `${recipientName} yazır...`,
                      )}
                    </div>
                  )}
                </div>

                {selectedConversation?.isIncomingRequest ? (
                  <div className="shrink-0 border-t border-white/[0.07] bg-[#111116] p-4">
                    <p className="mb-3 text-center text-xs text-zinc-500">
                      {t(
                        "messages:requests.acceptToReply",
                        undefined,
                        "Cavab yazmaq üçün mesaj istəyini qəbul et.",
                      )}
                    </p>
                    <div className="flex justify-center gap-3">
                      <button
                        type="button"
                        onClick={async () => {
                          const confirmed = await requestConfirmation({
                            title: t(
                              "messages:requests.rejectTitle",
                              { userName: selectedConversation.otherUser.userName },
                              `${selectedConversation.otherUser.userName} istifadəçisinin mesaj istəyi rədd edilsin?`,
                            ),
                            description: t(
                              "messages:requests.rejectDescription",
                              undefined,
                              "Bu söhbət istəyi bağlanacaq.",
                            ),
                            detail: t(
                              "messages:requests.rejectDetail",
                              undefined,
                              "İstifadəçi sonradan yeni mesaj istəyi göndərə bilər.",
                            ),
                            confirmLabel: t(
                              "messages:requests.rejectConfirm",
                              undefined,
                              "İstəyi rədd et",
                            ),
                            kind: "remove-user",
                            tone: "warning",
                          });
                          if (confirmed) rejectMutation.mutate(selectedConversation.id);
                        }}
                        className="h-10 rounded-xl border border-red-400/15 bg-red-500/[0.07] px-5 text-sm font-semibold text-red-300"
                      >
                        {t("messages:requests.reject", undefined, "Rədd et")}
                      </button>
                      <button
                        type="button"
                        onClick={() => acceptMutation.mutate(selectedConversation.id)}
                        className="h-10 rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white"
                      >
                        {t("messages:requests.accept", undefined, "Qəbul et")}
                      </button>
                    </div>
                  </div>
                ) : isOutgoingRequestAwaitingDecision ? (
                  <div className="shrink-0 border-t border-amber-400/10 bg-[#111116] px-5 py-5 text-center">
                    <ShieldQuestion size={22} className="mx-auto text-amber-300" />
                    <p className="mt-2 text-sm font-semibold text-zinc-200">
                      {t(
                        "messages:requests.sentTitle",
                        undefined,
                        "Mesaj istəyin göndərildi",
                      )}
                    </p>
                    <p className="mx-auto mt-1 max-w-md text-xs leading-5 text-zinc-500">
                      {t(
                        "messages:requests.sentDescription",
                        undefined,
                        "Qarşı tərəf istəyini qəbul edənədək əlavə mesaj göndərə bilməzsən.",
                      )}
                    </p>
                  </div>
                ) : (
                  <form
                    onSubmit={handleSubmit}
                    className="shrink-0 border-t border-white/[0.07] bg-[#111116] p-3 sm:p-4"
                  >
                    {(validationMessage || mutationError) && (
                      <p className="mb-2 rounded-xl bg-red-500/10 px-3 py-2 text-xs text-red-200">
                        {validationMessage ?? mutationError}
                      </p>
                    )}
                    {replyTarget && (
                      <div className="mb-2 flex items-center gap-3 rounded-xl border border-violet-400/15 bg-violet-500/[0.07] px-3 py-2">
                        <Reply size={16} className="shrink-0 text-violet-300" />
                        <span className="min-w-0 flex-1">
                          <strong className="block text-[11px] text-violet-200">
                            {replyTarget.senderUserId === currentUser?.id
                              ? t("messages:reply.self", undefined, "Öz mesajına cavab")
                              : t(
                                  "messages:reply.user",
                                  { userName: recipientName },
                                  `${recipientName} istifadəçisinə cavab`,
                                )}
                          </strong>
                          <span className="mt-0.5 block truncate text-xs text-zinc-500">
                            {getReplyPreview(replyTarget)}
                          </span>
                        </span>
                        <button
                          type="button"
                          onClick={() => setReplyTarget(null)}
                          className="text-zinc-600 hover:text-white"
                          aria-label={t("messages:reply.cancel", undefined, "Cavabı ləğv et")}
                        >
                          <X size={16} />
                        </button>
                      </div>
                    )}
                    {mediaFiles.length > 0 && (
                      <div className="mb-2 flex items-center gap-2 rounded-xl border border-violet-300/10 bg-violet-500/[0.06] px-3 py-2 text-xs text-zinc-400">
                        <ImagePlus size={15} className="text-violet-300" />
                        <button
                          type="button"
                          onClick={() => setMediaPreviewOpen(true)}
                          className="min-w-0 flex-1 truncate text-left font-semibold text-zinc-300 transition hover:text-white"
                        >
                          {translateText(
                            "messages:composer.selectedMedia",
                            { count: mediaFiles.length },
                            `${mediaFiles.length} media seçilib`,
                          )}
                        </button>
                        <button
                          type="button"
                          onClick={() => {
                            setMediaFiles([]);
                            setMediaPreviewOpen(false);
                            setMediaPreviewIndex(0);
                          }}
                          className="text-zinc-500 transition hover:text-red-300"
                          aria-label={translateText("messages:composer.clearMedia", undefined, "Bütün mediaları sil")}
                        >
                          <X size={15} />
                        </button>
                      </div>
                    )}
                    <div className="flex items-end gap-2">
                      <input
                        ref={fileInputRef}
                        type="file"
                        multiple
                        accept="image/jpeg,image/png,image/webp,video/mp4,video/webm"
                        onChange={handleMediaChange}
                        className="sr-only"
                      />
                      <button
                        type="button"
                        onClick={() => fileInputRef.current?.click()}
                        className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl text-zinc-400 transition hover:bg-white/[0.06] hover:text-white"
                        aria-label={t(
                          "messages:composer.addMedia",
                          undefined,
                          "Foto və ya video əlavə et",
                        )}
                      >
                        <ImagePlus size={20} />
                      </button>
                      <button
                        type="button"
                        onClick={() => setGamePickerOpen(true)}
                        className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl text-zinc-400 transition hover:bg-white/[0.06] hover:text-violet-200"
                        aria-label={t(
                          "messages:composer.recommendGame",
                          undefined,
                          "Oyun təklif et",
                        )}
                      >
                        <Gamepad2 size={20} />
                      </button>
                      <ChatEmojiPicker
                        disabled={
                          selectedConversation?.canSendMessage === false ||
                          sendMutation.isPending
                        }
                        onSelect={insertEmoji}
                      />
                      <textarea
                        ref={messageInputRef}
                        value={messageText}
                        onChange={(event) => handleTyping(event.target.value)}
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
                            ? t(
                                "messages:composer.requestPlaceholder",
                                undefined,
                                "Bir mesaj istəyi yaza bilərsən...",
                              )
                            : t(
                                "messages:composer.messagePlaceholder",
                                undefined,
                                "Mesaj yaz...",
                              )
                        }
                        disabled={
                          selectedConversation?.canSendMessage === false ||
                          sendMutation.isPending
                        }
                        className="max-h-32 min-h-11 flex-1 resize-none rounded-2xl border border-white/[0.09] bg-white/[0.04] px-4 py-3 text-sm text-zinc-100 outline-none placeholder:text-zinc-700 focus:border-violet-400/30"
                      />
                      <button
                        type="submit"
                        disabled={
                          sendMutation.isPending ||
                          !recipientId ||
                          (!messageText.trim() && mediaFiles.length === 0) ||
                          selectedConversation?.canSendMessage === false
                        }
                        className="relative flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-violet-600 text-white transition hover:bg-violet-500 disabled:opacity-40"
                        aria-label={t("messages:composer.send", undefined, "Mesajı göndər")}
                      >
                        {sendMutation.isPending ? (
                          <LoaderCircle size={18} className="animate-spin" />
                        ) : (
                          <Send size={18} />
                        )}
                        {mediaFiles.length > 0 && (
                          <span className="absolute -right-1.5 -top-1.5 flex h-5 min-w-5 items-center justify-center rounded-full border-2 border-[#111116] bg-white px-1 text-[10px] font-black text-violet-700">
                            {mediaFiles.length}
                          </span>
                        )}
                      </button>
                    </div>
                  </form>
                )}

                {mediaPreviewOpen && mediaFiles.length > 0 && (
                  <MediaComposerPreviewModal
                    files={mediaFiles}
                    selectedIndex={mediaPreviewIndex}
                    messageText={messageText}
                    isSending={sendMutation.isPending}
                    onSelect={setMediaPreviewIndex}
                    onMessageChange={handleTyping}
                    onRemove={removeMediaFile}
                    onAdd={() => fileInputRef.current?.click()}
                    onClose={() => setMediaPreviewOpen(false)}
                    onSend={submitCurrentMessage}
                  />
                )}

                {streakCelebration && (
                  <StreakCelebration
                    event={streakCelebration}
                    otherUserName={recipientName}
                  />
                )}
              </>
            ) : (
              <div className="flex flex-1 flex-col items-center justify-center px-6 text-center">
                <MessageCircle size={42} className="text-zinc-700" />
                <h2 className="mt-5 text-xl font-bold text-white">
                  {t("messages:empty.selectTitle", undefined, "Söhbət seç")}
                </h2>
                <p className="mt-2 max-w-sm text-sm leading-6 text-zinc-600">
                  {t(
                    "messages:empty.selectDescription",
                    undefined,
                    "Mesaj tarixçən solda görünəcək. Yeni söhbət üçün istifadəçi profilindən “Mesaj yaz” seç.",
                  )}
                </p>
              </div>
            )}
          </section>
        </div>
      </main>

      {conversationContextMenu &&
        contextConversation &&
        createPortal(
          <div
            role="menu"
            onPointerDown={(event) => event.stopPropagation()}
            className="chat-conversation-context-menu fixed z-[185] w-52 rounded-2xl p-2"
            style={{
              left: conversationContextMenu.x,
              top: conversationContextMenu.y,
            }}
          >
            {contextConversation.requestStatus === "Accepted" && (
              <button
                type="button"
                role="menuitem"
                disabled={archiveMutation.isPending}
                onClick={() => {
                  setConversationContextMenu(null);
                  archiveMutation.mutate({
                    conversationId: contextConversation.id,
                    archived: !contextConversation.isArchived,
                  });
                }}
                className="chat-conversation-context-action flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-sm font-semibold transition disabled:opacity-40"
              >
                {contextConversation.isArchived ? (
                  <Undo2 size={17} />
                ) : (
                  <Archive size={17} />
                )}
                {contextConversation.isArchived
                  ? t(
                      "messages:conversationActions.unarchive",
                      undefined,
                      language === "en" ? "Unarchive" : "Arxivdən çıxar",
                    )
                  : t(
                      "messages:conversationActions.archive",
                      undefined,
                      language === "en" ? "Archive" : "Arxivlə",
                    )}
              </button>
            )}
            <button
              type="button"
              role="menuitem"
              disabled={deleteConversationMutation.isPending}
              onClick={() => void requestConversationDelete(contextConversation)}
              className="chat-conversation-context-action is-danger flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-sm font-semibold transition disabled:opacity-40"
            >
              <Trash2 size={17} />
              {t(
                "messages:conversationActions.delete",
                undefined,
                language === "en" ? "Delete chat" : "Söhbəti sil",
              )}
            </button>
          </div>,
          document.body,
        )}

      {deleteChoiceMessage &&
        createPortal(
          <div
            className="fixed inset-0 z-[160] flex items-center justify-center bg-black/75 p-4 backdrop-blur-sm"
            onMouseDown={(event) => {
              if (event.target === event.currentTarget) setDeleteChoiceMessage(null);
            }}
          >
            <div className="w-full max-w-md rounded-3xl border border-white/[0.09] bg-[#111116] p-5 shadow-2xl shadow-black/50">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h2 className="text-lg font-bold text-white">
                    {t("messages:delete.choiceTitle", undefined, "Mesajı sil")}
                  </h2>
                  <p className="mt-1 text-sm leading-6 text-zinc-500">
                    {t(
                      "messages:delete.choiceDescription",
                      undefined,
                      "Mesajın haradan silinəcəyini seç.",
                    )}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => setDeleteChoiceMessage(null)}
                  className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-zinc-500 hover:bg-white/[0.06] hover:text-white"
                  aria-label={t("messages:delete.cancel", undefined, "Bağla")}
                >
                  <X size={18} />
                </button>
              </div>
              <div className="mt-5 space-y-2">
                <button
                  type="button"
                  onClick={() => void chooseDeleteScope("ForMe")}
                  className="w-full rounded-2xl border border-white/[0.08] bg-white/[0.035] p-4 text-left transition hover:border-violet-400/25 hover:bg-violet-500/[0.06]"
                >
                  <strong className="block text-sm text-zinc-100">
                    {t("messages:delete.forMeOption", undefined, "Məndən sil")}
                  </strong>
                  <span className="mt-1 block text-xs leading-5 text-zinc-500">
                    {t(
                      "messages:delete.forMeOptionDescription",
                      undefined,
                      "Mesaj yalnız sənin söhbət tarixçəndən silinəcək.",
                    )}
                  </span>
                </button>
                <button
                  type="button"
                  onClick={() => void chooseDeleteScope("ForEveryone")}
                  className="w-full rounded-2xl border border-red-400/15 bg-red-500/[0.05] p-4 text-left transition hover:border-red-400/30 hover:bg-red-500/[0.09]"
                >
                  <strong className="block text-sm text-red-200">
                    {t(
                      "messages:delete.forEveryoneOption",
                      undefined,
                      "Hər kəsdən sil",
                    )}
                  </strong>
                  <span className="mt-1 block text-xs leading-5 text-red-200/55">
                    {t(
                      "messages:delete.forEveryoneOptionDescription",
                      undefined,
                      "Mesaj söhbətin hər iki tərəfindən silinəcək.",
                    )}
                  </span>
                </button>
              </div>
            </div>
          </div>,
          document.body,
        )}

      {forwardMessage &&
        createPortal(
          <div
            className="fixed inset-0 z-[160] flex items-center justify-center bg-black/75 p-4 backdrop-blur-sm"
            onMouseDown={(event) => {
              if (event.target === event.currentTarget && !forwardMessageMutation.isPending) {
                setForwardMessage(null);
              }
            }}
          >
            <div className="flex max-h-[80vh] w-full max-w-lg flex-col overflow-hidden rounded-3xl border border-white/[0.09] bg-[#111116] shadow-2xl shadow-black/50">
              <div className="flex items-start justify-between gap-4 border-b border-white/[0.07] p-5">
                <div>
                  <h2 className="text-lg font-bold text-white">
                    {t("messages:forward.title", undefined, "Mesajı yönləndir")}
                  </h2>
                  <p className="mt-1 text-sm leading-6 text-zinc-500">
                    {t(
                      "messages:forward.description",
                      undefined,
                      "Mesajı göndərmək istədiyin söhbətləri seç.",
                    )}
                  </p>
                </div>
                <button
                  type="button"
                  disabled={forwardMessageMutation.isPending}
                  onClick={() => setForwardMessage(null)}
                  className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-zinc-500 hover:bg-white/[0.06] hover:text-white disabled:opacity-40"
                  aria-label={t("messages:forward.close", undefined, "Bağla")}
                >
                  <X size={18} />
                </button>
              </div>

              <div className="p-4 pb-2">
                <label className="flex h-11 items-center gap-3 rounded-2xl border border-white/[0.08] bg-white/[0.035] px-4">
                  <Search size={16} className="text-zinc-600" />
                  <input
                    autoFocus
                    type="search"
                    value={forwardSearchText}
                    onChange={(event) => setForwardSearchText(event.target.value)}
                    placeholder={t(
                      "messages:forward.searchPlaceholder",
                      undefined,
                      "Söhbətlərdə axtar...",
                    )}
                    className="min-w-0 flex-1 bg-transparent text-sm text-zinc-100 outline-none placeholder:text-zinc-600"
                  />
                </label>
              </div>

              <div className="min-h-0 flex-1 space-y-1 overflow-y-auto px-4 py-2">
                {forwardCandidates.map((conversation) => {
                  const selected = forwardTargetConversationIds.includes(
                    conversation.id,
                  );
                  return (
                    <button
                      key={conversation.id}
                      type="button"
                      onClick={() => toggleForwardTarget(conversation.id)}
                      className={`flex w-full items-center gap-3 rounded-2xl border p-3 text-left transition ${
                        selected
                          ? "border-violet-400/30 bg-violet-500/[0.09]"
                          : "border-transparent hover:bg-white/[0.045]"
                      }`}
                    >
                      <UserAvatar
                        userName={conversation.otherUser.userName}
                        profileImageUrl={conversation.otherUser.profileImageUrl}
                        className="h-10 w-10"
                      />
                      <span className="min-w-0 flex-1">
                        <strong className="block truncate text-sm text-zinc-100">
                          {conversation.otherUser.userName}
                        </strong>
                        <span className="mt-0.5 block truncate text-xs text-zinc-600">
                          {getMessagePreview(conversation.lastMessage)}
                        </span>
                      </span>
                      <span
                        className={`flex h-6 w-6 items-center justify-center rounded-full border ${
                          selected
                            ? "border-violet-400 bg-violet-500 text-white"
                            : "border-white/[0.12] text-transparent"
                        }`}
                      >
                        <Check size={14} />
                      </span>
                    </button>
                  );
                })}
                {forwardCandidates.length === 0 && (
                  <p className="px-4 py-10 text-center text-sm leading-6 text-zinc-600">
                    {t(
                      "messages:forward.empty",
                      undefined,
                      "Yönləndirmək üçün uyğun söhbət tapılmadı.",
                    )}
                  </p>
                )}
              </div>

              <div className="border-t border-white/[0.07] p-4">
                {forwardMessageMutation.isError && (
                  <p className="mb-3 rounded-xl bg-red-500/10 px-3 py-2 text-xs text-red-200">
                    {getApiErrorMessage(
                      forwardMessageMutation.error,
                      t(
                        "messages:forward.failed",
                        undefined,
                        "Mesaj yönləndirilmədi.",
                      ),
                    )}
                  </p>
                )}
                <button
                  type="button"
                  onClick={submitForwardMessage}
                  disabled={
                    forwardTargetConversationIds.length === 0 ||
                    forwardMessageMutation.isPending
                  }
                  className="flex h-11 w-full items-center justify-center gap-2 rounded-2xl bg-violet-600 text-sm font-bold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-40"
                >
                  {forwardMessageMutation.isPending ? (
                    <LoaderCircle size={17} className="animate-spin" />
                  ) : (
                    <Forward size={17} />
                  )}
                  {t(
                    "messages:forward.confirm",
                    { count: forwardTargetConversationIds.length },
                    `Yönləndir (${forwardTargetConversationIds.length})`,
                  )}
                </button>
              </div>
            </div>
          </div>,
          document.body,
        )}

      {gamePickerOpen && (
        <div className="fixed inset-0 z-[120] flex items-center justify-center bg-black/75 p-4 backdrop-blur-sm">
          <div className="w-full max-w-2xl rounded-3xl border border-white/[0.09] bg-[#111116] p-5 shadow-2xl">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.15em] text-violet-300">
                  {t("messages:game.recommend", undefined, "Oyun təklif et")}
                </p>
                <h2 className="mt-1 text-xl font-bold text-white">
                  {t("messages:game.searchTitle", undefined, "Oyunu axtar")}
                </h2>
              </div>
              <button
                type="button"
                onClick={() => setGamePickerOpen(false)}
                className="flex h-9 w-9 items-center justify-center rounded-xl text-zinc-500 hover:bg-white/[0.06]"
              >
                <X size={18} />
              </button>
            </div>
            <label className="mt-5 flex h-12 items-center gap-3 rounded-2xl border border-white/[0.09] bg-white/[0.04] px-4">
              <Search size={18} className="text-zinc-600" />
              <input
                autoFocus
                value={gameSearch}
                onChange={(event) => setGameSearch(event.target.value)}
                placeholder={t(
                  "messages:game.searchPlaceholder",
                  undefined,
                  "Ən azı 2 simvol yaz...",
                )}
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
                      replyToMessageId: replyTarget?.id ?? null,
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
                      <img
                        src={resolveAssetUrl(game.coverImageUrl) ?? game.coverImageUrl}
                        alt=""
                        className="h-full w-full object-cover"
                      />
                    ) : (
                      <Gamepad2 size={20} className="text-zinc-700" />
                    )}
                  </span>
                  <span>
                    <strong className="block text-sm text-zinc-100">{game.title}</strong>
                    <span className="mt-1 block text-xs text-zinc-600">
                      Kliklə və birbaşa göndər
                    </span>
                  </span>
                </button>
              ))}
              {gameSearchQuery.isLoading && (
                <LoaderCircle size={20} className="mx-auto animate-spin text-zinc-600" />
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
