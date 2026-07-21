import {
  ArrowUpRight,
  CalendarClock,
  CalendarDays,
  Flame,
  Gamepad2,
} from "lucide-react";
import { useState } from "react";
import { Link } from "react-router-dom";
import { resolveAssetUrl } from "../../lib/helpers";
import type { GameListItemDTO } from "../../types/home";

const releaseDateFormatter = new Intl.DateTimeFormat(
  "az-AZ",
  {
    day: "2-digit",
    month: "short",
    year: "numeric",
  },
);

interface DiscoveryGameCardProps {
  game: GameListItemDTO;
  index: number;
  variant: "trending" | "comingSoon";
}

function getReleaseDateValue(
  releaseDate: string | null,
): Date | null {
  if (!releaseDate) {
    return null;
  }

  const value = new Date(`${releaseDate}T00:00:00`);

  return Number.isNaN(value.getTime()) ? null : value;
}

function getComingSoonLabel(
  releaseDate: string | null,
): string {
  const value = getReleaseDateValue(releaseDate);

  if (!value) {
    return "Tezliklə";
  }

  const today = new Date();
  today.setHours(0, 0, 0, 0);

  const remainingDays = Math.max(
    0,
    Math.ceil(
      (value.getTime() - today.getTime()) /
        (1000 * 60 * 60 * 24),
    ),
  );

  if (remainingDays === 0) {
    return "Bu gün";
  }

  if (remainingDays === 1) {
    return "Sabah";
  }

  return `${remainingDays} gün qalıb`;
}

function getReleaseMetadata(
  game: GameListItemDTO,
  variant: DiscoveryGameCardProps["variant"],
): string {
  if (variant === "comingSoon") {
    const releaseDate = getReleaseDateValue(
      game.releaseDate,
    );

    return releaseDate
      ? releaseDateFormatter.format(releaseDate)
      : "Tarix açıqlanmayıb";
  }

  const primaryPlatform = game.platforms[0]?.name;

  if (primaryPlatform) {
    return primaryPlatform;
  }

  const releaseDate = getReleaseDateValue(
    game.releaseDate,
  );

  return releaseDate
    ? String(releaseDate.getFullYear())
    : "Platforma qeyd edilməyib";
}

function DiscoveryGameCard({
  game,
  index,
  variant,
}: DiscoveryGameCardProps) {
  const coverUrl = resolveAssetUrl(
    game.coverImageUrl,
  );

  const [failedCoverUrl, setFailedCoverUrl] =
    useState<string | null>(null);

  const shouldShowCover =
    coverUrl !== null &&
    coverUrl !== failedCoverUrl;

  const isTrending = variant === "trending";

  return (
    <Link
      to={`/games/${game.source}/${game.id}`}
      className="home-discovery-game-card group block min-w-0"
    >
      <article className="overflow-hidden rounded-2xl border border-white/[0.07] bg-[#111116] transition duration-300 hover:-translate-y-1 hover:border-violet-400/30 hover:shadow-2xl hover:shadow-violet-950/20">
        <div className="relative aspect-[3/4] overflow-hidden bg-gradient-to-br from-zinc-800 to-zinc-950">
          <span
            className={`absolute left-2.5 top-2.5 z-10 inline-flex items-center gap-1 rounded-lg border px-2 py-1 text-[10px] font-bold backdrop-blur-md ${
              isTrending
                ? "border-orange-300/20 bg-orange-500/15 text-orange-200"
                : "border-violet-300/20 bg-violet-500/20 text-violet-100"
            }`}
          >
            {isTrending ? (
              <>
                <Flame size={11} />
                #{index + 1}
              </>
            ) : (
              <>
                <CalendarClock size={11} />
                {getComingSoonLabel(game.releaseDate)}
              </>
            )}
          </span>

          {shouldShowCover ? (
            <img
              src={coverUrl}
              alt={`${game.title} oyun örtüyü`}
              loading="lazy"
              className="h-full w-full object-cover transition duration-500 group-hover:scale-105"
              onError={() => {
                setFailedCoverUrl(coverUrl);
              }}
            />
          ) : (
            <div className="flex h-full flex-col items-center justify-center gap-3 px-4 text-center text-zinc-600">
              <Gamepad2 size={30} />

              <span className="line-clamp-2 text-xs font-medium">
                {game.title}
              </span>
            </div>
          )}

          <div className="pointer-events-none absolute inset-0 bg-gradient-to-t from-[#09090c]/85 via-transparent to-transparent" />

          <ArrowUpRight
            size={17}
            className="absolute bottom-3 right-3 text-white/0 transition duration-300 group-hover:text-white"
          />
        </div>

        <div className="p-3.5">
          <h3
            className="line-clamp-2 min-h-10 text-sm font-semibold leading-5 text-zinc-100"
            title={game.title}
          >
            {game.title}
          </h3>

          <div className="mt-2 flex min-w-0 items-center gap-1.5 text-[11px] text-zinc-500">
            <CalendarDays
              size={12}
              className="shrink-0"
            />

            <span className="truncate">
              {getReleaseMetadata(game, variant)}
            </span>
          </div>
        </div>
      </article>
    </Link>
  );
}

function DiscoveryRailSkeleton() {
  return (
    <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-6">
      {Array.from({ length: 6 }).map((_, index) => (
        <div
          key={index}
          className="overflow-hidden rounded-2xl border border-white/[0.06] bg-[#111116]"
        >
          <div className="aspect-[3/4] animate-pulse bg-white/[0.04]" />

          <div className="space-y-2 p-3.5">
            <div className="h-4 animate-pulse rounded bg-white/[0.05]" />
            <div className="h-3 w-2/3 animate-pulse rounded bg-white/[0.035]" />
          </div>
        </div>
      ))}
    </div>
  );
}

interface DiscoveryRailProps {
  title: string;
  eyebrow: string;
  description: string;
  games: GameListItemDTO[];
  variant: DiscoveryGameCardProps["variant"];
  isLoading: boolean;
}

function DiscoveryRail({
  title,
  eyebrow,
  description,
  games,
  variant,
  isLoading,
}: DiscoveryRailProps) {
  return (
    <section className="home-discovery-rail">
      <div className="mb-6 flex items-end justify-between gap-4">
        <div>
          <p
            className={`text-xs font-bold uppercase tracking-[0.22em] ${
              variant === "trending"
                ? "text-orange-300"
                : "text-violet-400"
            }`}
          >
            {eyebrow}
          </p>

          <h2 className="mt-2 text-2xl font-bold tracking-tight text-white sm:text-3xl">
            {title}
          </h2>

          <p className="mt-2 max-w-2xl text-sm leading-6 text-zinc-500">
            {description}
          </p>
        </div>
      </div>

      {isLoading ? (
        <DiscoveryRailSkeleton />
      ) : games.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-white/10 bg-white/[0.02] px-5 py-8 text-center text-sm text-zinc-500">
          Bu bölmə üçün oyun tapılmadı.
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-6">
          {games.map((game, index) => (
            <DiscoveryGameCard
              key={`${variant}-${game.source}-${game.id}`}
              game={game}
              index={index}
              variant={variant}
            />
          ))}
        </div>
      )}
    </section>
  );
}

interface HomeDiscoverySectionProps {
  trendingGames: GameListItemDTO[];
  comingSoonGames: GameListItemDTO[];
  isLoading: boolean;
  isError: boolean;
}

export function HomeDiscoverySection({
  trendingGames,
  comingSoonGames,
  isLoading,
  isError,
}: HomeDiscoverySectionProps) {
  return (
    <section className="home-discovery-section border-b border-white/5 bg-white/[0.01]">
      <div className="mx-auto max-w-7xl space-y-14 px-5 py-16 lg:px-8 lg:py-20">
        {isError &&
        trendingGames.length === 0 &&
        comingSoonGames.length === 0 ? (
          <div className="rounded-2xl border border-amber-300/10 bg-amber-500/[0.04] px-5 py-6 text-center text-sm text-zinc-500">
            Trend və yaxınlaşan oyun məlumatları hazırda
            əlçatan deyil. Səhifənin digər hissələri normal
            işləməyə davam edir.
          </div>
        ) : (
          <>
            <DiscoveryRail
              eyebrow="İndi populyar"
              title="Trend oyunlar"
              description="IGDB, Steam və Twitch aktivlik göstəricilərinin birləşdirilmiş trend sıralaması."
              games={trendingGames}
              variant="trending"
              isLoading={isLoading}
            />

            <DiscoveryRail
              eyebrow="Buraxılışa hazırlaş"
              title="Tezliklə çıxacaq"
              description="Növbəti bir il ərzində çıxacaq və oyunçular tərəfindən ən çox gözlənilən oyunlar."
              games={comingSoonGames}
              variant="comingSoon"
              isLoading={isLoading}
            />
          </>
        )}
      </div>
    </section>
  );
}
