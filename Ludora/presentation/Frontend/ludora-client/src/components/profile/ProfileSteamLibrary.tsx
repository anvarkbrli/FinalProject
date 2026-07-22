import {
  CalendarDays,
  ChevronRight,
  Clock3,
  Gamepad2,
  Link2,
  LoaderCircle,
  RefreshCw,
  Unlink,
} from "lucide-react";
import { useState } from "react";
import { Link } from "react-router-dom";
import { getApiErrorMessage } from "../../lib/api";
import { formatDate, resolveAssetUrl } from "../../lib/helpers";
import type { SteamOwnedGameDTO } from "../../types/steam";

interface ProfileSteamLibraryProps {
  games: SteamOwnedGameDTO[];
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  isOwnProfile: boolean;
  isSyncing: boolean;
  onSync: () => void;
}

function formatPlaytime(minutes: number): string {
  if (minutes <= 0) return "Oynanılmayıb";

  const hours = Math.floor(minutes / 60);
  const remainingMinutes = minutes % 60;

  if (hours === 0) return `${remainingMinutes} dəq`;
  if (remainingMinutes === 0) return `${hours} saat`;
  return `${hours} saat ${remainingMinutes} dəq`;
}

function formatLastPlayed(value: string | null | undefined): string {
  if (!value) return "Son oynanma yoxdur";

  return new Intl.DateTimeFormat("az-AZ", {
    day: "2-digit",
    month: "short",
    year: "numeric",
  }).format(new Date(value));
}

function SteamGameCover({ game }: { game: SteamOwnedGameDTO }) {
  const localCoverUrl = resolveAssetUrl(game.localCoverImageUrl ?? null);
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
          game.isMatched
            ? "border-emerald-300/15 bg-emerald-500/15 text-emerald-200"
            : "border-white/[0.08] bg-black/65 text-zinc-400"
        }`}
      >
        {game.isMatched ? <Link2 size={11} /> : <Unlink size={11} />}
        {game.isMatched ? "WellPlayd ilə uyğun" : "Yalnız Steam"}
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
}: ProfileSteamLibraryProps) {
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
  const matchedCount = games.filter((game) => game.isMatched).length;

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
          {games.map((game) => {
            const gameHref = game.isMatched && game.localGameId
              ? `/games/Local/${game.localGameId}`
              : `/games?search=${encodeURIComponent(game.name)}`;
            const cardContent = (
              <>
                <SteamGameCover game={game} />
                <div className="p-4">
                  <div className="flex min-w-0 items-start justify-between gap-3">
                    <h3 className="line-clamp-2 min-h-10 text-sm font-bold leading-5 text-zinc-100">
                      {game.localGameName ?? game.name}
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

                  {game.localReleaseDate && (
                    <span className="mt-3 inline-flex items-center gap-1.5 text-xs text-zinc-600">
                      <CalendarDays size={13} />
                      Çıxış: {formatDate(game.localReleaseDate)}
                    </span>
                  )}

                  <div className="mt-4 flex items-center justify-between gap-3 text-xs">
                    <span className="inline-flex items-center gap-1.5 font-semibold text-violet-300">
                      <Clock3 size={14} />
                      {formatPlaytime(game.playtimeMinutes)}
                    </span>
                    <span className="text-zinc-600">{formatLastPlayed(game.lastPlayedAtUtc)}</span>
                  </div>
                </div>
              </>
            );

            return (
              <Link
                key={game.steamAppId}
                to={gameHref}
                title={
                  game.isMatched
                    ? `${game.localGameName ?? game.name} səhifəsinə keç`
                    : `${game.name} oyununu WellPlayd kataloqunda axtar`
                }
                className={`group overflow-hidden rounded-2xl border bg-[#101015] transition duration-300 hover:-translate-y-1 hover:shadow-xl ${
                  game.isMatched
                    ? "border-white/[0.07] hover:border-violet-400/30 hover:shadow-violet-950/20"
                    : "border-white/[0.05] hover:border-white/[0.12] hover:shadow-black/20"
                }`}
              >
                {cardContent}
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}
