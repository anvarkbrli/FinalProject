import { useQuery } from "@tanstack/react-query";
import {
  ChevronLeft,
  ChevronRight,
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
  useLocation,
  useSearchParams,
} from "react-router-dom";
import {
  SearchableSelect,
  type SearchableSelectOption,
} from "../components/common/SearchableSelect";
import { GameCard } from "../components/home/GameCard";
import { GameCardSkeleton } from "../components/home/GameCardSkeleton";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { getApiErrorMessage } from "../lib/api";
import { canViewCatalogSources } from "../lib/authRoles";
import {
  isPriorityPlatform,
  sortPlatformsByCurrentRelevance,
} from "../lib/catalogPriority";
import {
  getCatalogEntityById,
  getGameCompanies,
  getGameGenres,
  getGamePlatforms,
  getGames,
} from "../services/gamesService";
import { useAuthStore } from "../stores/authStore";
import type { CatalogSource } from "../types/home";
import type {
  CatalogFilterItemDTO,
  GameCatalogItemDTO,
  GameSort,
} from "../types/games";

const PAGE_SIZE = 16;

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
    label: "İstifadəçi reytinqi: yüksəkdən aşağı",
  },
  {
    value: "rating_asc",
    label: "İstifadəçi reytinqi: aşağıdan yüksək",
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

  const [sourceValue, idValue] = value.split(":");
  const id = Number(idValue);

  if (
    (sourceValue !== "Local" && sourceValue !== "Igdb") ||
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

function getInitialCatalogReference(
  searchParams: URLSearchParams,
  prefix: "company" | "genre" | "platform",
): string {
  const source = searchParams.get(`${prefix}Source`);
  const id = Number(searchParams.get(`${prefix}Id`));

  if (
    (source !== "Local" && source !== "Igdb") ||
    !Number.isInteger(id) ||
    id < 1
  ) {
    return "";
  }

  return `${source}:${id}`;
}

function getCatalogDescription(
  source: CatalogSource,
): string {
  return source === "Local"
    ? "Ludora kataloqu"
    : "IGDB kataloqu";
}

function normalizeCatalogName(value: string): string {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLocaleLowerCase("az-AZ")
    .trim();
}

function mergeCatalogItems(
  currentItems: CatalogFilterItemDTO[],
  incomingItems: CatalogFilterItemDTO[],
): CatalogFilterItemDTO[] {
  const result = new Map<string, CatalogFilterItemDTO>();

  for (const item of [...currentItems, ...incomingItems]) {
    result.set(serializeCatalogReference(item), item);
  }

  return Array.from(result.values());
}

function prepareCatalogItems(
  items: CatalogFilterItemDTO[],
  canViewSources: boolean,
  selectedReference: string,
): CatalogFilterItemDTO[] {
  if (canViewSources) {
    return items;
  }

  const byName = new Map<string, CatalogFilterItemDTO>();

  for (const item of items) {
    const normalizedName = normalizeCatalogName(item.name);
    const current = byName.get(normalizedName);
    const itemReference = serializeCatalogReference(item);

    if (
      !current ||
      itemReference === selectedReference ||
      (current.source === "Local" && item.source === "Igdb")
    ) {
      byName.set(normalizedName, item);
    }
  }

  return Array.from(byName.values());
}

function findPreferredCatalogItemByName(
  items: CatalogFilterItemDTO[],
  name: string | null,
): CatalogFilterItemDTO | null {
  if (!name?.trim()) {
    return null;
  }

  const normalizedName = normalizeCatalogName(name);
  const matches = items.filter(
    (item) => normalizeCatalogName(item.name) === normalizedName,
  );

  return (
    matches.find((item) => item.source === "Igdb") ??
    matches[0] ??
    null
  );
}

function sortAlphabetically(
  items: CatalogFilterItemDTO[],
): CatalogFilterItemDTO[] {
  return [...items].sort((firstItem, secondItem) =>
    firstItem.name.localeCompare(secondItem.name, "az-AZ", {
      sensitivity: "base",
    }),
  );
}

type PaginationItem =
  | number
  | "left-ellipsis"
  | "right-ellipsis";

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
    return [1, 2, 3, 4, 5, "right-ellipsis", totalPages];
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

interface CatalogFilterNavigationState {
  catalogFilter?: {
    kind: "company" | "genre" | "platform";
    source: CatalogSource;
    id: number;
    name: string;
  };
}

export function GamesPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const location = useLocation();

  const navigationState =
    location.state as CatalogFilterNavigationState | null;

  const navigationCompany =
    navigationState?.catalogFilter?.kind === "company"
      ? navigationState.catalogFilter
      : null;

  const accessToken = useAuthStore(
    (state) => state.accessToken,
  );

  const canViewSources =
    canViewCatalogSources(accessToken);

  const [searchInput, setSearchInput] = useState(
    searchParams.get("search") ?? "",
  );

  const [debouncedSearch, setDebouncedSearch] = useState(
    searchParams.get("search")?.trim() ?? "",
  );

  const [companyReference, setCompanyReference] = useState(
    () => {
      const referenceFromUrl =
        getInitialCatalogReference(
          searchParams,
          "company",
        );

      if (referenceFromUrl) {
        return referenceFromUrl;
      }

      return navigationCompany
        ? `${navigationCompany.source}:${navigationCompany.id}`
        : "";
    },
  );

  const [genreReference, setGenreReference] = useState(
    () =>
      getInitialCatalogReference(
        searchParams,
        "genre",
      ),
  );

  const [platformReference, setPlatformReference] =
    useState(() =>
      getInitialCatalogReference(
        searchParams,
        "platform",
      ),
    );

  const [releaseYear, setReleaseYear] = useState(
    searchParams.get("releaseYear") ?? "",
  );

  const [sortBy, setSortBy] = useState<GameSort>(() => {
    const value = searchParams.get("sortBy") ?? "";

    return sortOptions.some(
      (option) => option.value === value,
    )
      ? (value as GameSort)
      : "";
  });

  const [pageNumber, setPageNumber] = useState(1);

  const [
    isMobileFiltersOpen,
    setIsMobileFiltersOpen,
  ] = useState(false);

  const routeCompanyName =
    searchParams.get("companyName")?.trim() ?? "";

  const [companySearch, setCompanySearch] =
    useState("");

  const [
    debouncedCompanySearch,
    setDebouncedCompanySearch,
  ] = useState("");

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setDebouncedSearch(searchInput.trim());
    }, 350);

    return () => window.clearTimeout(timeoutId);
  }, [searchInput]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setDebouncedCompanySearch(
        companySearch.trim(),
      );
    }, 250);

    return () => window.clearTimeout(timeoutId);
  }, [companySearch]);

  const companyQuerySearch =
    debouncedCompanySearch ||
    (!companyReference ? routeCompanyName : "");

  const selectedCompanyReference =
    parseCatalogReference(companyReference);

  const companiesQuery = useQuery({
    queryKey: [
      "game-catalog",
      "companies",
      companyQuerySearch,
    ],
    queryFn: () =>
      getGameCompanies(
        companyQuerySearch || undefined,
      ),
    staleTime: 5 * 60_000,
  });

  const selectedCompanyQuery = useQuery({
    queryKey: [
      "game-catalog",
      "selected-company",
      selectedCompanyReference?.source,
      selectedCompanyReference?.id,
    ],
    queryFn: () =>
      getCatalogEntityById(
        "company",
        selectedCompanyReference!.source,
        selectedCompanyReference!.id,
      ),
    enabled: selectedCompanyReference !== null,
    staleTime: 5 * 60_000,
  });

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

  const knownCompanies = mergeCatalogItems(
    companiesQuery.data ?? [],
    selectedCompanyQuery.data
      ? [selectedCompanyQuery.data]
      : [],
  );

  const companyFromName =
    findPreferredCatalogItemByName(
      knownCompanies,
      routeCompanyName,
    );

  const genreFromName =
    findPreferredCatalogItemByName(
      genresQuery.data ?? [],
      searchParams.get("genreName"),
    );

  const platformFromName =
    findPreferredCatalogItemByName(
      platformsQuery.data ?? [],
      searchParams.get("platformName"),
    );

  const effectiveCompanyReference =
    companyReference ||
    (companyFromName
      ? serializeCatalogReference(companyFromName)
      : "");

  const effectiveGenreReference =
    genreReference ||
    (genreFromName
      ? serializeCatalogReference(genreFromName)
      : "");

  const effectivePlatformReference =
    platformReference ||
    (platformFromName
      ? serializeCatalogReference(platformFromName)
      : "");

  const parsedCompany = parseCatalogReference(
    effectiveCompanyReference,
  );

  const parsedGenre = parseCatalogReference(
    effectiveGenreReference,
  );

  const parsedPlatform = parseCatalogReference(
    effectivePlatformReference,
  );

  const releaseYearNumber =
    releaseYear.trim() === ""
      ? undefined
      : Number(releaseYear);

  const isReleaseYearValid =
    releaseYear.trim() === "" ||
    (releaseYearNumber !== undefined &&
      Number.isInteger(releaseYearNumber) &&
      releaseYearNumber >= 1900 &&
      releaseYearNumber <= 2200);

  const validReleaseYear = isReleaseYearValid
    ? releaseYearNumber
    : undefined;

  const gamesQuery = useQuery({
    queryKey: [
      "game-catalog",
      "games",
      debouncedSearch,
      effectiveCompanyReference,
      effectiveGenreReference,
      effectivePlatformReference,
      validReleaseYear,
      sortBy,
      pageNumber,
    ],
    queryFn: () =>
      getGames({
        searchName: debouncedSearch || undefined,
        companySource: parsedCompany?.source,
        companyId: parsedCompany?.id,
        genreSource: parsedGenre?.source,
        genreId: parsedGenre?.id,
        platformSource: parsedPlatform?.source,
        platformId: parsedPlatform?.id,
        releaseYear: validReleaseYear,
        sortBy,
        includeRelatedContent: false,
        pageNumber,
        pageSize: PAGE_SIZE,
      }),
    placeholderData: (previousData) => previousData,
  });

  const games: GameCatalogItemDTO[] =
    gamesQuery.data?.items ?? [];

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

  const sortedCompanies = useMemo(() => {
    return sortAlphabetically(
      prepareCatalogItems(
        knownCompanies,
        canViewSources,
        effectiveCompanyReference,
      ),
    );
  }, [
    knownCompanies,
    canViewSources,
    effectiveCompanyReference,
  ]);

  const sortedPlatforms = useMemo(() => {
    return sortPlatformsByCurrentRelevance(
      prepareCatalogItems(
        platformsQuery.data ?? [],
        canViewSources,
        effectivePlatformReference,
      ),
    );
  }, [
    platformsQuery.data,
    canViewSources,
    effectivePlatformReference,
  ]);

  const sortedGenres = useMemo(() => {
    return sortAlphabetically(
      prepareCatalogItems(
        genresQuery.data ?? [],
        canViewSources,
        effectiveGenreReference,
      ),
    );
  }, [
    genresQuery.data,
    canViewSources,
    effectiveGenreReference,
  ]);

  const companyOptions =
    useMemo<SearchableSelectOption[]>(
      () =>
        sortedCompanies.map((company) => ({
          value:
            serializeCatalogReference(company),
          label: company.name,
          description: canViewSources
            ? getCatalogDescription(company.source)
            : undefined,
        })),
      [sortedCompanies, canViewSources],
    );

  const platformOptions =
    useMemo<SearchableSelectOption[]>(
      () =>
        sortedPlatforms.map((platform) => ({
          value:
            serializeCatalogReference(platform),
          label: platform.name,
          description: canViewSources
            ? getCatalogDescription(platform.source)
            : undefined,
          group: isPriorityPlatform(
            platform.name,
            platform.source,
            platform.id,
          )
            ? "Məşhur platformalar"
            : "Digər platformalar",
        })),
      [sortedPlatforms, canViewSources],
    );

  const genreOptions =
    useMemo<SearchableSelectOption[]>(
      () =>
        sortedGenres.map((genre) => ({
          value:
            serializeCatalogReference(genre),
          label: genre.name,
          description: canViewSources
            ? getCatalogDescription(genre.source)
            : undefined,
        })),
      [sortedGenres, canViewSources],
    );

  const sortSelectOptions =
    useMemo<SearchableSelectOption[]>(
      () =>
        sortOptions
          .filter(
            (option) => option.value !== "",
          )
          .map((option) => ({
            value: option.value,
            label: option.label,
          })),
      [],
    );

  function handleCompanyChange(value: string) {
    setPageNumber(1);
    setCompanyReference(value);

    const nextSearchParams =
      new URLSearchParams(searchParams);

    nextSearchParams.delete("companyName");
    nextSearchParams.delete("companySource");
    nextSearchParams.delete("companyId");

    setSearchParams(nextSearchParams, {
      replace: true,
    });
  }

  function handleGenreChange(value: string) {
    setPageNumber(1);
    setGenreReference(value);

    const nextSearchParams =
      new URLSearchParams(searchParams);

    nextSearchParams.delete("genreName");
    nextSearchParams.delete("genreSource");
    nextSearchParams.delete("genreId");

    setSearchParams(nextSearchParams, {
      replace: true,
    });
  }

  function handlePlatformChange(value: string) {
    setPageNumber(1);
    setPlatformReference(value);

    const nextSearchParams =
      new URLSearchParams(searchParams);

    nextSearchParams.delete("platformName");
    nextSearchParams.delete("platformSource");
    nextSearchParams.delete("platformId");

    setSearchParams(nextSearchParams, {
      replace: true,
    });
  }

  const activeFilterCount = [
    searchInput.trim(),
    effectiveCompanyReference,
    effectiveGenreReference,
    effectivePlatformReference,
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
    setPageNumber(1);

    setSearchParams({}, {
      replace: true,
    });
  }

  function clearSearch() {
    setSearchInput("");
    setDebouncedSearch("");
    setPageNumber(1);
  }

  function handlePageChange(
    nextPage: number,
  ) {
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
        .getElementById("game-results")
        ?.scrollIntoView({
          behavior: "smooth",
          block: "start",
        });
    });
  }

  const gamesError = gamesQuery.isError
    ? getApiErrorMessage(
        gamesQuery.error,
        "Oyunları yükləmək mümkün olmadı.",
      )
    : null;

  return (
    <div className="min-h-screen wellplayd-page-bg text-zinc-100">
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
                Oyunları ada, şirkətə, janra,
                platformaya və çıxış ilinə görə
                axtar.
              </p>
            </div>

            <div className="relative mt-9 max-w-3xl">
              <Search
                size={20}
                className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600"
              />

              <input
                type="search"
                autoComplete="off"
                value={searchInput}
                onChange={(event) => {
                  setSearchInput(
                    event.target.value,
                  );

                  setPageNumber(1);
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
                    effectiveCompanyReference
                  }
                  options={companyOptions}
                  onChange={
                    handleCompanyChange
                  }
                  placeholder="Şirkət seç"
                  clearLabel="Bütün şirkətlər"
                  searchPlaceholder="Şirkət axtar..."
                  emptyMessage="Şirkət tapılmadı."
                  onSearchChange={
                    setCompanySearch
                  }
                  isSearching={
                    companiesQuery.isFetching
                  }
                  disabled={
                    companiesQuery.isLoading &&
                    knownCompanies.length === 0
                  }
                />
              </label>

              <label className="block">
                <span className="mb-2 block text-xs font-semibold uppercase tracking-wider text-zinc-500">
                  Janr
                </span>

                <SearchableSelect
                  value={
                    effectiveGenreReference
                  }
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
                    effectivePlatformReference
                  }
                  options={platformOptions}
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

                    setPageNumber(1);
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

                    setPageNumber(1);
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
              Çıxış ili 1900–2200 aralığında
              olmalıdır.
            </div>
          )}

          <div
            id="game-results"
            className="mt-10 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between"
          >
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
                  {gamesQuery.isFetching && (
                    <LoaderCircle
                      size={14}
                      className="mr-2 inline animate-spin"
                    />
                  )}

                  Ümumi{" "}
                  {totalCount.toLocaleString(
                    "az-AZ",
                  )}{" "}
                  uyğun oyun tapıldı
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

              {totalPages > 1 && (
                <nav
                  className="mt-10 flex flex-wrap items-center justify-center gap-2"
                  aria-label="Oyun səhifələri"
                >
                  <button
                    type="button"
                    onClick={() => {
                      handlePageChange(
                        pageNumber - 1,
                      );
                    }}
                    disabled={
                      pageNumber === 1 ||
                      gamesQuery.isFetching
                    }
                    className="flex h-10 items-center gap-1 rounded-xl border border-white/[0.08] bg-white/[0.035] px-3 text-sm font-semibold text-zinc-400 transition hover:bg-white/[0.07] hover:text-white disabled:cursor-not-allowed disabled:opacity-40"
                    aria-label="Əvvəlki səhifə"
                  >
                    <ChevronLeft size={17} />
                    <span className="hidden sm:inline">
                      Əvvəlki
                    </span>
                  </button>

                  {paginationItems.map(
                    (item) => {
                      if (
                        typeof item !== "number"
                      ) {
                        return (
                          <span
                            key={item}
                            className="flex h-10 w-8 items-center justify-center text-sm text-zinc-600"
                            aria-hidden="true"
                          >
                            …
                          </span>
                        );
                      }

                      const isCurrentPage =
                        item === pageNumber;

                      return (
                        <button
                          key={item}
                          type="button"
                          onClick={() => {
                            handlePageChange(item);
                          }}
                          disabled={
                            gamesQuery.isFetching
                          }
                          aria-current={
                            isCurrentPage
                              ? "page"
                              : undefined
                          }
                          className={`flex h-10 min-w-10 items-center justify-center rounded-xl border px-3 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-60 ${
                            isCurrentPage
                              ? "border-violet-400/30 bg-violet-600 text-white"
                              : "border-white/[0.08] bg-white/[0.035] text-zinc-400 hover:bg-white/[0.07] hover:text-white"
                          }`}
                        >
                          {item}
                        </button>
                      );
                    },
                  )}

                  <button
                    type="button"
                    onClick={() => {
                      handlePageChange(
                        pageNumber + 1,
                      );
                    }}
                    disabled={
                      pageNumber === totalPages ||
                      gamesQuery.isFetching
                    }
                    className="flex h-10 items-center gap-1 rounded-xl border border-white/[0.08] bg-white/[0.035] px-3 text-sm font-semibold text-zinc-400 transition hover:bg-white/[0.07] hover:text-white disabled:cursor-not-allowed disabled:opacity-40"
                    aria-label="Növbəti səhifə"
                  >
                    <span className="hidden sm:inline">
                      Növbəti
                    </span>

                    <ChevronRight size={17} />
                  </button>
                </nav>
              )}
            </>
          )}
        </section>
      </main>

      <Footer />
    </div>
  );
}