import {
  CalendarClock,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  Flame,
  Gamepad2,
} from "lucide-react";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/useI18n";
import { resolveAssetUrl } from "../../lib/helpers";
import type {
  GameListItemDTO,
  HomeComingSoonGameDTO,
} from "../../types/home";

type DiscoveryGame =
  | GameListItemDTO
  | HomeComingSoonGameDTO;

interface DiscoveryGameCardProps {
  game: DiscoveryGame;
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

function formatReleaseDate(value: Date): string {
  const day = String(value.getDate()).padStart(2, "0");
  const month = String(value.getMonth() + 1).padStart(
    2,
    "0",
  );
  const year = value.getFullYear();

  return `${day}.${month}.${year}`;
}

function getComingSoonLabel(
  releaseDate: string | null,
  language: "az" | "en",
): string {
  const value = getReleaseDateValue(releaseDate);

  if (!value) {
    return language === "en" ? "Soon" : "Tezliklə";
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
    return language === "en" ? "Today" : "Bu gün";
  }

  if (remainingDays === 1) {
    return language === "en" ? "1 day left" : "Sabah";
  }

  return language === "en"
    ? `${remainingDays} days left`
    : `${remainingDays} gün qalıb`;
}

const retiredOrNichePlatformPatterns = [
  "google stadia",
  "amazon fire tv",
  "fire os",
  "ouya",
  "onlive game system",
];

function getPlatformPriority(platformName: string): number {
  const name = platformName.trim().toLocaleLowerCase("en");

  if (
    retiredOrNichePlatformPatterns.some((pattern) =>
      name.includes(pattern),
    )
  ) {
    return -1;
  }

  if (
    name.includes("microsoft windows") ||
    name === "pc" ||
    name.startsWith("pc ")
  ) {
    return 1_000;
  }

  if (name.includes("playstation 5")) {
    return 900;
  }

  if (name.includes("playstation 4")) {
    return 850;
  }

  if (name.includes("xbox series")) {
    return 800;
  }

  if (name.includes("xbox one")) {
    return 750;
  }

  if (name.includes("nintendo switch 2")) {
    return 700;
  }

  if (name.includes("nintendo switch")) {
    return 680;
  }

  if (name === "android") {
    return 600;
  }

  if (name === "ios" || name.includes("iphone")) {
    return 580;
  }

  if (name.includes("playstation 3")) {
    return 450;
  }

  if (name.includes("xbox 360")) {
    return 430;
  }

  if (name.includes("mac")) {
    return 200;
  }

  if (name.includes("linux")) {
    return 150;
  }

  return 300;
}

function getPrimaryPlatform(
  platforms: GameListItemDTO["platforms"],
): string | undefined {
  return platforms
    .map((platform, index) => ({
      name: platform.name.trim(),
      priority: getPlatformPriority(platform.name),
      index,
    }))
    .filter(
      (platform) =>
        platform.name.length > 0 &&
        platform.priority >= 0,
    )
    .sort(
      (left, right) =>
        right.priority - left.priority ||
        left.index - right.index,
    )[0]?.name;
}

function getReleaseMetadata(
  game: DiscoveryGame,
  variant: DiscoveryGameCardProps["variant"],
): string {
  if (variant === "comingSoon") {
    const releaseDate = getReleaseDateValue(
      game.releaseDate,
    );

    return releaseDate
      ? formatReleaseDate(releaseDate)
      : "Tarix açıqlanmayıb";
  }

  const primaryPlatform =
    "platforms" in game
      ? getPrimaryPlatform(game.platforms)
      : undefined;

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
  const { language } = useI18n();
  const coverUrl = resolveAssetUrl(
    game.coverImageUrl,
  );

  const [failedCoverUrl, setFailedCoverUrl] =
    useState<string | null>(null);

  const shouldShowCover =
    coverUrl !== null &&
    coverUrl !== failedCoverUrl;

  const isTrending = variant === "trending";
  const wrapperClassName =
    "home-discovery-game-card group block min-w-0";

  const card = (
    <article className="overflow-hidden rounded-2xl border border-white/[0.07] bg-[#111116] transition duration-300 hover:-translate-y-1 hover:border-violet-400/30 hover:shadow-2xl hover:shadow-violet-950/20">
      <div className="relative aspect-[3/4] overflow-hidden bg-gradient-to-br from-zinc-800 to-zinc-950">
        <span
          className={`absolute left-2.5 top-2.5 z-10 inline-flex items-center gap-1 rounded-lg border px-2 py-1 text-[10px] font-bold backdrop-blur-md ${
            isTrending
              ? "border-orange-300/20 bg-orange-500/15 text-orange-200"
              : "coming-soon-countdown border-white/20 bg-[#09090c]/95 text-white shadow-[0_4px_14px_rgba(0,0,0,0.75)]"
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
              {getComingSoonLabel(game.releaseDate, language)}
            </>
          )}
        </span>

        {shouldShowCover ? (
          <img
            src={coverUrl}
            alt={`${game.title} oyun örtüyü`}
            loading="lazy"
            decoding="async"
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

        <ChevronRight
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
  );

  return (
    <Link
      to={`/games/${game.source}/${game.id}`}
      className={wrapperClassName}
    >
      {card}
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

interface DiscoveryCarouselProps {
  games: DiscoveryGame[];
  variant: DiscoveryGameCardProps["variant"];
}

function DiscoveryCarousel({
  games,
  variant,
}: DiscoveryCarouselProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [isPaused, setIsPaused] = useState(false);
  const [isInViewport, setIsInViewport] = useState(true);
  const [isPageVisible, setIsPageVisible] = useState(true);
  const [isMobileLike, setIsMobileLike] = useState(false);

  const displayedGames = useMemo(
    () => games.slice(0, 10),
    [games],
  );

  const getCarouselStep = useCallback(() => {
    const container = containerRef.current;

    if (!container || displayedGames.length < 2) {
      return null;
    }

    const items =
      container.querySelectorAll<HTMLElement>(
        "[data-discovery-carousel-item]",
      );

    if (items.length < 2) {
      return null;
    }

    const step = items[1].offsetLeft - items[0].offsetLeft;

    return step > 0 ? { container, step } : null;
  }, [displayedGames.length]);

  const moveCarousel = useCallback(
    (direction: -1 | 1) => {
      const metrics = getCarouselStep();

      if (!metrics) {
        return;
      }

      const { container, step } = metrics;
      const maxScrollLeft = Math.max(
        0,
        container.scrollWidth - container.clientWidth,
      );
      const edgeTolerance = Math.max(4, step * 0.2);
      const isAtStart = container.scrollLeft <= edgeTolerance;
      const isAtEnd =
        container.scrollLeft >= maxScrollLeft - edgeTolerance;

      // Keep the arrows looping without cloning every card three times.
      if (direction === 1 && isAtEnd) {
        container.scrollTo({ left: 0, behavior: "auto" });
        return;
      }

      if (direction === -1 && isAtStart) {
        container.scrollTo({
          left: maxScrollLeft,
          behavior: "auto",
        });
        return;
      }

      container.scrollBy({
        left: step * direction,
        behavior: "smooth",
      });
    },
    [getCarouselStep],
  );

  useEffect(() => {
    const container = containerRef.current;

    if (!container || typeof IntersectionObserver === "undefined") {
      setIsInViewport(true);
      return;
    }

    const observer = new IntersectionObserver(
      ([entry]) => {
        setIsInViewport(entry.isIntersecting);
      },
      { rootMargin: "240px 0px" },
    );

    observer.observe(container);

    return () => {
      observer.disconnect();
    };
  }, []);

  useEffect(() => {
    const updateVisibility = () => {
      setIsPageVisible(document.visibilityState === "visible");
    };

    updateVisibility();
    document.addEventListener("visibilitychange", updateVisibility);

    return () => {
      document.removeEventListener("visibilitychange", updateVisibility);
    };
  }, []);

  useEffect(() => {
    const mediaQuery = window.matchMedia(
      "(max-width: 767px), (hover: none) and (pointer: coarse)",
    );
    const updateMobileLike = () => {
      setIsMobileLike(mediaQuery.matches);
    };

    updateMobileLike();
    mediaQuery.addEventListener?.("change", updateMobileLike);

    return () => {
      mediaQuery.removeEventListener?.("change", updateMobileLike);
    };
  }, []);

  useEffect(() => {
    if (
      displayedGames.length < 2 ||
      isPaused ||
      !isInViewport ||
      !isPageVisible ||
      isMobileLike
    ) {
      return;
    }

    const reduceMotion = window.matchMedia(
      "(prefers-reduced-motion: reduce)",
    ).matches;

    if (reduceMotion) {
      return;
    }

    const intervalId = window.setInterval(() => {
      moveCarousel(1);
    }, 4200);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [
    displayedGames.length,
    isInViewport,
    isMobileLike,
    isPageVisible,
    isPaused,
    moveCarousel,
  ]);

  return (
    <div
      className="group/carousel relative"
      onMouseEnter={() => setIsPaused(true)}
      onMouseLeave={() => setIsPaused(false)}
      onFocusCapture={() => setIsPaused(true)}
      onBlurCapture={(event) => {
        if (
          !event.currentTarget.contains(
            event.relatedTarget as Node | null,
          )
        ) {
          setIsPaused(false);
        }
      }}
    >
      <div
        ref={containerRef}
        className="home-discovery-carousel flex snap-x snap-mandatory gap-4 overflow-x-auto py-1"
      >
        {displayedGames.map((game, index) => (
          <div
            key={`${variant}-slide-${game.source}-${game.id}`}
            data-discovery-carousel-item
            className="home-discovery-slide min-w-0 flex-none snap-start"
          >
            <DiscoveryGameCard
              game={game}
              index={index}
              variant={variant}
            />
          </div>
        ))}
      </div>

      {displayedGames.length > 1 ? (
        <>
          <button
            type="button"
            aria-label="Əvvəlki oyunları göstər"
            onClick={() => moveCarousel(-1)}
            className="absolute left-2 top-1/2 z-20 grid size-10 -translate-y-1/2 place-items-center rounded-full border opacity-95 shadow-xl transition hover:scale-105 hover:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400"
            style={{
              backgroundColor:
                "var(--wp-surface-strong)",
              borderColor: "var(--wp-border-strong)",
              color: "var(--wp-text-strong)",
            }}
          >
            <ChevronLeft size={20} />
          </button>

          <button
            type="button"
            aria-label="Növbəti oyunları göstər"
            onClick={() => moveCarousel(1)}
            className="absolute right-2 top-1/2 z-20 grid size-10 -translate-y-1/2 place-items-center rounded-full border opacity-95 shadow-xl transition hover:scale-105 hover:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400"
            style={{
              backgroundColor:
                "var(--wp-surface-strong)",
              borderColor: "var(--wp-border-strong)",
              color: "var(--wp-text-strong)",
            }}
          >
            <ChevronRight size={20} />
          </button>
        </>
      ) : null}
    </div>
  );
}

interface DiscoveryRailProps {
  title: string;
  eyebrow: string;
  games: DiscoveryGame[];
  variant: DiscoveryGameCardProps["variant"];
  isLoading: boolean;
}

function DiscoveryRail({
  title,
  eyebrow,
  games,
  variant,
  isLoading,
}: DiscoveryRailProps) {
  return (
    <section className="home-discovery-rail">
      <div className="mb-5 flex items-end justify-between gap-4">
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

          <h2 className="mt-1.5 text-2xl font-bold tracking-tight text-white sm:text-3xl">
            {title}
          </h2>
        </div>
      </div>

      {isLoading ? (
        <DiscoveryRailSkeleton />
      ) : games.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-white/10 bg-white/[0.02] px-5 py-8 text-center text-sm text-zinc-500">
          Bu bölmə üçün oyun tapılmadı.
        </div>
      ) : (
        <DiscoveryCarousel
          games={games}
          variant={variant}
        />
      )}
    </section>
  );
}

interface HomeDiscoverySectionProps {
  trendingGames: GameListItemDTO[];
  comingSoonGames: HomeComingSoonGameDTO[];
  isLoading: boolean;
  isError: boolean;
}

export function HomeDiscoverySection({
  trendingGames,
  comingSoonGames,
  isLoading,
  isError,
}: HomeDiscoverySectionProps) {
  const { language } = useI18n();

  return (
    <section className="home-discovery-section border-b border-white/5 bg-transparent">
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
              eyebrow={language === "en" ? "Popular now" : "İndi populyar"}
              title={language === "en" ? "Now trending" : "Trend oyunlar"}
              games={trendingGames}
              variant="trending"
              isLoading={isLoading}
            />

            <DiscoveryRail
              eyebrow={language === "en" ? "Prepare for release" : "Buraxılışa hazırlaş"}
              title={language === "en" ? "Coming soon" : "Tezliklə çıxacaq"}
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
