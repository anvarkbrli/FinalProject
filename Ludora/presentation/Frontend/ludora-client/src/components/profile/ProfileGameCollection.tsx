import {
  Bookmark,
  ChevronLeft,
  ChevronRight,
  Gamepad2,
  Heart,
  LoaderCircle,
  LockKeyhole,
  Star,
} from "lucide-react";
import { useState } from "react";
import { Link } from "react-router-dom";
import { getApiErrorMessage } from "../../lib/api";
import {
  getReleaseYear,
  resolveAssetUrl,
} from "../../lib/helpers";
import type { PagedResultDTO } from "../../types/home";
import type { UserGameDTO } from "../../types/userGames";

interface ProfileGameCollectionProps {
  kind: "wishlist" | "favorites";
  result: PagedResultDTO<UserGameDTO> | undefined;
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

function GameCover({
  game,
  showRating,
}: {
  game: UserGameDTO;
  showRating: boolean;
}) {
  const coverUrl = resolveAssetUrl(game.coverUrl);
  const [failedUrl, setFailedUrl] =
    useState<string | null>(null);

  return (
    <div className="relative aspect-[3/4] overflow-hidden bg-gradient-to-br from-zinc-800 to-zinc-950">
      {coverUrl && coverUrl !== failedUrl ? (
        <img
          src={coverUrl}
          alt={`${game.name} örtüyü`}
          className="h-full w-full object-cover transition duration-500 group-hover:scale-105"
          loading="lazy"
          onError={() => {
            setFailedUrl(coverUrl);
          }}
        />
      ) : (
        <div className="flex h-full flex-col items-center justify-center gap-3 px-4 text-center text-zinc-700">
          <Gamepad2 size={30} />
          <span className="line-clamp-2 text-xs font-semibold">
            {game.name}
          </span>
        </div>
      )}

      <div className="pointer-events-none absolute inset-0 bg-gradient-to-t from-[#09090c]/80 via-transparent to-transparent" />

      {showRating && game.rating !== null && (
        <span className="absolute right-2 top-2 flex items-center gap-1 rounded-lg border border-amber-300/15 bg-black/70 px-2 py-1 text-[11px] font-bold text-amber-300 backdrop-blur-sm">
          <Star size={11} fill="currentColor" />
          {game.rating.toFixed(1)}
        </span>
      )}
    </div>
  );
}

export function ProfileGameCollection({
  kind,
  result,
  isLoading,
  isError,
  error,
  page,
  onPageChange,
}: ProfileGameCollectionProps) {
  const Icon = kind === "wishlist" ? Bookmark : Heart;
  const title = kind === "wishlist" ? "Wishlist" : "Favorilər";
  const emptyMessage =
    kind === "wishlist"
      ? "Wishlist-ə əlavə edilmiş oyun yoxdur."
      : "Favoriyə əlavə edilmiş oyun yoxdur.";

  if (isLoading) {
    return (
      <div className="flex items-center justify-center gap-3 py-16 text-sm text-zinc-500">
        <LoaderCircle size={19} className="animate-spin" />
        {title} yüklənir...
      </div>
    );
  }

  if (isError) {
    return (
      <div className="rounded-3xl border border-amber-400/15 bg-amber-500/[0.05] p-8 text-center">
        <LockKeyhole
          size={30}
          className="mx-auto text-amber-300"
        />
        <p className="mt-4 text-sm leading-6 text-amber-100/70">
          {getApiErrorMessage(
            error,
            `Bu istifadəçinin ${title.toLocaleLowerCase("az-AZ")} hissəsinə baxmaq mümkün deyil.`,
          )}
        </p>
      </div>
    );
  }

  const games = result?.items ?? [];

  if (games.length === 0) {
    return (
      <div className="rounded-3xl border border-dashed border-white/[0.08] p-12 text-center">
        <Icon
          size={31}
          className="mx-auto text-zinc-700"
        />
        <p className="mt-4 text-sm text-zinc-500">
          {emptyMessage}
        </p>
      </div>
    );
  }

  return (
    <div>
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6">
        {games.map((game) => (
          <Link
            key={`${game.source}-${game.gameId}`}
            to={`/games/${game.source}/${game.gameId}`}
            className="group overflow-hidden rounded-2xl border border-white/[0.07] bg-[#101015] transition duration-300 hover:-translate-y-1 hover:border-violet-400/30 hover:shadow-xl hover:shadow-violet-950/20"
          >
            <GameCover game={game} showRating={kind !== "favorites"} />

            <div className="p-3.5">
              <h2
                className="line-clamp-2 min-h-10 text-sm font-semibold leading-5 text-zinc-100"
                title={game.name}
              >
                {game.name}
              </h2>

              <div className="mt-3 flex flex-wrap items-center gap-1.5 text-[10px] font-semibold">
                <span className="rounded-md bg-white/[0.05] px-2 py-1 text-zinc-500">
                  {getReleaseYear(game.releaseDate)}
                </span>

                {kind !== "favorites" && game.status && (
                  <span className="rounded-md bg-violet-500/10 px-2 py-1 text-violet-300">
                    {statusLabels[game.status]}
                  </span>
                )}
              </div>
            </div>
          </Link>
        ))}
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
