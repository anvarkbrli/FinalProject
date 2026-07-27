import {
  ChevronRight,
  Clock3,
  Gamepad2,
  Monitor,
  Shapes,
  Star,
  Trophy,
  X,
} from "lucide-react";
import {
  useEffect,
  useState,
} from "react";
import { Link } from "react-router-dom";
import { ProfileBadges } from "./ProfileBadges";
import { resolveAssetUrl } from "../../lib/helpers";
import type {
  ProfileGameEntryDTO,
  UserPlaytimeChartPointDTO,
  UserPlaytimePeriod,
  UserPlaytimeStatisticsDTO,
  UserProfileDTO,
} from "../../types/profile";
import type { UserGameDTO } from "../../types/userGames";

interface ProfileOverviewProps {
  profile: UserProfileDTO;
  recentPlayedGames: ProfileGameEntryDTO[];
  recentPlayedGamesLoading: boolean;
  favoriteGames: UserGameDTO[];
  favoriteGamesLoading: boolean;
  canViewFavorites: boolean;
  onOpenFavorites: () => void;
  onOpenRecentPlayed: () => void;
  onOpenTopGenre: (genre: string) => void;
  onOpenTopPlatform: (platform: string) => void;
  playtimeStatistics: UserPlaytimeStatisticsDTO | undefined;
  playtimePeriod: UserPlaytimePeriod;
  playtimeStatisticsLoading: boolean;
  playtimeStatisticsError: string | null;
  onPlaytimePeriodChange: (period: UserPlaytimePeriod) => void;
  onRatingSelect: (rating: number) => void;
}

const ratingSteps = [0.5, 1, 1.5, 2, 2.5, 3, 3.5, 4, 4.5, 5];

const playtimePeriods: Array<{
  value: UserPlaytimePeriod;
  label: string;
}> = [
  { value: "Weekly", label: "Həftəlik" },
  { value: "Monthly", label: "Aylıq" },
  { value: "Yearly", label: "İllik" },
];

const azWeekdayNames = [
  "Bazar",
  "Bazar ertəsi",
  "Çərşənbə axşamı",
  "Çərşənbə",
  "Cümə axşamı",
  "Cümə",
  "Şənbə",
] as const;

const azMonthNames = [
  "Yanvar",
  "Fevral",
  "Mart",
  "Aprel",
  "May",
  "İyun",
  "İyul",
  "Avqust",
  "Sentyabr",
  "Oktyabr",
  "Noyabr",
  "Dekabr",
] as const;

interface PreviewGameItem {
  key: string;
  to: string;
  name: string;
  coverUrl: string | null;
}

interface GamePreviewPanelProps {
  title: string;
  description: string;
  games: PreviewGameItem[];
  isLoading: boolean;
  emptyMessage: string;
  onOpenAll?: () => void;
  allHref?: string;
}

function GamePreviewPanel({
  title,
  description,
  games,
  isLoading,
  emptyMessage,
  onOpenAll,
  allHref,
}: GamePreviewPanelProps) {
  const allButtonClassName =
    "flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-white/[0.07] bg-violet-500/10 text-violet-300 transition hover:border-violet-400/25 hover:bg-violet-500/15 hover:text-violet-100";

  return (
    <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.18em] text-violet-300">
            {title}
          </p>
          <p className="mt-2 text-sm text-zinc-500">
            {description}
          </p>
        </div>

        {allHref ? (
          <Link
            to={allHref}
            className={allButtonClassName}
            aria-label={`${title} siyahısının hamısına bax`}
            title="Hamısına bax"
          >
            <ChevronRight size={18} />
          </Link>
        ) : (
          <button
            type="button"
            onClick={onOpenAll}
            className={allButtonClassName}
            aria-label={`${title} siyahısının hamısına bax`}
            title="Hamısına bax"
          >
            <ChevronRight size={18} />
          </button>
        )}
      </div>

      {isLoading ? (
        <div className="mt-5 grid gap-3 sm:grid-cols-2">
          {Array.from({ length: 4 }).map((_, index) => (
            <div
              key={index}
              className="h-[5.5rem] animate-pulse rounded-2xl border border-white/[0.06] bg-white/[0.025]"
            />
          ))}
        </div>
      ) : games.length === 0 ? (
        <div className="mt-5 flex min-h-[12rem] items-center justify-center rounded-2xl border border-dashed border-white/[0.08] px-5 py-8 text-center text-sm text-zinc-600">
          {emptyMessage}
        </div>
      ) : (
        <div className="mt-5 grid gap-3 sm:grid-cols-2">
          {games.slice(0, 4).map((game) => {
            const coverUrl = resolveAssetUrl(game.coverUrl);

            return (
              <Link
                key={game.key}
                to={game.to}
                className="group flex h-[5.5rem] min-w-0 items-center gap-3 rounded-2xl border border-white/[0.07] bg-white/[0.025] p-3 transition hover:border-violet-400/20 hover:bg-violet-500/[0.04]"
              >
                <span className="flex h-16 w-12 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-white/[0.04] text-zinc-700">
                  {coverUrl ? (
                    <img
                      src={coverUrl}
                      alt={`${game.name} örtüyü`}
                      className="h-full w-full object-cover transition duration-300 group-hover:scale-105"
                    />
                  ) : (
                    <Gamepad2 size={18} />
                  )}
                </span>

                <span className="min-w-0 flex-1">
                  <span className="line-clamp-2 block text-sm font-bold leading-5 text-zinc-100">
                    {game.name}
                  </span>
                </span>
              </Link>
            );
          })}
        </div>
      )}
    </section>
  );
}

function formatRating(value: number): string {
  return Number.isInteger(value) ? value.toFixed(0) : value.toFixed(1);
}

function formatPlaytime(totalMinutes: number): string {
  const totalHours = totalMinutes / 60;

  if (totalHours < 1) {
    return `${totalMinutes} dəq`;
  }

  return `${new Intl.NumberFormat("az-AZ", {
    maximumFractionDigits: totalHours < 100 ? 1 : 0,
  }).format(totalHours)} saat`;
}

function parseChartDate(value: string): Date {
  return new Date(`${value}T00:00:00`);
}

function formatChartLabel(
  startDate: string,
  endDate: string,
  period: UserPlaytimePeriod,
): string {
  const start = parseChartDate(startDate);
  const end = parseChartDate(endDate);

  if (period === "Weekly") {
    return azWeekdayNames[start.getDay()];
  }

  if (period === "Monthly") {
    const dayRange = start.getDate() === end.getDate()
      ? String(start.getDate())
      : `${start.getDate()}–${end.getDate()}`;

    return `${dayRange} ${azMonthNames[start.getMonth()]}`;
  }

  return azMonthNames[start.getMonth()];
}

function formatChartRangeTitle(
  startDate: string,
  endDate: string,
): string {
  const start = parseChartDate(startDate);
  const end = parseChartDate(endDate);

  if (startDate === endDate) {
    return `${azWeekdayNames[start.getDay()]}, ${start.getDate()} ${azMonthNames[start.getMonth()]} ${start.getFullYear()}`;
  }

  if (
    start.getMonth() === end.getMonth() &&
    start.getFullYear() === end.getFullYear()
  ) {
    return `${start.getDate()}–${end.getDate()} ${azMonthNames[start.getMonth()]} ${start.getFullYear()}`;
  }

  return `${start.getDate()} ${azMonthNames[start.getMonth()]} ${start.getFullYear()} – ${end.getDate()} ${azMonthNames[end.getMonth()]} ${end.getFullYear()}`;
}

function formatTopStatPlaytime(totalMinutes: number): string {
  return totalMinutes > 0
    ? formatPlaytime(totalMinutes)
    : "Məlumat yoxdur";
}

export function ProfileOverview({
  profile,
  recentPlayedGames,
  recentPlayedGamesLoading,
  favoriteGames,
  favoriteGamesLoading,
  canViewFavorites,
  onOpenFavorites,
  onOpenRecentPlayed,
  onOpenTopGenre,
  onOpenTopPlatform,
  playtimeStatistics,
  playtimePeriod,
  playtimeStatisticsLoading,
  playtimeStatisticsError,
  onPlaytimePeriodChange,
  onRatingSelect,
}: ProfileOverviewProps) {
  const [selectedPlaytimePoint, setSelectedPlaytimePoint] =
    useState<UserPlaytimeChartPointDTO | null>(null);
  const topGenre = playtimeStatistics?.topGenre;
  const topPlatform = playtimeStatistics?.topPlatform;

  useEffect(() => {
    if (!selectedPlaytimePoint) {
      return;
    }

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    function handleEscape(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setSelectedPlaytimePoint(null);
      }
    }

    document.addEventListener("keydown", handleEscape);

    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", handleEscape);
    };
  }, [selectedPlaytimePoint]);

  const counts = new Map(
    profile.statistics.ratingDistribution.map((item) => [
      item.rating,
      item.count,
    ]),
  );
  const maximumCount = Math.max(
    1,
    ...ratingSteps.map((rating) => counts.get(rating) ?? 0),
  );

  const playtimeChart = playtimeStatistics?.chart ?? [];
  const maximumPlaytime = Math.max(
    1,
    ...playtimeChart.map((item) => item.playtimeMinutes),
  );
  const topGameCoverUrl = resolveAssetUrl(
    playtimeStatistics?.topGame?.coverUrl ?? null,
  );
  const recentPreviewGames: PreviewGameItem[] = recentPlayedGames
    .slice(0, 4)
    .map((game) => ({
      key: `${game.source}-${game.gameId}`,
      to: `/games/${game.source}/${game.gameId}`,
      name: game.name,
      coverUrl: game.coverUrl,
    }));
  const favoritePreviewGames: PreviewGameItem[] = favoriteGames
    .slice(0, 4)
    .map((game) => ({
      key: `${game.source}-${game.gameId}`,
      to: `/games/${game.source}/${game.gameId}`,
      name: game.name,
      coverUrl: game.coverUrl,
    }));

  return (
    <div className="space-y-5">
      <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.18em] text-fuchsia-300">
              Puan qrafiki
            </p>
            <p className="mt-2 text-sm text-zinc-500">
              Rəy yazılıb-yazılmamasından asılı olmayaraq verilən bütün puanlar.
            </p>
          </div>
          <div className="text-right">
            <p className="flex items-center justify-end gap-1 text-2xl font-bold text-white">
              {profile.statistics.averageRating === null
                ? "—"
                : profile.statistics.averageRating.toFixed(1)}
              <Star size={16} className="fill-fuchsia-300 text-fuchsia-300" />
            </p>
            <p className="mt-1 text-xs text-zinc-600">
              {profile.statistics.ratingCount} puan
            </p>
          </div>
        </div>

        <div className="mt-7 flex h-36 items-end gap-1.5">
          {ratingSteps.map((rating) => {
            const count = counts.get(rating) ?? 0;
            const height =
              count === 0 ? 4 : Math.max(14, (count / maximumCount) * 100);

            return (
              <button
                key={rating}
                type="button"
                onClick={() => {
                  onRatingSelect(rating);
                }}
                className="group relative flex h-full min-w-0 flex-1 cursor-pointer items-end rounded-t-lg outline-none focus-visible:ring-2 focus-visible:ring-violet-400/60 focus-visible:ring-offset-2 focus-visible:ring-offset-[#101015]"
                aria-label={`${formatRating(rating)} ulduz: ${count} oyun`}
              >
                <span
                  className={`w-full rounded-t-lg transition group-hover:-translate-y-1 group-focus-visible:-translate-y-1 ${
                    count > 0
                      ? "bg-gradient-to-t from-violet-600 to-fuchsia-400"
                      : "bg-white/[0.06]"
                  }`}
                  style={{ height: `${height}%` }}
                />
                <span className="pointer-events-none absolute bottom-[calc(100%+8px)] left-1/2 z-10 hidden -translate-x-1/2 whitespace-nowrap rounded-lg border border-white/[0.1] bg-[#181820] px-2.5 py-1.5 text-[11px] font-semibold text-zinc-100 shadow-xl group-hover:block group-focus-visible:block">
                  {formatRating(rating)}★ · {count} oyun
                </span>
              </button>
            );
          })}
        </div>
        <div className="mt-2 flex justify-between text-[10px] text-zinc-600">
          <span>0.5★</span>
          <span>5★</span>
        </div>
      </section>

      <section className="grid gap-5 xl:grid-cols-[1.35fr_0.95fr]">
        <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <p className="text-xs font-bold uppercase tracking-[0.18em] text-fuchsia-300">
                Oyun vaxtı qrafiki
              </p>
              <p className="mt-2 text-sm text-zinc-500">
                Loglarda qeyd olunan oyun vaxtının və oynanılan oyun sayının dövrlər üzrə bölgüsü.
              </p>
            </div>

            <div className="flex w-fit items-center gap-1 rounded-xl border border-white/[0.07] bg-white/[0.025] p-1">
              {playtimePeriods.map((item) => (
                <button
                  key={item.value}
                  type="button"
                  onClick={() => onPlaytimePeriodChange(item.value)}
                  className={`rounded-lg px-3 py-2 text-xs font-semibold transition ${
                    playtimePeriod === item.value
                      ? "bg-violet-500/15 text-violet-200"
                      : "text-zinc-500 hover:bg-white/[0.04] hover:text-zinc-200"
                  }`}
                >
                  {item.label}
                </button>
              ))}
            </div>
          </div>

          <div className="mt-4 flex flex-wrap items-center gap-2">
            <span className="inline-flex items-center gap-2 rounded-xl border border-white/[0.07] bg-white/[0.03] px-3 py-2 text-xs font-semibold text-zinc-400">
              <Clock3 size={14} className="text-violet-300" />
              Bu dövr: {playtimeStatistics && playtimeStatistics.periodTotalPlaytimeMinutes > 0
                ? formatPlaytime(playtimeStatistics.periodTotalPlaytimeMinutes)
                : "Məlumat yoxdur"}
            </span>
            <span className="inline-flex items-center gap-2 rounded-xl border border-white/[0.07] bg-white/[0.03] px-3 py-2 text-xs font-semibold text-zinc-500">
              Ümumi: {playtimeStatistics && playtimeStatistics.allTimeTotalPlaytimeMinutes > 0
                ? formatPlaytime(playtimeStatistics.allTimeTotalPlaytimeMinutes)
                : "Məlumat yoxdur"}
            </span>
          </div>

          {playtimeStatisticsLoading ? (
            <div className="mt-6 flex h-44 items-end gap-2">
              {Array.from({ length: playtimePeriod === "Yearly" ? 12 : 7 }).map((_, index) => (
                <span
                  key={index}
                  className="h-full min-w-0 flex-1 animate-pulse rounded-t-lg bg-white/[0.035]"
                  style={{ height: `${28 + ((index * 13) % 56)}%` }}
                />
              ))}
            </div>
          ) : playtimeStatisticsError ? (
            <div className="mt-6 flex h-44 items-center justify-center rounded-2xl border border-red-400/15 bg-red-500/[0.04] px-5 text-center text-sm text-red-200">
              {playtimeStatisticsError}
            </div>
          ) : (
            <>
              <div className="mt-6 flex h-44 items-end gap-2">
                {playtimeChart.map((item) => {
                  const height = item.playtimeMinutes === 0
                    ? 4
                    : Math.max(
                        12,
                        (item.playtimeMinutes / maximumPlaytime) * 100,
                      );

                  return (
                    <button
                      type="button"
                      key={`${item.startDate}-${item.endDate}`}
                      onClick={() => setSelectedPlaytimePoint(item)}
                      className="group relative flex h-full min-w-0 flex-1 cursor-pointer items-end rounded-t-lg outline-none focus-visible:ring-2 focus-visible:ring-violet-400/60 focus-visible:ring-offset-2 focus-visible:ring-offset-[#101015]"
                      aria-label={`${formatChartRangeTitle(item.startDate, item.endDate)}: ${formatPlaytime(item.playtimeMinutes)}, ${item.gameCount} oyun`}
                    >
                      <span
                        className={`w-full rounded-t-lg transition group-hover:-translate-y-1 group-focus-visible:-translate-y-1 ${
                          item.playtimeMinutes > 0
                            ? "bg-gradient-to-t from-violet-600 to-fuchsia-400"
                            : "bg-white/[0.06]"
                        }`}
                        style={{ height: `${height}%` }}
                      />
                      <span className="pointer-events-none absolute bottom-[calc(100%+8px)] left-1/2 z-10 hidden -translate-x-1/2 whitespace-nowrap rounded-lg border border-white/[0.1] bg-[#181820] px-2.5 py-1.5 text-[11px] font-semibold text-zinc-100 shadow-xl group-hover:block group-focus-visible:block">
                        {formatPlaytime(item.playtimeMinutes)} · {item.gameCount} oyun
                      </span>
                    </button>
                  );
                })}
              </div>
              <div
                className="mt-2 grid gap-2 text-center text-[10px] text-zinc-600"
                style={{
                  gridTemplateColumns: `repeat(${Math.max(playtimeChart.length, 1)}, minmax(0, 1fr))`,
                }}
              >
                {playtimeChart.map((item) => (
                  <span
                    key={`${item.startDate}-${item.endDate}-label`}
                    className="break-words leading-4"
                    title={formatChartRangeTitle(item.startDate, item.endDate)}
                  >
                    {formatChartLabel(
                      item.startDate,
                      item.endDate,
                      playtimePeriod,
                    )}
                  </span>
                ))}
              </div>
            </>
          )}
        </section>

        <div className="grid gap-3 sm:grid-cols-3 xl:grid-cols-1 xl:grid-rows-3">
          {playtimeStatistics?.topGame ? (
            <Link
              to={`/games/${playtimeStatistics.topGame.source}/${playtimeStatistics.topGame.gameId}`}
              className="group flex min-h-0 items-center gap-4 rounded-3xl border border-white/[0.07] bg-[#101015] p-5 transition hover:border-violet-400/20 hover:bg-violet-500/[0.04]"
            >
              <span className="flex h-16 w-12 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-violet-500/10 text-violet-300">
                {topGameCoverUrl ? (
                  <img
                    src={topGameCoverUrl}
                    alt={`${playtimeStatistics.topGame.name} örtüyü`}
                    className="h-full w-full object-cover transition duration-300 group-hover:scale-105"
                  />
                ) : (
                  <Trophy size={18} />
                )}
              </span>
              <span className="min-w-0">
                <span className="block text-[11px] font-bold uppercase tracking-[0.14em] text-zinc-600">
                  Ən çox oynanan oyun
                </span>
                <span className="mt-2 block truncate text-sm font-bold text-zinc-100">
                  {playtimeStatistics.topGame.name}
                </span>
                <span className="mt-1 block text-xs text-violet-300">
                  {formatTopStatPlaytime(playtimeStatistics.topGame.playtimeMinutes)}
                </span>
              </span>
            </Link>
          ) : (
            <article className="flex min-h-0 items-center gap-4 rounded-3xl border border-white/[0.07] bg-[#101015] p-5">
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                <Trophy size={18} />
              </span>
              <span className="min-w-0">
                <span className="block text-[11px] font-bold uppercase tracking-[0.14em] text-zinc-600">
                  Ən çox oynanan oyun
                </span>
                <span className="mt-2 block text-sm font-bold text-zinc-400">
                  Məlumat yoxdur
                </span>
              </span>
            </article>
          )}

          {topGenre ? (
            <button
              type="button"
              onClick={() => {
                onOpenTopGenre(topGenre.name);
              }}
              className="group flex min-h-0 w-full items-center gap-4 rounded-3xl border border-white/[0.07] bg-[#101015] p-5 text-left transition hover:border-violet-400/20 hover:bg-violet-500/[0.04]"
              aria-label={`${topGenre.name} janrındakı profil oyunlarını göstər`}
            >
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                <Shapes size={18} />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block text-[11px] font-bold uppercase tracking-[0.14em] text-zinc-600">
                  Ən çox oynanan janr
                </span>
                <span className="mt-2 block truncate text-sm font-bold text-zinc-100">
                  {topGenre.name}
                </span>
                <span className="mt-1 block text-xs text-violet-300">
                  {formatTopStatPlaytime(topGenre.playtimeMinutes)} · {topGenre.percentage}%
                </span>
              </span>
              <ChevronRight
                size={16}
                className="shrink-0 text-zinc-700 transition group-hover:text-violet-300"
              />
            </button>
          ) : (
            <article className="flex min-h-0 items-center gap-4 rounded-3xl border border-white/[0.07] bg-[#101015] p-5">
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                <Shapes size={18} />
              </span>
              <span className="min-w-0">
                <span className="block text-[11px] font-bold uppercase tracking-[0.14em] text-zinc-600">
                  Ən çox oynanan janr
                </span>
                <span className="mt-2 block text-sm font-bold text-zinc-400">
                  Məlumat yoxdur
                </span>
              </span>
            </article>
          )}

          {topPlatform ? (
            <button
              type="button"
              onClick={() => {
                onOpenTopPlatform(topPlatform.name);
              }}
              className="group flex min-h-0 w-full items-center gap-4 rounded-3xl border border-white/[0.07] bg-[#101015] p-5 text-left transition hover:border-violet-400/20 hover:bg-violet-500/[0.04]"
              aria-label={`${topPlatform.name} platformasında oynanmış profil oyunlarını göstər`}
            >
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                <Monitor size={18} />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block text-[11px] font-bold uppercase tracking-[0.14em] text-zinc-600">
                  Ən çox oynanan platforma
                </span>
                <span className="mt-2 block truncate text-sm font-bold text-zinc-100">
                  {topPlatform.name}
                </span>
                <span className="mt-1 block text-xs text-violet-300">
                  {formatTopStatPlaytime(topPlatform.playtimeMinutes)} · {topPlatform.percentage}%
                </span>
              </span>
              <ChevronRight
                size={16}
                className="shrink-0 text-zinc-700 transition group-hover:text-violet-300"
              />
            </button>
          ) : (
            <article className="flex min-h-0 items-center gap-4 rounded-3xl border border-white/[0.07] bg-[#101015] p-5">
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                <Monitor size={18} />
              </span>
              <span className="min-w-0">
                <span className="block text-[11px] font-bold uppercase tracking-[0.14em] text-zinc-600">
                  Ən çox oynanan platforma
                </span>
                <span className="mt-2 block text-sm font-bold text-zinc-400">
                  Məlumat yoxdur
                </span>
              </span>
            </article>
          )}
        </div>
      </section>

      <section className={`grid gap-5 ${canViewFavorites ? "xl:grid-cols-2" : ""}`}>
        <GamePreviewPanel
          title="Son oynanan oyunlar"
          description="İstifadəçinin ən son tamamladığı 4 oyun."
          games={recentPreviewGames}
          isLoading={recentPlayedGamesLoading}
          emptyMessage="Hələ oynanılmış oyun yoxdur."
          onOpenAll={onOpenRecentPlayed}
        />

        {canViewFavorites && (
          <GamePreviewPanel
            title="Sevimli oyunlar"
            description="Yalnız sənə görünən sevimli oyun kolleksiyası."
            games={favoritePreviewGames}
            isLoading={favoriteGamesLoading}
            emptyMessage="Hələ sevimli oyun yoxdur."
            onOpenAll={onOpenFavorites}
          />
        )}
      </section>

      {selectedPlaytimePoint && (
        <div
          className="fixed inset-0 z-[80] flex items-center justify-center bg-black/75 px-4 py-8 backdrop-blur-sm"
          onMouseDown={(event) => {
            if (event.currentTarget === event.target) {
              setSelectedPlaytimePoint(null);
            }
          }}
        >
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="playtime-modal-title"
            className="max-h-[80vh] w-full max-w-xl overflow-hidden rounded-3xl border border-violet-400/15 bg-[#111116] shadow-2xl shadow-violet-950/40"
          >
            <div className="flex items-start justify-between gap-4 border-b border-white/[0.07] p-5 sm:p-6">
              <div>
                <p className="text-[11px] font-bold uppercase tracking-[0.18em] text-fuchsia-300">
                  Oyun vaxtı detalları
                </p>
                <h2
                  id="playtime-modal-title"
                  className="mt-2 text-lg font-bold text-white"
                >
                  {formatChartRangeTitle(
                    selectedPlaytimePoint.startDate,
                    selectedPlaytimePoint.endDate,
                  )}
                </h2>
                <p className="mt-2 text-sm text-zinc-500">
                  {formatPlaytime(selectedPlaytimePoint.playtimeMinutes)} · {selectedPlaytimePoint.gameCount} oyun
                </p>
              </div>

              <button
                type="button"
                onClick={() => setSelectedPlaytimePoint(null)}
                className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-white/[0.08] bg-white/[0.04] text-zinc-400 transition hover:bg-white/[0.08] hover:text-white"
                aria-label="Modalı bağla"
              >
                <X size={18} />
              </button>
            </div>

            <div className="max-h-[calc(80vh-9rem)] overflow-y-auto p-5 sm:p-6">
              {selectedPlaytimePoint.games.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-white/[0.08] px-5 py-10 text-center text-sm text-zinc-600">
                  Bu vaxt aralığında oyun logu qeydə alınmayıb.
                </div>
              ) : (
                <div className="space-y-3">
                  {selectedPlaytimePoint.games.map((game) => {
                    const coverUrl = resolveAssetUrl(game.coverUrl);

                    return (
                      <Link
                        key={`${game.source}-${game.gameId}`}
                        to={`/games/${game.source}/${game.gameId}`}
                        onClick={() => setSelectedPlaytimePoint(null)}
                        className="group flex items-center gap-4 rounded-2xl border border-white/[0.07] bg-white/[0.025] p-3 transition hover:border-violet-400/20 hover:bg-violet-500/[0.04]"
                      >
                        <span className="flex h-16 w-12 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-white/[0.04] text-zinc-700">
                          {coverUrl ? (
                            <img
                              src={coverUrl}
                              alt={`${game.name} örtüyü`}
                              className="h-full w-full object-cover transition duration-300 group-hover:scale-105"
                            />
                          ) : (
                            <Gamepad2 size={18} />
                          )}
                        </span>

                        <span className="min-w-0 flex-1">
                          <span className="block truncate text-sm font-bold text-zinc-100">
                            {game.name}
                          </span>
                          <span className="mt-1 flex items-center gap-1.5 text-xs text-violet-300">
                            <Clock3 size={13} />
                            {formatPlaytime(game.playtimeMinutes)}
                          </span>
                        </span>

                        <ChevronRight
                          size={17}
                          className="shrink-0 text-zinc-600 transition group-hover:text-violet-300"
                        />
                      </Link>
                    );
                  })}
                </div>
              )}
            </div>
          </section>
        </div>
      )}

      <ProfileBadges badges={profile.badges} />
    </div>
  );
}
