import type { InfiniteData } from "@tanstack/react-query";
import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  Bell,
  CalendarDays,
  CheckCheck,
  CircleAlert,
  Crown,
  Heart,
  ListPlus,
  LoaderCircle,
  MessageCircle,
  Shield,
  ShieldAlert,
  Star,
  UserPlus,
  X,
} from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Link } from "react-router-dom";
import { useI18n, type I18nContextValue } from "../../i18n/useI18n";
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
    case "FollowedDiscussionReplyActivity":
    case "FollowingDiscussionCreated":
    case "CommunityDiscussionCreated":
      return <MessageCircle size={18} />;
    case "FollowingReviewCreated":
      return <Star size={18} />;
    case "FollowingPublicListCreated":
    case "GameListCollaborationInvited":
    case "GameListCollaborationAccepted":
      return <ListPlus size={18} />;
    case "FollowRequestReceived":
    case "UserFollowed":
    case "FollowRequestAccepted":
      return <UserPlus size={18} />;
    case "CommunityOwnershipOffered":
      return <Crown size={18} />;
    case "CommunityModeratorAssigned":
      return <Shield size={18} />;
    default:
      return <CircleAlert size={18} />;
  }
}

function getNotificationTone(
  notification: NotificationDTO,
  t: I18nContextValue["t"],
): NotificationTone {
  if (notification.type === "WarningIssued") {
    return {
      containerClassName:
        "notification-item--warning border-red-300/25 bg-red-500/[0.10] hover:bg-red-500/[0.15]",
      iconClassName: "bg-red-400/15 text-red-200",
      dotClassName: "bg-red-300",
      badgeLabel: t("notifications:badges.warning"),
      badgeClassName: "bg-red-400/12 text-red-200",
    };
  }

  if (
    notification.type === "AnnouncementPublished" ||
    notification.priority === "Critical"
  ) {
    return {
      containerClassName:
        "notification-item--announcement border-amber-300/20 bg-amber-400/[0.08] hover:bg-amber-400/[0.13]",
      iconClassName: "bg-amber-400/15 text-amber-200",
      dotClassName: "bg-amber-300",
      badgeLabel: t("notifications:badges.announcement"),
      badgeClassName: "bg-amber-400/12 text-amber-200",
    };
  }

  if (notification.priority === "High") {
    return {
      containerClassName: notification.isRead
        ? "notification-item--high border-white/[0.06] bg-white/[0.02] hover:bg-white/[0.05]"
        : "notification-item--high border-fuchsia-300/15 bg-fuchsia-500/[0.07] hover:bg-fuchsia-500/[0.11]",
      iconClassName: "bg-fuchsia-500/12 text-fuchsia-300",
      dotClassName: "bg-fuchsia-400",
      badgeLabel: t("notifications:badges.high"),
      badgeClassName: "bg-fuchsia-500/10 text-fuchsia-200",
    };
  }

  return {
    containerClassName: notification.isRead
      ? "notification-item--standard border-white/[0.05] bg-white/[0.018] hover:bg-white/[0.045]"
      : "notification-item--standard border-violet-300/15 bg-violet-500/[0.075] hover:bg-violet-500/[0.12]",
    iconClassName: "bg-violet-500/12 text-violet-300",
    dotClassName: "bg-violet-400",
    badgeLabel: null,
    badgeClassName: "",
  };
}

function getLocalizedNotificationContent(
  notification: NotificationDTO,
  language: "az" | "en",
  t: I18nContextValue["t"],
): { title: string; message: string } {
  if (notification.type === "AnnouncementPublished") {
    return {
      title: notification.title,
      message: notification.message,
    };
  }

  if (notification.type === "WarningIssued") {
    return {
      title: t("notifications:types.WarningIssued.title"),
      message: notification.message,
    };
  }

  if (language === "az") {
    return {
      title: notification.title,
      message: notification.message,
    };
  }

  if (notification.type === "FollowRequestReceived") {
    const communityName =
      notification.message.match(/^(.+?) icmasına/)?.[1]?.trim();

    if (notification.title.includes("üzvlük sorğusu")) {
      return {
        title: "New community membership request",
        message: communityName
          ? `requested to join the ${communityName} community.`
          : "sent a community membership request.",
      };
    }

    if (notification.title.includes("icmaya dəvət")) {
      return {
        title: "Private community invitation",
        message: communityName
          ? `invited you to join the ${communityName} community.`
          : "invited you to join a private community.",
      };
    }
  }

  if (notification.type === "CommunityOwnershipOffered") {
    const communityName =
      notification.message.match(/^(.+?) icma(?:sında|sının)/)?.[1]?.trim();
    const offered = notification.title.includes("təklifi");
    const failed = notification.title.includes("ötürülmədi");

    return {
      title: offered
        ? "Community ownership offer"
        : failed
          ? "Community ownership transfer failed"
          : "Community ownership transferred",
      message: communityName
        ? offered
          ? `You were offered ownership of the ${communityName} community.`
          : failed
            ? `No member accepted the ownership offer for the ${communityName} community.`
            : `Ownership of the ${communityName} community was transferred.`
        : t("notifications:types.CommunityOwnershipOffered.message"),
    };
  }

  const translationKey = `notifications:types.${notification.type}`;
  return {
    title: t(`${translationKey}.title`, undefined, notification.title),
    message: t(`${translationKey}.message`, undefined, notification.message),
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
  onWarningOpen,
}: {
  notification: NotificationDTO;
  now: Date;
  onRead: (notificationId: number) => void;
  onWarningOpen: (notification: NotificationDTO) => void;
}) {
  const { language, t } = useI18n();
  const actorImageUrl = resolveAssetUrl(notification.actorProfileImageUrl);
  const tone = getNotificationTone(notification, t);
  const localizedContent = getLocalizedNotificationContent(
    notification,
    language,
    t,
  );
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
            {localizedContent.title}
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
          {localizedContent.message}
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
          aria-label={t("notifications:unreadAria")}
        />
      )}
    </>
  );

  if (notification.type === "WarningIssued") {
    return (
      <button
        type="button"
        className={`${rootClassName} notification-item-button`}
        onClick={() => onWarningOpen(notification)}
      >
        {content}
      </button>
    );
  }

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
  const { language, t } = useI18n();
  const queryClient = useQueryClient();
  const containerRef = useRef<HTMLDivElement>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [now, setNow] = useState(() => new Date());
  const [selectedWarning, setSelectedWarning] =
    useState<NotificationDTO | null>(null);

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
    onSuccess: async (updated) => {
      queryClient.setQueryData<InfiniteData<NotificationPageDTO>>(
        notificationQueryKeys.list,
        (current) => updateNotificationInPages(current, updated),
      );

      await queryClient.invalidateQueries({
        queryKey: notificationQueryKeys.unreadCount,
      });
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
        setSelectedWarning(null);
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
    if (!selectedWarning) {
      return;
    }

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    return () => {
      document.body.style.overflow = previousOverflow;
    };
  }, [selectedWarning]);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const intervalId = window.setInterval(() => setNow(new Date()), 30_000);

    return () => window.clearInterval(intervalId);
  }, [isOpen]);

  const unreadCount = unreadCountQuery.data?.count ?? 0;
  const notifications =
    notificationsQuery.data?.pages.flatMap((page) => page.items) ?? [];

  return (
    <>
      <div ref={containerRef} className="notification-bell relative order-1">
      <button
        type="button"
        aria-label={
          unreadCount > 0
            ? t("notifications:bellUnreadAria", { count: unreadCount })
            : t("notifications:title")
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
          aria-label={t("notifications:title")}
          className="notification-panel fixed left-3 right-3 top-[78px] z-[70] overflow-hidden rounded-3xl border border-white/[0.09] bg-[#101015] shadow-2xl shadow-black/55 sm:absolute sm:left-auto sm:right-0 sm:top-[calc(100%+10px)] sm:w-[420px]"
        >
          <header className="notification-panel__header flex items-center justify-between border-b border-white/[0.06] px-5 py-4">
            <div>
              <h2 className="font-bold text-white">
                {t("notifications:title")}
              </h2>
              <p className="mt-0.5 text-xs text-zinc-600">
                {unreadCount > 0
                  ? t("notifications:unread", { count: unreadCount })
                  : t("notifications:allRead")}
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
                {t("notifications:markAllRead")}
              </button>
            )}
          </header>

          <div className="notification-list max-h-[min(68vh,560px)] space-y-2 overflow-y-auto p-3">
            {notificationsQuery.isLoading ? (
              <div className="flex min-h-40 items-center justify-center gap-2 text-sm text-zinc-500">
                <LoaderCircle size={17} className="animate-spin" />
                {t("notifications:loading")}
              </div>
            ) : notificationsQuery.isError ? (
              <div className="rounded-2xl border border-red-400/15 bg-red-500/[0.06] p-4 text-sm text-red-200">
                {t("notifications:loadError")}
              </div>
            ) : notifications.length === 0 ? (
              <div className="flex min-h-40 flex-col items-center justify-center text-center">
                <Bell size={24} className="text-zinc-700" />
                <p className="mt-3 text-sm font-semibold text-zinc-400">
                  {t("notifications:emptyTitle")}
                </p>
                <p className="mt-1 text-xs text-zinc-600">
                  {t("notifications:emptyDescription")}
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
                    onWarningOpen={(warningNotification) => {
                      readMutation.mutate(warningNotification.id);
                      setSelectedWarning(warningNotification);
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
                    {t("notifications:loadMore")}
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
              {t("notifications:manageSettings")}
            </Link>
          </footer>
        </section>
      )}
      </div>

      {selectedWarning &&
        createPortal(
          <div
            className="fixed inset-0 z-[2000] grid place-items-center bg-black/75 p-4 backdrop-blur-md"
            role="dialog"
            aria-modal="true"
            aria-labelledby="warning-notification-title"
            onMouseDown={(event) => {
              if (event.target === event.currentTarget) {
                setSelectedWarning(null);
              }
            }}
          >
            <section className="w-full max-w-lg overflow-hidden rounded-3xl border border-red-300/15 bg-[#111116] shadow-2xl shadow-black/70">
              <header className="flex items-start justify-between gap-4 border-b border-white/[0.07] bg-red-500/[0.06] px-6 py-5">
                <div className="flex items-start gap-3">
                  <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-red-500/15 text-red-200">
                    <ShieldAlert size={21} />
                  </span>
                  <div>
                    <p className="text-[10px] font-bold uppercase tracking-[0.16em] text-red-300/70">
                      {t("notifications:officialWarning")}
                    </p>
                    <h2
                      id="warning-notification-title"
                      className="mt-1 text-lg font-bold text-white"
                    >
                      {getLocalizedNotificationContent(
                        selectedWarning,
                        language,
                        t,
                      ).title}
                    </h2>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setSelectedWarning(null)}
                  className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-zinc-500 transition hover:bg-white/[0.07] hover:text-white"
                  aria-label={t("notifications:closeWarningAria")}
                >
                  <X size={18} />
                </button>
              </header>

              <div className="px-6 py-6">
                <p className="whitespace-pre-wrap text-sm leading-7 text-zinc-300">
                  {selectedWarning.message}
                </p>
                <div className="mt-5 flex items-center gap-2 rounded-xl border border-white/[0.06] bg-white/[0.025] px-4 py-3 text-xs text-zinc-500">
                  <CalendarDays size={15} className="text-zinc-600" />
                  {formatDateTime(selectedWarning.createdAtUtc)}
                </div>
                <p className="mt-4 text-xs leading-5 text-zinc-600">
                  {t("notifications:warningReadNotice")}
                </p>
              </div>

              <footer className="flex justify-end border-t border-white/[0.07] px-6 py-4">
                <button
                  type="button"
                  onClick={() => setSelectedWarning(null)}
                  className="h-10 rounded-xl bg-red-500 px-5 text-sm font-bold text-white transition hover:bg-red-400"
                >
                  {t("common:actions.close")}
                </button>
              </footer>
            </section>
          </div>,
          document.body,
        )}
    </>
  );
}
