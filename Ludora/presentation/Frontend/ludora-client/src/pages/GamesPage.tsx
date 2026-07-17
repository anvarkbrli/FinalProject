import {
  useInfiniteQuery,
  useQuery,
} from "@tanstack/react-query";
import {
  ChevronDown,
  Filter,
  Gamepad2,
  LoaderCircle,
  RefreshCcw,
  RotateCcw,
  Search,
  SlidersHorizontal,
  X,
} from "lucide-react";
import {
  useEffect,
  useMemo,
  useState,
} from "react";
import { GameCard } from "../components/home/GameCard";
import { GameCardSkeleton } from "../components/home/GameCardSkeleton";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { getApiErrorMessage } from "../lib/api";
import {
  getGameGenres,
  getGamePlatforms,
  getGames,
} from "../services/gamesService";
import type { CatalogSource } from "../types/home";
import type {
  CatalogFilterItemDTO,
  GameSort,
} from "../types/games";

const PAGE_SIZE = 12;

const sortOptions: Array<{
  value: GameSort;
  label: string;
}> = [
  {
    value: "",
    label: "Ən yeni oyunlar",
  },
  {
    value: "rating_desc",
    label: "Reytinq: yüksəkdən aşağı",
  },
  {
    value: "rating_asc",
    label: "Reytinq: aşağıdan yüksək",
  },
  {
    value: "play_desc",
    label: "Ən çox oynanılan",
  },
  {
    value: "play_asc",
    label: "Ən az oynanılan",
  },
  {
    value: "year_desc",
    label: "Çıxış ili: yenidən köhnəyə",
  },
  {
    value: "year_asc",
    label: "Çıxış ili: köhnədən yeniyə",
  },
  {
    value: "name_asc",
    label: "Ad: A–Z",
  },
  {
    value: "name_desc",
    label: "Ad: Z–A",
  },
];

function serializeCatalogReference(
  item: CatalogFilterItemDTO,
): string {
  return `${item.source}:${item.id}`;
}

function parseCatalogReference(value: string): {
  source: CatalogSource;
  id: number;
} | null {
  if (!value) {
    return null;
  }

  const [sourceValue, idValue] = value.split(":");
  const id = Number(idValue);

  if (
    (sourceValue !== "Local" &&
      sourceValue !== "Igdb") ||
    !Number.isInteger(id) ||
    id < 1
  ) {
    return null;
  }

  return {
    source: sourceValue,
    id,
  };
}

function getSourceLabel(
  source: CatalogSource,
): string {
  return source === "Local" ? "Ludora" : "IGDB";
}

export function GamesPage() {
  const [searchInput, setSearchInput] =
    useState("");

  const [debouncedSearch, setDebouncedSearch] =
    useState("");

  const [source, setSource] = useState<
    CatalogSource | ""
  >("");

  const [genreReference, setGenreReference] =
    useState("");

  const [
    platformReference,
    setPlatformReference,
  ] = useState("");

  const [releaseYear, setReleaseYear] =
    useState("");

  const [sortBy, setSortBy] =
    useState<GameSort>("");

  const [isMobileFiltersOpen, setIsMobileFiltersOpen] =
    useState(false);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setDebouncedSearch(searchInput.trim());
    }, 350);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [searchInput]);

  useEffect(() => {
    if (source !== "Igdb") {
      return;
    }

    const parsedGenre =
      parseCatalogReference(genreReference);

    const parsedPlatform =
      parseCatalogReference(platformReference);

    if (parsedGenre?.source === "Local") {
      setGenreReference("");
    }

    if (parsedPlatform?.source === "Local") {
      setPlatformReference("");
    }

    if (
      sortBy === "play_asc" ||
      sortBy === "play_desc"
    ) {
      setSortBy("");
    }
  }, [
    genreReference,
    platformReference,
    sortBy,
    source,
  ]);

  const genresQuery = useQuery({
    queryKey: ["game-catalog", "genres"],
    queryFn: getGameGenres,
    staleTime: 5 * 60_000,
  });

  const platformsQuery = useQuery({
    queryKey: ["game-catalog", "platforms"],
    queryFn: getGamePlatforms,
    staleTime: 5 * 60_000,
  });

  const parsedGenre =
    parseCatalogReference(genreReference);

  const parsedPlatform =
    parseCatalogReference(platformReference);

  const numericReleaseYear =
    releaseYear.trim() === ""
      ? undefined
      : Number(releaseYear);

  const validReleaseYear =
    numericReleaseYear !== undefined &&
    Number.isInteger(numericReleaseYear) &&
    numericReleaseYear >= 1900 &&
    numericReleaseYear <= 2200
      ? numericReleaseYear
      : undefined;

  const gamesQuery = useInfiniteQuery({
    queryKey: [
      "game-catalog",
      "games",
      source,
      debouncedSearch,
      genreReference,
      platformReference,
      validReleaseYear,
      sortBy,
    ],

    initialPageParam: 1,

    queryFn: ({ pageParam }) => {
      return getGames({
        source: source || undefined,
        searchName:
          debouncedSearch || undefined,

        genreSource: parsedGenre?.source,
        genreId: parsedGenre?.id,

        platformSource:
          parsedPlatform?.source,
        platformId: parsedPlatform?.id,

        releaseYear: validReleaseYear,
        sortBy,

        pageNumber: pageParam,
        pageSize: PAGE_SIZE,
      });
    },

    getNextPageParam: (
      lastPage,
      allPages,
    ) => {
      if (lastPage.length < PAGE_SIZE) {
        return undefined;
      }

      return allPages.length + 1;
    },
  });

  const games = useMemo(() => {
    const uniqueGames = new Map<
      string,
      NonNullable<
        typeof gamesQuery.data
      >["pages"][number][number]
    >();

    for (
      const page of gamesQuery.data?.pages ?? []
    ) {
      for (const game of page) {
        uniqueGames.set(
          `${game.source}-${game.id}`,
          game,
        );
      }
    }

    return Array.from(uniqueGames.values());
  }, [gamesQuery.data]);

  const visibleGenres = useMemo(() => {
    const genres = genresQuery.data ?? [];

    if (source === "Igdb") {
      return genres.filter(
        (genre) => genre.source === "Igdb",
      );
    }

    return genres;
  }, [genresQuery.data, source]);

  const visiblePlatforms = useMemo(() => {
    const platforms =
      platformsQuery.data ?? [];

    if (source === "Igdb") {
      return platforms.filter(
        (platform) =>
          platform.source === "Igdb",
      );
    }

    return platforms;
  }, [platformsQuery.data, source]);

  const visibleSortOptions =
    source === "Igdb"
      ? sortOptions.filter(
          (option) =>
            option.value !== "play_asc" &&
            option.value !== "play_desc",
        )
      : sortOptions;

  const activeFilterCount = [
    source,
    genreReference,
    platformReference,
    validReleaseYear,
    sortBy,
  ].filter(Boolean).length;

  function resetFilters() {
    setSearchInput("");
    setDebouncedSearch("");
    setSource("");
    setGenreReference("");
    setPlatformReference("");
    setReleaseYear("");
    setSortBy("");
  }

  const gamesError = gamesQuery.isError
    ? getApiErrorMessage(
        gamesQuery.error,
        "Oyunları yükləmək mümkün olmadı.",
      )
    : null;

  const filterContent = (
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
      <label className="block">
        <span className="mb-2 block text-xs font-semibold uppercase tracking-wider text-zinc-500">
          Mənbə
        </span>

        <div className="relative">
          <select
            value={source}
            onChange={(event) => {
              setSource(
                event.target.value as
                  | CatalogSource
                  | "",
              );
            }}
            className="h-11 w-full appearance-none rounded-xl border border-white/[0.08] bg-[#111116] px-3 pr-10 text-sm text-zinc-300 outline-none transition focus:border-violet-400/40 focus:ring-4 focus:ring-violet-500/10"
          >
            <option value="">
              Bütün mənbələr
            </option>

            <option value="Local">
              Ludora
            </option>

            <option value="Igdb">
              IGDB
            </option>
          </select>

          <ChevronDown
            size={16}
            className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-zinc-600"
          />
        </div>
      </label>

      <label className="block">
        <span className="mb-2 block text-xs font-semibold uppercase tracking-wider text-zinc-500">
          Janr
        </span>

        <div className="relative">
          <select
            value={genreReference}
            onChange={(event) => {
              setGenreReference(
                event.target.value,
              );
            }}
            disabled={genresQuery.isLoading}
            className="h-11 w-full appearance-none rounded-xl border border-white/[0.08] bg-[#111116] px-3 pr-10 text-sm text-zinc-300 outline-none transition focus:border-violet-400/40 focus:ring-4 focus:ring-violet-500/10 disabled:opacity-50"
          >
            <option value="">
              Bütün janrlar
            </option>

            {visibleGenres.map((genre) => (
              <option
                key={serializeCatalogReference(
                  genre,
                )}
                value={serializeCatalogReference(
                  genre,
                )}
              >
                {genre.name} —{" "}
                {getSourceLabel(genre.source)}
              </option>
            ))}
          </select>

          <ChevronDown
            size={16}
            className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-zinc-600"
          />
        </div>
      </label>

      <label className="block">
        <span className="mb-2 block text-xs font-semibold uppercase tracking-wider text-zinc-500">
          Platforma
        </span>

        <div className="relative">
          <select
            value={platformReference}
            onChange={(event) => {
              setPlatformReference(
                event.target.value,
              );
            }}
            disabled={platformsQuery.isLoading}
            className="h-11 w-full appearance-none rounded-xl border border-white/[0.08] bg-[#111116] px-3 pr-10 text-sm text-zinc-300 outline-none transition focus:border-violet-400/40 focus:ring-4 focus:ring-violet-500/10 disabled:opacity-50"
          >
            <option value="">
              Bütün platformalar
            </option>

            {visiblePlatforms.map(
              (platform) => (
                <option
                  key={serializeCatalogReference(
                    platform,
                  )}
                  value={serializeCatalogReference(
                    platform,
                  )}
                >
                  {platform.name} —{" "}
                  {getSourceLabel(
                    platform.source,
                  )}
                </option>
              ),
            )}
          </select>

          <ChevronDown
            size={16}
            className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-zinc-600"
          />
        </div>
      </label>

      <label className="block">
        <span className="mb-2 block text-xs font-semibold uppercase tracking-wider text-zinc-500">
          Çıxış ili
        </span>

        <input
          type="number"
          min={1900}
          max={2200}
          value={releaseYear}
          onChange={(event) => {
            setReleaseYear(
              event.target.value,
            );
          }}
          placeholder="Məsələn: 2024"
          className="h-11 w-full rounded-xl border border-white/[0.08] bg-[#111116] px-3 text-sm text-zinc-300 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/40 focus:ring-4 focus:ring-violet-500/10"
        />
      </label>

      <label className="block">
        <span className="mb-2 block text-xs font-semibold uppercase tracking-wider text-zinc-500">
          Sıralama
        </span>

        <div className="relative">
          <select
            value={sortBy}
            onChange={(event) => {
              setSortBy(
                event.target.value as GameSort,
              );
            }}
            className="h-11 w-full appearance-none rounded-xl border border-white/[0.08] bg-[#111116] px-3 pr-10 text-sm text-zinc-300 outline-none transition focus:border-violet-400/40 focus:ring-4 focus:ring-violet-500/10"
          >
            {visibleSortOptions.map(
              (option) => (
                <option
                  key={option.value}
                  value={option.value}
                >
                  {option.label}
                </option>
              ),
            )}
          </select>

          <ChevronDown
            size={16}
            className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-zinc-600"
          />
        </div>
      </label>
    </div>
  );

  return (
    <div className="min-h-screen bg-[#07070a] text-zinc-100">
      <Navbar />

      <main>
        <section className="relative isolate overflow-hidden border-b border-white/[0.06]">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_10%,rgba(124,58,237,0.18),transparent_32%),radial-gradient(circle_at_85%_65%,rgba(217,70,239,0.08),transparent_30%)]" />

          <div className="pointer-events-none absolute inset-0 opacity-[0.025] [background-image:linear-gradient(to_right,#fff_1px,transparent_1px),linear-gradient(to_bottom,#fff_1px,transparent_1px)] [background-size:48px_48px]" />

          <div className="relative mx-auto max-w-7xl px-5 py-16 lg:px-8 lg:py-20">
            <div className="max-w-3xl">
              <div className="inline-flex items-center gap-2 rounded-full border border-violet-400/20 bg-violet-500/10 px-3 py-1.5 text-xs font-semibold text-violet-200">
                <Gamepad2 size={14} />
                Ludora oyun kataloqu
              </div>

              <h1 className="mt-6 text-4xl font-bold tracking-[-0.04em] text-white sm:text-5xl">
                Növbəti oyununu kəşf et
              </h1>

              <p className="mt-4 max-w-2xl text-base leading-8 text-zinc-400">
                Oyunları ada, janra, platformaya,
                çıxış ilinə və reytinqə görə axtar.
              </p>
            </div>

            <div className="relative mt-9 max-w-3xl">
              <Search
                size={20}
                className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600"
              />

              <input
                type="search"
                value={searchInput}
                onChange={(event) => {
                  setSearchInput(
                    event.target.value,
                  );
                }}
                placeholder="Oyun adı ilə axtar..."
                className="h-14 w-full rounded-2xl border border-white/[0.09] bg-black/25 pl-12 pr-12 text-sm text-white outline-none backdrop-blur transition placeholder:text-zinc-600 focus:border-violet-400/50 focus:bg-black/35 focus:ring-4 focus:ring-violet-500/10"
              />

              {searchInput && (
                <button
                  type="button"
                  onClick={() => {
                    setSearchInput("");
                    setDebouncedSearch("");
                  }}
                  className="absolute right-3 top-1/2 flex h-8 w-8 -translate-y-1/2 items-center justify-center rounded-lg text-zinc-600 transition hover:bg-white/5 hover:text-zinc-300"
                  aria-label="Axtarışı təmizlə"
                >
                  <X size={17} />
                </button>
              )}
            </div>
          </div>
        </section>

        <section className="mx-auto max-w-7xl px-5 py-10 lg:px-8">
          <div className="rounded-2xl border border-white/[0.07] bg-[#0d0d12] p-4 sm:p-5">
            <div className="flex items-center justify-between gap-4 xl:hidden">
              <button
                type="button"
                onClick={() => {
                  setIsMobileFiltersOpen(
                    (current) => !current,
                  );
                }}
                className="flex items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.04] px-4 py-2.5 text-sm font-semibold text-zinc-300"
              >
                <Filter size={17} />

                Filterlər

                {activeFilterCount > 0 && (
                  <span className="flex h-5 min-w-5 items-center justify-center rounded-full bg-violet-600 px-1.5 text-[10px] text-white">
                    {activeFilterCount}
                  </span>
                )}
              </button>

              {activeFilterCount > 0 && (
                <button
                  type="button"
                  onClick={resetFilters}
                  className="flex items-center gap-2 text-xs font-semibold text-zinc-500 transition hover:text-zinc-200"
                >
                  <RotateCcw size={14} />
                  Təmizlə
                </button>
              )}
            </div>

            <div className="hidden xl:block">
              {filterContent}
            </div>

            {isMobileFiltersOpen && (
              <div className="mt-5 border-t border-white/[0.06] pt-5 xl:hidden">
                {filterContent}
              </div>
            )}
          </div>

          {releaseYear &&
            validReleaseYear === undefined && (
              <div className="mt-3 rounded-xl border border-amber-400/15 bg-amber-500/[0.06] px-4 py-3 text-sm text-amber-200">
                Çıxış ili 1900–2200 aralığında
                olmalıdır.
              </div>
            )}

          <div className="mt-10 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-[0.22em] text-violet-400">
                <SlidersHorizontal size={15} />
                Nəticələr
              </div>

              <h2 className="mt-2 text-2xl font-bold text-white">
                Oyunlar
              </h2>

              {!gamesQuery.isLoading && (
                <p className="mt-2 text-sm text-zinc-500">
                  Hazırda {games.length} oyun göstərilir
                </p>
              )}
            </div>

            {activeFilterCount > 0 && (
              <button
                type="button"
                onClick={resetFilters}
                className="hidden items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.035] px-4 py-2.5 text-sm font-medium text-zinc-400 transition hover:bg-white/[0.07] hover:text-white xl:flex"
              >
                <RotateCcw size={16} />
                Bütün filterləri təmizlə
              </button>
            )}
          </div>

          {gamesQuery.isLoading ? (
            <div className="mt-7 grid grid-cols-2 gap-4 sm:grid-cols-3 sm:gap-5 lg:grid-cols-4">
              {Array.from({
                length: PAGE_SIZE,
              }).map((_, index) => (
                <GameCardSkeleton key={index} />
              ))}
            </div>
          ) : gamesError ? (
            <div className="mt-7 rounded-2xl border border-red-400/15 bg-red-500/[0.06] p-8 text-center">
              <p className="font-semibold text-red-200">
                {gamesError}
              </p>

              <button
                type="button"
                onClick={() => {
                  void gamesQuery.refetch();
                }}
                className="mx-auto mt-5 flex items-center gap-2 rounded-xl border border-red-400/20 bg-red-500/10 px-4 py-2.5 text-sm font-semibold text-red-200 transition hover:bg-red-500/20"
              >
                <RefreshCcw size={16} />
                Yenidən yoxla
              </button>
            </div>
          ) : games.length === 0 ? (
            <div className="mt-7 rounded-3xl border border-dashed border-white/[0.09] bg-white/[0.02] px-6 py-16 text-center">
              <Gamepad2
                size={36}
                className="mx-auto text-zinc-700"
              />

              <h3 className="mt-5 text-lg font-semibold text-zinc-300">
                Oyun tapılmadı
              </h3>

              <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-zinc-600">
                Axtarış mətnini və ya filterləri
                dəyişərək yenidən yoxla.
              </p>

              <button
                type="button"
                onClick={resetFilters}
                className="mt-6 rounded-xl bg-violet-600 px-5 py-3 text-sm font-semibold text-white transition hover:bg-violet-500"
              >
                Filterləri təmizlə
              </button>
            </div>
          ) : (
            <>
              <div className="mt-7 grid grid-cols-2 gap-4 sm:grid-cols-3 sm:gap-5 lg:grid-cols-4">
                {games.map((game) => (
                  <GameCard
                    key={`${game.source}-${game.id}`}
                    game={game}
                  />
                ))}
              </div>

              {gamesQuery.hasNextPage && (
                <div className="mt-10 flex justify-center">
                  <button
                    type="button"
                    onClick={() => {
                      void gamesQuery.fetchNextPage();
                    }}
                    disabled={
                      gamesQuery.isFetchingNextPage
                    }
                    className="flex min-w-48 items-center justify-center gap-2 rounded-xl border border-violet-400/20 bg-violet-500/10 px-5 py-3 text-sm font-semibold text-violet-200 transition hover:bg-violet-500/20 disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    {gamesQuery.isFetchingNextPage ? (
                      <>
                        <LoaderCircle
                          size={17}
                          className="animate-spin"
                        />

                        Yüklənir...
                      </>
                    ) : (
                      "Daha çox göstər"
                    )}
                  </button>
                </div>
              )}
            </>
          )}
        </section>
      </main>

      <Footer />
    </div>
  );
}