import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Bell,
  CheckCheck,
  CircleAlert,
  Heart,
  ListPlus,
  LoaderCircle,
  MessageCircle,
  ShieldAlert,
  Star,
  UserPlus,
} from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { formatDateTime, resolveAssetUrl } from "../../lib/helpers";
import {
  getNotifications,
  getUnreadNotificationCount,
  markAllNotificationsAsRead,
  markNotificationAsRead,
} from "../../services/notificationsService";
import type {
  NotificationDTO,
  NotificationType,
} from "../../types/notifications";

const listQueryKey = ["notifications", "list", 1, 12] as const;
const unreadCountQueryKey = ["notifications", "unread-count"] as const;

function renderNotificationIcon(type: NotificationType) {
  switch (type) {
    case "AnnouncementPublished":
    case "WarningIssued":
      return <ShieldAlert size={18} />;
    case "PostLiked":
    case "ReviewLiked":
    case "GameListLiked":
    case "DiscussionLiked":
    case "DiscussionReplyLiked":
      return <Heart size={18} />;
    case "PostCommented":
    case "ReviewCommented":
    case "GameListCommented":
    case "DiscussionReplied":
    case "FollowingDiscussionCreated":
      return <MessageCircle size={18} />;
    case "FollowingReviewCreated":
      return <Star size={18} />;
    case "FollowingPublicListCreated":
      return <ListPlus size={18} />;
    case "FollowRequestReceived":
    case "UserFollowed":
    case "FollowRequestAccepted":
      return <UserPlus size={18} />;
    default:
      return <CircleAlert size={18} />;
  }
}

function NotificationItem({
  notification,
  onRead,
}: {
  notification: NotificationDTO;
  onRead: (notificationId: number) => void;
}) {
  const actorImageUrl = resolveAssetUrl(notification.actorProfileImageUrl);
  const isCritical = notification.priority === "Critical";
  const isAdmin = notification.isAdminNotification;

  const content = (
    <div
      className={`flex gap-3 rounded-2xl border p-3.5 transition ${
        isAdmin
          ? "border-amber-300/20 bg-amber-400/[0.08] hover:bg-amber-400/[0.12]"
          : notification.isRead
            ? "border-white/[0.05] bg-white/[0.018] hover:bg-white/[0.045]"
            : "border-violet-300/15 bg-violet-500/[0.075] hover:bg-violet-500/[0.12]"
      }`}
    >
      <span
        className={`relative flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-xl ${
          isAdmin
            ? "bg-amber-400/15 text-amber-200"
            : "bg-violet-500/12 text-violet-300"
        }`}
      >
        {actorImageUrl ? (
          <img
            src={actorImageUrl}
            alt=""
            className="h-full w-full object-cover"
          />
        ) : (
          renderNotificationIcon(notification.type)
        )}
      </span>

      <span className="min-w-0 flex-1">
        <span
          className={`block text-sm leading-5 ${
            isAdmin || isCritical
              ? "font-bold text-white"
              : notification.isRead
                ? "font-medium text-zinc-300"
                : "font-semibold text-white"
          }`}
        >
          {notification.title}
        </span>
        <span className="mt-1 block text-xs leading-5 text-zinc-500">
          {notification.message}
        </span>
        <span className="mt-1.5 block text-[11px] font-medium text-zinc-600">
          {formatDateTime(notification.createdAtUtc)}
        </span>
      </span>

      {!notification.isRead && (
        <span
          className={`mt-1 h-2 w-2 shrink-0 rounded-full ${
            isAdmin ? "bg-amber-300" : "bg-violet-400"
          }`}
          aria-label="Oxunmamış bildiriş"
        />
      )}
    </div>
  );

  if (!notification.actionUrl) {
    return (
      <button
        type="button"
        className="block w-full text-left"
        onClick={() => onRead(notification.id)}
      >
        {content}
      </button>
    );
  }

  return (
    <Link
      to={notification.actionUrl}
      className="block"
      onClick={() => onRead(notification.id)}
    >
      {content}
    </Link>
  );
}

export function NotificationBell() {
  const queryClient = useQueryClient();
  const containerRef = useRef<HTMLDivElement>(null);
  const [isOpen, setIsOpen] = useState(false);


  const notificationsQuery = useQuery({
    queryKey: listQueryKey,
    queryFn: () => getNotifications(1, 12),
    enabled: isOpen,
  });

  const unreadCountQuery = useQuery({
    queryKey: unreadCountQueryKey,
    queryFn: getUnreadNotificationCount,
    refetchInterval: 60_000,
  });

  const readMutation = useMutation({
    mutationFn: markNotificationAsRead,
    onSuccess: (updated) => {
      queryClient.setQueryData<{
        items: NotificationDTO[];
        pageNumber: number;
        pageSize: number;
        totalCount: number;
        totalPages: number;
      }>(listQueryKey, (current) => {
        if (!current) {
          return current;
        }

        return {
          ...current,
          items: current.items.map((item) =>
            item.id === updated.id ? updated : item,
          ),
        };
      });

      queryClient.setQueryData<{ count: number }>(
        unreadCountQueryKey,
        (current) => ({ count: Math.max(0, (current?.count ?? 1) - 1) }),
      );
    },
  });

  const readAllMutation = useMutation({
    mutationFn: markAllNotificationsAsRead,
    onSuccess: async () => {
      queryClient.setQueryData<{ count: number }>(unreadCountQueryKey, {
        count: 0,
      });
      queryClient.setQueryData<{
        items: NotificationDTO[];
        pageNumber: number;
        pageSize: number;
        totalCount: number;
        totalPages: number;
      }>(listQueryKey, (current) =>
        current
          ? {
              ...current,
              items: current.items.map((item) => ({
                ...item,
                isRead: true,
                readAtUtc: item.readAtUtc ?? new Date().toISOString(),
              })),
            }
          : current,
      );

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: unreadCountQueryKey }),
        queryClient.invalidateQueries({ queryKey: listQueryKey }),
      ]);
    },
  });

  useEffect(() => {
    function handleOutsideClick(event: MouseEvent) {
      if (
        containerRef.current &&
        !containerRef.current.contains(event.target as Node)
      ) {
        setIsOpen(false);
      }
    }

    function handleEscape(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setIsOpen(false);
      }
    }

    document.addEventListener("mousedown", handleOutsideClick);
    document.addEventListener("keydown", handleEscape);

    return () => {
      document.removeEventListener("mousedown", handleOutsideClick);
      document.removeEventListener("keydown", handleEscape);
    };
  }, []);

  const unreadCount = unreadCountQuery.data?.count ?? 0;

  return (
    <div ref={containerRef} className="relative order-1">
      <button
        type="button"
        aria-label="Bildirişlər"
        aria-haspopup="dialog"
        aria-expanded={isOpen}
        onClick={() => setIsOpen((current) => !current)}
        className="relative flex h-11 w-11 items-center justify-center rounded-xl border border-white/[0.08] bg-white/[0.04] text-zinc-400 transition hover:border-white/[0.14] hover:bg-white/[0.08] hover:text-white"
      >
        <Bell size={19} />
        {unreadCount > 0 && (
          <span className="absolute -right-1 -top-1 flex min-h-5 min-w-5 items-center justify-center rounded-full border-2 border-[#08080b] bg-violet-500 px-1 text-[10px] font-bold leading-none text-white">
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        )}
      </button>

      {isOpen && (
        <div
          role="dialog"
          aria-label="Bildirişlər"
          className="absolute right-0 top-[calc(100%+10px)] z-50 w-[min(92vw,420px)] overflow-hidden rounded-3xl border border-white/[0.09] bg-[#101015] shadow-2xl shadow-black/55"
        >
          <div className="flex items-center justify-between border-b border-white/[0.06] px-5 py-4">
            <div>
              <h2 className="font-bold text-white">Bildirişlər</h2>
              <p className="mt-0.5 text-xs text-zinc-600">
                {unreadCount > 0
                  ? `${unreadCount} oxunmamış bildiriş`
                  : "Bütün bildirişlər oxunub"}
              </p>
            </div>
            {unreadCount > 0 && (
              <button
                type="button"
                disabled={readAllMutation.isPending}
                onClick={() => readAllMutation.mutate()}
                className="flex items-center gap-1.5 rounded-lg px-2.5 py-2 text-xs font-semibold text-violet-300 transition hover:bg-violet-500/10 disabled:opacity-50"
              >
                {readAllMutation.isPending ? (
                  <LoaderCircle size={14} className="animate-spin" />
                ) : (
                  <CheckCheck size={14} />
                )}
                Hamısını oxu
              </button>
            )}
          </div>

          <div className="max-h-[min(68vh,560px)] space-y-2 overflow-y-auto p-3">
            {notificationsQuery.isLoading ? (
              <div className="flex min-h-40 items-center justify-center gap-2 text-sm text-zinc-500">
                <LoaderCircle size={17} className="animate-spin" />
                Bildirişlər yüklənir...
              </div>
            ) : notificationsQuery.isError ? (
              <div className="rounded-2xl border border-red-400/15 bg-red-500/[0.06] p-4 text-sm text-red-200">
                Bildirişlər yüklənmədi.
              </div>
            ) : (notificationsQuery.data?.items.length ?? 0) === 0 ? (
              <div className="flex min-h-40 flex-col items-center justify-center text-center">
                <Bell size={24} className="text-zinc-700" />
                <p className="mt-3 text-sm font-semibold text-zinc-400">
                  Hələ bildiriş yoxdur
                </p>
                <p className="mt-1 text-xs text-zinc-600">
                  Yeni fəaliyyətlər burada görünəcək.
                </p>
              </div>
            ) : (
              notificationsQuery.data?.items.map((notification) => (
                <NotificationItem
                  key={notification.id}
                  notification={notification}
                  onRead={(notificationId) => {
                    if (!notification.isRead) {
                      readMutation.mutate(notificationId);
                    }
                    setIsOpen(false);
                  }}
                />
              ))
            )}
          </div>

          <div className="border-t border-white/[0.06] p-3">
            <Link
              to="/settings#notifications"
              onClick={() => setIsOpen(false)}
              className="flex h-10 items-center justify-center rounded-xl bg-white/[0.04] text-xs font-semibold text-zinc-300 transition hover:bg-white/[0.08] hover:text-white"
            >
              Bildiriş ayarlarını idarə et
            </Link>
          </div>
        </div>
      )}
    </div>
  );
}
