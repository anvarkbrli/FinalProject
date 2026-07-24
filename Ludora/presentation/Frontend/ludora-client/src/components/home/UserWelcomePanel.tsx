import { useQuery } from "@tanstack/react-query";
import {
  ArrowRight,
  Gamepad2,
  History,
  ListChecks,
  MessageSquareText,
  Sparkles,
  Star,
} from "lucide-react";
import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { resolveAssetUrl } from "../../lib/helpers";
import { getMyUserGameDashboard } from "../../services/userGamesService";
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
    queryKey: ["homepage", "my-user-game-dashboard", user?.id],
    queryFn: ({ signal }) =>
      getMyUserGameDashboard(signal),
    enabled: hasValidSession,
  });

  const dashboardData = useMemo(() => {
    const statusCounts: Record<UserGameStatus, number> =
      userGamesQuery.data?.statusCounts ?? {
      Playing: 0,
      PlanToPlay: 0,
      Played: 0,
      Dropped: 0,
    };
    const ratingCounts = new Map<number, number>(
      ratingSteps.map((rating) => [rating, 0]),
    );

    for (const item of userGamesQuery.data?.ratingDistribution ?? []) {
      ratingCounts.set(item.rating, item.count);
    }

    return {
      statusCounts,
      wishlistCount: userGamesQuery.data?.wishlistCount ?? 0,
      averageRating: userGamesQuery.data?.averageRating ?? null,
      ratingCount: userGamesQuery.data?.ratingCount ?? 0,
      ratingCounts,
      recentGame: userGamesQuery.data?.recentGame ?? null,
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
  const isRatingDataUnavailable =
    userGamesQuery.isLoading || userGamesQuery.isError;
  const reviewCount = userGamesQuery.data?.reviewCount;
  const listCount = userGamesQuery.data?.gameListCount;
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
      href: `/users/${user.userName}?tab=games&gameStatus=Played`,
    },
    {
      label: "Oynayır",
      value: isUserGamesUnavailable
        ? "—"
        : dashboardData.statusCounts.Playing,
      href: `/users/${user.userName}?tab=games&gameStatus=Playing`,
    },
    {
      label: "Oynayacaq",
      value: isUserGamesUnavailable
        ? "—"
        : dashboardData.statusCounts.PlanToPlay,
      href: `/users/${user.userName}?tab=games&gameStatus=PlanToPlay`,
    },
    {
      label: "Yarım qoyub",
      value: isUserGamesUnavailable
        ? "—"
        : dashboardData.statusCounts.Dropped,
      href: `/users/${user.userName}?tab=games&gameStatus=Dropped`,
    },
    {
      label: "Wishlist",
      value: isUserGamesUnavailable
        ? "—"
        : dashboardData.wishlistCount,
      href: `/users/${user.userName}?tab=wishlist`,
    },
    {
      label: "Rəylər",
      value: userGamesQuery.isLoading ? "—" : (reviewCount ?? "—"),
      href: `/users/${user.userName}?tab=reviews`,
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
                  <Link
                    key={stat.label}
                    to={stat.href}
                    aria-label={`${stat.label} bölməsinə keç`}
                    className="group rounded-xl border border-white/[0.055] bg-white/[0.025] px-3 py-3 text-center transition hover:border-violet-400/25 hover:bg-violet-500/[0.08] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400/70"
                  >
                    <p className="text-xs text-zinc-500 transition group-hover:text-zinc-300">
                      {stat.label}
                    </p>

                    <p className="mt-1 text-2xl font-bold tracking-tight text-zinc-100 transition group-hover:text-violet-200">
                      {stat.value}
                    </p>
                  </Link>
                ))}
              </div>

              <div className="mt-5 flex flex-wrap items-center justify-center gap-x-5 gap-y-2 border-t border-white/[0.05] pt-4 text-xs text-zinc-500">
                <span>{user.followerCount} izləyici</span>
                <span>{user.followingCount} izlənilən</span>
                <span>{user.postCount} paylaşım</span>
                <span>
                  {userGamesQuery.isLoading ? "—" : (listCount ?? "—")} siyahı
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
                    {isRatingDataUnavailable ||
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
                    <Link
                      key={rating}
                      to={`/users/${user.userName}`}
                      className="group relative flex h-full min-w-0 flex-1 items-end rounded-t-md outline-none focus-visible:ring-2 focus-visible:ring-violet-400/70"
                      aria-label={`${formatRating(rating)} ulduz verdiyin ${count} oyunu profildə göstər`}
                    >
                      <span
                        className={`w-full rounded-t-md transition duration-200 group-hover:-translate-y-1 group-hover:opacity-100 ${
                          count > 0
                            ? "bg-gradient-to-t from-violet-600 to-fuchsia-400 opacity-90 group-hover:shadow-[0_0_22px_rgba(217,70,239,0.28)]"
                            : "bg-white/[0.06] group-hover:bg-violet-500/20"
                        }`}
                        style={{
                          height: `${heightPercentage}%`,
                        }}
                      />

                      <span className="pointer-events-none absolute bottom-[calc(100%+10px)] left-1/2 z-20 hidden -translate-x-1/2 whitespace-nowrap rounded-xl border border-white/[0.1] bg-[#181820] px-3 py-2 text-[11px] font-semibold text-zinc-100 shadow-2xl shadow-black/50 group-hover:block group-focus-visible:block">
                        {formatRating(rating)} ulduz: {count} oyun
                        <span className="absolute left-1/2 top-full h-2 w-2 -translate-x-1/2 -translate-y-1/2 rotate-45 border-b border-r border-white/[0.1] bg-[#181820]" />
                      </span>
                    </Link>
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
                  to={`/users/${user.userName}?tab=posts`}
                  className="flex flex-col items-center gap-2 rounded-xl border border-white/[0.06] bg-white/[0.025] px-2 py-3 text-[11px] text-zinc-500 transition hover:border-violet-400/20 hover:text-violet-200"
                >
                  <MessageSquareText size={17} />
                  Postlar
                </Link>

                <Link
                  to={`/users/${user.userName}?tab=lists`}
                  className="flex flex-col items-center gap-2 rounded-xl border border-white/[0.06] bg-white/[0.025] px-2 py-3 text-[11px] text-zinc-500 transition hover:border-violet-400/20 hover:text-violet-200"
                >
                  <ListChecks size={17} />
                  Listlər
                </Link>

                <Link
                  to={`/users/${user.userName}?tab=feed`}
                  className="flex flex-col items-center gap-2 rounded-xl border border-white/[0.06] bg-white/[0.025] px-2 py-3 text-[11px] text-zinc-500 transition hover:border-violet-400/20 hover:text-violet-200"
                >
                  <History size={17} />
                  Axın
                </Link>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
