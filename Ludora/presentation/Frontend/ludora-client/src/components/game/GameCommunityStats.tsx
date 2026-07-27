import { useQuery } from "@tanstack/react-query";
import {
  Bookmark,
  Gamepad2,
  ChevronLeft,
  ChevronRight,
  Clock3,
  CircleStop,
  LoaderCircle,
  PlayCircle,
  Star,
  UserRound,
  Users,
  X,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import {
  useEffect,
  useMemo,
  useState,
} from "react";
import { Link } from "react-router-dom";
import { RatingStarIcon } from "../common/RatingStarIcon";
import { getApiErrorMessage } from "../../lib/api";
import { resolveAssetUrl } from "../../lib/helpers";
import {
  getGameCategoryUsers,
  getGameRatingUsers,
} from "../../services/gameCommunityService";
import type {
  GameCommunityUserCategory,
  GameCommunityUserDTO,
} from "../../types/gameCommunity";
import type {
  GameCommunityStatsDTO,
  PagedResultDTO,
} from "../../types/games";
import type { CatalogSource } from "../../types/home";

const PAGE_SIZE = 20;

const ratingLevels = [
  5,
  4.5,
  4,
  3.5,
  3,
  2.5,
  2,
  1.5,
  1,
  0.5,
] as const;

type CommunitySelection =
  | {
      type: "rating";
      rating: number;
      title: string;
      description: string;
    }
  | {
      type: "category";
      category: GameCommunityUserCategory;
      title: string;
      description: string;
    };

interface CategoryCard {
  key: GameCommunityUserCategory;
  label: string;
  count: number;
  detail: string;
  modalDescription: string;
  icon: LucideIcon;
  iconClassName: string;
  iconContainerClassName: string;
  hoverClassName: string;
  accentClassName: string;
}

function formatCount(value: number): string {
  return new Intl.NumberFormat(
    "az-AZ",
  ).format(Math.max(0, value));
}

function formatRating(value: number): string {
  return value.toFixed(1);
}

function AverageRatingStars({
  rating,
}: {
  rating: number | null;
}) {
  return (
    <div
      className="flex items-center gap-2"
      aria-label={
        rating === null
          ? "Reytinq yoxdur"
          : `Orta reytinq ${rating.toFixed(1)} ulduzdur`
      }
    >
      {Array.from({ length: 5 }).map((_, index) => {
        const fillPercent =
          rating === null
            ? 0
            : Math.min(
                100,
                Math.max(0, (rating - index) * 100),
              );

        return (
          <RatingStarIcon
            key={index}
            fillPercent={fillPercent}
            size={32}
            strokeWidth={1.8}
          />
        );
      })}
    </div>
  );
}

function getInitials(
  userName: string,
): string {
  const normalizedUserName =
    userName.trim();

  if (!normalizedUserName) {
    return "?";
  }

  return normalizedUserName
    .split(/[\s._-]+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) =>
      part.charAt(0).toUpperCase(),
    )
    .join("");
}

function CommunityUsersModal({
  selection,
  result,
  isLoading,
  isFetching,
  error,
  pageNumber,
  onPageChange,
  onRetry,
  onClose,
}: {
  selection: CommunitySelection | null;
  result:
    | PagedResultDTO<GameCommunityUserDTO>
    | undefined;
  isLoading: boolean;
  isFetching: boolean;
  error: unknown;
  pageNumber: number;
  onPageChange: (
    pageNumber: number,
  ) => void;
  onRetry: () => void;
  onClose: () => void;
}) {
  useEffect(() => {
    if (!selection) {
      return;
    }

    const previousOverflow =
      document.body.style.overflow;

    document.body.style.overflow =
      "hidden";

    const handleKeyDown = (
      event: KeyboardEvent,
    ) => {
      if (event.key === "Escape") {
        onClose();
      }
    };

    window.addEventListener(
      "keydown",
      handleKeyDown,
    );

    return () => {
      document.body.style.overflow =
        previousOverflow;

      window.removeEventListener(
        "keydown",
        handleKeyDown,
      );
    };
  }, [selection, onClose]);

  if (!selection) {
    return null;
  }

  const users = result?.items ?? [];
  const totalCount = result?.totalCount ?? 0;
  const totalPages = result?.totalPages ?? 0;
  const canGoPrevious =
    pageNumber > 1 && !isFetching;
  const canGoNext =
    totalPages > 0 &&
    pageNumber < totalPages &&
    !isFetching;

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 sm:p-6">
      <button
        type="button"
        aria-label="Modalı bağla"
        onClick={onClose}
        className="absolute inset-0 bg-black/75 backdrop-blur-md"
      />

      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="community-users-modal-title"
        className="relative z-10 flex max-h-[82vh] w-full max-w-2xl flex-col overflow-hidden rounded-3xl border border-white/10 bg-[#11131a] shadow-2xl shadow-black/60"
      >
        <div className="flex items-start justify-between gap-4 border-b border-white/[0.08] px-5 py-5 sm:px-6">
          <div className="min-w-0">
            <h3
              id="community-users-modal-title"
              className="text-xl font-black tracking-tight text-white"
            >
              {selection.title}
            </h3>

            <p className="mt-1 text-sm leading-6 text-zinc-400">
              {selection.description}
            </p>

            {!isLoading && !error && (
              <p className="mt-2 text-xs font-semibold uppercase tracking-[0.16em] text-violet-300">
                {formatCount(totalCount)} istifadəçi
              </p>
            )}
          </div>

          <button
            type="button"
            onClick={onClose}
            className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl border border-white/10 bg-white/[0.03] text-zinc-400 transition hover:border-white/20 hover:bg-white/[0.07] hover:text-white"
          >
            <X size={18} />
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5 sm:px-6">
          {isLoading ? (
            <div className="flex min-h-64 flex-col items-center justify-center text-center">
              <LoaderCircle
                size={30}
                className="animate-spin text-violet-300"
              />
              <p className="mt-4 text-sm font-semibold text-zinc-300">
                İstifadəçilər yüklənir...
              </p>
            </div>
          ) : error ? (
            <div className="flex min-h-64 flex-col items-center justify-center rounded-2xl border border-red-400/15 bg-red-500/[0.05] px-5 text-center">
              <p className="text-sm font-semibold text-red-200">
                {getApiErrorMessage(
                  error,
                  "İstifadəçi siyahısını yükləmək mümkün olmadı.",
                )}
              </p>
              <button
                type="button"
                onClick={onRetry}
                className="mt-4 rounded-xl bg-white px-4 py-2 text-sm font-bold text-black transition hover:bg-zinc-200"
              >
                Yenidən yoxla
              </button>
            </div>
          ) : users.length > 0 ? (
            <div className="space-y-3">
              {users.map((user) => {
                const profileImageUrl =
                  resolveAssetUrl(
                    user.profileImageUrl,
                  );

                return (
                  <Link
                    key={user.id}
                    to={`/users/${encodeURIComponent(user.userName)}`}
                    className="flex items-center gap-4 rounded-2xl border border-white/[0.08] bg-white/[0.025] px-4 py-4 transition hover:border-violet-400/20 hover:bg-violet-500/[0.05]"
                  >
                    {profileImageUrl ? (
                      <img
                        src={profileImageUrl}
                        alt={user.userName}
                        className="h-12 w-12 shrink-0 rounded-2xl object-cover"
                      />
                    ) : (
                      <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl border border-violet-300/15 bg-violet-500/10 text-sm font-black text-violet-200">
                        {getInitials(
                          user.userName,
                        )}
                      </div>
                    )}

                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-bold text-white">
                        {user.userName}
                      </p>
                    </div>

                    <UserRound
                      size={17}
                      className="shrink-0 text-zinc-600"
                    />
                  </Link>
                );
              })}
            </div>
          ) : (
            <div className="flex min-h-64 flex-col items-center justify-center rounded-2xl border border-dashed border-white/10 bg-white/[0.02] px-5 text-center">
              <Users
                size={30}
                className="text-zinc-600"
              />
              <p className="mt-4 text-sm font-semibold text-zinc-300">
                Bu siyahıda hələ istifadəçi yoxdur.
              </p>
            </div>
          )}
        </div>

        {totalPages > 1 && (
          <div className="flex items-center justify-between gap-4 border-t border-white/[0.08] px-5 py-4 sm:px-6">
            <button
              type="button"
              disabled={!canGoPrevious}
              onClick={() =>
                onPageChange(pageNumber - 1)
              }
              className="inline-flex items-center gap-2 rounded-xl border border-white/10 bg-white/[0.03] px-3 py-2 text-sm font-semibold text-zinc-300 transition hover:border-white/20 hover:bg-white/[0.06] disabled:cursor-not-allowed disabled:opacity-40"
            >
              <ChevronLeft size={16} />
              Əvvəlki
            </button>

            <span className="text-sm font-semibold text-zinc-500">
              {pageNumber} / {totalPages}
            </span>

            <button
              type="button"
              disabled={!canGoNext}
              onClick={() =>
                onPageChange(pageNumber + 1)
              }
              className="inline-flex items-center gap-2 rounded-xl border border-white/10 bg-white/[0.03] px-3 py-2 text-sm font-semibold text-zinc-300 transition hover:border-white/20 hover:bg-white/[0.06] disabled:cursor-not-allowed disabled:opacity-40"
            >
              Növbəti
              <ChevronRight size={16} />
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

export function GameCommunityStats({
  source,
  gameId,
  stats,
}: {
  source: CatalogSource;
  gameId: number;
  stats: GameCommunityStatsDTO;
}) {
  const [activeSelection, setActiveSelection] =
    useState<CommunitySelection | null>(null);
  const [pageNumber, setPageNumber] = useState(1);

  const ratingDistribution = useMemo(() => {
    const ratingCountMap = new Map(
      stats.ratingDistribution.map((item) => [
        item.rating.toFixed(1),
        item.count,
      ]),
    );

    return ratingLevels.map((rating) => ({
      rating,
      count:
        ratingCountMap.get(
          rating.toFixed(1),
        ) ?? 0,
    }));
  }, [stats.ratingDistribution]);

  const categoryCards: CategoryCard[] = [
    {
      key: "Played",
      label: "Oynayıb",
      count: stats.playedCount,
      detail:
        "Oyunu tamamlayan və ya oynayıb qeyd edənlər",
      modalDescription:
        "Oyunu oynanılmış kimi işarələyən istifadəçilər.",
      icon: Gamepad2,
      iconClassName: "text-emerald-300",
      iconContainerClassName:
        "border-emerald-400/15 bg-emerald-400/[0.07]",
      hoverClassName:
        "hover:border-emerald-400/30 hover:shadow-emerald-500/10",
      accentClassName: "bg-emerald-400",
    },
    {
      key: "Playing",
      label: "Oynayır",
      count: stats.playingCount,
      detail:
        "Hazırda bu oyunu oynayan istifadəçilər",
      modalDescription:
        "Hazırda bu oyunu oynayan istifadəçilər.",
      icon: PlayCircle,
      iconClassName: "text-violet-300",
      iconContainerClassName:
        "border-violet-400/15 bg-violet-400/[0.07]",
      hoverClassName:
        "hover:border-violet-400/30 hover:shadow-violet-500/10",
      accentClassName: "bg-violet-400",
    },
    {
      key: "PlanToPlay",
      label: "Oynayacaq",
      count: stats.planToPlayCount,
      detail:
        "Sonra oynamağı planlaşdıran istifadəçilər",
      modalDescription:
        "Oyunu gələcəkdə oynamağı planlaşdıran istifadəçilər.",
      icon: Clock3,
      iconClassName: "text-sky-300",
      iconContainerClassName:
        "border-sky-400/15 bg-sky-400/[0.07]",
      hoverClassName:
        "hover:border-sky-400/30 hover:shadow-sky-500/10",
      accentClassName: "bg-sky-400",
    },
    {
      key: "Wishlist",
      label: "Wishlist",
      count: stats.wishlistCount,
      detail:
        "İstək siyahısına əlavə edən istifadəçilər",
      modalDescription:
        "Oyunu wishlist siyahısına əlavə edən istifadəçilər.",
      icon: Bookmark,
      iconClassName: "text-amber-300",
      iconContainerClassName:
        "border-amber-400/15 bg-amber-400/[0.07]",
      hoverClassName:
        "hover:border-amber-400/30 hover:shadow-amber-500/10",
      accentClassName: "bg-amber-400",
    },
    {
      key: "Dropped",
      label: "Yarım qoyub",
      count: stats.droppedCount,
      detail:
        "Oyunu tamamlamadan dayandıran istifadəçilər",
      modalDescription:
        "Oyunu yarım qoyan istifadəçilər.",
      icon: CircleStop,
      iconClassName: "text-rose-300",
      iconContainerClassName:
        "border-rose-400/15 bg-rose-400/[0.07]",
      hoverClassName:
        "hover:border-rose-400/30 hover:shadow-rose-500/10",
      accentClassName: "bg-rose-400",
    },
  ];

  const usersQuery = useQuery({
    queryKey: [
      "game-community-users",
      source,
      gameId,
      activeSelection?.type,
      activeSelection?.type === "rating"
        ? activeSelection.rating
        : activeSelection?.category,
      pageNumber,
    ],
    queryFn: () => {
      if (!activeSelection) {
        throw new Error(
          "Community seçimi tapılmadı.",
        );
      }

      const filter = {
        pageNumber,
        pageSize: PAGE_SIZE,
      };

      if (activeSelection.type === "rating") {
        return getGameRatingUsers(
          source,
          gameId,
          activeSelection.rating,
          filter,
        );
      }

      return getGameCategoryUsers(
        source,
        gameId,
        activeSelection.category,
        filter,
      );
    },
    enabled: activeSelection !== null,
    staleTime: 30_000,
  });

  function openRatingUsers(rating: number) {
    setPageNumber(1);
    setActiveSelection({
      type: "rating",
      rating,
      title: `${formatRating(
        rating,
      )} ulduz verənlər`,
      description: `${formatRating(
        rating,
      )} reytinqini verən istifadəçilərin siyahısı.`,
    });
  }

  function openCategoryUsers(card: CategoryCard) {
    setPageNumber(1);
    setActiveSelection({
      type: "category",
      category: card.key,
      title: `${card.label} siyahısı`,
      description: card.modalDescription,
    });
  }

  function closeModal() {
    setActiveSelection(null);
    setPageNumber(1);
  }

  return (
    <>
      <div className="mt-9 flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-violet-300/80">
            İcma fəaliyyəti
          </p>
          <h2 className="mt-2 text-2xl font-black tracking-tight text-white">
            Oyun statistikası
          </h2>
        </div>

        <p className="max-w-md text-sm leading-6 text-zinc-500">
          Reytinq sətrinə və ya statistik kartına klikləyərək istifadəçiləri görə bilərsən.
        </p>
      </div>

      <div className="mt-5 space-y-4">
        <section className="overflow-hidden rounded-3xl border border-white/10 bg-gradient-to-br from-[#171a25] via-[#141722] to-[#10121a] shadow-xl shadow-black/20 transition duration-300 hover:border-amber-300/20">
          <div className="grid lg:min-h-[460px] lg:grid-cols-[340px_minmax(0,1fr)]">
            <div className="relative overflow-hidden border-b border-white/[0.07] px-6 py-7 sm:px-8 sm:py-8 lg:border-b-0 lg:border-r lg:px-10 lg:py-10">
              <div className="pointer-events-none absolute -left-16 -top-20 h-56 w-56 rounded-full bg-amber-400/[0.07] blur-3xl" />

              <div className="relative flex items-center gap-4">
                <div className="inline-flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl border border-amber-300/20 bg-amber-400/10 shadow-lg shadow-amber-500/5">
                  <Star
                    size={25}
                    strokeWidth={1.8}
                    className="text-amber-300"
                  />
                </div>

                <p className="text-lg font-bold text-zinc-100">
                  Orta reytinq
                </p>
              </div>

              <div className="relative mt-12 lg:mt-14">
                <p className="text-7xl font-black leading-none tracking-[-0.055em] text-white sm:text-[84px]">
                  {stats.averageRating !== null
                    ? stats.averageRating.toFixed(1)
                    : "—"}
                </p>

                <div className="mt-5">
                  <AverageRatingStars
                    rating={stats.averageRating}
                  />
                  <p className="mt-4 text-sm text-zinc-500">
                    {formatCount(stats.ratingCount)} istifadəçi reytinqi
                  </p>
                </div>
              </div>
            </div>

            <div className="flex items-center p-4 sm:p-6 lg:p-8">
              <div className="w-full space-y-1">
                {ratingDistribution.map((item) => {
                  const width =
                    stats.ratingCount > 0
                      ? (item.count /
                          stats.ratingCount) *
                        100
                      : 0;

                  return (
                    <button
                      key={item.rating}
                      type="button"
                      onClick={() =>
                        openRatingUsers(item.rating)
                      }
                      className="group grid w-full grid-cols-[58px_minmax(0,1fr)_36px] items-center gap-3 rounded-xl border border-transparent px-2 py-2 text-left outline-none transition hover:bg-white/[0.045] focus:border-transparent focus:outline-none focus-visible:border-amber-300/30 focus-visible:bg-white/[0.045] focus-visible:outline-none sm:grid-cols-[62px_minmax(0,1fr)_44px] sm:gap-4"
                    >
                      <span className="text-sm font-bold tabular-nums text-zinc-300 transition group-hover:text-amber-200">
                        {formatRating(item.rating)} ★
                      </span>

                      <div className="h-2.5 overflow-hidden rounded-full bg-white/[0.07] ring-1 ring-inset ring-white/[0.025]">
                        <div
                          className="h-full min-w-0 rounded-full bg-gradient-to-r from-amber-300 via-amber-400 to-amber-500 shadow-[0_0_16px_rgba(251,191,36,0.2)] transition-[width] duration-500 ease-out"
                          style={{ width: `${width}%` }}
                        />
                      </div>

                      <span className="text-right text-sm font-medium tabular-nums text-zinc-500 transition group-hover:text-zinc-300">
                        {formatCount(item.count)}
                      </span>
                    </button>
                  );
                })}
              </div>
            </div>
          </div>
        </section>

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">
          {categoryCards.map((card) => {
            const Icon = card.icon;

            return (
              <button
                key={card.key}
                type="button"
                onClick={() => openCategoryUsers(card)}
                className={`group relative min-h-[210px] overflow-hidden rounded-3xl border border-white/10 bg-gradient-to-b from-[#171a25] to-[#11131b] p-5 text-left shadow-lg shadow-black/20 outline-none transition duration-300 hover:-translate-y-1 hover:shadow-xl focus:outline-none focus-visible:outline-none sm:min-h-[220px] xl:min-h-[230px] ${card.hoverClassName}`}
              >
                <div
                  className={`absolute inset-x-4 bottom-0 h-px opacity-80 transition group-hover:opacity-100 ${card.accentClassName}`}
                />

                <div
                  className={`inline-flex h-10 w-10 items-center justify-center rounded-xl border transition group-hover:scale-105 ${card.iconContainerClassName}`}
                >
                  <Icon
                    size={19}
                    strokeWidth={1.8}
                    className={card.iconClassName}
                  />
                </div>

                <div className="mt-6">
                  <p className="text-sm font-semibold text-zinc-300">
                    {card.label}
                  </p>
                  <p className="mt-3 text-4xl font-black leading-none tracking-[-0.04em] text-white">
                    {formatCount(card.count)}
                  </p>
                  <p className="mt-3 max-w-[14rem] text-xs leading-5 text-zinc-500">
                    {card.detail}
                  </p>
                </div>
              </button>
            );
          })}
        </div>
      </div>

      <CommunityUsersModal
        selection={activeSelection}
        result={usersQuery.data}
        isLoading={usersQuery.isPending}
        isFetching={usersQuery.isFetching}
        error={usersQuery.error}
        pageNumber={pageNumber}
        onPageChange={setPageNumber}
        onRetry={() => {
          void usersQuery.refetch();
        }}
        onClose={closeModal}
      />
    </>
  );
}
