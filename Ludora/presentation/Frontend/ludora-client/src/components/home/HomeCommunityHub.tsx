import { useQuery } from "@tanstack/react-query";
import {
  ArrowRight,
  Bookmark,
  ChevronLeft,
  ChevronRight,
  Eye,
  Gamepad2,
  Heart,
  History,
  ListChecks,
  LockKeyhole,
  Megaphone,
  MessageSquareText,
  PenLine,
  Play,
  Sparkles,
  Star,
  UserPlus,
  Users,
} from "lucide-react";
import { useEffect, useMemo, useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/useI18n";
import type { TranslationParameters } from "../../i18n/localization";
import { formatRelativeDate, resolveAssetUrl } from "../../lib/helpers";
import {
  getHomeAnnouncements,
  getHomeSocialDashboard,
  markAnnouncementViewed,
} from "../../services/homeService";
import { isAuthSessionValid, useAuthStore } from "../../stores/authStore";
import type {
  HomeSocialGameDTO,
  HomeSocialUserDTO,
} from "../../types/home";
import type { UserActivityDTO } from "../../types/profile";

type SocialTab = "activity" | "playing" | "ratings" | "connections";
type RatingView = "stars" | "reviews";

const tabs: Array<{ id: SocialTab; icon: typeof History }> = [
  { id: "activity", icon: History },
  { id: "playing", icon: Play },
  { id: "ratings", icon: Star },
  { id: "connections", icon: Users },
];

const socialFallbacks = {
  az: {
    eyebrow: "Sənin üçün",
    title: "Dostlar və icma",
    description:
      "İzlədiyin insanların son fəaliyyətləri, oynadıqları oyunlar və sənə uyğun yeni əlaqələr.",
    "announcements.eyebrow": "Rəsmi məlumat",
    "announcements.title": "WellPlayd elanları",
    "announcements.empty": "Hazırda aktiv elan yoxdur.",
    "announcements.login":
      "Elanları və sənə uyğun yenilikləri görmək üçün hesabına daxil ol.",
    "announcements.previous": "Əvvəlki elan",
    "announcements.next": "Növbəti elan",
    "tabs.activity": "Fəaliyyət",
    "tabs.playing": "İndi oynayırlar",
    "tabs.ratings": "Dost reytinqləri",
    "tabs.connections": "Uyğunluq",
    loginTitle: "Dostlarının fəaliyyətini gör",
    loginDescription:
      "İzlədiyin insanların nə oynadığını, verdiyi reytinqləri və sənə uyğun istifadəçiləri görmək üçün daxil ol.",
    loginAction: "Daxil ol",
    emptyActivity: "İzlədiyin insanlardan yeni fəaliyyət yoxdur.",
    emptyPlaying: "Hazırda dostlarından heç kim oyun oynayır statusunda deyil.",
    emptyRatings: "Dostların hələ yalnız reytinq paylaşmayıb.",
    emptyReviews: "Dostların hələ yazılı rəy paylaşmayıb.",
    emptyConnections: "Uyğunluq hesablamaq üçün daha çox oyun və əlaqə lazımdır.",
    friendCount: "{count} dost",
    moreFriends: "+{count}",
    tasteTitle: "Ortaq oyun zövqü",
    tasteWith: "{name} ilə uyğunluq",
    sharedGames: "{count} ortaq oyun",
    sharedFavorites: "{count} ortaq favori",
    suggestionsTitle: "İzləmək maraqlı ola bilər",
    mutual: "{count} ortaq əlaqə",
    private: "Gizli profil",
    viewProfile: "Profilə bax",
    allFeed: "Bütün fəaliyyətlərə bax",
    "ratings.stars": "Yalnız reytinq",
    "ratings.reviews": "Yazılı rəylər",
    "ratings.starsDescription": "Rəy yazmadan yalnız ulduz verən dostların.",
    "ratings.reviewsDescription": "Dostlarının son yazılı oyun rəyləri.",
    "ratings.spoiler": "Bu rəy spoiler ehtiva edir",
    "ratings.reveal": "Rəyi göstər",
  },
  en: {
    eyebrow: "For you",
    title: "Friends and community",
    description:
      "Recent activity from people you follow, what they are playing, and connections that match your taste.",
    "announcements.eyebrow": "Official update",
    "announcements.title": "WellPlayd announcements",
    "announcements.empty": "There are no active announcements right now.",
    "announcements.login":
      "Sign in to see announcements and updates selected for you.",
    "announcements.previous": "Previous announcement",
    "announcements.next": "Next announcement",
    "tabs.activity": "Activity",
    "tabs.playing": "Playing now",
    "tabs.ratings": "Friend ratings",
    "tabs.connections": "Matches",
    loginTitle: "See what your friends are doing",
    loginDescription:
      "Sign in to see what people you follow are playing, their ratings, and users with similar taste.",
    loginAction: "Sign in",
    emptyActivity: "There is no new activity from people you follow.",
    emptyPlaying: "None of your friends are marked as playing a game right now.",
    emptyRatings: "Your friends have not shared rating-only activity yet.",
    emptyReviews: "Your friends have not shared a written review yet.",
    emptyConnections: "More games and connections are needed to calculate matches.",
    friendCount: "{count} friends",
    moreFriends: "+{count}",
    tasteTitle: "Shared game taste",
    tasteWith: "Match with {name}",
    sharedGames: "{count} shared games",
    sharedFavorites: "{count} shared favorites",
    suggestionsTitle: "People worth following",
    mutual: "{count} mutual connections",
    private: "Private profile",
    viewProfile: "View profile",
    allFeed: "View all activity",
    "ratings.stars": "Ratings only",
    "ratings.reviews": "Written reviews",
    "ratings.starsDescription": "Friends who rated a game without writing a review.",
    "ratings.reviewsDescription": "Recent written game reviews from your friends.",
    "ratings.spoiler": "This review contains spoilers",
    "ratings.reveal": "Reveal review",
  },
} as const;

type SocialFallbackKey = keyof typeof socialFallbacks.az;

function interpolateFallback(
  value: string,
  parameters?: TranslationParameters,
): string {
  if (!parameters) return value;
  return value.replace(/\{(\w+)\}/g, (placeholder, name: string) => {
    const parameter = parameters[name];
    return parameter === null || parameter === undefined
      ? placeholder
      : String(parameter);
  });
}

function UserAvatar({
  user,
  className = "h-10 w-10",
}: {
  user: HomeSocialUserDTO;
  className?: string;
}) {
  const imageUrl = resolveAssetUrl(user.profileImageUrl);

  return (
    <span
      className={`flex shrink-0 items-center justify-center overflow-hidden rounded-xl border border-[color:var(--wp-border)] bg-violet-500/10 text-xs font-black text-violet-400 ${className}`}
    >
      {imageUrl ? (
        <img
          src={imageUrl}
          alt={user.userName}
          className="h-full w-full object-cover"
        />
      ) : (
        user.userName.trim().charAt(0).toUpperCase() || "?"
      )}
    </span>
  );
}

function GameCover({
  game,
  className = "h-16 w-12",
}: {
  game: HomeSocialGameDTO;
  className?: string;
}) {
  const imageUrl = resolveAssetUrl(game.coverImageUrl);

  return (
    <span
      className={`flex shrink-0 items-center justify-center overflow-hidden rounded-xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-soft)] text-[color:var(--wp-text-muted)] ${className}`}
    >
      {imageUrl ? (
        <img
          src={imageUrl}
          alt={game.title}
          className="h-full w-full object-cover"
        />
      ) : (
        <Gamepad2 size={18} />
      )}
    </span>
  );
}

function ActivityIcon({ type }: { type: UserActivityDTO["type"] }) {
  let Icon = History;
  let styles = "bg-violet-500/10 text-violet-400";

  if (type === "GameStatusChanged") {
    Icon = Play;
    styles = "bg-emerald-500/10 text-emerald-400";
  } else if (type === "GameRated") {
    Icon = Star;
    styles = "bg-amber-500/10 text-amber-400";
  } else if (type === "GameAddedToFavorites") {
    Icon = Heart;
    styles = "bg-rose-500/10 text-rose-400";
  } else if (type === "GameAddedToWishlist") {
    Icon = Bookmark;
    styles = "bg-sky-500/10 text-sky-400";
  } else if (type.startsWith("GameList")) {
    Icon = ListChecks;
  } else if (type.startsWith("Post") || type.startsWith("Review")) {
    Icon = MessageSquareText;
    styles = "bg-fuchsia-500/10 text-fuchsia-400";
  } else if (type.startsWith("UserFollow")) {
    Icon = UserPlus;
    styles = "bg-cyan-500/10 text-cyan-400";
  }

  return (
    <span
      className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-xl ${styles}`}
    >
      <Icon size={16} />
    </span>
  );
}

function getStatusLabel(
  status: UserActivityDTO["status"],
  language: "az" | "en",
) {
  const labels =
    language === "en"
      ? {
          Playing: "Playing",
          Played: "Played",
          PlanToPlay: "Going to play",
          Dropped: "Abandoned",
        }
      : {
          Playing: "Oynayır",
          Played: "Oynayıb",
          PlanToPlay: "Oynayacaq",
          Dropped: "Yarım qoyub",
        };

  return status
    ? labels[status]
    : language === "en"
      ? "a new status"
      : "yeni status";
}

function ActivityDescription({
  activity,
  language,
}: {
  activity: UserActivityDTO;
  language: "az" | "en";
}) {
  const gameLink =
    activity.gameSource && activity.gameId !== null ? (
      <Link
        to={`/games/${activity.gameSource}/${activity.gameId}`}
        className="font-bold text-[var(--wp-text-strong)] transition hover:text-[var(--wp-accent-strong)]"
      >
        {activity.gameName ?? (language === "en" ? "a game" : "bir oyun")}
      </Link>
    ) : null;

  let content: ReactNode;
  switch (activity.type) {
    case "GameStatusChanged":
      content =
        language === "en" ? (
          <>
            marked {gameLink} as “{getStatusLabel(activity.status, language)}”.
          </>
        ) : (
          <>
            {gameLink} üçün statusunu “{getStatusLabel(activity.status, language)}”
            etdi.
          </>
        );
      break;
    case "GameRated":
      content =
        language === "en" ? (
          <>
            rated {gameLink} <strong>{activity.rating?.toFixed(1)}</strong>.
          </>
        ) : (
          <>
            {gameLink} oyununa <strong>{activity.rating?.toFixed(1)}</strong>{" "}
            reytinq verdi.
          </>
        );
      break;
    case "GameAddedToFavorites":
      content =
        language === "en" ? (
          <>added {gameLink} to favorites.</>
        ) : (
          <>{gameLink} oyununu favorilərinə əlavə etdi.</>
        );
      break;
    case "GameAddedToWishlist":
      content =
        language === "en" ? (
          <>added {gameLink} to the wishlist.</>
        ) : (
          <>{gameLink} oyununu wishlist-ə əlavə etdi.</>
        );
      break;
    case "ReviewCreated":
      content =
        language === "en" ? (
          <>shared a review for {gameLink}.</>
        ) : (
          <>{gameLink} üçün yeni rəy paylaşdı.</>
        );
      break;
    case "PostCreated":
      content =
        language === "en" ? <>shared a new post.</> : <>yeni post paylaşdı.</>;
      break;
    case "GameListCreated":
      content =
        language === "en" ? (
          <>
            created the list <strong>{activity.subjectTitle}</strong>.
          </>
        ) : (
          <>
            <strong>{activity.subjectTitle}</strong> adlı siyahı yaratdı.
          </>
        );
      break;
    case "GameAddedToList":
      content =
        language === "en" ? (
          <>
            added {gameLink} to <strong>{activity.subjectTitle}</strong>.
          </>
        ) : (
          <>
            {gameLink} oyununu <strong>{activity.subjectTitle}</strong> siyahısına
            əlavə etdi.
          </>
        );
      break;
    default:
      content =
        language === "en" ? (
          <>shared a new activity.</>
        ) : (
          <>yeni fəaliyyət paylaşdı.</>
        );
  }

  return (
    <p className="min-w-0 text-sm leading-6 text-[color:var(--wp-text)]">
      <Link
        to={`/users/${activity.actorUserName}`}
        className="font-black text-[var(--wp-text-strong)] transition hover:text-[var(--wp-accent-strong)]"
      >
        {activity.actorUserName}
      </Link>{" "}
      {content}
    </p>
  );
}

function EmptyState({
  icon: Icon,
  children,
}: {
  icon: typeof History;
  children: ReactNode;
}) {
  return (
    <div className="flex min-h-56 flex-col items-center justify-center rounded-3xl border border-dashed border-[color:var(--wp-border)] bg-[color:var(--wp-surface)] px-6 text-center">
      <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-violet-500/10 text-violet-400">
        <Icon size={21} />
      </span>
      <p className="mt-4 max-w-sm text-sm leading-6 text-[color:var(--wp-text-muted)]">
        {children}
      </p>
    </div>
  );
}

export function HomeCommunityHub() {
  const { language, t } = useI18n();
  const [activeTab, setActiveTab] = useState<SocialTab>("activity");
  const [ratingView, setRatingView] = useState<RatingView>("stars");
  const [announcementIndex, setAnnouncementIndex] = useState(0);
  const [revealedReviewIds, setRevealedReviewIds] = useState<Set<number>>(
    () => new Set(),
  );
  const accessToken = useAuthStore((state) => state.accessToken);
  const expiresAtUtc = useAuthStore((state) => state.expiresAtUtc);
  const user = useAuthStore((state) => state.user);
  const isInitialized = useAuthStore((state) => state.isInitialized);
  const isAuthenticated =
    isAuthSessionValid(accessToken, expiresAtUtc) && user !== null;

  const socialT = (
    key: SocialFallbackKey,
    parameters?: TranslationParameters,
  ) => {
    const fallback = interpolateFallback(
      socialFallbacks[language][key],
      parameters,
    );
    const fullKey = `home:social.${key}`;
    const translated = t(fullKey, parameters, fallback);
    return translated === fullKey ? fallback : translated;
  };

  const announcementsQuery = useQuery({
    queryKey: ["homepage", "announcements", user?.id],
    queryFn: ({ signal }) => getHomeAnnouncements(signal),
    enabled: isInitialized && isAuthenticated,
    staleTime: 2 * 60 * 1000,
  });

  const socialQuery = useQuery({
    queryKey: ["homepage", "social", user?.id],
    queryFn: ({ signal }) => getHomeSocialDashboard(signal),
    enabled: isInitialized && isAuthenticated,
    staleTime: 60 * 1000,
  });

  const announcements = announcementsQuery.data?.items ?? [];
  const safeAnnouncementIndex =
    announcements.length === 0
      ? 0
      : Math.min(announcementIndex, announcements.length - 1);
  const announcement = announcements[safeAnnouncementIndex];
  const friendReviews = socialQuery.data?.friendReviews ?? [];

  useEffect(() => {
    if (!announcement || !isAuthenticated) return;
    void markAnnouncementViewed(announcement.id).catch(() => undefined);
  }, [announcement, isAuthenticated]);

  const tabCounts = useMemo(
    () => ({
      activity: socialQuery.data?.activities.length ?? 0,
      playing: socialQuery.data?.friendsPlaying.length ?? 0,
      ratings:
        (socialQuery.data?.friendRatings.length ?? 0) + friendReviews.length,
      connections:
        (socialQuery.data?.tasteMatch ? 1 : 0) +
        (socialQuery.data?.friendSuggestions.length ?? 0),
    }),
    [friendReviews.length, socialQuery.data],
  );

  function moveAnnouncement(direction: -1 | 1) {
    if (announcements.length < 2) return;
    setAnnouncementIndex(
      (current) =>
        (current + direction + announcements.length) % announcements.length,
    );
  }

  return (
    <section className="mx-auto max-w-7xl px-5 py-16 lg:px-8 lg:py-20">
      <div className="mb-7 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p className="text-xs font-black uppercase tracking-[0.22em] text-violet-400">
            {socialT("eyebrow")}
          </p>
          <h2 className="mt-2 text-2xl font-black tracking-tight text-[var(--wp-text-strong)] sm:text-3xl">
            {socialT("title")}
          </h2>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-[color:var(--wp-text-muted)]">
            {socialT("description")}
          </p>
        </div>

        {isAuthenticated && socialQuery.data && (
          <span className="inline-flex w-fit items-center gap-2 rounded-full border border-[color:var(--wp-border)] bg-[color:var(--wp-surface)] px-3 py-1.5 text-xs font-semibold text-[color:var(--wp-text-muted)]">
            <Users size={14} className="text-violet-400" />
            {socialT("friendCount", {
              count: socialQuery.data.followedUserCount,
            })}
          </span>
        )}
      </div>

      <div className="grid gap-5 xl:grid-cols-[minmax(280px,0.72fr)_minmax(0,1.7fr)]">
        <aside className="wp-surface-card relative min-h-[34rem] overflow-hidden rounded-[28px] p-6 sm:p-7">
          <div className="pointer-events-none absolute -right-16 -top-16 h-52 w-52 rounded-full bg-violet-500/10 blur-3xl" />
          <div className="relative flex h-full flex-col">
            <div className="flex items-start justify-between gap-4">
              <span className="flex h-12 w-12 items-center justify-center rounded-2xl border border-violet-400/15 bg-violet-500/10 text-violet-400">
                <Megaphone size={21} />
              </span>
              {announcements.length > 1 && (
                <span className="rounded-full border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-soft)] px-3 py-1 text-xs tabular-nums text-[color:var(--wp-text-muted)]">
                  {safeAnnouncementIndex + 1}/{announcements.length}
                </span>
              )}
            </div>

            <p className="mt-7 text-[10px] font-black uppercase tracking-[0.2em] text-violet-400">
              {socialT("announcements.eyebrow")}
            </p>
            <h3 className="mt-2 text-xl font-black text-[var(--wp-text-strong)]">
              {socialT("announcements.title")}
            </h3>

            {!isAuthenticated ? (
              <div className="mt-8 flex flex-1 flex-col justify-center rounded-3xl border border-dashed border-[color:var(--wp-border)] bg-[color:var(--wp-surface)] p-5">
                <p className="text-sm leading-7 text-[color:var(--wp-text-muted)]">
                  {socialT("announcements.login")}
                </p>
              </div>
            ) : announcementsQuery.isLoading ? (
              <div className="mt-8 flex-1 animate-pulse space-y-4">
                <div className="h-7 w-3/4 rounded-lg bg-[color:var(--wp-surface-soft)]" />
                <div className="h-4 rounded bg-[color:var(--wp-surface-soft)]" />
                <div className="h-4 w-5/6 rounded bg-[color:var(--wp-surface-soft)]" />
              </div>
            ) : announcement ? (
              <div className="mt-8 flex flex-1 flex-col">
                <div className="rounded-3xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface)] p-5">
                  <p className="text-xs font-semibold text-[color:var(--wp-text-muted)]">
                    {formatRelativeDate(
                      announcement.publishedAtUtc ?? announcement.createdAt,
                    )}
                  </p>
                  <h4 className="mt-3 text-lg font-black leading-7 text-[var(--wp-text-strong)]">
                    {announcement.title}
                  </h4>
                  <p
                    data-user-content
                    className="mt-4 whitespace-pre-wrap text-sm leading-7 text-[color:var(--wp-text)]"
                  >
                    {announcement.content}
                  </p>
                </div>

                <div className="mt-auto flex items-center justify-between gap-3 pt-6">
                  <span className="text-xs text-[color:var(--wp-text-muted)]">
                    {announcement.createdByUserName}
                  </span>
                  {announcements.length > 1 && (
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={() => moveAnnouncement(-1)}
                        aria-label={socialT("announcements.previous")}
                        className="flex h-10 w-10 items-center justify-center rounded-xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-soft)] text-[color:var(--wp-text-muted)] transition hover:text-[var(--wp-text-strong)]"
                      >
                        <ChevronLeft size={18} />
                      </button>
                      <button
                        type="button"
                        onClick={() => moveAnnouncement(1)}
                        aria-label={socialT("announcements.next")}
                        className="flex h-10 w-10 items-center justify-center rounded-xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-soft)] text-[color:var(--wp-text-muted)] transition hover:text-[var(--wp-text-strong)]"
                      >
                        <ChevronRight size={18} />
                      </button>
                    </div>
                  )}
                </div>
              </div>
            ) : (
              <div className="mt-8 flex flex-1 items-center justify-center rounded-3xl border border-dashed border-[color:var(--wp-border)] bg-[color:var(--wp-surface)] p-5 text-center text-sm leading-6 text-[color:var(--wp-text-muted)]">
                {socialT("announcements.empty")}
              </div>
            )}
          </div>
        </aside>

        <div className="wp-surface-card overflow-hidden rounded-[28px]">
          {!isAuthenticated ? (
            <div className="flex min-h-[34rem] flex-col items-center justify-center px-6 text-center sm:px-10">
              <span className="flex h-16 w-16 items-center justify-center rounded-3xl border border-violet-400/15 bg-violet-500/10 text-violet-400">
                <Users size={28} />
              </span>
              <h3 className="mt-6 text-2xl font-black text-[var(--wp-text-strong)]">
                {socialT("loginTitle")}
              </h3>
              <p className="mt-3 max-w-lg text-sm leading-7 text-[color:var(--wp-text-muted)]">
                {socialT("loginDescription")}
              </p>
              <Link
                to="/login"
                className="mt-7 inline-flex items-center gap-2 rounded-xl bg-violet-600 px-5 py-3 text-sm font-bold text-white transition hover:bg-violet-500"
              >
                {socialT("loginAction")}
                <ArrowRight size={17} />
              </Link>
            </div>
          ) : (
            <>
              <div className="border-b border-[color:var(--wp-border)] px-4 pt-4 sm:px-6 sm:pt-6">
                <div className="grid grid-cols-2 gap-2 lg:grid-cols-4">
                  {tabs.map(({ id, icon: Icon }) => (
                    <button
                      key={id}
                      type="button"
                      onClick={() => setActiveTab(id)}
                      className={`flex min-w-0 items-center justify-center gap-2 rounded-xl px-3 py-3 text-xs font-bold transition sm:text-sm ${
                        activeTab === id
                          ? "bg-violet-500/12 text-[var(--wp-accent-strong)]"
                          : "text-[color:var(--wp-text-muted)] hover:bg-[color:var(--wp-surface-soft)] hover:text-[var(--wp-text-strong)]"
                      }`}
                    >
                      <Icon size={16} />
                      <span className="truncate">
                        {socialT(`tabs.${id}` as SocialFallbackKey)}
                      </span>
                      {tabCounts[id] > 0 && (
                        <span className="rounded-full bg-[color:var(--wp-surface-soft)] px-1.5 py-0.5 text-[10px] tabular-nums">
                          {tabCounts[id]}
                        </span>
                      )}
                    </button>
                  ))}
                </div>
              </div>

              <div className="min-h-[29rem] p-4 sm:p-6">
                {socialQuery.isLoading ? (
                  <div className="grid gap-3 md:grid-cols-2">
                    {Array.from({ length: 6 }).map((_, index) => (
                      <div
                        key={index}
                        className="h-24 animate-pulse rounded-2xl bg-[color:var(--wp-surface-soft)]"
                      />
                    ))}
                  </div>
                ) : socialQuery.isError || !socialQuery.data ? (
                  <EmptyState icon={History}>
                    {language === "en"
                      ? "The social dashboard could not be loaded."
                      : "Sosial məlumatları yükləmək mümkün olmadı."}
                  </EmptyState>
                ) : activeTab === "activity" ? (
                  socialQuery.data.activities.length === 0 ? (
                    <EmptyState icon={History}>
                      {socialT("emptyActivity")}
                    </EmptyState>
                  ) : (
                    <div>
                      <div className="grid gap-3 md:grid-cols-2">
                        {socialQuery.data.activities
                          .slice(0, 6)
                          .map((activity) => (
                            <article
                              key={activity.id}
                              className="flex min-h-24 items-start gap-3 rounded-2xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface)] p-4"
                            >
                              <ActivityIcon type={activity.type} />
                              <div className="min-w-0 flex-1">
                                <ActivityDescription
                                  activity={activity}
                                  language={language}
                                />
                                <p className="mt-2 text-xs text-[color:var(--wp-text-muted)]">
                                  {formatRelativeDate(activity.createdAt)}
                                </p>
                              </div>
                              {activity.gameCoverImageUrl && (
                                <span className="h-14 w-10 shrink-0 overflow-hidden rounded-lg border border-[color:var(--wp-border)]">
                                  <img
                                    src={
                                      resolveAssetUrl(
                                        activity.gameCoverImageUrl,
                                      ) ?? ""
                                    }
                                    alt=""
                                    className="h-full w-full object-cover"
                                  />
                                </span>
                              )}
                            </article>
                          ))}
                      </div>
                      <Link
                        to={`/users/${user.userName}?tab=feed&scope=following`}
                        className="mt-5 inline-flex items-center gap-2 text-sm font-bold text-[var(--wp-accent-strong)] transition hover:text-violet-400"
                      >
                        {socialT("allFeed")}
                        <ArrowRight size={16} />
                      </Link>
                    </div>
                  )
                ) : activeTab === "playing" ? (
                  socialQuery.data.friendsPlaying.length === 0 ? (
                    <EmptyState icon={Play}>{socialT("emptyPlaying")}</EmptyState>
                  ) : (
                    <div className="grid gap-4 md:grid-cols-2">
                      {socialQuery.data.friendsPlaying.map((item) => (
                        <Link
                          key={`${item.game.source}-${item.game.id}`}
                          to={`/games/${item.game.source}/${item.game.id}`}
                          className="group flex min-h-36 items-center gap-4 rounded-3xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface)] p-4 transition hover:-translate-y-0.5 hover:border-violet-400/25"
                        >
                          <GameCover game={item.game} className="h-28 w-20" />
                          <div className="min-w-0 flex-1">
                            <p className="line-clamp-2 font-black leading-6 text-[var(--wp-text-strong)] group-hover:text-[var(--wp-accent-strong)]">
                              {item.game.title}
                            </p>
                            <div className="mt-4 flex items-center">
                              {item.friends.map((friend, index) => (
                                <span
                                  key={friend.id}
                                  style={{ marginLeft: index === 0 ? 0 : -8 }}
                                >
                                  <UserAvatar
                                    user={friend}
                                    className="h-8 w-8 rounded-full"
                                  />
                                </span>
                              ))}
                              {item.friendCount > item.friends.length && (
                                <span className="ml-2 text-xs font-bold text-[color:var(--wp-text-muted)]">
                                  {socialT("moreFriends", {
                                    count:
                                      item.friendCount - item.friends.length,
                                  })}
                                </span>
                              )}
                            </div>
                            <p className="mt-3 text-xs text-[color:var(--wp-text-muted)]">
                              {socialT("friendCount", {
                                count: item.friendCount,
                              })}
                            </p>
                          </div>
                        </Link>
                      ))}
                    </div>
                  )
                ) : activeTab === "ratings" ? (
                  <div>
                    <div className="mb-5 flex flex-col gap-3 rounded-2xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface)] p-2 sm:flex-row">
                      {(
                        [
                          ["stars", Star],
                          ["reviews", PenLine],
                        ] as const
                      ).map(([view, Icon]) => (
                        <button
                          key={view}
                          type="button"
                          onClick={() => setRatingView(view)}
                          className={`flex flex-1 items-center justify-center gap-2 rounded-xl px-4 py-3 text-sm font-bold transition ${
                            ratingView === view
                              ? "bg-[color:var(--wp-surface-strong)] text-[var(--wp-accent-strong)] shadow-[var(--wp-shadow-card)]"
                              : "text-[color:var(--wp-text-muted)] hover:text-[var(--wp-text-strong)]"
                          }`}
                        >
                          <Icon size={16} />
                          {socialT(
                            view === "stars"
                              ? "ratings.stars"
                              : "ratings.reviews",
                          )}
                          <span className="rounded-full bg-[color:var(--wp-surface-soft)] px-2 py-0.5 text-[10px] tabular-nums">
                            {view === "stars"
                              ? socialQuery.data.friendRatings.length
                              : friendReviews.length}
                          </span>
                        </button>
                      ))}
                    </div>

                    <p className="mb-4 text-xs leading-5 text-[color:var(--wp-text-muted)]">
                      {socialT(
                        ratingView === "stars"
                          ? "ratings.starsDescription"
                          : "ratings.reviewsDescription",
                      )}
                    </p>

                    {ratingView === "stars" ? (
                      socialQuery.data.friendRatings.length === 0 ? (
                        <EmptyState icon={Star}>
                          {socialT("emptyRatings")}
                        </EmptyState>
                      ) : (
                        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                          {socialQuery.data.friendRatings.map((item, index) => (
                            <article
                              key={`${item.user.id}-${item.game.source}-${item.game.id}-${index}`}
                              className="rounded-2xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface)] p-4"
                            >
                              <div className="flex items-center justify-between gap-3">
                                <Link
                                  to={`/users/${item.user.userName}`}
                                  className="flex min-w-0 items-center gap-2"
                                >
                                  <UserAvatar
                                    user={item.user}
                                    className="h-9 w-9"
                                  />
                                  <span className="truncate text-sm font-bold text-[var(--wp-text-strong)]">
                                    {item.user.userName}
                                  </span>
                                </Link>
                                <span className="inline-flex items-center gap-1 rounded-lg border border-amber-400/15 bg-amber-500/10 px-2.5 py-1.5 text-sm font-black text-amber-400">
                                  <Star size={14} fill="currentColor" />
                                  {item.rating.toFixed(1)}
                                </span>
                              </div>
                              <Link
                                to={`/games/${item.game.source}/${item.game.id}`}
                                className="mt-4 flex items-center gap-3"
                              >
                                <GameCover game={item.game} />
                                <span className="line-clamp-2 text-sm font-bold leading-6 text-[color:var(--wp-text)] transition hover:text-[var(--wp-accent-strong)]">
                                  {item.game.title}
                                </span>
                              </Link>
                              <p className="mt-3 text-xs text-[color:var(--wp-text-muted)]">
                                {formatRelativeDate(item.ratedAtUtc)}
                              </p>
                            </article>
                          ))}
                        </div>
                      )
                    ) : friendReviews.length === 0 ? (
                      <EmptyState icon={PenLine}>
                        {socialT("emptyReviews")}
                      </EmptyState>
                    ) : (
                      <div className="grid gap-4 md:grid-cols-2">
                        {friendReviews.map((item) => {
                          const isRevealed = revealedReviewIds.has(
                            item.reviewId,
                          );
                          return (
                            <article
                              key={item.reviewId}
                              className="flex min-h-52 flex-col rounded-3xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface)] p-5"
                            >
                              <div className="flex items-start justify-between gap-3">
                                <Link
                                  to={`/users/${item.user.userName}`}
                                  className="flex min-w-0 items-center gap-3"
                                >
                                  <UserAvatar user={item.user} />
                                  <div className="min-w-0">
                                    <p className="truncate text-sm font-black text-[var(--wp-text-strong)]">
                                      {item.user.userName}
                                    </p>
                                    <p className="mt-1 text-xs text-[color:var(--wp-text-muted)]">
                                      {formatRelativeDate(item.createdAtUtc)}
                                    </p>
                                  </div>
                                </Link>
                                {item.rating !== null && (
                                  <span className="inline-flex shrink-0 items-center gap-1 rounded-lg border border-amber-400/15 bg-amber-500/10 px-2.5 py-1.5 text-sm font-black text-amber-400">
                                    <Star size={14} fill="currentColor" />
                                    {item.rating.toFixed(1)}
                                  </span>
                                )}
                              </div>

                              <Link
                                to={`/games/${item.game.source}/${item.game.id}`}
                                className="mt-4 flex items-center gap-3 rounded-2xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-strong)] p-3 transition hover:border-violet-400/25"
                              >
                                <GameCover
                                  game={item.game}
                                  className="h-20 w-14"
                                />
                                <span className="line-clamp-2 text-sm font-black leading-6 text-[var(--wp-text-strong)]">
                                  {item.game.title}
                                </span>
                              </Link>

                              {item.containsSpoilers && !isRevealed ? (
                                <button
                                  type="button"
                                  onClick={() =>
                                    setRevealedReviewIds((current) => {
                                      const next = new Set(current);
                                      next.add(item.reviewId);
                                      return next;
                                    })
                                  }
                                  className="mt-4 flex flex-1 flex-col items-center justify-center rounded-2xl border border-amber-400/15 bg-amber-500/[0.06] px-4 py-5 text-center transition hover:bg-amber-500/10"
                                >
                                  <Eye size={20} className="text-amber-400" />
                                  <span className="mt-2 text-sm font-bold text-amber-400">
                                    {socialT("ratings.spoiler")}
                                  </span>
                                  <span className="mt-1 text-xs text-[color:var(--wp-text-muted)]">
                                    {socialT("ratings.reveal")}
                                  </span>
                                </button>
                              ) : (
                                <p
                                  data-user-content
                                  className="mt-4 line-clamp-5 flex-1 whitespace-pre-wrap text-sm leading-7 text-[color:var(--wp-text)]"
                                >
                                  {item.content}
                                </p>
                              )}
                            </article>
                          );
                        })}
                      </div>
                    )}
                  </div>
                ) : socialQuery.data.tasteMatch ||
                  socialQuery.data.friendSuggestions.length > 0 ? (
                  <div className="grid gap-4 lg:grid-cols-[0.9fr_1.1fr]">
                    <div className="relative overflow-hidden rounded-3xl border border-violet-400/20 bg-[linear-gradient(145deg,color-mix(in_srgb,var(--wp-accent)_14%,var(--wp-surface-strong)),var(--wp-surface-strong))] p-5">
                      <div className="pointer-events-none absolute -right-10 -top-10 h-36 w-36 rounded-full bg-violet-500/15 blur-3xl" />
                      <div className="relative">
                        <p className="text-[10px] font-black uppercase tracking-[0.2em] text-violet-400">
                          {socialT("tasteTitle")}
                        </p>
                        {socialQuery.data.tasteMatch ? (
                          <>
                            <div className="mt-5 flex items-center gap-3">
                              <UserAvatar
                                user={socialQuery.data.tasteMatch.user}
                                className="h-12 w-12"
                              />
                              <div className="min-w-0">
                                <p className="truncate text-sm font-bold text-[var(--wp-text-strong)]">
                                  {socialT("tasteWith", {
                                    name: socialQuery.data.tasteMatch.user
                                      .userName,
                                  })}
                                </p>
                                <p className="mt-1 text-xs text-[color:var(--wp-text-muted)]">
                                  {socialT("sharedGames", {
                                    count:
                                      socialQuery.data.tasteMatch
                                        .sharedGameCount,
                                  })}
                                </p>
                              </div>
                            </div>
                            <div className="mt-6 flex items-end justify-between gap-4">
                              <strong className="text-5xl font-black tracking-[-0.06em] text-[var(--wp-accent-strong)]">
                                {socialQuery.data.tasteMatch.matchPercent}%
                              </strong>
                              <span className="text-right text-xs leading-5 text-[color:var(--wp-text-muted)]">
                                {socialT("sharedFavorites", {
                                  count:
                                    socialQuery.data.tasteMatch
                                      .sharedFavoriteCount,
                                })}
                              </span>
                            </div>
                            {socialQuery.data.tasteMatch.sharedGames.length >
                              0 && (
                              <div className="mt-6 flex gap-2">
                                {socialQuery.data.tasteMatch.sharedGames.map(
                                  (game) => (
                                    <Link
                                      key={`${game.source}-${game.id}`}
                                      to={`/games/${game.source}/${game.id}`}
                                      title={game.title}
                                    >
                                      <GameCover
                                        game={game}
                                        className="h-20 w-14"
                                      />
                                    </Link>
                                  ),
                                )}
                              </div>
                            )}
                          </>
                        ) : (
                          <p className="mt-6 text-sm leading-7 text-[color:var(--wp-text-muted)]">
                            {socialT("emptyConnections")}
                          </p>
                        )}
                      </div>
                    </div>

                    <div className="rounded-3xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface)] p-5">
                      <div className="flex items-center gap-2">
                        <Sparkles size={17} className="text-fuchsia-400" />
                        <h3 className="font-black text-[var(--wp-text-strong)]">
                          {socialT("suggestionsTitle")}
                        </h3>
                      </div>
                      <div className="mt-4 space-y-3">
                        {socialQuery.data.friendSuggestions.length === 0 ? (
                          <p className="py-8 text-center text-sm leading-6 text-[color:var(--wp-text-muted)]">
                            {socialT("emptyConnections")}
                          </p>
                        ) : (
                          socialQuery.data.friendSuggestions.map(
                            (suggestion) => (
                              <Link
                                key={suggestion.user.id}
                                to={`/users/${suggestion.user.userName}`}
                                className="flex items-center gap-3 rounded-2xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-strong)] p-3 transition hover:border-violet-400/25"
                              >
                                <UserAvatar user={suggestion.user} />
                                <div className="min-w-0 flex-1">
                                  <div className="flex items-center gap-2">
                                    <p className="truncate text-sm font-black text-[var(--wp-text-strong)]">
                                      {suggestion.user.userName}
                                    </p>
                                    {suggestion.isPrivate && (
                                      <LockKeyhole
                                        size={12}
                                        className="text-[color:var(--wp-text-muted)]"
                                      />
                                    )}
                                  </div>
                                  <p className="mt-1 truncate text-xs text-[color:var(--wp-text-muted)]">
                                    {suggestion.sharedFavoriteCount > 0
                                      ? socialT("sharedFavorites", {
                                          count:
                                            suggestion.sharedFavoriteCount,
                                        })
                                      : suggestion.sharedGameCount > 0
                                        ? socialT("sharedGames", {
                                            count: suggestion.sharedGameCount,
                                          })
                                        : socialT("mutual", {
                                            count:
                                              suggestion.mutualFollowCount,
                                          })}
                                  </p>
                                </div>
                                <ArrowRight
                                  size={16}
                                  className="text-[color:var(--wp-text-muted)]"
                                />
                              </Link>
                            ),
                          )
                        )}
                      </div>
                    </div>
                  </div>
                ) : (
                  <EmptyState icon={Users}>
                    {socialT("emptyConnections")}
                  </EmptyState>
                )}
              </div>
            </>
          )}
        </div>
      </div>
    </section>
  );
}
