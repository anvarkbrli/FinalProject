import type { InfiniteData } from "@tanstack/react-query";
import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
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
import { formatDateTime, formatRelativeDate, resolveAssetUrl } from "../../lib/helpers";
import {
  getNotifications,
  getUnreadNotificationCount,
  markAllNotificationsAsRead,
  markNotificationAsRead,
  notificationQueryKeys,
} from "../../services/notificationsService";
import type {
  NotificationDTO,
  NotificationPageDTO,
  NotificationType,
} from "../../types/notifications";

const pageSize = 10;
interface NotificationTone {
  containerClassName: string;
  iconClassName: string;
  dotClassName: string;
  badgeLabel: string | null;
  badgeClassName: string;
}

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

function getNotificationTone(notification: NotificationDTO): NotificationTone {
  if (notification.type === "WarningIssued") {
    return {
      containerClassName:
        "border-red-300/25 bg-red-500/[0.10] hover:bg-red-500/[0.15]",
      iconClassName: "bg-red-400/15 text-red-200",
      dotClassName: "bg-red-300",
      badgeLabel: "Vacib xəbərdarlıq",
      badgeClassName: "bg-red-400/12 text-red-200",
    };
  }

  if (
    notification.type === "AnnouncementPublished" ||
    notification.priority === "Critical"
  ) {
    return {
      containerClassName:
        "border-amber-300/20 bg-amber-400/[0.08] hover:bg-amber-400/[0.13]",
      iconClassName: "bg-amber-400/15 text-amber-200",
      dotClassName: "bg-amber-300",
      badgeLabel: "Admin elanı",
      badgeClassName: "bg-amber-400/12 text-amber-200",
    };
  }

  if (notification.priority === "High") {
    return {
      containerClassName: notification.isRead
        ? "border-white/[0.06] bg-white/[0.02] hover:bg-white/[0.05]"
        : "border-fuchsia-300/15 bg-fuchsia-500/[0.07] hover:bg-fuchsia-500/[0.11]",
      iconClassName: "bg-fuchsia-500/12 text-fuchsia-300",
      dotClassName: "bg-fuchsia-400",
      badgeLabel: "Diqqət",
      badgeClassName: "bg-fuchsia-500/10 text-fuchsia-200",
    };
  }

  return {
    containerClassName: notification.isRead
      ? "border-white/[0.05] bg-white/[0.018] hover:bg-white/[0.045]"
      : "border-violet-300/15 bg-violet-500/[0.075] hover:bg-violet-500/[0.12]",
    iconClassName: "bg-violet-500/12 text-violet-300",
    dotClassName: "bg-violet-400",
    badgeLabel: null,
    badgeClassName: "",
  };
}

function updateNotificationInPages(
  current: InfiniteData<NotificationPageDTO> | undefined,
  updated: NotificationDTO,
): InfiniteData<NotificationPageDTO> | undefined {
  if (!current) {
    return current;
  }

  return {
    ...current,
    pages: current.pages.map((page) => ({
      ...page,
      items: page.items.map((item) =>
        item.id === updated.id ? updated : item,
      ),
    })),
  };
}

function markAllPagesAsRead(
  current: InfiniteData<NotificationPageDTO> | undefined,
): InfiniteData<NotificationPageDTO> | undefined {
  if (!current) {
    return current;
  }

  const readAtUtc = new Date().toISOString();

  return {
    ...current,
    pages: current.pages.map((page) => ({
      ...page,
      items: page.items.map((item) => ({
        ...item,
        isRead: true,
        readAtUtc: item.readAtUtc ?? readAtUtc,
      })),
    })),
  };
}

function NotificationItem({
  notification,
  now,
  onRead,
}: {
  notification: NotificationDTO;
  now: Date;
  onRead: (notificationId: number) => void;
}) {
  const actorImageUrl = resolveAssetUrl(notification.actorProfileImageUrl);
  const tone = getNotificationTone(notification);
  const shouldShowActorImage = !notification.isAdminNotification && actorImageUrl;
  const rootClassName = `notification-item ${
    notification.isRead
      ? "notification-item--read"
      : "notification-item--unread"
  } flex w-full gap-3 rounded-2xl border p-3.5 text-left transition ${tone.containerClassName}`;

  const content = (
    <>
      <span
        className={`notification-item__icon relative flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-xl ${tone.iconClassName}`}
      >
        {shouldShowActorImage ? (
          <img
            src={actorImageUrl}
            alt=""
            className="h-full w-full object-cover"
          />
        ) : (
          renderNotificationIcon(notification.type)
        )}
      </span>

      <span className="notification-item__content min-w-0 flex-1">
        <span className="flex flex-wrap items-center gap-2">
          <span
            className={`notification-item__title block text-sm leading-5 ${
              notification.isAdminNotification ||
              notification.priority === "Critical"
                ? "font-bold text-white"
                : notification.isRead
                  ? "font-medium text-zinc-300"
                  : "font-semibold text-white"
            }`}
          >
            {notification.title}
          </span>

          {tone.badgeLabel && (
            <span
              className={`notification-item__priority rounded-full px-2 py-0.5 text-[9px] font-bold uppercase tracking-[0.1em] ${tone.badgeClassName}`}
            >
              {tone.badgeLabel}
            </span>
          )}
        </span>

        <span className="notification-item__message mt-1 block text-xs leading-5 text-zinc-500">
          {notification.actorUserName && !notification.isAdminNotification && (
            <strong className="font-semibold text-zinc-300">
              {notification.actorUserName}{" "}
            </strong>
          )}
          {notification.message}
        </span>

        <time
          dateTime={notification.createdAtUtc}
          title={formatDateTime(notification.createdAtUtc)}
          className="notification-item__time mt-1.5 block text-[11px] font-medium text-zinc-600"
        >
          {formatRelativeDate(notification.createdAtUtc, now)}
        </time>
      </span>

      {!notification.isRead && (
        <span
          className={`notification-item__unread-dot mt-1 h-2 w-2 shrink-0 rounded-full ${tone.dotClassName}`}
          aria-label="Oxunmamış bildiriş"
        />
      )}
    </>
  );

  if (!notification.actionUrl) {
    return (
      <button
        type="button"
        className={`${rootClassName} notification-item-button`}
        onClick={() => onRead(notification.id)}
      >
        {content}
      </button>
    );
  }

  return (
    <Link
      to={notification.actionUrl}
      className={`${rootClassName} notification-item-link`}
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
  const [now, setNow] = useState(() => new Date());

  const notificationsQuery = useInfiniteQuery({
    queryKey: notificationQueryKeys.list,
    queryFn: ({ pageParam }) => getNotifications(pageParam, pageSize),
    initialPageParam: 1,
    getNextPageParam: (lastPage) =>
      lastPage.pageNumber < lastPage.totalPages
        ? lastPage.pageNumber + 1
        : undefined,
    enabled: isOpen,
  });

  const unreadCountQuery = useQuery({
    queryKey: notificationQueryKeys.unreadCount,
    queryFn: getUnreadNotificationCount,
    refetchInterval: 60_000,
    staleTime: 15_000,
  });

  const readMutation = useMutation({
    mutationFn: markNotificationAsRead,
    onSuccess: (updated) => {
      queryClient.setQueryData<InfiniteData<NotificationPageDTO>>(
        notificationQueryKeys.list,
        (current) => updateNotificationInPages(current, updated),
      );

      queryClient.setQueryData<{ count: number }>(
        notificationQueryKeys.unreadCount,
        (current) => ({ count: Math.max(0, (current?.count ?? 1) - 1) }),
      );
    },
  });

  const readAllMutation = useMutation({
    mutationFn: markAllNotificationsAsRead,
    onSuccess: async () => {
      queryClient.setQueryData<{ count: number }>(
        notificationQueryKeys.unreadCount,
        { count: 0 },
      );
      queryClient.setQueryData<InfiniteData<NotificationPageDTO>>(
        notificationQueryKeys.list,
        markAllPagesAsRead,
      );

      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: notificationQueryKeys.unreadCount,
        }),
        queryClient.invalidateQueries({
          queryKey: notificationQueryKeys.list,
        }),
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

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    setNow(new Date());
    const intervalId = window.setInterval(() => setNow(new Date()), 30_000);

    return () => window.clearInterval(intervalId);
  }, [isOpen]);

  const unreadCount = unreadCountQuery.data?.count ?? 0;
  const notifications =
    notificationsQuery.data?.pages.flatMap((page) => page.items) ?? [];

  return (
    <div ref={containerRef} className="notification-bell relative order-1">
      <button
        type="button"
        aria-label={
          unreadCount > 0
            ? `Bildirişlər, ${unreadCount} oxunmamış`
            : "Bildirişlər"
        }
        aria-haspopup="dialog"
        aria-expanded={isOpen}
        onClick={() => {
          if (!isOpen) {
            setNow(new Date());
          }

          setIsOpen((current) => !current);
        }}
        className="notification-bell__trigger relative flex h-11 w-11 items-center justify-center rounded-xl border border-white/[0.08] bg-white/[0.04] text-zinc-400 transition hover:border-white/[0.14] hover:bg-white/[0.08] hover:text-white"
      >
        <Bell size={19} />
        {unreadCount > 0 && (
          <span
            aria-live="polite"
            className="notification-bell__badge absolute -right-1 -top-1 flex min-h-5 min-w-5 items-center justify-center rounded-full border-2 border-[#08080b] bg-violet-500 px-1 text-[10px] font-bold leading-none text-white"
          >
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        )}
      </button>

      {isOpen && (
        <section
          role="dialog"
          aria-label="Bildirişlər"
          className="notification-panel fixed left-3 right-3 top-[78px] z-[70] overflow-hidden rounded-3xl border border-white/[0.09] bg-[#101015] shadow-2xl shadow-black/55 sm:absolute sm:left-auto sm:right-0 sm:top-[calc(100%+10px)] sm:w-[420px]"
        >
          <header className="notification-panel__header flex items-center justify-between border-b border-white/[0.06] px-5 py-4">
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
                className="notification-panel__read-all flex items-center gap-1.5 rounded-lg px-2.5 py-2 text-xs font-semibold text-violet-300 transition hover:bg-violet-500/10 disabled:opacity-50"
              >
                {readAllMutation.isPending ? (
                  <LoaderCircle size={14} className="animate-spin" />
                ) : (
                  <CheckCheck size={14} />
                )}
                Hamısını oxu
              </button>
            )}
          </header>

          <div className="notification-list max-h-[min(68vh,560px)] space-y-2 overflow-y-auto p-3">
            {notificationsQuery.isLoading ? (
              <div className="flex min-h-40 items-center justify-center gap-2 text-sm text-zinc-500">
                <LoaderCircle size={17} className="animate-spin" />
                Bildirişlər yüklənir...
              </div>
            ) : notificationsQuery.isError ? (
              <div className="rounded-2xl border border-red-400/15 bg-red-500/[0.06] p-4 text-sm text-red-200">
                Bildirişlər yüklənmədi.
              </div>
            ) : notifications.length === 0 ? (
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
              <>
                {notifications.map((notification) => (
                  <NotificationItem
                    key={notification.id}
                    notification={notification}
                    now={now}
                    onRead={(notificationId) => {
                      if (!notification.isRead) {
                        readMutation.mutate(notificationId);
                      }
                      setIsOpen(false);
                    }}
                  />
                ))}

                {notificationsQuery.hasNextPage && (
                  <button
                    type="button"
                    disabled={notificationsQuery.isFetchingNextPage}
                    onClick={() => notificationsQuery.fetchNextPage()}
                    className="notification-list__load-more flex h-10 w-full items-center justify-center gap-2 rounded-xl border border-white/[0.06] bg-white/[0.025] text-xs font-semibold text-zinc-400 transition hover:bg-white/[0.06] hover:text-white disabled:opacity-50"
                  >
                    {notificationsQuery.isFetchingNextPage && (
                      <LoaderCircle size={14} className="animate-spin" />
                    )}
                    Daha çox göstər
                  </button>
                )}
              </>
            )}
          </div>

          <footer className="notification-panel__footer border-t border-white/[0.06] p-3">
            <Link
              to="/settings#notifications"
              onClick={() => setIsOpen(false)}
              className="flex h-10 items-center justify-center rounded-xl bg-white/[0.04] text-xs font-semibold text-zinc-300 transition hover:bg-white/[0.08] hover:text-white"
            >
              Bildiriş ayarlarını idarə et
            </Link>
          </footer>
        </section>
      )}
    </div>
  );
}
