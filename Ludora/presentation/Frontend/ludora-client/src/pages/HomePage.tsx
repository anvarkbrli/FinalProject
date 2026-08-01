import { useQuery } from "@tanstack/react-query";
import {
  ArrowRight,
  BookOpen,
  Gamepad2,
  ListChecks,
  MessageSquareText,
  Sparkles,
} from "lucide-react";
import { DiscussionCard } from "../components/home/DiscussionCard";
import { DiscussionCardSkeleton } from "../components/home/DiscussionCardSkeleton";
import { GameCard } from "../components/home/GameCard";
import { GameCardSkeleton } from "../components/home/GameCardSkeleton";
import { HomeAnnouncements } from "../components/home/HomeAnnouncements";
import { HomeDiscoverySection } from "../components/home/HomeDiscoverySection";
import { UserWelcomePanel } from "../components/home/UserWelcomePanel";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { resolveAssetUrl } from "../lib/helpers";
import {
  getFeaturedGames,
  getForumCategories,
  getHomeDiscovery,
  getTrendingDiscussions,
} from "../services/homeService";
import type { GameListItemDTO } from "../types/home";

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

export function HomePage() {
  const gamesQuery = useQuery({
    queryKey: ["homepage", "featured-games"],
    queryFn: getFeaturedGames,
  });

  const discoveryQuery = useQuery({
    queryKey: ["homepage", "igdb-discovery"],
    queryFn: getHomeDiscovery,
    staleTime: 20 * 60 * 1000,
  });

  const discussionsQuery = useQuery({
    queryKey: ["homepage", "trending-discussions"],
    queryFn: getTrendingDiscussions,
  });

  const categoriesQuery = useQuery({
    queryKey: ["homepage", "forum-categories"],
    queryFn: getForumCategories,
  });

  const games = gamesQuery.data ?? [];
  const discussions = discussionsQuery.data?.items ?? [];
  const categories = categoriesQuery.data ?? [];

  const homeDiscussions = discussions.slice(0, 4);

  const featuredDiscussion = homeDiscussions[0];

  return (
    <div className="min-h-screen overflow-x-hidden wellplayd-page-bg text-zinc-100">
      <Navbar />

      <main>
        <HomeAnnouncements />

        <UserWelcomePanel />

        <section
          id="home"
          className="relative isolate overflow-hidden border-b border-white/5"
        >
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_20%_20%,rgba(124,58,237,0.18),transparent_32%),radial-gradient(circle_at_85%_30%,rgba(217,70,239,0.10),transparent_28%)]" />

          <div className="pointer-events-none absolute inset-0 opacity-[0.035] [background-image:linear-gradient(to_right,#fff_1px,transparent_1px),linear-gradient(to_bottom,#fff_1px,transparent_1px)] [background-size:48px_48px]" />

          <div className="relative mx-auto grid max-w-7xl gap-14 px-5 py-20 lg:grid-cols-[1.08fr_0.92fr] lg:items-center lg:px-8 lg:py-28">
            <div>
              <div className="inline-flex items-center gap-2 rounded-full border border-violet-400/20 bg-violet-500/10 px-3 py-1.5 text-xs font-semibold text-violet-200">
                <Sparkles size={14} />
                Oyun izləmə və icma bir platformada
              </div>

              <h1 className="mt-7 max-w-3xl text-4xl font-bold leading-[1.08] tracking-[-0.04em] text-white sm:text-5xl lg:text-6xl">
                Oynadıqların bir yerdə.

                <span className="mt-2 block bg-gradient-to-r from-violet-300 via-fuchsia-300 to-rose-300 bg-clip-text text-transparent">
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
                  className="inline-flex items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 py-3.5 text-sm font-semibold text-white shadow-xl shadow-violet-950/40 transition hover:bg-violet-500"
                >
                  Oyunları kəşf et
                  <ArrowRight size={17} />
                </a>

                <a
                  href="#forum"
                  className="inline-flex items-center justify-center gap-2 rounded-xl border border-white/10 bg-white/5 px-5 py-3.5 text-sm font-semibold text-zinc-200 transition hover:border-white/20 hover:bg-white/10"
                >
                  <MessageSquareText size={17} />
                  İcmada nələr olur?
                </a>
              </div>

            </div>

            <div className="relative">
              <div className="absolute -inset-8 rounded-full bg-violet-600/10 blur-3xl" />

              <div className="relative rounded-[28px] border border-white/10 bg-white/[0.035] p-5 shadow-2xl shadow-black/40 backdrop-blur-xl">
                <div className="grid grid-cols-3 gap-3 pb-9">
                  {[0, 1, 2].map((index) => (
                    <HeroGameCover
                      key={index}
                      game={games[index]}
                      index={index}
                    />
                  ))}
                </div>

                <div className="rounded-2xl border border-white/[0.08] bg-[#0b0b0f]/90 p-4">
                  <div className="flex items-center gap-3">
                    <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-fuchsia-500/10 text-fuchsia-300">
                      <MessageSquareText size={19} />
                    </span>

                    <div className="min-w-0">
                      <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-fuchsia-300">
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
                      <span>
                        {featuredDiscussion.likeCount} bəyənmə
                      </span>

                      <span>
                        {featuredDiscussion.replyCount} cavab
                      </span>

                      <span>
                        {featuredDiscussion.viewCount} baxış
                      </span>
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>
        </section>

        <HomeDiscoverySection
          trendingGames={
            discoveryQuery.data?.trendingGames ?? []
          }
          comingSoonGames={
            discoveryQuery.data?.comingSoonGames ?? []
          }
          isLoading={discoveryQuery.isLoading}
          isError={discoveryQuery.isError}
        />

        <section className="mx-auto grid max-w-7xl gap-12 px-5 py-20 lg:grid-cols-[minmax(0,1.6fr)_minmax(320px,0.8fr)] lg:px-8">
          <div
            id="games"
            className="scroll-mt-24"
          >
            <div className="mb-7">
              <p className="text-xs font-bold uppercase tracking-[0.22em] text-violet-400">
                Oyun kataloqu
              </p>

              <h2 className="mt-2 text-2xl font-bold tracking-tight text-white sm:text-3xl">
                Yüksək reytinqli oyunlar
              </h2>

              <p className="mt-2 max-w-xl text-sm leading-6 text-zinc-500">
                Kataloqdakı oyunlar reytinqə görə sıralanaraq
                göstərilir.
              </p>
            </div>

            {gamesQuery.isLoading ? (
              <div className="grid grid-cols-2 gap-4 sm:gap-5 xl:grid-cols-3">
                {Array.from({ length: 6 }).map(
                  (_, index) => (
                    <GameCardSkeleton key={index} />
                  ),
                )}
              </div>
            ) : gamesQuery.isError ? (
              <div className="rounded-2xl border border-red-400/15 bg-red-500/5 p-8 text-center">
                <p className="font-semibold text-red-200">
                  Oyunlar yüklənmədi
                </p>

                <p className="mt-2 text-sm text-zinc-500">
                  Backend-in işlədiyini və API ünvanını
                  yoxla.
                </p>
              </div>
            ) : games.length === 0 ? (
              <div className="rounded-2xl border border-dashed border-white/10 bg-white/[0.02] p-10 text-center">
                <Gamepad2
                  size={30}
                  className="mx-auto text-zinc-600"
                />

                <p className="mt-4 font-semibold text-zinc-300">
                  Hələ oyun tapılmadı
                </p>

                <p className="mt-2 text-sm text-zinc-600">
                  Bazaya oyun əlavə edildikdə burada
                  göstəriləcək.
                </p>
              </div>
            ) : (
              <div className="grid grid-cols-2 gap-4 sm:gap-5 xl:grid-cols-3">
                {games.map((game) => (
                  <GameCard
                    key={`${game.source}-${game.id}`}
                    game={game}
                  />
                ))}
              </div>
            )}
          </div>

          <aside
            id="forum"
            className="scroll-mt-24"
          >
            <div className="lg:sticky lg:top-[88px]">
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.22em] text-fuchsia-400">
                  Forum
                </p>

                <h2 className="mt-2 text-2xl font-bold tracking-tight text-white">
                  İcmada indi
                </h2>

                <p className="mt-2 text-sm leading-6 text-zinc-500">
                  Bəyənmə və aktivliyə görə populyar
                  müzakirələr.
                </p>
              </div>

              <div className="mt-7 space-y-3">
                {discussionsQuery.isLoading ? (
                  Array.from({ length: 4 }).map(
                    (_, index) => (
                      <DiscussionCardSkeleton key={index} />
                    ),
                  )
                ) : discussionsQuery.isError ? (
                  <div className="rounded-2xl border border-red-400/15 bg-red-500/5 p-6 text-sm text-red-200">
                    Müzakirələr yüklənmədi.
                  </div>
                ) : homeDiscussions.length === 0 ? (
                  <div className="rounded-2xl border border-dashed border-white/10 bg-white/[0.02] p-7 text-center text-sm text-zinc-500">
                    Hələ forum müzakirəsi yoxdur.
                  </div>
                ) : (
                  homeDiscussions.map((discussion) => (
                    <DiscussionCard
                      key={discussion.id}
                      discussion={discussion}
                    />
                  ))
                )}
              </div>

              {categories.length > 0 && (
                <div className="mt-7 rounded-2xl border border-white/[0.07] bg-[#101015] p-5">
                  <p className="text-sm font-semibold text-zinc-200">
                    Aktiv kateqoriyalar
                  </p>

                  <div className="mt-4 flex flex-wrap gap-2">
                    {categories.map((category) => (
                      <span
                        key={category.id}
                        className="rounded-lg border border-white/[0.07] bg-white/[0.035] px-3 py-2 text-xs text-zinc-400"
                      >
                        {category.name}

                        <span className="ml-1.5 text-zinc-600">
                          {category.discussionCount}
                        </span>
                      </span>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </aside>
        </section>

        <section className="border-y border-white/5 bg-white/[0.015]">
          <div className="mx-auto grid max-w-7xl gap-4 px-5 py-16 md:grid-cols-3 lg:px-8">
            <article className="rounded-2xl border border-white/[0.07] bg-[#0e0e13] p-6">
              <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                <Gamepad2 size={21} />
              </span>

              <h3 className="mt-5 font-semibold text-white">
                Oyunlarını izlə
              </h3>

              <p className="mt-2 text-sm leading-6 text-zinc-500">
                Hazırda oynadığın, planlaşdırdığın,
                bitirdiyin və yarımçıq qoyduğun oyunları
                qeyd et.
              </p>
            </article>

            <article className="rounded-2xl border border-white/[0.07] bg-[#0e0e13] p-6">
              <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-fuchsia-500/10 text-fuchsia-300">
                <ListChecks size={21} />
              </span>

              <h3 className="mt-5 font-semibold text-white">
                Öz siyahılarını yarat
              </h3>

              <p className="mt-2 text-sm leading-6 text-zinc-500">
                Sevdiyin oyunları mövzuya, janra və şəxsi
                seçimlərinə uyğun kolleksiyalarda topla.
              </p>
            </article>

            <article className="rounded-2xl border border-white/[0.07] bg-[#0e0e13] p-6">
              <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-rose-500/10 text-rose-300">
                <BookOpen size={21} />
              </span>

              <h3 className="mt-5 font-semibold text-white">
                İcmaya qoşul
              </h3>

              <p className="mt-2 text-sm leading-6 text-zinc-500">
                Kateqoriyalar üzrə müzakirələr aç, cavab
                yaz, mövzuları bəyən və trend söhbətləri
                izlə.
              </p>
            </article>
          </div>
        </section>

        <section className="px-5 py-20 lg:px-8">
          <div className="mx-auto max-w-5xl overflow-hidden rounded-[28px] border border-violet-400/15 bg-gradient-to-br from-violet-500/15 via-fuchsia-500/[0.07] to-transparent p-8 text-center sm:p-12">
            <p className="text-xs font-bold uppercase tracking-[0.25em] text-violet-300">
              Bir oyundan daha çoxu
            </p>

            <h2 className="mx-auto mt-4 max-w-2xl text-3xl font-bold tracking-tight text-white sm:text-4xl">
              Növbəti oyunun yalnız siyahında deyil,
              söhbətin içində də olsun.
            </h2>

            <p className="mx-auto mt-4 max-w-xl text-sm leading-7 text-zinc-400">
              WellPlayd oyun kataloqunu şəxsi izləmə sistemi
              və forum icması ilə birləşdirir.
            </p>

            <a
              href="#home"
              className="mt-8 inline-flex items-center gap-2 rounded-xl bg-white px-5 py-3 text-sm font-semibold text-zinc-950 transition hover:bg-zinc-200"
            >
              Yuxarı qayıt

              <ArrowRight
                size={17}
                className="-rotate-90"
              />
            </a>
          </div>
        </section>
      </main>

      <Footer />
    </div>
  );
}
