import {
  Ban,
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
  Pencil,
  Star,
  Trash2,
  UserMinus,
  UserPlus,
} from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { getApiErrorMessage } from "../../lib/api";
import {
  formatDateTime,
  resolveAssetUrl,
} from "../../lib/helpers";
import type {
  UserActivitiesResultDTO,
  UserActivityDTO,
} from "../../types/profile";

interface ProfileActivityJournalProps {
  result: UserActivitiesResultDTO | undefined;
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  page: number;
  onPageChange: (page: number) => void;
}

const statusLabels = {
  Played: "Oynayıb",
  Playing: "Oynayır",
  PlanToPlay: "Oynayacaq",
  Dropped: "Yarım qoyub",
} as const;

function GameLink({
  activity,
}: {
  activity: UserActivityDTO;
}) {
  if (
    !activity.gameSource ||
    activity.gameId === null
  ) {
    return (
      <span className="font-semibold text-zinc-200">
        oyun
      </span>
    );
  }

  return (
    <Link
      to={`/games/${activity.gameSource}/${activity.gameId}`}
      className="font-semibold text-violet-300 transition hover:text-violet-200"
    >
      {activity.gameName ?? "Oyun"}
    </Link>
  );
}

function UserLink({
  activity,
}: {
  activity: UserActivityDTO;
}) {
  if (!activity.targetUserName) {
    return (
      <span className="font-semibold text-zinc-200">
        istifadəçi
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

function getActivityPresentation(
  activity: UserActivityDTO,
): {
  icon: typeof History;
  content: ReactNode;
  accent: string;
} {
  const game = <GameLink activity={activity} />;
  const user = <UserLink activity={activity} />;
  const listName = activity.subjectTitle ?? "oyun siyahısı";

  switch (activity.type) {
    case "GameStatusChanged":
      return {
        icon: Gamepad2,
        content: (
          <>
            {game} oyununu{" "}
            <span className="font-semibold text-zinc-200">
              {activity.status
                ? statusLabels[activity.status]
                : "yeni"}
            </span>{" "}
            statusuna keçirdi.
          </>
        ),
        accent: "bg-sky-500/10 text-sky-300",
      };
    case "GameStatusRemoved":
      return {
        icon: Gamepad2,
        content: <>{game} oyununun statusunu sildi.</>,
        accent: "bg-zinc-500/10 text-zinc-400",
      };
    case "GameAddedToFavorites":
      return {
        icon: Heart,
        content: <>{game} oyununu favorilərə əlavə etdi.</>,
        accent: "bg-rose-500/10 text-rose-300",
      };
    case "GameRemovedFromFavorites":
      return {
        icon: Heart,
        content: <>{game} oyununu favorilərdən çıxardı.</>,
        accent: "bg-zinc-500/10 text-zinc-400",
      };
    case "GameAddedToWishlist":
      return {
        icon: Bookmark,
        content: <>{game} oyununu wishlist-ə əlavə etdi.</>,
        accent: "bg-violet-500/10 text-violet-300",
      };
    case "GameRemovedFromWishlist":
      return {
        icon: Bookmark,
        content: <>{game} oyununu wishlist-dən çıxardı.</>,
        accent: "bg-zinc-500/10 text-zinc-400",
      };
    case "GameRated":
      return {
        icon: Star,
        content: (
          <>
            {game} oyununa{" "}
            <span className="font-semibold text-amber-300">
              {activity.rating?.toFixed(1)}
            </span>{" "}
            puan verdi.
          </>
        ),
        accent: "bg-amber-500/10 text-amber-300",
      };
    case "GameRatingRemoved":
      return {
        icon: Star,
        content: <>{game} oyununa verdiyi puanı sildi.</>,
        accent: "bg-zinc-500/10 text-zinc-400",
      };
    case "ReviewCreated":
      return {
        icon: MessageSquareText,
        content: <>{game} oyunu üçün rəy yazdı.</>,
        accent: "bg-emerald-500/10 text-emerald-300",
      };
    case "ReviewUpdated":
      return {
        icon: Pencil,
        content: <>{game} oyunundakı rəyini yenilədi.</>,
        accent: "bg-blue-500/10 text-blue-300",
      };
    case "ReviewDeleted":
      return {
        icon: Trash2,
        content: <>{game} oyunundakı rəyini sildi.</>,
        accent: "bg-red-500/10 text-red-300",
      };
    case "ReviewLiked":
      return {
        icon: Heart,
        content: (
          <>
            {user} tərəfindən {game} oyunu üçün yazılan rəyi
            bəyəndi.
          </>
        ),
        accent: "bg-rose-500/10 text-rose-300",
      };
    case "ReviewUnliked":
      return {
        icon: Heart,
        content: (
          <>
            {user} tərəfindən {game} oyunu üçün yazılan rəydən
            bəyənməni götürdü.
          </>
        ),
        accent: "bg-zinc-500/10 text-zinc-400",
      };
    case "ReviewCommented":
      return {
        icon: MessageCircle,
        content: (
          <>
            {user} tərəfindən {game} oyunu üçün yazılan rəyə
            şərh yazdı.
          </>
        ),
        accent: "bg-cyan-500/10 text-cyan-300",
      };
    case "ReviewCommentUpdated":
      return {
        icon: Pencil,
        content: <>{game} oyunundakı rəyə yazdığı şərhi yenilədi.</>,
        accent: "bg-blue-500/10 text-blue-300",
      };
    case "ReviewCommentDeleted":
      return {
        icon: Trash2,
        content: <>{game} oyunundakı rəyə yazdığı şərhi sildi.</>,
        accent: "bg-red-500/10 text-red-300",
      };
    case "UserFollowRequested":
      return {
        icon: UserPlus,
        content: <>{user} istifadəçisinə izləmə sorğusu göndərdi.</>,
        accent: "bg-violet-500/10 text-violet-300",
      };
    case "UserFollowed":
      return {
        icon: UserPlus,
        content: <>{user} istifadəçisini izləməyə başladı.</>,
        accent: "bg-emerald-500/10 text-emerald-300",
      };
    case "UserUnfollowed":
      return {
        icon: UserMinus,
        content: <>{user} istifadəçisini izləməyi dayandırdı.</>,
        accent: "bg-zinc-500/10 text-zinc-400",
      };
    case "FollowRequestCancelled":
      return {
        icon: UserMinus,
        content: <>{user} üçün izləmə sorğusunu ləğv etdi.</>,
        accent: "bg-zinc-500/10 text-zinc-400",
      };
    case "UserBlocked":
      return {
        icon: Ban,
        content: <>{user} istifadəçisini blokladı.</>,
        accent: "bg-red-500/10 text-red-300",
      };
    case "UserUnblocked":
      return {
        icon: Ban,
        content: <>{user} istifadəçisinin blokunu açdı.</>,
        accent: "bg-zinc-500/10 text-zinc-400",
      };
    case "PostCreated":
      return {
        icon: MessageSquareText,
        content: <>Yeni post paylaşdı.</>,
        accent: "bg-violet-500/10 text-violet-300",
      };
    case "PostUpdated":
      return {
        icon: Pencil,
        content: <>Postunu yenilədi.</>,
        accent: "bg-blue-500/10 text-blue-300",
      };
    case "PostDeleted":
      return {
        icon: Trash2,
        content: <>Postunu sildi.</>,
        accent: "bg-red-500/10 text-red-300",
      };
    case "GameListCreated":
      return {
        icon: ListChecks,
        content: (
          <>
            <span className="font-semibold text-zinc-200">
              {listName}
            </span>{" "}
            adlı siyahı yaratdı.
          </>
        ),
        accent: "bg-violet-500/10 text-violet-300",
      };
    case "GameListUpdated":
      return {
        icon: Pencil,
        content: (
          <>
            <span className="font-semibold text-zinc-200">
              {listName}
            </span>{" "}
            siyahısını yenilədi.
          </>
        ),
        accent: "bg-blue-500/10 text-blue-300",
      };
    case "GameListDeleted":
      return {
        icon: Trash2,
        content: (
          <>
            <span className="font-semibold text-zinc-200">
              {listName}
            </span>{" "}
            siyahısını sildi.
          </>
        ),
        accent: "bg-red-500/10 text-red-300",
      };
    default:
      return {
        icon: History,
        content: <>Yeni hərəkət etdi.</>,
        accent: "bg-zinc-500/10 text-zinc-400",
      };
  }
}

export function ProfileActivityJournal({
  result,
  isLoading,
  isError,
  error,
  page,
  onPageChange,
}: ProfileActivityJournalProps) {
  if (isLoading) {
    return (
      <div className="flex items-center justify-center gap-3 py-16 text-sm text-zinc-500">
        <LoaderCircle size={19} className="animate-spin" />
        Jurnal yüklənir...
      </div>
    );
  }

  if (isError) {
    return (
      <div className="rounded-3xl border border-red-400/15 bg-red-500/[0.05] p-8 text-center text-sm text-red-100/70">
        {getApiErrorMessage(
          error,
          "Jurnalı yükləmək mümkün olmadı.",
        )}
      </div>
    );
  }

  const activities = result?.items ?? [];

  if (activities.length === 0) {
    return (
      <div className="rounded-3xl border border-dashed border-white/[0.08] p-12 text-center">
        <History
          size={31}
          className="mx-auto text-zinc-700"
        />
        <p className="mt-4 text-sm text-zinc-500">
          Jurnalda hələ heç bir hərəkət yoxdur.
        </p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-4xl">
      <div className="space-y-3">
        {activities.map((activity) => {
          const presentation =
            getActivityPresentation(activity);
          const Icon = presentation.icon;
          const coverUrl = resolveAssetUrl(
            activity.gameCoverImageUrl,
          );

          return (
            <article
              key={activity.id}
              className="flex items-center gap-4 rounded-2xl border border-white/[0.07] bg-[#101015] p-4 sm:p-5"
            >
              <span
                className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl ${presentation.accent}`}
              >
                <Icon size={19} />
              </span>

              <div className="min-w-0 flex-1">
                <p className="text-sm leading-6 text-zinc-400">
                  {presentation.content}
                </p>
                <p className="mt-1.5 text-xs text-zinc-600">
                  {formatDateTime(activity.createdAt)}
                </p>
              </div>

              {coverUrl &&
                activity.gameSource &&
                activity.gameId !== null && (
                  <Link
                    to={`/games/${activity.gameSource}/${activity.gameId}`}
                    className="hidden h-16 w-12 shrink-0 overflow-hidden rounded-lg bg-zinc-900 sm:block"
                  >
                    <img
                      src={coverUrl}
                      alt={`${activity.gameName ?? "Oyun"} örtüyü`}
                      className="h-full w-full object-cover"
                    />
                  </Link>
                )}
            </article>
          );
        })}
      </div>

      {(result?.totalPages ?? 0) > 1 && (
        <div className="mt-8 flex items-center justify-center gap-3">
          <button
            type="button"
            onClick={() => {
              onPageChange(page - 1);
            }}
            disabled={page <= 1}
            className="flex h-10 items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.03] px-4 text-sm font-semibold text-zinc-400 transition hover:bg-white/[0.07] hover:text-white disabled:cursor-not-allowed disabled:opacity-40"
          >
            <ChevronLeft size={16} />
            Əvvəlki
          </button>

          <span className="min-w-24 text-center text-xs font-semibold text-zinc-500">
            {page} / {result?.totalPages}
          </span>

          <button
            type="button"
            onClick={() => {
              onPageChange(page + 1);
            }}
            disabled={
              page >= (result?.totalPages ?? 1)
            }
            className="flex h-10 items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.03] px-4 text-sm font-semibold text-zinc-400 transition hover:bg-white/[0.07] hover:text-white disabled:cursor-not-allowed disabled:opacity-40"
          >
            Növbəti
            <ChevronRight size={16} />
          </button>
        </div>
      )}
    </div>
  );
}
