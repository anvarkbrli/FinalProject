import {
  ArrowRight,
  Gamepad2,
  MessageSquareText,
  Sparkles,
} from "lucide-react";
import { resolveAssetUrl } from "../../lib/helpers";
import type {
  DiscussionListItemDTO,
  GameListItemDTO,
} from "../../types/home";

interface HomeHeroSectionProps {
  games: GameListItemDTO[];
  featuredDiscussion: DiscussionListItemDTO | undefined;
}

interface HeroGameCoverProps {
  game: GameListItemDTO | undefined;
  index: number;
}

function HeroGameCover({
  game,
  index,
}: HeroGameCoverProps) {
  const coverUrl = resolveAssetUrl(game?.coverImageUrl);

  return (
    <div
      className={`relative aspect-[3/4] overflow-hidden rounded-2xl border border-white/10 bg-gradient-to-br from-zinc-800 to-zinc-950 shadow-2xl ${
        index === 1 ? "translate-y-7" : ""
      }`}
    >
      {coverUrl ? (
        <img
          src={coverUrl}
          alt={game?.title ?? "Oyun örtüyü"}
          className="h-full w-full object-cover"
          decoding="async"
          onError={(event) => {
            event.currentTarget.style.display = "none";
          }}
        />
      ) : (
        <div className="flex h-full flex-col items-center justify-center gap-3 px-3 text-center text-zinc-600">
          <Gamepad2 size={28} />

          <span className="line-clamp-2 text-xs font-medium">
            {game?.title ?? "Oyun"}
          </span>
        </div>
      )}

      <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-transparent to-transparent" />

      {game && (
        <p className="absolute bottom-3 left-3 right-3 line-clamp-2 text-xs font-semibold text-white">
          {game.title}
        </p>
      )}
    </div>
  );
}

export function HomeHeroSection({
  games,
  featuredDiscussion,
}: HomeHeroSectionProps) {
  return (
    <section
      id="home"
      className="home-hero relative isolate overflow-hidden border-b border-white/5"
    >
      <div className="home-hero-aurora pointer-events-none absolute inset-0" />

      <div className="home-hero-grid pointer-events-none absolute inset-0" />

      <div className="relative mx-auto grid max-w-7xl gap-14 px-5 py-20 lg:grid-cols-[1.08fr_0.92fr] lg:items-center lg:px-8 lg:py-28">
        <div>
          <div className="home-hero-kicker inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-xs font-semibold">
            <Sparkles size={14} />
            Oyun izləmə və icma bir platformada
          </div>

          <h1 className="mt-7 max-w-3xl text-4xl font-bold leading-[1.08] tracking-[-0.04em] text-white sm:text-5xl lg:text-6xl">
            Oynadıqların bir yerdə.

            <span className="home-hero-title-gradient mt-2 block bg-clip-text text-transparent">
              Söhbət etdiyin insanlar yanında.
            </span>
          </h1>

          <p className="mt-6 max-w-xl text-base leading-8 text-zinc-400 sm:text-lg">
            Oyunlarını izləmə siyahısına əlavə et,
            statusunu qeyd et, şəxsi kolleksiyalar yarat
            və icmadakı müzakirələrə qoşul.
          </p>

          <div className="mt-8 flex flex-col gap-3 sm:flex-row">
            <a
              href="#games"
              className="home-primary-action inline-flex items-center justify-center gap-2 rounded-xl px-5 py-3.5 text-sm font-semibold text-white transition"
            >
              Oyunları kəşf et
              <ArrowRight size={17} />
            </a>

            <a
              href="#forum"
              className="home-secondary-action inline-flex items-center justify-center gap-2 rounded-xl border border-white/10 bg-white/5 px-5 py-3.5 text-sm font-semibold text-zinc-200 transition"
            >
              <MessageSquareText size={17} />
              İcmada nələr olur?
            </a>
          </div>
        </div>

        <div className="relative">
          <div className="home-hero-preview-glow absolute -inset-8 rounded-full blur-3xl" />

          <div className="home-hero-preview relative rounded-[28px] border p-5 backdrop-blur-xl">
            <div className="grid grid-cols-3 gap-3 pb-9">
              {[0, 1, 2].map((index) => (
                <HeroGameCover
                  key={index}
                  game={games[index]}
                  index={index}
                />
              ))}
            </div>

            <div className="home-trend-card rounded-2xl border p-4">
              <div className="flex items-center gap-3">
                <span className="home-trend-icon flex h-10 w-10 shrink-0 items-center justify-center rounded-xl">
                  <MessageSquareText size={19} />
                </span>

                <div className="min-w-0">
                  <p className="home-trend-label text-[10px] font-bold uppercase tracking-[0.2em]">
                    İcmada trend
                  </p>

                  <p className="mt-1 truncate text-sm font-semibold text-zinc-200">
                    {featuredDiscussion?.title ??
                      "İlk müzakirəni sən başlat"}
                  </p>
                </div>
              </div>

              {featuredDiscussion && (
                <div className="mt-4 flex flex-wrap gap-4 border-t border-white/5 pt-3 text-xs text-zinc-500">
                  <span>{featuredDiscussion.likeCount} bəyənmə</span>
                  <span>{featuredDiscussion.replyCount} cavab</span>
                  <span>{featuredDiscussion.viewCount} baxış</span>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
