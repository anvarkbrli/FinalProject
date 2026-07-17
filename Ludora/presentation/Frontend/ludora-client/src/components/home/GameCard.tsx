import {
  CalendarDays,
  Gamepad2,
  Star,
} from "lucide-react";
import {
  getReleaseYear,
  resolveAssetUrl,
} from "../../lib/helpers";
import type { GameListItemDTO } from "../../types/home";

interface GameCardProps {
  game: GameListItemDTO;
}

export function GameCard({ game }: GameCardProps) {
  const coverUrl = resolveAssetUrl(game.coverImageUrl);
  const visibleGenres = game.genres.slice(0, 2);

  return (
    <article className="group overflow-hidden rounded-2xl border border-white/[0.07] bg-[#111116] transition duration-300 hover:-translate-y-1 hover:border-violet-400/30 hover:shadow-2xl hover:shadow-violet-950/20">
      <div className="relative aspect-[3/4] overflow-hidden bg-gradient-to-br from-zinc-800 to-zinc-950">
        {coverUrl ? (
          <img
            src={coverUrl}
            alt={`${game.title} oyun örtüyü`}
            loading="lazy"
            className="h-full w-full object-cover transition duration-500 group-hover:scale-105"
            onError={(event) => {
              event.currentTarget.style.display = "none";
            }}
          />
        ) : (
          <div className="flex h-full flex-col items-center justify-center gap-3 px-5 text-center text-zinc-600">
            <Gamepad2 size={34} />

            <span className="text-sm font-medium">
              {game.title}
            </span>
          </div>
        )}

        <div className="absolute inset-0 bg-gradient-to-t from-[#09090c] via-transparent to-transparent" />

        <span className="absolute left-3 top-3 rounded-full border border-white/10 bg-black/60 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-zinc-200 backdrop-blur-md">
          {game.source === "Igdb" ? "IGDB" : "Ludora"}
        </span>

        {game.averageRating !== null && (
          <span className="absolute bottom-3 right-3 flex items-center gap-1 rounded-lg border border-white/10 bg-black/65 px-2.5 py-1.5 text-xs font-semibold text-amber-300 backdrop-blur-md">
            <Star
              size={13}
              fill="currentColor"
            />

            {game.averageRating.toFixed(1)}
          </span>
        )}
      </div>

      <div className="p-4">
        <h3
          className="line-clamp-2 min-h-12 font-semibold leading-6 text-zinc-100"
          title={game.title}
        >
          {game.title}
        </h3>

        <div className="mt-3 flex items-center gap-1.5 text-xs text-zinc-500">
          <CalendarDays size={14} />

          <span>{getReleaseYear(game.releaseDate)}</span>
        </div>

        <div className="mt-4 flex min-h-6 flex-wrap gap-1.5">
          {visibleGenres.length > 0 ? (
            visibleGenres.map((genre) => (
              <span
                key={`${genre.source}-${genre.id}`}
                className="rounded-md bg-white/5 px-2 py-1 text-[10px] font-medium text-zinc-400"
              >
                {genre.name}
              </span>
            ))
          ) : (
            <span className="text-xs text-zinc-600">
              Janr qeyd edilməyib
            </span>
          )}
        </div>
      </div>
    </article>
  );
}