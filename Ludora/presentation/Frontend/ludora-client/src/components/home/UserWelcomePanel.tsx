import { useQuery } from "@tanstack/react-query";
import {
  ArrowRight,
  Gamepad2,
  ListChecks,
  MessageSquareText,
  Settings,
  Sparkles,
  Star,
  UserRound,
} from "lucide-react";
import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { resolveAssetUrl } from "../../lib/helpers";
import {
  getUserGameLists,
  getUserReviews,
} from "../../services/profileService";
import { getMyUserGames } from "../../services/userGamesService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../../stores/authStore";
import type { UserGameStatus } from "../../types/userGames";

const ratingSteps = [
  0.5,
  1,
  1.5,
  2,
  2.5,
  3,
  3.5,
  4,
  4.5,
  5,
];

const statusLabels: Record<UserGameStatus, string> = {
  Playing: "Oynayır",
  PlanToPlay: "Oynayacaq",
  Played: "Oynayıb",
  Dropped: "Yarım qoyub",
};

function formatRating(value: number): string {
  return Number.isInteger(value)
    ? value.toFixed(0)
    : value.toFixed(1);
}

export function UserWelcomePanel() {
  const [failedProfileImageUrl, setFailedProfileImageUrl] =
    useState<string | null>(null);

  const user = useAuthStore((state) => state.user);
  const accessToken = useAuthStore(
    (state) => state.accessToken,
  );
  const expiresAtUtc = useAuthStore(
    (state) => state.expiresAtUtc,
  );

  const hasValidSession =
    isAuthSessionValid(accessToken, expiresAtUtc) &&
    user !== null;

  const userGamesQuery = useQuery({
    queryKey: ["homepage", "my-user-games"],
    queryFn: getMyUserGames,
    enabled: hasValidSession,
  });

  const reviewsQuery = useQuery({
    queryKey: ["homepage", "my-reviews", user?.id],
    queryFn: () => getUserReviews(user!.id),
    enabled: hasValidSession,
  });

  const listsQuery = useQuery({
    queryKey: ["homepage", "my-game-lists", user?.id],
    queryFn: () => getUserGameLists(user!.id),
    enabled: hasValidSession,
  });

  const dashboardData = useMemo(() => {
    const userGames = userGamesQuery.data ?? [];

    const statusCounts: Record<UserGameStatus, number> = {
      Playing: 0,
      PlanToPlay: 0,
      Played: 0,
      Dropped: 0,
    };

    let wishlistCount = 0;
    let ratingTotal = 0;
    let ratingCount = 0;

    const ratingCounts = new Map<number, number>(
      ratingSteps.map((rating) => [rating, 0]),
    );

    for (const game of userGames) {
      if (game.status) {
        statusCounts[game.status] += 1;
      }

      if (game.isWishlisted) {
        wishlistCount += 1;
      }

      if (game.rating !== null) {
        ratingTotal += game.rating;
        ratingCount += 1;
        ratingCounts.set(
          game.rating,
          (ratingCounts.get(game.rating) ?? 0) + 1,
        );
      }
    }

    const recentGame = [...userGames].sort((first, second) => {
      const firstDate = new Date(
        first.updatedAt ?? first.createdAt,
      ).getTime();
      const secondDate = new Date(
        second.updatedAt ?? second.createdAt,
      ).getTime();

      return secondDate - firstDate;
    })[0];

    return {
      statusCounts,
      wishlistCount,
      averageRating:
        ratingCount > 0 ? ratingTotal / ratingCount : null,
      ratingCount,
      ratingCounts,
      recentGame,
    };
  }, [userGamesQuery.data]);

  if (!hasValidSession || !user) {
    return null;
  }

  const profileImageUrl = resolveAssetUrl(
    user.profileImageUrl,
  );
  const userInitial =
    user.userName.trim().charAt(0).toUpperCase() || "?";
  const isUserGamesUnavailable =
    userGamesQuery.isLoading || userGamesQuery.isError;
  const reviewCount = reviewsQuery.data?.totalCount;
  const listCount = listsQuery.data?.length;
  const maximumRatingCount = Math.max(
    1,
    ...ratingSteps.map(
      (rating) => dashboardData.ratingCounts.get(rating) ?? 0,
    ),
  );

  const stats = [
    {
      label: "Oynayıb",
      value: isUserGamesUnavailable
        ? "—"
        : dashboardData.statusCounts.Played,
    },
    {
      label: "Oynayır",
      value: isUserGamesUnavailable
        ? "—"
        : dashboardData.statusCounts.Playing,
    },
    {
      label: "Oynayacaq",
      value: isUserGamesUnavailable
        ? "—"
        : dashboardData.statusCounts.PlanToPlay,
    },
    {
      label: "Yarım qoyub",
      value: isUserGamesUnavailable
        ? "—"
        : dashboardData.statusCounts.Dropped,
    },
    {
      label: "Wishlist",
      value: isUserGamesUnavailable
        ? "—"
        : dashboardData.wishlistCount,
    },
    {
      label: "Rəylər",
      value: reviewsQuery.isLoading ? "—" : (reviewCount ?? "—"),
    },
  ];

  return (
    <section className="border-b border-white/5 bg-[#09090d]">
      <div className="mx-auto max-w-7xl px-5 py-6 lg:px-8 lg:py-8">
        <div className="relative overflow-hidden rounded-[26px] border border-white/[0.08] bg-[#101015] shadow-2xl shadow-black/20">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_12%_0%,rgba(124,58,237,0.16),transparent_32%),radial-gradient(circle_at_88%_100%,rgba(217,70,239,0.08),transparent_30%)]" />

          <div className="relative flex flex-col gap-5 border-b border-white/[0.06] px-5 py-5 sm:flex-row sm:items-center sm:justify-between sm:px-7">
            <div className="flex min-w-0 items-center gap-4">
              <Link
                to={`/users/${user.userName}`}
                className="flex h-12 w-12 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-violet-300/15 bg-violet-500/15 text-base font-bold text-violet-200 transition hover:border-violet-300/35"
                aria-label={`${user.userName} profilinə keç`}
              >
                {profileImageUrl &&
                profileImageUrl !== failedProfileImageUrl ? (
                  <img
                    src={profileImageUrl}
                    alt={`${user.userName} profil şəkli`}
                    className="h-full w-full object-cover"
                    onError={() => {
                      setFailedProfileImageUrl(profileImageUrl);
                    }}
                  />
                ) : (
                  userInitial
                )}
              </Link>

              <div className="min-w-0">
                <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-[0.2em] text-violet-300">
                  <Sparkles size={14} />
                  Yenidən xoş gəldin
                </div>

                <p className="mt-1.5 truncate text-base text-zinc-400 sm:text-lg">
                  <Link
                    to={`/users/${user.userName}`}
                    className="font-bold text-white transition hover:text-violet-300"
                  >
                    {user.userName}
                  </Link>
                  <span className="text-zinc-600">, </span>
                  növbəti oyunun üçün Ludora hazırdır.
                </p>
              </div>
            </div>

            <Link
              to={`/users/${user.userName}`}
              className="inline-flex shrink-0 items-center justify-center gap-2 rounded-xl border border-white/[0.09] bg-white/[0.04] px-4 py-2.5 text-sm font-semibold text-zinc-200 transition hover:border-violet-400/25 hover:bg-violet-500/10 hover:text-white"
            >
              Profilə bax
              <ArrowRight size={16} />
            </Link>
          </div>

          <div className="relative grid lg:grid-cols-[1.25fr_1fr_0.82fr]">
            <div className="border-b border-white/[0.06] p-5 sm:p-7 lg:border-b-0 lg:border-r">
              <div className="grid grid-cols-2 gap-x-3 gap-y-5 sm:grid-cols-3">
                {stats.map((stat) => (
                  <div
                    key={stat.label}
                    className="rounded-xl border border-white/[0.055] bg-white/[0.025] px-3 py-3 text-center"
                  >
                    <p className="text-xs text-zinc-500">
                      {stat.label}
                    </p>

                    <p className="mt-1 text-2xl font-bold tracking-tight text-zinc-100">
                      {stat.value}
                    </p>
                  </div>
                ))}
              </div>

              <div className="mt-5 flex flex-wrap items-center justify-center gap-x-5 gap-y-2 border-t border-white/[0.05] pt-4 text-xs text-zinc-500">
                <span>{user.followerCount} izləyici</span>
                <span>{user.followingCount} izlənilən</span>
                <span>{user.postCount} paylaşım</span>
                <span>
                  {listsQuery.isLoading ? "—" : (listCount ?? "—")} siyahı
                </span>
              </div>
            </div>

            <div className="border-b border-white/[0.06] p-5 sm:p-7 lg:border-b-0 lg:border-r">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <p className="text-xs font-bold uppercase tracking-[0.18em] text-fuchsia-300">
                    Puanların
                  </p>

                  <p className="mt-1 text-sm text-zinc-500">
                    Verdiyin oyun puanlarının paylanması
                  </p>
                </div>

                <div className="text-right">
                  <p className="flex items-center justify-end gap-1 text-xl font-bold text-white">
                    {isUserGamesUnavailable ||
                    dashboardData.averageRating === null
                      ? "—"
                      : dashboardData.averageRating.toFixed(1)}
                    <Star
                      size={15}
                      className="fill-fuchsia-300 text-fuchsia-300"
                    />
                  </p>

                  <p className="mt-1 text-[11px] text-zinc-600">
                    {userGamesQuery.isLoading
                      ? "Yüklənir"
                      : userGamesQuery.isError
                        ? "Yüklənmədi"
                        : `${dashboardData.ratingCount} oyun`}
                  </p>
                </div>
              </div>

              <div className="mt-6 flex h-24 items-end gap-1.5">
                {ratingSteps.map((rating) => {
                  const count =
                    dashboardData.ratingCounts.get(rating) ?? 0;
                  const heightPercentage =
                    count === 0
                      ? 4
                      : Math.max(
                          16,
                          (count / maximumRatingCount) * 100,
                        );

                  return (
                    <div
                      key={rating}
                      className="group flex h-full min-w-0 flex-1 items-end"
                      title={`${formatRating(rating)} ulduz: ${count} oyun`}
                    >
                      <div
                        className={`w-full rounded-t-md transition group-hover:opacity-100 ${
                          count > 0
                            ? "bg-gradient-to-t from-violet-600 to-fuchsia-400 opacity-90"
                            : "bg-white/[0.06]"
                        }`}
                        style={{
                          height: `${heightPercentage}%`,
                        }}
                      />
                    </div>
                  );
                })}
              </div>

              <div className="mt-2 flex justify-between text-[10px] text-zinc-600">
                <span>0.5★</span>
                <span>5★</span>
              </div>
            </div>

            <div className="p-5 sm:p-7">
              <p className="text-xs font-bold uppercase tracking-[0.18em] text-violet-300">
                Tez keçid
              </p>

              {userGamesQuery.isLoading ? (
                <div className="mt-4 h-[118px] animate-pulse rounded-2xl border border-white/[0.06] bg-white/[0.025]" />
              ) : userGamesQuery.isError ? (
                <div className="mt-4 rounded-2xl border border-red-400/10 bg-red-500/[0.04] p-4">
                  <p className="text-sm font-semibold text-red-200">
                    Fəaliyyət yüklənmədi
                  </p>

                  <p className="mt-1 text-xs leading-5 text-zinc-600">
                    API bağlantısını yoxladıqdan sonra səhifəni yenilə.
                  </p>
                </div>
              ) : dashboardData.recentGame ? (
                <Link
                  to={`/games/${dashboardData.recentGame.source}/${dashboardData.recentGame.gameId}`}
                  className="mt-4 block rounded-2xl border border-white/[0.07] bg-white/[0.025] p-4 transition hover:border-violet-400/20 hover:bg-violet-500/[0.06]"
                >
                  <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-zinc-600">
                    Son oyun fəaliyyəti
                  </p>

                  <p className="mt-2 line-clamp-2 text-sm font-semibold text-zinc-200">
                    {dashboardData.recentGame.name}
                  </p>

                  <p className="mt-1 text-xs text-zinc-500">
                    {dashboardData.recentGame.status
                      ? statusLabels[
                          dashboardData.recentGame.status
                        ]
                      : dashboardData.recentGame.isWishlisted
                        ? "Wishlist-də"
                        : dashboardData.recentGame.isFavorite
                          ? "Favorilərdə"
                          : dashboardData.recentGame.rating !== null
                            ? `${formatRating(dashboardData.recentGame.rating)}★ puan`
                            : "Oyun fəaliyyəti"}
                  </p>
                </Link>
              ) : (
                <div className="mt-4 rounded-2xl border border-dashed border-white/[0.08] bg-white/[0.02] p-4">
                  <Gamepad2
                    size={20}
                    className="text-zinc-600"
                  />

                  <p className="mt-3 text-sm font-semibold text-zinc-300">
                    İlk oyununu qeyd et
                  </p>

                  <p className="mt-1 text-xs leading-5 text-zinc-600">
                    Status, wishlist və ya puan əlavə etdikdə burada görünəcək.
                  </p>
                </div>
              )}

              <div className="mt-3 grid grid-cols-3 gap-2">
                <Link
                  to="/games"
                  className="flex flex-col items-center gap-2 rounded-xl border border-white/[0.06] bg-white/[0.025] px-2 py-3 text-[11px] text-zinc-500 transition hover:border-violet-400/20 hover:text-violet-200"
                >
                  <Gamepad2 size={17} />
                  Oyunlar
                </Link>

                <Link
                  to={`/users/${user.userName}`}
                  className="flex flex-col items-center gap-2 rounded-xl border border-white/[0.06] bg-white/[0.025] px-2 py-3 text-[11px] text-zinc-500 transition hover:border-violet-400/20 hover:text-violet-200"
                >
                  <UserRound size={17} />
                  Profil
                </Link>

                <Link
                  to="/settings"
                  className="flex flex-col items-center gap-2 rounded-xl border border-white/[0.06] bg-white/[0.025] px-2 py-3 text-[11px] text-zinc-500 transition hover:border-violet-400/20 hover:text-violet-200"
                >
                  <Settings size={17} />
                  Ayarlar
                </Link>
              </div>

              <div className="mt-3 flex items-center justify-center gap-4 text-[11px] text-zinc-600">
                <span className="inline-flex items-center gap-1.5">
                  <ListChecks size={13} />
                  {listsQuery.isLoading ? "—" : (listCount ?? "—")}
                </span>
                <span className="inline-flex items-center gap-1.5">
                  <MessageSquareText size={13} />
                  {reviewsQuery.isLoading ? "—" : (reviewCount ?? "—")}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
