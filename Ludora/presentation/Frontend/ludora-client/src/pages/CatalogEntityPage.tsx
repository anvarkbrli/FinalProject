import { useQuery } from "@tanstack/react-query";
import {
  ArrowLeft,
  Building2,
  ChevronLeft,
  ChevronRight,
  Gamepad2,
  Layers3,
  SlidersHorizontal,
  type LucideIcon,
} from "lucide-react";
import { useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { GameCard } from "../components/home/GameCard";
import { GameCardSkeleton } from "../components/home/GameCardSkeleton";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { getApiErrorMessage } from "../lib/api";
import {
  getCatalogEntityById,
  getGames,
} from "../services/gamesService";
import type { CatalogSource } from "../types/home";
import type {
  CatalogEntityKind,
  GameCatalogFilterDTO,
  GameSort,
} from "../types/games";

const PAGE_SIZE = 16;

const sortOptions: Array<{
  value: GameSort;
  label: string;
}> = [
  {
    value: "rating_desc",
    label: "Ən populyar",
  },
  {
    value: "rating_asc",
    label: "Ən az populyar",
  },
  {
    value: "year_desc",
    label: "Ən yeni çıxan",
  },
  {
    value: "year_asc",
    label: "Ən köhnə çıxan",
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

type PaginationItem =
  | number
  | "left-ellipsis"
  | "right-ellipsis";

interface CatalogEntityPageProps {
  kind: CatalogEntityKind;
}

interface CatalogEntityConfig {
  title: string;
  gamesTitle: string;
  emptyMessage: string;
  icon: LucideIcon;
}

const entityConfigs: Record<
  CatalogEntityKind,
  CatalogEntityConfig
> = {
  genre: {
    title: "Janr",
    gamesTitle: "Bu janra aid oyunlar",
    emptyMessage:
      "Bu janra uyğun əsas oyun tapılmadı.",
    icon: Layers3,
  },
  platform: {
    title: "Platforma",
    gamesTitle: "Bu platformadakı oyunlar",
    emptyMessage:
      "Bu platformaya uyğun əsas oyun tapılmadı.",
    icon: Gamepad2,
  },
  company: {
    title: "Şirkət",
    gamesTitle: "Bu şirkətin oyunları",
    emptyMessage:
      "Bu şirkətə uyğun əsas oyun tapılmadı.",
    icon: Building2,
  },
};

function parseRouteSource(
  value: string | undefined,
): CatalogSource | null {
  if (value === "Local" || value === "Igdb") {
    return value;
  }

  return null;
}

function getPaginationItems(
  currentPage: number,
  totalPages: number,
): PaginationItem[] {
  if (totalPages <= 7) {
    return Array.from(
      { length: totalPages },
      (_, index) => index + 1,
    );
  }

  if (currentPage <= 4) {
    return [
      1,
      2,
      3,
      4,
      5,
      "right-ellipsis",
      totalPages,
    ];
  }

  if (currentPage >= totalPages - 3) {
    return [
      1,
      "left-ellipsis",
      totalPages - 4,
      totalPages - 3,
      totalPages - 2,
      totalPages - 1,
      totalPages,
    ];
  }

  return [
    1,
    "left-ellipsis",
    currentPage - 1,
    currentPage,
    currentPage + 1,
    "right-ellipsis",
    totalPages,
  ];
}

function buildGamesFilter(
  kind: CatalogEntityKind,
  source: CatalogSource,
  id: number,
  pageNumber: number,
  sortBy: GameSort,
): GameCatalogFilterDTO {
  const commonFilter: GameCatalogFilterDTO = {
    includeRelatedContent: false,
    pageNumber,
    pageSize: PAGE_SIZE,
    sortBy,
  };

  if (kind === "genre") {
    return {
      ...commonFilter,
      genreSource: source,
      genreId: id,
    };
  }

  if (kind === "platform") {
    return {
      ...commonFilter,
      platformSource: source,
      platformId: id,
    };
  }

  return {
    ...commonFilter,
    companySource: source,
    companyId: id,
  };
}

export function CatalogEntityPage({
  kind,
}: CatalogEntityPageProps) {
  const { source: sourceValue, id: idValue } =
    useParams();

  const source = parseRouteSource(sourceValue);
  const id = Number(idValue);
  const [pageNumber, setPageNumber] =
    useState(1);
  const [sortBy, setSortBy] =
    useState<GameSort>("rating_desc");

  const isValidRoute =
    source !== null &&
    Number.isSafeInteger(id) &&
    id > 0;

  const config = entityConfigs[kind];
  const EntityIcon = config.icon;

  const entityQuery = useQuery({
    queryKey: [
      "catalog-entity",
      kind,
      source,
      id,
    ],
    queryFn: () =>
      getCatalogEntityById(
        kind,
        source!,
        id,
      ),
    enabled: isValidRoute,
  });

  const gamesQuery = useQuery({
    queryKey: [
      "catalog-entity-games",
      kind,
      source,
      id,
      pageNumber,
      sortBy,
    ],
    queryFn: () =>
      getGames(
        buildGamesFilter(
          kind,
          source!,
          id,
          pageNumber,
          sortBy,
        ),
      ),
    enabled: isValidRoute,
    placeholderData: (previousData) =>
      previousData,
  });

  const games = gamesQuery.data?.items ?? [];
  const totalCount =
    gamesQuery.data?.totalCount ?? 0;
  const totalPages =
    gamesQuery.data?.totalPages ?? 0;

  const paginationItems = useMemo(
    () =>
      getPaginationItems(
        pageNumber,
        totalPages,
      ),
    [pageNumber, totalPages],
  );

  const entityError = entityQuery.isError
    ? getApiErrorMessage(
        entityQuery.error,
        `${config.title} məlumatını yükləmək mümkün olmadı.`,
      )
    : null;

  const gamesError = gamesQuery.isError
    ? getApiErrorMessage(
        gamesQuery.error,
        "Oyunları yükləmək mümkün olmadı.",
      )
    : null;

  function handleSortChange(
    nextSort: GameSort,
  ) {
    setSortBy(nextSort);
    setPageNumber(1);
  }

  function handlePageChange(nextPage: number) {
    if (
      nextPage < 1 ||
      nextPage > totalPages ||
      nextPage === pageNumber
    ) {
      return;
    }

    setPageNumber(nextPage);

    window.requestAnimationFrame(() => {
      document
        .getElementById("catalog-entity-games")
        ?.scrollIntoView({
          behavior: "smooth",
          block: "start",
        });
    });
  }

  return (
    <div className="min-h-screen bg-[#07070a] text-zinc-100">
      <Navbar />

      <main>
        {!isValidRoute ? (
          <div className="mx-auto max-w-3xl px-5 py-24 text-center">
            <EntityIcon
              size={36}
              className="mx-auto text-zinc-600"
            />
            <h1 className="mt-5 text-2xl font-bold text-white">
              Səhifə ünvanı yanlışdır
            </h1>
            <Link
              to="/games"
              className="mt-6 inline-flex items-center gap-2 text-sm font-semibold text-violet-300 transition hover:text-violet-200"
            >
              <ArrowLeft size={17} />
              Oyun kataloquna qayıt
            </Link>
          </div>
        ) : entityQuery.isLoading ? (
          <section className="mx-auto max-w-7xl animate-pulse px-5 py-16 lg:px-8">
            <div className="h-5 w-40 rounded bg-white/[0.06]" />
            <div className="mt-10 h-9 max-w-md rounded bg-white/[0.06]" />
            <div className="mt-4 h-5 max-w-xs rounded bg-white/[0.04]" />
          </section>
        ) : entityError ? (
          <div className="mx-auto max-w-3xl px-5 py-24 text-center">
            <EntityIcon
              size={36}
              className="mx-auto text-red-300"
            />
            <h1 className="mt-5 text-2xl font-bold text-white">
              {config.title} yüklənmədi
            </h1>
            <p className="mt-3 text-sm text-zinc-500">
              {entityError}
            </p>
            <Link
              to="/games"
              className="mt-6 inline-flex items-center gap-2 text-sm font-semibold text-violet-300 transition hover:text-violet-200"
            >
              <ArrowLeft size={17} />
              Oyun kataloquna qayıt
            </Link>
          </div>
        ) : entityQuery.data ? (
          <>
            <section className="relative isolate overflow-hidden border-b border-white/[0.06]">
              <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_10%,rgba(124,58,237,0.18),transparent_32%),radial-gradient(circle_at_85%_65%,rgba(217,70,239,0.08),transparent_30%)]" />

              <div className="relative mx-auto max-w-7xl px-5 py-14 lg:px-8 lg:py-18">
                <Link
                  to="/games"
                  className="inline-flex items-center gap-2 text-sm font-semibold text-zinc-500 transition hover:text-zinc-200"
                >
                  <ArrowLeft size={17} />
                  Oyun kataloquna qayıt
                </Link>

                <div className="mt-8 flex items-start gap-4">
                  <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl border border-violet-400/20 bg-violet-500/10 text-violet-200">
                    <EntityIcon size={23} />
                  </div>

                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="rounded-full border border-white/[0.08] bg-white/[0.04] px-3 py-1 text-xs font-medium text-zinc-400">
                        {config.title}
                      </span>
                      <span className="rounded-full border border-violet-400/20 bg-violet-500/10 px-3 py-1 text-xs font-semibold text-violet-200">
                        {entityQuery.data.source ===
                        "Local"
                          ? "Ludora"
                          : "IGDB"}
                      </span>
                    </div>

                    <h1 className="mt-4 break-words text-4xl font-bold tracking-[-0.04em] text-white sm:text-5xl">
                      {entityQuery.data.name}
                    </h1>

                    <p className="mt-4 text-sm text-zinc-400">
                      Ümumi {totalCount} uyğun oyun
                      tapıldı.
                    </p>
                  </div>
                </div>
              </div>
            </section>

            <section
              id="catalog-entity-games"
              className="mx-auto max-w-7xl scroll-mt-24 px-5 py-12 lg:px-8"
            >
              <div className="mb-7 flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
                <div>
                  <h2 className="text-2xl font-bold tracking-tight text-white">
                    {config.gamesTitle}
                  </h2>
                  <p className="mt-2 text-sm text-zinc-500">
                    DLC, bundle və digər əlavə
                    məzmunlar əsas siyahıda
                    göstərilmir.
                  </p>
                </div>

                <label className="w-full sm:w-64">
                  <span className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.14em] text-zinc-500">
                    <SlidersHorizontal size={14} />
                    Sıralama
                  </span>
                  <select
                    value={sortBy}
                    onChange={(event) =>
                      handleSortChange(
                        event.target
                          .value as GameSort,
                      )
                    }
                    disabled={gamesQuery.isLoading}
                    className="h-11 w-full rounded-xl border border-white/[0.08] bg-white/[0.035] px-3 text-sm font-semibold text-zinc-200 outline-none transition hover:bg-white/[0.06] focus:border-violet-400/40 focus:ring-2 focus:ring-violet-500/10 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    {sortOptions.map((option) => (
                      <option
                        key={option.value}
                        value={option.value}
                        className="bg-zinc-950 text-zinc-200"
                      >
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>
              </div>

              {gamesError ? (
                <div className="rounded-2xl border border-red-400/20 bg-red-500/[0.06] p-6 text-sm text-red-200">
                  {gamesError}
                </div>
              ) : gamesQuery.isLoading ? (
                <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
                  {Array.from(
                    { length: PAGE_SIZE },
                    (_, index) => (
                      <GameCardSkeleton
                        key={index}
                      />
                    ),
                  )}
                </div>
              ) : games.length > 0 ? (
                <>
                  <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
                    {games.map((game) => (
                      <GameCard
                        key={`${game.source}-${game.id}`}
                        game={game}
                      />
                    ))}
                  </div>

                  {totalPages > 1 && (
                    <nav
                      className="mt-10 flex flex-wrap items-center justify-center gap-2"
                      aria-label="Oyun səhifələri"
                    >
                      <button
                        type="button"
                        onClick={() =>
                          handlePageChange(
                            pageNumber - 1,
                          )
                        }
                        disabled={pageNumber === 1}
                        className="inline-flex h-10 items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.035] px-3 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.07] disabled:cursor-not-allowed disabled:opacity-40"
                      >
                        <ChevronLeft size={17} />
                        Əvvəlki
                      </button>

                      {paginationItems.map(
                        (item) => {
                          if (
                            item ===
                              "left-ellipsis" ||
                            item ===
                              "right-ellipsis"
                          ) {
                            return (
                              <span
                                key={item}
                                className="flex h-10 w-10 items-center justify-center text-sm text-zinc-600"
                              >
                                …
                              </span>
                            );
                          }

                          return (
                            <button
                              key={item}
                              type="button"
                              onClick={() =>
                                handlePageChange(
                                  item,
                                )
                              }
                              aria-current={
                                item ===
                                pageNumber
                                  ? "page"
                                  : undefined
                              }
                              className={`flex h-10 min-w-10 items-center justify-center rounded-xl border px-3 text-sm font-semibold transition ${
                                item === pageNumber
                                  ? "border-violet-400/40 bg-violet-500/15 text-violet-100"
                                  : "border-white/[0.08] bg-white/[0.035] text-zinc-400 hover:bg-white/[0.07] hover:text-zinc-200"
                              }`}
                            >
                              {item}
                            </button>
                          );
                        },
                      )}

                      <button
                        type="button"
                        onClick={() =>
                          handlePageChange(
                            pageNumber + 1,
                          )
                        }
                        disabled={
                          pageNumber === totalPages
                        }
                        className="inline-flex h-10 items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.035] px-3 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.07] disabled:cursor-not-allowed disabled:opacity-40"
                      >
                        Növbəti
                        <ChevronRight size={17} />
                      </button>
                    </nav>
                  )}
                </>
              ) : (
                <div className="rounded-2xl border border-dashed border-white/10 bg-white/[0.02] p-10 text-center">
                  <EntityIcon
                    size={30}
                    className="mx-auto text-zinc-600"
                  />
                  <p className="mt-4 font-semibold text-zinc-300">
                    Oyun tapılmadı
                  </p>
                  <p className="mt-2 text-sm text-zinc-600">
                    {config.emptyMessage}
                  </p>
                </div>
              )}
            </section>
          </>
        ) : null}
      </main>

      <Footer />
    </div>
  );
}
