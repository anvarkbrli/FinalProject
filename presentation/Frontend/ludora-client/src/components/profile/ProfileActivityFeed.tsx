import {
  Bookmark,
  ChevronLeft,
  ChevronRight,
  Gamepad2,
  Heart,
  History,
  ListChecks,
  LoaderCircle,
  MessageCircle,
  MessageSquareText,
  NotebookPen,
  Pencil,
  Star,
  Trash2,
  UserPlus,
} from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/useI18n";
import { getApiErrorMessage } from "../../lib/api";
import { formatDateTime, resolveAssetUrl } from "../../lib/helpers";
import type {
  UserActivitiesResultDTO,
  UserActivityDTO,
  UserActivityFeedCategory,
  UserActivityFeedScope,
} from "../../types/profile";

interface ProfileActivityFeedProps {
  result: UserActivitiesResultDTO | undefined;
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  page: number;
  category: UserActivityFeedCategory;
  scope: UserActivityFeedScope;
  currentUserId: string;
  onCategoryChange: (category: UserActivityFeedCategory) => void;
  onScopeChange: (scope: UserActivityFeedScope) => void;
  onPageChange: (page: number) => void;
}

const filters: Array<{
  value: UserActivityFeedCategory;
  label: string;
}> = [
  { value: "All", label: "Hamısı" },
  { value: "Statuses", label: "Statuslar" },
  { value: "Lists", label: "Siyahılar" },
  { value: "Ratings", label: "Reytinqlər" },
  { value: "Reviews", label: "Loglar" },
  { value: "Posts", label: "Postlar" },
];

const statusLabels = {
  Played: "Oynayıb",
  Playing: "Oynayır",
  PlanToPlay: "Oynayacaq",
  Dropped: "Yarım qoyub",
} as const;

function GameLink({
  activity,
  language = "az",
}: {
  activity: UserActivityDTO;
  language?: "az" | "en";
}) {
  if (!activity.gameSource || activity.gameId === null) {
    return (
      <span className="font-semibold text-zinc-200">
        {language === "en" ? "game" : "oyun"}
      </span>
    );
  }

  return (
    <Link
      to={`/games/${activity.gameSource}/${activity.gameId}`}
      className="font-semibold text-violet-300 transition hover:text-violet-200"
    >
      {activity.gameName ?? (language === "en" ? "Game" : "Oyun")}
    </Link>
  );
}

function TargetUserLink({
  activity,
  language = "az",
}: {
  activity: UserActivityDTO;
  language?: "az" | "en";
}) {
  if (!activity.targetUserName) {
    return (
      <span className="font-semibold text-zinc-200">
        {language === "en" ? "user" : "istifadəçi"}
      </span>
    );
  }

  return (
    <Link
      to={`/users/${activity.targetUserName}`}
      className="font-semibold text-violet-300 transition hover:text-violet-200"
    >
      {activity.targetUserName}
    </Link>
  );
}

function ActorLink({
  activity,
  currentUserId,
}: {
  activity: UserActivityDTO;
  currentUserId: string;
}) {
  if (activity.actorUserId === currentUserId) {
    return <span className="font-semibold text-zinc-100">Sən</span>;
  }

  return (
    <Link
      to={`/users/${activity.actorUserName}`}
      className="font-semibold text-zinc-100 transition hover:text-violet-200"
    >
      {activity.actorUserName}
    </Link>
  );
}

function getPresentation(
  activity: UserActivityDTO,
  isOwnActivity: boolean,
  language: "az" | "en",
): {
  icon: typeof History;
  accent: string;
  action: ReactNode;
} {
  const game = <GameLink activity={activity} language={language} />;
  const user = <TargetUserLink activity={activity} language={language} />;
  const listName = activity.subjectTitle ?? "siyahı";
  const verb = (secondPerson: string, thirdPerson: string) =>
    isOwnActivity ? secondPerson : thirdPerson;

  if (language === "en") {
    const subject = isOwnActivity ? <>You </> : null;
    const englishStatusLabels = {
      Played: "Played",
      Playing: "Playing",
      PlanToPlay: "Going to play",
      Dropped: "Abandoned",
    } as const;
    const list = (
      <strong className="text-zinc-200">
        {activity.subjectTitle ?? "list"}
      </strong>
    );

    switch (activity.type) {
      case "GameStatusChanged":
        return { icon: Gamepad2, accent: "bg-emerald-500/10 text-emerald-300", action: <>{subject}changed {game} to “{activity.status ? englishStatusLabels[activity.status] : "Status"}”.</> };
      case "GameStatusRemoved":
        return { icon: Gamepad2, accent: "bg-zinc-500/10 text-zinc-400", action: <>{subject}removed the status from {game}.</> };
      case "GameAddedToFavorites":
        return { icon: Heart, accent: "bg-rose-500/10 text-rose-300", action: <>{subject}added {game} to favorites.</> };
      case "GameRemovedFromFavorites":
        return { icon: Heart, accent: "bg-zinc-500/10 text-zinc-400", action: <>{subject}removed {game} from favorites.</> };
      case "GameAddedToWishlist":
        return { icon: Bookmark, accent: "bg-cyan-500/10 text-cyan-300", action: <>{subject}added {game} to the wishlist.</> };
      case "GameRemovedFromWishlist":
        return { icon: Bookmark, accent: "bg-zinc-500/10 text-zinc-400", action: <>{subject}removed {game} from the wishlist.</> };
      case "GameRated":
        return { icon: Star, accent: "bg-amber-500/10 text-amber-300", action: <>{subject}rated {game} {activity.rating ?? "—"} stars.</> };
      case "GameRatingRemoved":
        return { icon: Star, accent: "bg-zinc-500/10 text-zinc-400", action: <>{subject}removed the rating from {game}.</> };
      case "ReviewCreated":
        return { icon: NotebookPen, accent: "bg-violet-500/10 text-violet-300", action: <>{subject}logged {game}.</> };
      case "ReviewUpdated":
        return { icon: Pencil, accent: "bg-blue-500/10 text-blue-300", action: <>{subject}updated the log for {game}.</> };
      case "ReviewDeleted":
        return { icon: Trash2, accent: "bg-red-500/10 text-red-300", action: <>{subject}deleted the review for {game}.</> };
      case "ReviewLiked":
        return { icon: Heart, accent: "bg-rose-500/10 text-rose-300", action: <>{subject}liked {user}'s review of {game}.</> };
      case "ReviewUnliked":
        return { icon: Heart, accent: "bg-zinc-500/10 text-zinc-400", action: <>{subject}removed a like from a review of {game}.</> };
      case "ReviewCommented":
        return { icon: MessageCircle, accent: "bg-cyan-500/10 text-cyan-300", action: <>{subject}commented on a review of {game}.</> };
      case "ReviewCommentUpdated":
        return { icon: Pencil, accent: "bg-blue-500/10 text-blue-300", action: <>{subject}updated a review comment.</> };
      case "ReviewCommentDeleted":
        return { icon: Trash2, accent: "bg-red-500/10 text-red-300", action: <>{subject}deleted a review comment.</> };
      case "UserFollowRequested":
        return { icon: UserPlus, accent: "bg-cyan-500/10 text-cyan-300", action: <>{subject}sent {user} a follow request.</> };
      case "UserFollowed":
        return { icon: UserPlus, accent: "bg-emerald-500/10 text-emerald-300", action: <>{subject}started following {user}.</> };
      case "UserUnfollowed":
      case "FollowRequestCancelled":
        return { icon: UserPlus, accent: "bg-zinc-500/10 text-zinc-400", action: <>{subject}stopped following {user}.</> };
      case "PostCreated":
        return { icon: MessageSquareText, accent: "bg-violet-500/10 text-violet-300", action: <>{subject}shared a new post.</> };
      case "PostUpdated":
        return { icon: Pencil, accent: "bg-blue-500/10 text-blue-300", action: <>{subject}updated a post.</> };
      case "PostDeleted":
        return { icon: Trash2, accent: "bg-red-500/10 text-red-300", action: <>{subject}deleted a post.</> };
      case "PostLiked":
        return { icon: Heart, accent: "bg-rose-500/10 text-rose-300", action: <>{subject}liked {user}'s post.</> };
      case "PostUnliked":
        return { icon: Heart, accent: "bg-zinc-500/10 text-zinc-400", action: <>{subject}removed a like from a post.</> };
      case "PostCommented":
        return { icon: MessageCircle, accent: "bg-cyan-500/10 text-cyan-300", action: <>{subject}commented on {user}'s post.</> };
      case "PostCommentUpdated":
        return { icon: Pencil, accent: "bg-blue-500/10 text-blue-300", action: <>{subject}updated a post comment.</> };
      case "PostCommentDeleted":
        return { icon: Trash2, accent: "bg-red-500/10 text-red-300", action: <>{subject}deleted a post comment.</> };
      case "GameListCreated":
        return { icon: ListChecks, accent: "bg-violet-500/10 text-violet-300", action: <>{subject}created the list {list}.</> };
      case "GameListUpdated":
        return { icon: Pencil, accent: "bg-blue-500/10 text-blue-300", action: <>{subject}updated the list {list}.</> };
      case "GameListDeleted":
        return { icon: Trash2, accent: "bg-red-500/10 text-red-300", action: <>{subject}deleted the list {list}.</> };
      case "GameAddedToList":
        return { icon: ListChecks, accent: "bg-violet-500/10 text-violet-300", action: <>{subject}added {game} to {list}.</> };
      case "GameRemovedFromList":
        return { icon: Trash2, accent: "bg-red-500/10 text-red-300", action: <>{subject}removed {game} from {list}.</> };
      case "GameListGameReordered":
        return { icon: ListChecks, accent: "bg-amber-500/10 text-amber-300", action: <>{subject}reordered the games in {list}.</> };
      case "GameListLiked":
        return { icon: Heart, accent: "bg-rose-500/10 text-rose-300", action: <>{subject}liked the list {list}.</> };
      case "GameListUnliked":
        return { icon: Heart, accent: "bg-zinc-500/10 text-zinc-400", action: <>{subject}removed a like from {list}.</> };
      case "GameListCommented":
        return { icon: MessageCircle, accent: "bg-cyan-500/10 text-cyan-300", action: <>{subject}commented on the list {list}.</> };
      case "GameListCommentUpdated":
        return { icon: Pencil, accent: "bg-blue-500/10 text-blue-300", action: <>{subject}updated a list comment.</> };
      case "GameListCommentDeleted":
        return { icon: Trash2, accent: "bg-red-500/10 text-red-300", action: <>{subject}deleted a list comment.</> };
      default:
        return { icon: History, accent: "bg-zinc-500/10 text-zinc-400", action: <>{subject}recorded new activity.</> };
    }
  }

  switch (activity.type) {
    case "GameStatusChanged":
      return {
        icon: Gamepad2,
        accent: "bg-emerald-500/10 text-emerald-300",
        action: <>{game} oyununu “{activity.status ? statusLabels[activity.status] : "Status"}” vəziyyətinə {verb("keçirdin", "keçirdi")}.</>,
      };
    case "GameStatusRemoved":
      return {
        icon: Gamepad2,
        accent: "bg-zinc-500/10 text-zinc-400",
        action: <>{game} oyununun statusunu {verb("sildin", "sildi")}.</>,
      };
    case "GameAddedToFavorites":
      return { icon: Heart, accent: "bg-rose-500/10 text-rose-300", action: <>{game} oyununu favorilərə əlavə {verb("etdin", "etdi")}.</> };
    case "GameRemovedFromFavorites":
      return { icon: Heart, accent: "bg-zinc-500/10 text-zinc-400", action: <>{game} oyununu favorilərdən {verb("çıxardın", "çıxardı")}.</> };
    case "GameAddedToWishlist":
      return { icon: Bookmark, accent: "bg-cyan-500/10 text-cyan-300", action: <>{game} oyununu wishlist-ə əlavə {verb("etdin", "etdi")}.</> };
    case "GameRemovedFromWishlist":
      return { icon: Bookmark, accent: "bg-zinc-500/10 text-zinc-400", action: <>{game} oyununu wishlist-dən {verb("çıxardın", "çıxardı")}.</> };
    case "GameRated":
      return { icon: Star, accent: "bg-amber-500/10 text-amber-300", action: <>{game} oyununa {activity.rating ?? "—"} ulduz {verb("verdin", "verdi")}.</> };
    case "GameRatingRemoved":
      return { icon: Star, accent: "bg-zinc-500/10 text-zinc-400", action: <>{game} oyununun reytinqini {verb("sildin", "sildi")}.</> };
    case "ReviewCreated":
      return { icon: NotebookPen, accent: "bg-violet-500/10 text-violet-300", action: <>{game} oyununu log {verb("etdin", "etdi")}.</> };
    case "ReviewUpdated":
      return { icon: Pencil, accent: "bg-blue-500/10 text-blue-300", action: <>{game} oyununun logunu {verb("yenilədin", "yenilədi")}.</> };
    case "ReviewDeleted":
      return { icon: Trash2, accent: "bg-red-500/10 text-red-300", action: <>{game} oyununun rəyini {verb("sildin", "sildi")}.</> };
    case "ReviewLiked":
      return { icon: Heart, accent: "bg-rose-500/10 text-rose-300", action: <>{user} istifadəçisinin {game} rəyini {verb("bəyəndin", "bəyəndi")}.</> };
    case "ReviewUnliked":
      return { icon: Heart, accent: "bg-zinc-500/10 text-zinc-400", action: <>{game} rəyindən bəyənməni {verb("götürdün", "götürdü")}.</> };
    case "ReviewCommented":
      return { icon: MessageCircle, accent: "bg-cyan-500/10 text-cyan-300", action: <>{game} rəyinə şərh {verb("yazdın", "yazdı")}.</> };
    case "ReviewCommentUpdated":
      return { icon: Pencil, accent: "bg-blue-500/10 text-blue-300", action: <>Rəyə {isOwnActivity ? "yazdığın" : "yazdığı"} şərhi {verb("yenilədin", "yenilədi")}.</> };
    case "ReviewCommentDeleted":
      return { icon: Trash2, accent: "bg-red-500/10 text-red-300", action: <>Rəyə {isOwnActivity ? "yazdığın" : "yazdığı"} şərhi {verb("sildin", "sildi")}.</> };
    case "UserFollowRequested":
      return { icon: UserPlus, accent: "bg-cyan-500/10 text-cyan-300", action: <>{user} istifadəçisinə izləmə sorğusu {verb("göndərdin", "göndərdi")}.</> };
    case "UserFollowed":
      return { icon: UserPlus, accent: "bg-emerald-500/10 text-emerald-300", action: <>{user} istifadəçisini izləməyə {verb("başladın", "başladı")}.</> };
    case "UserUnfollowed":
    case "FollowRequestCancelled":
      return { icon: UserPlus, accent: "bg-zinc-500/10 text-zinc-400", action: <>{user} ilə izləmə əlaqəsini {verb("dayandırdın", "dayandırdı")}.</> };
    case "PostCreated":
      return { icon: MessageSquareText, accent: "bg-violet-500/10 text-violet-300", action: <>Yeni post {verb("paylaşdın", "paylaşdı")}.</> };
    case "PostUpdated":
      return { icon: Pencil, accent: "bg-blue-500/10 text-blue-300", action: <>Postunu {verb("yenilədin", "yenilədi")}.</> };
    case "PostDeleted":
      return { icon: Trash2, accent: "bg-red-500/10 text-red-300", action: <>Postunu {verb("sildin", "sildi")}.</> };
    case "PostLiked":
      return { icon: Heart, accent: "bg-rose-500/10 text-rose-300", action: <>{user} istifadəçisinin postunu {verb("bəyəndin", "bəyəndi")}.</> };
    case "PostUnliked":
      return { icon: Heart, accent: "bg-zinc-500/10 text-zinc-400", action: <>Postdan bəyənməni {verb("götürdün", "götürdü")}.</> };
    case "PostCommented":
      return { icon: MessageCircle, accent: "bg-cyan-500/10 text-cyan-300", action: <>{user} istifadəçisinin postuna şərh {verb("yazdın", "yazdı")}.</> };
    case "PostCommentUpdated":
      return { icon: Pencil, accent: "bg-blue-500/10 text-blue-300", action: <>Posta {isOwnActivity ? "yazdığın" : "yazdığı"} şərhi {verb("yenilədin", "yenilədi")}.</> };
    case "PostCommentDeleted":
      return { icon: Trash2, accent: "bg-red-500/10 text-red-300", action: <>Posta {isOwnActivity ? "yazdığın" : "yazdığı"} şərhi {verb("sildin", "sildi")}.</> };
    case "GameListCreated":
      return { icon: ListChecks, accent: "bg-violet-500/10 text-violet-300", action: <><strong className="text-zinc-200">{listName}</strong> adlı siyahı {verb("yaratdın", "yaratdı")}.</> };
    case "GameListUpdated":
      return { icon: Pencil, accent: "bg-blue-500/10 text-blue-300", action: <><strong className="text-zinc-200">{listName}</strong> siyahısını {verb("yenilədin", "yenilədi")}.</> };
    case "GameListDeleted":
      return { icon: Trash2, accent: "bg-red-500/10 text-red-300", action: <><strong className="text-zinc-200">{listName}</strong> siyahısını {verb("sildin", "sildi")}.</> };
    case "GameAddedToList":
      return { icon: ListChecks, accent: "bg-violet-500/10 text-violet-300", action: <>{game} oyununu <strong className="text-zinc-200">{listName}</strong> siyahısına əlavə {verb("etdin", "etdi")}.</> };
    case "GameRemovedFromList":
      return { icon: Trash2, accent: "bg-red-500/10 text-red-300", action: <>{game} oyununu <strong className="text-zinc-200">{listName}</strong> siyahısından {verb("çıxardın", "çıxardı")}.</> };
    case "GameListGameReordered":
      return { icon: ListChecks, accent: "bg-amber-500/10 text-amber-300", action: <><strong className="text-zinc-200">{listName}</strong> siyahısında oyunların sırasını {verb("dəyişdin", "dəyişdi")}.</> };
    case "GameListLiked":
      return { icon: Heart, accent: "bg-rose-500/10 text-rose-300", action: <><strong className="text-zinc-200">{listName}</strong> siyahısını {verb("bəyəndin", "bəyəndi")}.</> };
    case "GameListUnliked":
      return { icon: Heart, accent: "bg-zinc-500/10 text-zinc-400", action: <><strong className="text-zinc-200">{listName}</strong> siyahısından bəyənməni {verb("götürdün", "götürdü")}.</> };
    case "GameListCommented":
      return { icon: MessageCircle, accent: "bg-cyan-500/10 text-cyan-300", action: <><strong className="text-zinc-200">{listName}</strong> siyahısına şərh {verb("yazdın", "yazdı")}.</> };
    case "GameListCommentUpdated":
      return { icon: Pencil, accent: "bg-blue-500/10 text-blue-300", action: <>Siyahıya {isOwnActivity ? "yazdığın" : "yazdığı"} şərhi {verb("yenilədin", "yenilədi")}.</> };
    case "GameListCommentDeleted":
      return { icon: Trash2, accent: "bg-red-500/10 text-red-300", action: <>Siyahıya {isOwnActivity ? "yazdığın" : "yazdığı"} şərhi {verb("sildin", "sildi")}.</> };
    default:
      return { icon: History, accent: "bg-zinc-500/10 text-zinc-400", action: <>Yeni fəaliyyət {verb("göstərdin", "göstərdi")}.</> };
  }
}

export function ProfileActivityFeed({
  result,
  isLoading,
  isError,
  error,
  page,
  category,
  scope,
  currentUserId,
  onCategoryChange,
  onScopeChange,
  onPageChange,
}: ProfileActivityFeedProps) {
  const { language } = useI18n();
  const activities = result?.items ?? [];
  const localizedFilters = filters.map((filter) => ({
    ...filter,
    label:
      language === "en"
        ? {
            All: "All",
            Statuses: "Statuses",
            Lists: "Lists",
            Ratings: "Ratings",
            Reviews: "Logs",
            Posts: "Posts",
          }[filter.value]
        : filter.label,
  }));

  return (
    <div className="mx-auto max-w-4xl">
      <section className="mb-5 rounded-2xl border border-white/[0.07] bg-[#101015] p-4">
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h2 className="font-semibold text-zinc-100">
                {language === "en" ? "Community activity" : "İcma axını"}
              </h2>
              <p className="mt-1 text-xs text-zinc-600">
                {scope === "Mine"
                  ? language === "en"
                    ? "Only your activity, from newest to oldest."
                    : "Yalnız sənin fəaliyyətin, ən yenidən köhnəyə."
                  : language === "en"
                    ? "Activity from people you follow, from newest to oldest."
                    : "Dostlarının fəaliyyəti, ən yenidən köhnəyə."}
              </p>
            </div>

            <label className="flex shrink-0 items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.03] px-3 py-2">
              <span className="text-xs font-semibold text-zinc-500">
                {language === "en" ? "Showing:" : "Göstərilir:"}
              </span>
              <select
                value={scope}
                onChange={(event) => {
                  onScopeChange(event.target.value as UserActivityFeedScope);
                }}
                className="bg-transparent text-xs font-bold text-zinc-200 outline-none"
              >
                <option value="Mine" className="bg-[#12121a]">
                  {language === "en" ? "Me" : "Özüm"}
                </option>
                <option value="Following" className="bg-[#12121a]">
                  {language === "en" ? "Following" : "Dostlarım"}
                </option>
              </select>
            </label>
          </div>

          <div className="flex gap-2 overflow-x-auto pb-1">
            {localizedFilters.map((filter) => (
              <button
                key={filter.value}
                type="button"
                onClick={() => onCategoryChange(filter.value)}
                className={`shrink-0 rounded-xl px-3 py-2 text-xs font-semibold transition ${
                  category === filter.value
                    ? "bg-violet-500/15 text-violet-200"
                    : "bg-white/[0.035] text-zinc-500 hover:text-zinc-200"
                }`}
              >
                {filter.label}
              </button>
            ))}
          </div>
        </div>
      </section>

      {isLoading ? (
        <div className="flex items-center justify-center gap-3 py-16 text-sm text-zinc-500">
          <LoaderCircle size={19} className="animate-spin" />
          {language === "en" ? "Loading activity..." : "Axın yüklənir..."}
        </div>
      ) : isError ? (
        <div className="rounded-3xl border border-red-400/15 bg-red-500/[0.05] p-8 text-center text-sm text-red-100/70">
          {getApiErrorMessage(
            error,
            language === "en"
              ? "The community activity could not be loaded."
              : "İcma axınını yükləmək mümkün olmadı.",
          )}
        </div>
      ) : activities.length === 0 ? (
        <div className="rounded-3xl border border-dashed border-white/[0.08] p-12 text-center">
          <History size={31} className="mx-auto text-zinc-700" />
          <p className="mt-4 text-sm text-zinc-500">
            {language === "en"
              ? "There is no activity for this filter yet."
              : "Bu filtr üzrə hələ fəaliyyət yoxdur."}
          </p>
        </div>
      ) : (
        <div className="space-y-3">
          {activities.map((activity) => {
            const isOwnActivity = activity.actorUserId === currentUserId;
            const presentation = getPresentation(
              activity,
              isOwnActivity,
              language,
            );
            const Icon = presentation.icon;
            const coverUrl = resolveAssetUrl(activity.gameCoverImageUrl);
            const actorImageUrl = resolveAssetUrl(activity.actorUserProfileImageUrl);

            return (
              <article
                key={activity.id}
                className="flex items-center gap-4 rounded-2xl border border-white/[0.07] bg-[#101015] p-4 sm:p-5"
              >
                <Link
                  to={`/users/${activity.actorUserName}`}
                  className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-violet-500/10 font-bold text-violet-300"
                >
                  {actorImageUrl ? (
                    <img
                      src={actorImageUrl}
                      alt={
                        language === "en"
                          ? `${activity.actorUserName}'s profile picture`
                          : `${activity.actorUserName} profil şəkli`
                      }
                      className="h-full w-full object-cover"
                    />
                  ) : (
                    activity.actorUserName.charAt(0).toUpperCase()
                  )}
                </Link>

                <span className={`hidden h-10 w-10 shrink-0 items-center justify-center rounded-xl sm:flex ${presentation.accent}`}>
                  <Icon size={18} />
                </span>

                <div className="min-w-0 flex-1">
                  <p className="text-sm leading-6 text-zinc-400">
                    {!isOwnActivity && (
                      <>
                        <ActorLink activity={activity} currentUserId={currentUserId} />{" "}
                      </>
                    )}
                    {presentation.action}
                  </p>
                  <p className="mt-1.5 text-xs text-zinc-600">
                    {formatDateTime(activity.createdAt)}
                  </p>
                </div>

                {coverUrl && activity.gameSource && activity.gameId !== null && (
                  <Link
                    to={`/games/${activity.gameSource}/${activity.gameId}`}
                    className="hidden h-16 w-12 shrink-0 overflow-hidden rounded-lg bg-zinc-900 sm:block"
                  >
                    <img
                      src={coverUrl}
                      alt={
                        language === "en"
                          ? `${activity.gameName ?? "Game"} cover`
                          : `${activity.gameName ?? "Oyun"} örtüyü`
                      }
                      className="h-full w-full object-cover"
                    />
                  </Link>
                )}
              </article>
            );
          })}
        </div>
      )}

      {(result?.totalPages ?? 0) > 1 && (
        <div className="mt-8 flex items-center justify-center gap-3">
          <button
            type="button"
            onClick={() => onPageChange(page - 1)}
            disabled={page <= 1}
            className="flex h-10 items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.03] px-4 text-sm font-semibold text-zinc-400 transition hover:bg-white/[0.07] hover:text-white disabled:opacity-40"
          >
            <ChevronLeft size={16} />{" "}
            {language === "en" ? "Previous" : "Əvvəlki"}
          </button>
          <span className="min-w-24 text-center text-xs font-semibold text-zinc-500">
            {page} / {result?.totalPages}
          </span>
          <button
            type="button"
            onClick={() => onPageChange(page + 1)}
            disabled={page >= (result?.totalPages ?? 1)}
            className="flex h-10 items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.03] px-4 text-sm font-semibold text-zinc-400 transition hover:bg-white/[0.07] hover:text-white disabled:opacity-40"
          >
            {language === "en" ? "Next" : "Növbəti"}{" "}
            <ChevronRight size={16} />
          </button>
        </div>
      )}
    </div>
  );
}
