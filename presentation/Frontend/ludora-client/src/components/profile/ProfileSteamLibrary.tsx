import {
  ChevronLeft,
  ChevronRight,
  Clock3,
  Gamepad2,
  Link2,
  LoaderCircle,
  RefreshCw,
  Unlink,
} from "lucide-react";
import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  getCurrentLanguage,
  getCurrentLocale,
} from "../../i18n/localization";
import { useI18n } from "../../i18n/useI18n";
import { getApiErrorMessage } from "../../lib/api";
import {
  formatPlaytime as formatLocalizedPlaytime,
  resolveAssetUrl,
} from "../../lib/helpers";
import type { SteamOwnedGameDTO } from "../../types/steam";

interface ProfileSteamLibraryProps {
  games: SteamOwnedGameDTO[];
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  isOwnProfile: boolean;
  isSyncing: boolean;
  onSync: () => void;
  onResolvePlaytime: () => void;
}

function formatPlaytime(minutes: number): string {
  if (getCurrentLanguage() === "en") {
    return minutes <= 0
      ? "Not played"
      : formatLocalizedPlaytime(minutes);
  }

  if (minutes <= 0) return "Oynanılmayıb";

  const hours = Math.floor(minutes / 60);
  const remainingMinutes = minutes % 60;

  if (hours === 0) return `${remainingMinutes} dəq`;
  if (remainingMinutes === 0) return `${hours} saat`;
  return `${hours} saat ${remainingMinutes} dəq`;
}

function formatLastPlayed(
  value: string | null | undefined,
  playtimeMinutes: number,
): string {
  const isEnglish = getCurrentLanguage() === "en";
  if (playtimeMinutes <= 0) {
    return isEnglish ? "Not played yet" : "Hələki oynanmayıb";
  }
  if (!value) {
    return isEnglish
      ? "Last played date is unknown"
      : "Son oynanma tarixi məlum deyil";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return isEnglish
      ? "Last played date is unknown"
      : "Son oynanma tarixi məlum deyil";
  }

  if (!isEnglish) {
    const day = String(date.getDate()).padStart(2, "0");
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const year = date.getFullYear();
    return `Son oynanma: ${day}.${month}.${year}`;
  }

  const formattedDate = new Intl.DateTimeFormat(getCurrentLocale(), {
    day: "numeric",
    month: "long",
    year: "numeric",
  }).format(date);
  return `Last played: ${formattedDate}`;
}

function SteamGameCover({ game }: { game: SteamOwnedGameDTO }) {
  const localCoverUrl = resolveAssetUrl(game.matchedCoverImageUrl ?? game.localCoverImageUrl ?? null);
  const steamHeaderUrl = `https://cdn.akamai.steamstatic.com/steam/apps/${game.steamAppId}/header.jpg`;
  const [failedUrls, setFailedUrls] = useState<Set<string>>(() => new Set());
  const candidates = [localCoverUrl, steamHeaderUrl, game.steamIconUrl]
    .filter((value): value is string => Boolean(value))
    .filter((value) => !failedUrls.has(value));
  const coverUrl = candidates[0] ?? null;

  return (
    <div className="relative aspect-[16/9] overflow-hidden bg-gradient-to-br from-zinc-800 to-zinc-950">
      {coverUrl ? (
        <img
          src={coverUrl}
          alt={`${game.name} şəkli`}
          loading="lazy"
          className="h-full w-full object-cover transition duration-500 group-hover:scale-105"
          onError={() => {
            setFailedUrls((currentValue) => {
              const nextValue = new Set(currentValue);
              nextValue.add(coverUrl);
              return nextValue;
            });
          }}
        />
      ) : (
        <div className="flex h-full flex-col items-center justify-center gap-3 px-4 text-center text-zinc-700">
          <Gamepad2 size={30} />
          <span className="line-clamp-2 text-xs font-semibold">{game.name}</span>
        </div>
      )}

      <div className="pointer-events-none absolute inset-0 bg-gradient-to-t from-[#09090c]/90 via-transparent to-transparent" />

      <span
        className={`absolute right-2 top-2 inline-flex items-center gap-1.5 rounded-lg border px-2 py-1 text-[10px] font-bold backdrop-blur-sm ${
          game.isSteamFallback
            ? "border-amber-300/15 bg-amber-500/15 text-amber-200"
            : game.isMatched
              ? "border-emerald-300/15 bg-emerald-500/15 text-emerald-200"
              : "border-white/[0.08] bg-black/65 text-zinc-400"
        }`}
      >
        {game.isSteamFallback ? <Unlink size={11} /> : <Link2 size={11} />}
        {game.isSteamFallback
          ? "Steam kataloqu"
          : game.isMatched
            ? "WellPlayd ilə uyğun"
            : "Uyğunlaşdırılmayıb"}
      </span>
    </div>
  );
}

export function ProfileSteamLibrary({
  games,
  isLoading,
  isError,
  error,
  isOwnProfile,
  isSyncing,
  onSync,
  onResolvePlaytime,
}: ProfileSteamLibraryProps) {
  const { language } = useI18n();
  const isEnglish = language === "en";
  const gamesPerPage = 16;
  const [currentPage, setCurrentPage] = useState(1);

  const totalPages = Math.max(1, Math.ceil(games.length / gamesPerPage));
  const activePage = Math.min(currentPage, totalPages);

  const paginatedGames = useMemo(() => {
    const startIndex = (activePage - 1) * gamesPerPage;
    return games.slice(startIndex, startIndex + gamesPerPage);
  }, [activePage, games]);
  if (isLoading) {
    return (
      <div className="flex items-center justify-center gap-3 py-20 text-sm text-zinc-500">
        <LoaderCircle size={20} className="animate-spin" />
        Steam kitabxanası yüklənir...
      </div>
    );
  }

  if (isError) {
    return (
      <div className="rounded-3xl border border-amber-400/15 bg-amber-500/[0.05] p-8 text-center">
        <Gamepad2 size={32} className="mx-auto text-amber-300" />
        <p className="mt-4 text-sm leading-7 text-amber-100/75">
          {getApiErrorMessage(
            error,
            "Steam kitabxanasına baxmaq mümkün olmadı.",
          )}
        </p>
      </div>
    );
  }

  const totalPlaytimeMinutes = games.reduce(
    (total, game) => total + game.playtimeMinutes,
    0,
  );
  const matchedCount = games.filter((game) => game.isMatched && !game.isSteamFallback).length;
  const hasPendingResolution = games.some((game) => game.needsResolution);

  return (
    <div className="space-y-6">
      <section className="flex flex-col gap-5 rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.18em] text-violet-300">
            Sahib olunan oyunlar
          </p>
          <h2 className="mt-2 text-xl font-bold text-white">Steam kitabxanası</h2>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-zinc-500">
            Steam-dən sinxronlaşdırılmış oyunlar və dəqiq ümumi oynanma vaxtları.
          </p>
        </div>

        {isOwnProfile && (
          <div className="flex flex-wrap gap-2 sm:justify-end">
            {hasPendingResolution && (
              <button
                type="button"
                onClick={onResolvePlaytime}
                className="inline-flex h-11 shrink-0 items-center justify-center gap-2 rounded-xl border border-amber-300/20 bg-amber-500/10 px-5 text-sm font-semibold text-amber-100 transition hover:bg-amber-500/15"
              >
                <Clock3 size={17} />
                Oyun saatlarını təsdiqlə
              </button>
            )}
            <button
              type="button"
              onClick={onSync}
              disabled={isSyncing}
              className="inline-flex h-11 shrink-0 items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isSyncing ? (
                <LoaderCircle size={17} className="animate-spin" />
              ) : (
                <RefreshCw size={17} />
              )}
              Steam ilə yenilə
            </button>
          </div>
        )}
      </section>

      <section className="grid gap-3 sm:grid-cols-3">
        <article className="rounded-2xl border border-white/[0.07] bg-[#101015] p-5">
          <span className="text-[11px] font-bold uppercase tracking-[0.14em] text-zinc-600">
            Oyun sayı
          </span>
          <strong className="mt-2 block text-2xl text-white">{games.length}</strong>
        </article>
        <article className="rounded-2xl border border-white/[0.07] bg-[#101015] p-5">
          <span className="text-[11px] font-bold uppercase tracking-[0.14em] text-zinc-600">
            WellPlayd uyğunluğu
          </span>
          <strong className="mt-2 block text-2xl text-white">{matchedCount}</strong>
        </article>
        <article className="rounded-2xl border border-white/[0.07] bg-[#101015] p-5">
          <span className="text-[11px] font-bold uppercase tracking-[0.14em] text-zinc-600">
            Ümumi oyun vaxtı
          </span>
          <strong className="mt-2 block text-2xl text-white">
            {formatPlaytime(totalPlaytimeMinutes)}
          </strong>
        </article>
      </section>

      {games.length === 0 ? (
        <div className="rounded-3xl border border-dashed border-white/[0.08] px-6 py-16 text-center">
          <Gamepad2 size={34} className="mx-auto text-zinc-700" />
          <p className="mt-4 text-sm text-zinc-500">
            Steam hesabı bağlı deyil və ya kitabxana hələ sinxronlaşdırılmayıb.
          </p>
        </div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {paginatedGames.map((game) => {
            const gameHref =
              game.matchedGameSource && game.matchedGameId
                ? `/games/${game.matchedGameSource}/${game.matchedGameId}`
                : game.localGameId
                  ? `/games/Local/${game.localGameId}`
                  : game.detailUrl;
            const steamStoreUrl =
              `https://store.steampowered.com/app/${game.steamAppId}`;
            const cardContent = (
              <>
                <SteamGameCover game={game} />
                <div className="p-4">
                  <div className="flex min-w-0 items-start justify-between gap-3">
                    <h3 className="line-clamp-2 min-h-10 text-sm font-bold leading-5 text-zinc-100">
                      {game.name}
                    </h3>
                    <ChevronRight
                      size={16}
                      className={`mt-0.5 shrink-0 transition ${
                        game.isMatched
                          ? "text-zinc-600 group-hover:text-violet-300"
                          : "text-zinc-700 group-hover:text-zinc-400"
                      }`}
                    />
                  </div>

                  <div className="mt-4 flex items-center justify-between gap-3 text-xs">
                    <span className="inline-flex items-center gap-1.5 font-semibold text-violet-300">
                      <Clock3 size={14} />
                      {formatPlaytime(game.playtimeMinutes)}
                    </span>
                    <span className="text-zinc-600">
                      {formatLastPlayed(
                        game.lastPlayedAtUtc,
                        game.playtimeMinutes,
                      )}
                    </span>
                  </div>
                </div>
              </>
            );

            return gameHref ? (
              <Link
                key={game.steamAppId}
                to={gameHref}
                title={
                  game.isSteamFallback
                    ? `${game.name} üçün Steam kataloq səhifəsinə keç`
                    : `${game.matchedGameName ?? game.name} səhifəsinə keç`
                }
                className={`group overflow-hidden rounded-2xl border bg-[#101015] transition duration-300 hover:-translate-y-1 hover:shadow-xl ${
                  game.isMatched
                    ? "border-white/[0.07] hover:border-violet-400/30 hover:shadow-violet-950/20"
                    : "border-white/[0.05] hover:border-white/[0.12] hover:shadow-black/20"
                }`}
              >
                {cardContent}
              </Link>
            ) : (
              <a
                key={game.steamAppId}
                href={steamStoreUrl}
                target="_blank"
                rel="noreferrer"
                title={`${game.name} üçün Steam mağaza səhifəsinə keç`}
                className="group overflow-hidden rounded-2xl border border-white/[0.05] bg-[#101015] transition duration-300 hover:-translate-y-1 hover:border-white/[0.12] hover:shadow-xl hover:shadow-black/20"
              >
                {cardContent}
              </a>
            );
          })}
        </div>
      )}

      {games.length > gamesPerPage && (
        <nav
          className="flex flex-col gap-3 rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-strong)] p-3 sm:flex-row sm:items-center sm:justify-between sm:px-4"
          aria-label={isEnglish ? "Owned games pagination" : "Sahib olunan oyunların səhifələnməsi"}
        >
          <p className="text-center text-xs text-zinc-500 sm:text-left">
            {isEnglish
              ? `Page ${activePage} of ${totalPages} · ${games.length} games`
              : `${activePage} / ${totalPages} səhifə · ${games.length} oyun`}
          </p>

          <div className="flex items-center justify-center gap-2">
            <button
              type="button"
              onClick={() => setCurrentPage(Math.max(1, activePage - 1))}
              disabled={activePage === 1}
              className="inline-flex h-10 items-center justify-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.025] px-3 text-sm font-semibold text-zinc-300 transition hover:border-violet-400/25 hover:bg-violet-500/[0.08] hover:text-violet-200 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <ChevronLeft size={16} />
              <span className="hidden sm:inline">
                {isEnglish ? "Previous" : "Əvvəlki"}
              </span>
            </button>

            <span className="inline-flex h-10 min-w-14 items-center justify-center rounded-xl border border-violet-400/30 bg-violet-500/[0.1] px-3 text-sm font-semibold text-violet-200 sm:hidden">
              {activePage} / {totalPages}
            </span>

            <div className="hidden items-center gap-1 sm:flex">
              {Array.from({ length: totalPages }, (_, index) => index + 1)
                .filter((page) =>
                  totalPages <= 7 ||
                  page === 1 ||
                  page === totalPages ||
                  Math.abs(page - activePage) <= 1,
                )
                .map((page, index, visiblePages) => {
                  const previousPage = visiblePages[index - 1];
                  const shouldShowEllipsis =
                    previousPage !== undefined && page - previousPage > 1;

                  return (
                    <span key={page} className="flex items-center gap-1">
                      {shouldShowEllipsis && (
                        <span className="px-1 text-sm text-zinc-600">…</span>
                      )}
                      <button
                        type="button"
                        onClick={() => setCurrentPage(page)}
                        aria-current={page === activePage ? "page" : undefined}
                        className={`flex h-10 min-w-10 items-center justify-center rounded-xl border px-3 text-sm font-semibold transition ${
                          page === activePage
                            ? "border-violet-400/40 bg-violet-500/[0.12] text-violet-200"
                            : "border-white/[0.07] bg-white/[0.02] text-zinc-500 hover:border-violet-400/25 hover:text-zinc-200"
                        }`}
                      >
                        {page}
                      </button>
                    </span>
                  );
                })}
            </div>

            <button
              type="button"
              onClick={() =>
                setCurrentPage(Math.min(totalPages, activePage + 1))
              }
              disabled={activePage === totalPages}
              className="inline-flex h-10 items-center justify-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.025] px-3 text-sm font-semibold text-zinc-300 transition hover:border-violet-400/25 hover:bg-violet-500/[0.08] hover:text-violet-200 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <span className="hidden sm:inline">
                {isEnglish ? "Next" : "Növbəti"}
              </span>
              <ChevronRight size={16} />
            </button>
          </div>
        </nav>
      )}
    </div>
  );
}
