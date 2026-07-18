import {
  useInfiniteQuery,
  useQuery,
} from "@tanstack/react-query";
import {
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
import {
  SearchableSelect,
  type SearchableSelectOption,
} from "../components/common/SearchableSelect";
import { GameCard } from "../components/home/GameCard";
import { GameCardSkeleton } from "../components/home/GameCardSkeleton";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { getApiErrorMessage } from "../lib/api";
import {
  isPriorityPlatform,
  sortPlatformsByCurrentRelevance,
} from "../lib/catalogPriority";
import { isDisplayableGame } from "../lib/gameQuality";
import {
  getGameCompanies,
  getGameGenres,
  getGamePlatforms,
  getGames,
} from "../services/gamesService";
import type {
  CatalogSource,
} from "../types/home";
import type {
  CatalogFilterItemDTO,
  GameCatalogItemDTO,
  GameSort,
} from "../types/games";

const PAGE_SIZE = 12;

const sortOptions: Array<{
  value: GameSort;
  label: string;
}> = [
  {
    value: "",
    label: "Standart sıralama",
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

function parseCatalogReference(
  value: string,
): {
  source: CatalogSource;
  id: number;
} | null {
  if (!value) {
    return null;
  }

  const [sourceValue, idValue] =
    value.split(":");

  const id = Number(idValue);

  if (
    (
      sourceValue !== "Local" &&
      sourceValue !== "Igdb"
    ) ||
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

function getCatalogDescription(
  source: CatalogSource,
): string {
  return source === "Local"
    ? "Ludora kataloqu"
    : "IGDB kataloqu";
}

function sortAlphabetically(
  items: CatalogFilterItemDTO[],
): CatalogFilterItemDTO[] {
  return [...items].sort(
    (firstItem, secondItem) => {
      return firstItem.name.localeCompare(
        secondItem.name,
        "az-AZ",
        {
          sensitivity: "base",
        },
      );
    },
  );
}

export function GamesPage() {
  const [
    searchInput,
    setSearchInput,
  ] = useState("");

  const [
    debouncedSearch,
    setDebouncedSearch,
  ] = useState("");

  const [
    companyReference,
    setCompanyReference,
  ] = useState("");

  const [
    genreReference,
    setGenreReference,
  ] = useState("");

  const [
    platformReference,
    setPlatformReference,
  ] = useState("");

  const [
    releaseYear,
    setReleaseYear,
  ] = useState("");

  const [sortBy, setSortBy] =
    useState<GameSort>("");

  const [
    isMobileFiltersOpen,
    setIsMobileFiltersOpen,
  ] = useState(false);

  useEffect(() => {
    const timeoutId =
      window.setTimeout(() => {
        setDebouncedSearch(
          searchInput.trim(),
        );
      }, 350);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [searchInput]);

  const companiesQuery = useQuery({
    queryKey: [
      "game-catalog",
      "companies",
    ],
    queryFn: getGameCompanies,
    staleTime: 5 * 60_000,
  });

  const genresQuery = useQuery({
    queryKey: [
      "game-catalog",
      "genres",
    ],
    queryFn: getGameGenres,
    staleTime: 5 * 60_000,
  });

  const platformsQuery = useQuery({
    queryKey: [
      "game-catalog",
      "platforms",
    ],
    queryFn: getGamePlatforms,
    staleTime: 5 * 60_000,
  });

  const parsedCompany =
    parseCatalogReference(
      companyReference,
    );

  const parsedGenre =
    parseCatalogReference(
      genreReference,
    );

  const parsedPlatform =
    parseCatalogReference(
      platformReference,
    );

  const releaseYearNumber =
    releaseYear.trim() === ""
      ? undefined
      : Number(releaseYear);

  const isReleaseYearValid =
    releaseYear.trim() === "" ||
    (
      releaseYearNumber !== undefined &&
      Number.isInteger(
        releaseYearNumber,
      ) &&
      releaseYearNumber >= 1900 &&
      releaseYearNumber <= 2200
    );

  const validReleaseYear =
    isReleaseYearValid
      ? releaseYearNumber
      : undefined;

  const gamesQuery = useInfiniteQuery({
    queryKey: [
      "game-catalog",
      "games",
      debouncedSearch,
      companyReference,
      genreReference,
      platformReference,
      validReleaseYear,
      sortBy,
    ],

    initialPageParam: 1,

    queryFn: ({ pageParam }) => {
      return getGames({
        searchName:
          debouncedSearch ||
          undefined,

        companySource:
          parsedCompany?.source,

        companyId:
          parsedCompany?.id,

        genreSource:
          parsedGenre?.source,

        genreId:
          parsedGenre?.id,

        platformSource:
          parsedPlatform?.source,

        platformId:
          parsedPlatform?.id,

        releaseYear:
          validReleaseYear,

        sortBy,

        pageNumber:
          pageParam,

        pageSize:
          PAGE_SIZE,
      });
    },

    getNextPageParam: (
      lastPage,
      allPages,
    ) => {
      if (
        lastPage.length <
        PAGE_SIZE
      ) {
        return undefined;
      }

      const nextPageNumber =
        allPages.length + 1;

      if (
        nextPageNumber *
          PAGE_SIZE >
        500
      ) {
        return undefined;
      }

      return nextPageNumber;
    },
  });

  const games = useMemo<
    GameCatalogItemDTO[]
  >(() => {
    const uniqueGames = new Map<
      string,
      GameCatalogItemDTO
    >();

    const pages =
      gamesQuery.data?.pages ?? [];

    for (const page of pages) {
      for (const game of page) {
        if (!isDisplayableGame(game)) {
          continue;
        }

        uniqueGames.set(
          `${game.source}-${game.id}`,
          game,
        );
      }
    }

    return Array.from(
      uniqueGames.values(),
    );
  }, [gamesQuery.data]);

  const sortedCompanies =
    useMemo(() => {
      return sortAlphabetically(
        companiesQuery.data ?? [],
      );
    }, [companiesQuery.data]);

  const sortedPlatforms =
    useMemo(() => {
      return sortPlatformsByCurrentRelevance(
        platformsQuery.data ?? [],
      );
    }, [platformsQuery.data]);

  const sortedGenres =
    useMemo(() => {
      return sortAlphabetically(
        genresQuery.data ?? [],
      );
    }, [genresQuery.data]);

  const companyOptions =
    useMemo<
      SearchableSelectOption[]
    >(() => {
      return sortedCompanies.map(
        (company) => ({
          value:
            serializeCatalogReference(
              company,
            ),

          label: company.name,

          description:
            getCatalogDescription(
              company.source,
            ),

        }),
      );
    }, [sortedCompanies]);

  const platformOptions =
    useMemo<
      SearchableSelectOption[]
    >(() => {
      return sortedPlatforms.map(
        (platform) => ({
          value:
            serializeCatalogReference(
              platform,
            ),

          label: platform.name,

          description:
            getCatalogDescription(
              platform.source,
            ),

          group: isPriorityPlatform(
            platform.name,
            platform.source,
            platform.id,
          )
            ? "Məşhur platformalar"
            : "Digər platformalar",
        }),
      );
    }, [sortedPlatforms]);

  const genreOptions =
    useMemo<
      SearchableSelectOption[]
    >(() => {
      return sortedGenres.map(
        (genre) => ({
          value:
            serializeCatalogReference(
              genre,
            ),

          label: genre.name,

          description:
            getCatalogDescription(
              genre.source,
            ),
        }),
      );
    }, [sortedGenres]);

  const sortSelectOptions =
    useMemo<
      SearchableSelectOption[]
    >(() => {
      return sortOptions
        .filter(
          (option) =>
            option.value !== "",
        )
        .map((option) => ({
          value: option.value,
          label: option.label,
        }));
    }, []);

  function handleCompanyChange(
    value: string,
  ) {
    const selectedReference =
      parseCatalogReference(value);

    setCompanyReference(value);

    if (!selectedReference) {
      return;
    }

    if (
      parsedGenre &&
      parsedGenre.source !==
        selectedReference.source
    ) {
      setGenreReference("");
    }

    if (
      parsedPlatform &&
      parsedPlatform.source !==
        selectedReference.source
    ) {
      setPlatformReference("");
    }
  }

  function handleGenreChange(
    value: string,
  ) {
    const selectedReference =
      parseCatalogReference(value);

    setGenreReference(value);

    if (!selectedReference) {
      return;
    }

    if (
      parsedCompany &&
      parsedCompany.source !==
        selectedReference.source
    ) {
      setCompanyReference("");
    }

    if (
      parsedPlatform &&
      parsedPlatform.source !==
        selectedReference.source
    ) {
      setPlatformReference("");
    }
  }

  function handlePlatformChange(
    value: string,
  ) {
    const selectedReference =
      parseCatalogReference(value);

    setPlatformReference(value);

    if (!selectedReference) {
      return;
    }

    if (
      parsedCompany &&
      parsedCompany.source !==
        selectedReference.source
    ) {
      setCompanyReference("");
    }

    if (
      parsedGenre &&
      parsedGenre.source !==
        selectedReference.source
    ) {
      setGenreReference("");
    }
  }

  const activeFilterCount = [
    searchInput.trim(),
    companyReference,
    genreReference,
    platformReference,
    releaseYear,
    sortBy,
  ].filter(Boolean).length;

  function resetFilters() {
    setSearchInput("");
    setDebouncedSearch("");
    setCompanyReference("");
    setGenreReference("");
    setPlatformReference("");
    setReleaseYear("");
    setSortBy("");
  }

  function clearSearch() {
    setSearchInput("");
    setDebouncedSearch("");
  }

  const gamesError =
    gamesQuery.isError
      ? getApiErrorMessage(
          gamesQuery.error,
          "Oyunları yükləmək mümkün olmadı.",
        )
      : null;

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
                Oyunları ada, şirkətə,
                janra, platformaya və
                çıxış ilinə görə axtar.
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
                  onClick={clearSearch}
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
                    (currentValue) =>
                      !currentValue,
                  );
                }}
                className="flex items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.04] px-4 py-2.5 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.08]"
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

            <div
              className={`${
                isMobileFiltersOpen
                  ? "grid"
                  : "hidden"
              } mt-5 gap-4 border-t border-white/[0.06] pt-5 md:grid-cols-2 xl:mt-0 xl:grid xl:grid-cols-5 xl:border-t-0 xl:pt-0`}
            >
              <label className="block">
                <span className="mb-2 block text-xs font-semibold uppercase tracking-wider text-zinc-500">
                  Şirkət
                </span>

                <SearchableSelect
                  value={
                    companyReference
                  }
                  options={companyOptions}
                  onChange={
                    handleCompanyChange
                  }
                  placeholder="Şirkət seç"
                  clearLabel="Bütün şirkətlər"
                  searchPlaceholder="Şirkət axtar..."
                  emptyMessage="Şirkət tapılmadı."
                  disabled={
                    companiesQuery.isLoading
                  }
                />
              </label>

              <label className="block">
                <span className="mb-2 block text-xs font-semibold uppercase tracking-wider text-zinc-500">
                  Janr
                </span>

                <SearchableSelect
                  value={genreReference}
                  options={genreOptions}
                  onChange={
                    handleGenreChange
                  }
                  placeholder="Janr seç"
                  clearLabel="Bütün janrlar"
                  searchPlaceholder="Janr axtar..."
                  emptyMessage="Janr tapılmadı."
                  disabled={
                    genresQuery.isLoading
                  }
                />
              </label>

              <label className="block">
                <span className="mb-2 block text-xs font-semibold uppercase tracking-wider text-zinc-500">
                  Platforma
                </span>

                <SearchableSelect
                  value={
                    platformReference
                  }
                  options={
                    platformOptions
                  }
                  onChange={
                    handlePlatformChange
                  }
                  placeholder="Platforma seç"
                  clearLabel="Bütün platformalar"
                  searchPlaceholder="Platforma axtar..."
                  emptyMessage="Platforma tapılmadı."
                  disabled={
                    platformsQuery.isLoading
                  }
                />
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
                  placeholder="Məsələn: 2026"
                  className="h-11 w-full rounded-xl border border-white/[0.08] bg-[#111116] px-3 text-sm text-zinc-300 outline-none transition placeholder:text-zinc-700 hover:border-white/[0.15] focus:border-violet-400/50 focus:ring-4 focus:ring-violet-500/10"
                />
              </label>

              <label className="block">
                <span className="mb-2 block text-xs font-semibold uppercase tracking-wider text-zinc-500">
                  Sıralama
                </span>

                <SearchableSelect
                  value={sortBy}
                  options={
                    sortSelectOptions
                  }
                  onChange={(value) => {
                    setSortBy(
                      value as GameSort,
                    );
                  }}
                  placeholder="Standart sıralama"
                  clearLabel="Standart sıralama"
                  searchable={false}
                />
              </label>
            </div>
          </div>

          {!isReleaseYearValid && (
            <div className="mt-3 rounded-xl border border-amber-400/15 bg-amber-500/[0.06] px-4 py-3 text-sm text-amber-200">
              Çıxış ili 1900–2200
              aralığında olmalıdır.
            </div>
          )}

          <div className="mt-10 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-[0.22em] text-violet-400">
                <SlidersHorizontal
                  size={15}
                />
                Nəticələr
              </div>

              <h2 className="mt-2 text-2xl font-bold text-white">
                Oyunlar
              </h2>

              {!gamesQuery.isLoading && (
                <p className="mt-2 text-sm text-zinc-500">
                  Hazırda {games.length} uyğun
                  oyun göstərilir
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
                <GameCardSkeleton
                  key={index}
                />
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
                className="mx-auto mt-5 flex items-center gap-2 rounded-xl border border-red-400/20 bg-red-500/10 px-4 py-2.5 text-sm font-semibold text-red-200"
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
                Uyğun oyun tapılmadı
              </h3>

              <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-zinc-600">
                Axtarış mətnini və ya
                filterləri dəyişərək yenidən
                yoxla.
              </p>

              <button
                type="button"
                onClick={resetFilters}
                className="mt-6 rounded-xl bg-violet-600 px-5 py-3 text-sm font-semibold text-white"
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
                    className="flex min-w-48 items-center justify-center gap-2 rounded-xl border border-violet-400/20 bg-violet-500/10 px-5 py-3 text-sm font-semibold text-violet-200 transition hover:bg-violet-500/20 disabled:opacity-60"
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