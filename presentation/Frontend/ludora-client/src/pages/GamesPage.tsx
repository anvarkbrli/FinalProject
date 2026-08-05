import {
  keepPreviousData,
  useQueries,
  useQuery,
} from "@tanstack/react-query";
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
  useRef,
  useState,
} from "react";
import {
  useLocation,
  useSearchParams,
} from "react-router-dom";
import { SearchableMultiSelect } from "../components/common/SearchableMultiSelect";
import {
  SearchableSelect,
  type SearchableSelectOption,
} from "../components/common/SearchableSelect";
import { GameCard } from "../components/home/GameCard";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { useI18n } from "../i18n/useI18n";
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

function getInitialCatalogReferences(
  searchParams: URLSearchParams,
  prefix: "company" | "genre" | "platform",
): string[] {
  const serializedReferences = searchParams.get(`${prefix}Filters`) ?? "";
  const references = serializedReferences
    .split(",")
    .map((value) => value.trim())
    .filter((value) => parseCatalogReference(value) !== null);

  if (references.length > 0) {
    return Array.from(new Set(references));
  }

  const source = searchParams.get(`${prefix}Source`);
  const id = Number(searchParams.get(`${prefix}Id`));

  if (
    (source !== "Local" && source !== "Igdb") ||
    !Number.isInteger(id) ||
    id < 1
  ) {
    return [];
  }

  return [`${source}:${id}`];
}

function getInitialPageNumber(
  searchParams: URLSearchParams,
): number {
  const value = Number(searchParams.get("page"));
  return Number.isInteger(value) && value > 0 ? value : 1;
}

function getInitialSort(
  searchParams: URLSearchParams,
): GameSort {
  const value = searchParams.get("sortBy") ?? "";
  return sortOptions.some((option) => option.value === value)
    ? (value as GameSort)
    : "";
}

function setOptionalSearchParam(
  searchParams: URLSearchParams,
  key: string,
  value: string,
) {
  if (value.trim()) {
    searchParams.set(key, value.trim());
  } else {
    searchParams.delete(key);
  }
}

function getCatalogReferencesFromNavigation(
  navigationState: CatalogFilterNavigationState | null,
  kind: "company" | "genre" | "platform",
): string[] {
  const filter = navigationState?.catalogFilter;
  return filter?.kind === kind
    ? [`${filter.source}:${filter.id}`]
    : [];
}

function getCatalogDescription(
  source: CatalogSource,
): string {
  return source === "Local"
    ? "WellPlayd kataloqu"
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
  selectedReferences: readonly string[],
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
      selectedReferences.includes(itemReference) ||
      (current.source === "Local" && item.source === "Igdb")
    ) {
      byName.set(normalizedName, item);
    }
  }

  return Array.from(byName.values());
}

function expandCatalogReferencesByName(
  selectedReferences: readonly string[],
  catalogItems: readonly CatalogFilterItemDTO[],
): string[] {
  if (selectedReferences.length === 0) {
    return [];
  }

  const selectedReferenceSet = new Set(selectedReferences);
  const selectedNames = new Set(
    catalogItems
      .filter((item) =>
        selectedReferenceSet.has(serializeCatalogReference(item)),
      )
      .map((item) => normalizeCatalogName(item.name)),
  );

  if (selectedNames.size === 0) {
    return [...selectedReferences];
  }

  return Array.from(
    new Set([
      ...selectedReferences,
      ...catalogItems
        .filter((item) =>
          selectedNames.has(normalizeCatalogName(item.name)),
        )
        .map(serializeCatalogReference),
    ]),
  );
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
  const { language } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const location = useLocation();

  const navigationState =
    location.state as CatalogFilterNavigationState | null;

  const navigationFilter = navigationState?.catalogFilter ?? null;
  const navigationFilterAppliedRef = useRef(false);

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

  const [companyReferences, setCompanyReferences] = useState<string[]>(
    () => {
      const referencesFromUrl = getInitialCatalogReferences(
        searchParams,
        "company",
      );

      return referencesFromUrl.length > 0
        ? referencesFromUrl
        : getCatalogReferencesFromNavigation(navigationState, "company");
    },
  );

  const [genreReferences, setGenreReferences] = useState<string[]>(
    () => {
      const referencesFromUrl = getInitialCatalogReferences(
        searchParams,
        "genre",
      );

      return referencesFromUrl.length > 0
        ? referencesFromUrl.slice(0, 1)
        : getCatalogReferencesFromNavigation(navigationState, "genre").slice(0, 1);
    },
  );

  const [platformReferences, setPlatformReferences] = useState<string[]>(
    () => {
      const referencesFromUrl = getInitialCatalogReferences(
        searchParams,
        "platform",
      );

      return referencesFromUrl.length > 0
        ? referencesFromUrl
        : getCatalogReferencesFromNavigation(navigationState, "platform");
    },
  );

  const [releaseYear, setReleaseYear] = useState(
    searchParams.get("releaseYear") ?? "",
  );

  const [sortBy, setSortBy] = useState<GameSort>(() =>
    getInitialSort(searchParams),
  );

  const [pageNumber, setPageNumber] = useState(() =>
    getInitialPageNumber(searchParams),
  );

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

  const serializedSearchParams = searchParams.toString();

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      const normalizedSearch = searchInput.trim();
      setDebouncedSearch(normalizedSearch);

      const currentSearchParams = new URLSearchParams(serializedSearchParams);
      const currentUrlSearch = currentSearchParams.get("search")?.trim() ?? "";
      if (normalizedSearch === currentUrlSearch) return;

      setOptionalSearchParam(currentSearchParams, "search", normalizedSearch);
      currentSearchParams.delete("page");
      setSearchParams(currentSearchParams, { replace: true });
    }, 350);

    return () => window.clearTimeout(timeoutId);
  }, [searchInput, serializedSearchParams, setSearchParams]);

  useEffect(() => {
    if (!navigationFilter || navigationFilterAppliedRef.current) return;

    navigationFilterAppliedRef.current = true;
    const nextSearchParams = new URLSearchParams(searchParams);
    const prefix = navigationFilter.kind;
    nextSearchParams.delete(`${prefix}Name`);
    nextSearchParams.delete(`${prefix}Source`);
    nextSearchParams.delete(`${prefix}Id`);
    nextSearchParams.set(
      `${prefix}Filters`,
      `${navigationFilter.source}:${navigationFilter.id}`,
    );
    nextSearchParams.delete("page");
    setSearchParams(nextSearchParams, { replace: true });
  }, [navigationFilter, searchParams, setSearchParams]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      const currentSearchParams = new URLSearchParams(serializedSearchParams);
      const nextSearch = currentSearchParams.get("search") ?? "";

      setSearchInput((current) => current === nextSearch ? current : nextSearch);
      setDebouncedSearch(nextSearch.trim());
      setCompanyReferences(getInitialCatalogReferences(currentSearchParams, "company"));
      setGenreReferences(
        getInitialCatalogReferences(currentSearchParams, "genre").slice(0, 1),
      );
      setPlatformReferences(getInitialCatalogReferences(currentSearchParams, "platform"));
      setReleaseYear(currentSearchParams.get("releaseYear") ?? "");
      setSortBy(getInitialSort(currentSearchParams));
      setPageNumber(getInitialPageNumber(currentSearchParams));
    }, 0);

    return () => window.clearTimeout(timeoutId);
  }, [serializedSearchParams]);

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
    (companyReferences.length === 0 ? routeCompanyName : "");

  const selectedCompanyReferences = companyReferences
    .map(parseCatalogReference)
    .filter((reference): reference is NonNullable<typeof reference> => reference !== null);

  const companiesQuery = useQuery({
    queryKey: [
      "game-catalog",
      "companies",
      companyQuerySearch,
    ],
    queryFn: ({ signal }) =>
      getGameCompanies(
        companyQuerySearch || undefined,
        signal,
      ),
    staleTime: 5 * 60_000,
  });

  const selectedCompanyQueries = useQueries({
    queries: selectedCompanyReferences.map((reference) => ({
      queryKey: [
        "game-catalog",
        "selected-company",
        reference.source,
        reference.id,
      ],
      queryFn: ({ signal }: { signal: AbortSignal }) =>
        getCatalogEntityById(
          "company",
          reference.source,
          reference.id,
          signal,
        ),
      staleTime: 5 * 60_000,
    })),
  });

  const genresQuery = useQuery({
    queryKey: ["game-catalog", "genres"],
    queryFn: ({ signal }) => getGameGenres(signal),
    staleTime: 5 * 60_000,
  });

  const platformsQuery = useQuery({
    queryKey: ["game-catalog", "platforms"],
    queryFn: ({ signal }) => getGamePlatforms(signal),
    staleTime: 5 * 60_000,
  });

  const selectedCompanyItems = selectedCompanyQueries
    .map((query) => query.data)
    .filter((item): item is CatalogFilterItemDTO => item !== undefined);

  const knownCompanies = mergeCatalogItems(
    companiesQuery.data ?? [],
    selectedCompanyItems,
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

  useEffect(() => {
    const resolvedFilters: Array<{
      prefix: "company" | "genre" | "platform";
      item: CatalogFilterItemDTO | null;
    }> = [
      { prefix: "company", item: companyFromName },
      { prefix: "genre", item: genreFromName },
      { prefix: "platform", item: platformFromName },
    ];

    const nextSearchParams = new URLSearchParams(searchParams);
    let changed = false;

    for (const { prefix, item } of resolvedFilters) {
      if (!item || !nextSearchParams.has(`${prefix}Name`)) continue;
      nextSearchParams.delete(`${prefix}Name`);
      nextSearchParams.delete(`${prefix}Source`);
      nextSearchParams.delete(`${prefix}Id`);
      nextSearchParams.set(
        `${prefix}Filters`,
        serializeCatalogReference(item),
      );
      changed = true;
    }

    if (changed) {
      nextSearchParams.delete("page");
      setSearchParams(nextSearchParams, { replace: true });
    }
  }, [companyFromName, genreFromName, platformFromName, searchParams, setSearchParams]);

  const effectiveCompanyReferences = useMemo(
    () =>
      companyReferences.length > 0
        ? companyReferences
        : companyFromName
          ? [serializeCatalogReference(companyFromName)]
          : [],
    [companyReferences, companyFromName],
  );

  const effectiveGenreReferences = useMemo(
    () =>
      genreReferences.length > 0
        ? genreReferences
        : genreFromName
          ? [serializeCatalogReference(genreFromName)]
          : [],
    [genreReferences, genreFromName],
  );

  const effectivePlatformReferences = useMemo(
    () =>
      platformReferences.length > 0
        ? platformReferences
        : platformFromName
          ? [serializeCatalogReference(platformFromName)]
          : [],
    [platformReferences, platformFromName],
  );

  // Public users see one logical option for equal Local/IGDB names. Expand
  // that option back to every matching catalog reference for the API query,
  // while keeping the visible selection single and source-agnostic.
  const queryCompanyReferences = useMemo(
    () =>
      canViewSources
        ? effectiveCompanyReferences
        : expandCatalogReferencesByName(
            effectiveCompanyReferences,
            knownCompanies,
          ),
    [canViewSources, effectiveCompanyReferences, knownCompanies],
  );

  const queryGenreReferences = useMemo(
    () =>
      canViewSources
        ? effectiveGenreReferences
        : expandCatalogReferencesByName(
            effectiveGenreReferences,
            genresQuery.data ?? [],
          ),
    [canViewSources, effectiveGenreReferences, genresQuery.data],
  );

  const queryPlatformReferences = useMemo(
    () =>
      canViewSources
        ? effectivePlatformReferences
        : expandCatalogReferencesByName(
            effectivePlatformReferences,
            platformsQuery.data ?? [],
          ),
    [canViewSources, effectivePlatformReferences, platformsQuery.data],
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
  const effectiveSortBy = sortBy;

  const gamesQuery = useQuery({
    queryKey: [
      "game-catalog",
      "games",
      debouncedSearch,
      queryCompanyReferences.join(","),
      queryGenreReferences.join(","),
      queryPlatformReferences.join(","),
      validReleaseYear,
      effectiveSortBy,
      pageNumber,
      PAGE_SIZE,
    ],
    queryFn: ({ signal }) =>
      getGames({
        searchName: debouncedSearch || undefined,
        companyReferences: queryCompanyReferences,
        genreReferences: queryGenreReferences,
        platformReferences: queryPlatformReferences,
        releaseYear: validReleaseYear,
        releasedOnly: true,
        sortBy: effectiveSortBy,
        includeRelatedContent: false,
        pageNumber,
        pageSize: PAGE_SIZE,
      }, signal),
    enabled: isReleaseYearValid,
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  });

  const currentPageData =
    gamesQuery.data?.pageNumber === pageNumber
      ? gamesQuery.data
      : undefined;

  const games: GameCatalogItemDTO[] =
    currentPageData?.items ?? [];

  const totalCount =
    currentPageData?.totalCount ?? 0;

  const totalPages =
    currentPageData?.totalPages ?? 0;

  const isInitialResultsLoad =
    gamesQuery.isLoading && gamesQuery.data === undefined;

  const isResultsPending =
    isReleaseYearValid &&
    (isInitialResultsLoad ||
      gamesQuery.isPlaceholderData ||
      (gamesQuery.isFetching && currentPageData === undefined));

  const hasResolvedEmptyResults =
    isReleaseYearValid &&
    gamesQuery.isSuccess &&
    !gamesQuery.isFetching &&
    !gamesQuery.isPlaceholderData &&
    currentPageData !== undefined &&
    games.length === 0;

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
        effectiveCompanyReferences,
      ),
    );
  }, [
    knownCompanies,
    canViewSources,
    effectiveCompanyReferences,
  ]);

  const sortedPlatforms = useMemo(() => {
    return sortPlatformsByCurrentRelevance(
      prepareCatalogItems(
        platformsQuery.data ?? [],
        canViewSources,
        effectivePlatformReferences,
      ),
    );
  }, [
    platformsQuery.data,
    canViewSources,
    effectivePlatformReferences,
  ]);

  const sortedGenres = useMemo(() => {
    return sortAlphabetically(
      prepareCatalogItems(
        genresQuery.data ?? [],
        canViewSources,
        effectiveGenreReferences,
      ),
    );
  }, [
    genresQuery.data,
    canViewSources,
    effectiveGenreReferences,
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

  function updateCatalogFilter(
    prefix: "company" | "genre" | "platform",
    values: string[],
  ) {
    const normalizedValues = Array.from(
      new Set(values.filter((value) => parseCatalogReference(value) !== null)),
    );
    const nextSearchParams = new URLSearchParams(searchParams);

    nextSearchParams.delete(`${prefix}Name`);
    nextSearchParams.delete(`${prefix}Source`);
    nextSearchParams.delete(`${prefix}Id`);

    if (normalizedValues.length > 0) {
      nextSearchParams.set(`${prefix}Filters`, normalizedValues.join(","));
    } else {
      nextSearchParams.delete(`${prefix}Filters`);
    }

    nextSearchParams.delete("page");
    setSearchParams(nextSearchParams, { replace: true });
  }

  function handleCompanyChange(values: string[]) {
    setCompanyReferences(values);
    setPageNumber(1);
    updateCatalogFilter("company", values);
  }

  function handleGenreChange(values: string[]) {
    const nextValues = values.slice(0, 1);
    setGenreReferences(nextValues);
    setPageNumber(1);
    updateCatalogFilter("genre", nextValues);
  }

  function handlePlatformChange(values: string[]) {
    setPlatformReferences(values);
    setPageNumber(1);
    updateCatalogFilter("platform", values);
  }

  function handleReleaseYearChange(value: string) {
    setReleaseYear(value);
    setPageNumber(1);
    const nextSearchParams = new URLSearchParams(searchParams);
    setOptionalSearchParam(nextSearchParams, "releaseYear", value);
    nextSearchParams.delete("page");
    setSearchParams(nextSearchParams, { replace: true });
  }

  function handleSortChange(value: string) {
    const nextSort = sortOptions.some((option) => option.value === value)
      ? (value as GameSort)
      : "";
    setSortBy(nextSort);
    setPageNumber(1);
    const nextSearchParams = new URLSearchParams(searchParams);
    setOptionalSearchParam(nextSearchParams, "sortBy", nextSort);
    nextSearchParams.delete("page");
    setSearchParams(nextSearchParams, { replace: true });
  }

  const activeFilterCount =
    (searchInput.trim() ? 1 : 0) +
    effectiveCompanyReferences.length +
    effectiveGenreReferences.length +
    effectivePlatformReferences.length +
    (releaseYear ? 1 : 0) +
    (effectiveSortBy ? 1 : 0);

  function resetFilters() {
    setSearchInput("");
    setDebouncedSearch("");
    setCompanyReferences([]);
    setGenreReferences([]);
    setPlatformReferences([]);
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
    const nextSearchParams = new URLSearchParams(searchParams);
    nextSearchParams.delete("search");
    nextSearchParams.delete("page");
    setSearchParams(nextSearchParams, { replace: true });
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
    const nextSearchParams = new URLSearchParams(searchParams);
    if (nextPage === 1) nextSearchParams.delete("page");
    else nextSearchParams.set("page", String(nextPage));
    setSearchParams(nextSearchParams);

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
                WellPlayd oyun kataloqu
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

                <SearchableMultiSelect
                  values={effectiveCompanyReferences}
                  options={companyOptions}
                  onChange={handleCompanyChange}
                  placeholder="Şirkət seç"
                  clearLabel="Bütün şirkətlər"
                  selectedCountLabel={(count) => `${count} şirkət seçilib`}
                  searchPlaceholder="Şirkət axtar..."
                  emptyMessage="Şirkət tapılmadı."
                  onSearchChange={setCompanySearch}
                  isSearching={companiesQuery.isFetching}
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
                  value={effectiveGenreReferences[0] ?? ""}
                  options={genreOptions}
                  onChange={(value) =>
                    handleGenreChange(value ? [value] : [])
                  }
                  placeholder="Janr seç"
                  clearLabel="Bütün janrlar"
                  searchPlaceholder="Janr axtar..."
                  emptyMessage="Janr tapılmadı."
                  disabled={genresQuery.isLoading}
                />
              </label>

              <label className="block">
                <span className="mb-2 block text-xs font-semibold uppercase tracking-wider text-zinc-500">
                  Platforma
                </span>

                <SearchableMultiSelect
                  values={effectivePlatformReferences}
                  options={platformOptions}
                  onChange={handlePlatformChange}
                  placeholder="Platforma seç"
                  clearLabel="Bütün platformalar"
                  selectedCountLabel={(count) => `${count} platforma seçilib`}
                  searchPlaceholder="Platforma axtar..."
                  emptyMessage="Platforma tapılmadı."
                  disabled={platformsQuery.isLoading}
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
                  onChange={(event) =>
                    handleReleaseYearChange(event.target.value)
                  }
                  placeholder="Məsələn: 2026"
                  className="h-11 w-full rounded-xl border border-white/[0.08] bg-[#111116] px-3 text-sm text-zinc-300 outline-none transition placeholder:text-zinc-700 hover:border-white/[0.15] focus:border-violet-400/50 focus:ring-4 focus:ring-violet-500/10"
                />
              </label>

              <label className="block">
                <span className="mb-2 block text-xs font-semibold uppercase tracking-wider text-zinc-500">
                  Sıralama
                </span>

                <SearchableSelect
                  value={effectiveSortBy}
                  options={
                    sortSelectOptions
                  }
                  onChange={handleSortChange}
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

              {isResultsPending ? (
                <p
                  className="mt-2 flex items-center gap-2 text-sm text-zinc-500"
                  role="status"
                  aria-live="polite"
                >
                  <LoaderCircle
                    size={14}
                    className="shrink-0 animate-spin text-violet-400"
                  />
                  {language === "en"
                    ? isInitialResultsLoad
                      ? "Loading games..."
                      : "Searching for games..."
                    : isInitialResultsLoad
                      ? "Oyunlar yüklənir..."
                      : "Oyunlar axtarılır..."}
                </p>
              ) : gamesQuery.isSuccess && currentPageData ? (
                <p className="mt-2 text-sm text-zinc-500">
                  {gamesQuery.isFetching && (
                    <LoaderCircle
                      size={14}
                      className="mr-2 inline animate-spin"
                    />
                  )}

                  {language === "en"
                    ? `${totalCount.toLocaleString("en-US")} matching ${
                        totalCount === 1 ? "game" : "games"
                      } found`
                    : `Ümumi ${totalCount.toLocaleString(
                        "az-AZ",
                      )} uyğun oyun tapıldı`}
                </p>
              ) : null}
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

          {isResultsPending ? (
            <div
              className="mt-7 rounded-3xl border border-dashed border-white/[0.09] bg-white/[0.02] px-6 py-16 text-center"
              role="status"
              aria-live="polite"
              aria-busy="true"
            >
              <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl border border-violet-400/20 bg-violet-500/10 shadow-[0_16px_40px_rgba(124,58,237,0.14)]">
                <LoaderCircle
                  size={28}
                  className="animate-spin text-violet-400"
                />
              </div>

              <h3 className="mt-5 text-lg font-semibold text-zinc-300">
                {language === "en"
                  ? isInitialResultsLoad
                    ? "Loading games"
                    : "Searching for games"
                  : isInitialResultsLoad
                    ? "Oyunlar yüklənir"
                    : "Oyunlar axtarılır"}
              </h3>

              <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-zinc-600">
                {language === "en"
                  ? "Please wait while the catalog is updated for the selected page and filters."
                  : "Seçilmiş səhifə və filtrlərə uyğun kataloq yenilənir. Bir qədər gözlə."}
              </p>
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
          ) : hasResolvedEmptyResults ? (
            <div className="mt-7 rounded-3xl border border-dashed border-white/[0.09] bg-white/[0.02] px-6 py-16 text-center">
              <Gamepad2
                size={36}
                className="mx-auto text-zinc-700"
              />

              <h3 className="mt-5 text-lg font-semibold text-zinc-300">
                {language === "en"
                  ? "No matching games found"
                  : "Uyğun oyun tapılmadı"}
              </h3>

              <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-zinc-600">
                {language === "en"
                  ? "Try changing the search text or filters and search again."
                  : "Axtarış mətnini və ya filtrləri dəyişərək yenidən yoxla."}
              </p>

              <button
                type="button"
                onClick={resetFilters}
                className="mt-6 rounded-xl bg-violet-600 px-5 py-3 text-sm font-semibold text-white transition hover:bg-violet-500"
              >
                {language === "en"
                  ? "Clear filters"
                  : "Filtrləri təmizlə"}
              </button>
            </div>
          ) : games.length > 0 ? (
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
          ) : null}
        </section>
      </main>

      <Footer />
    </div>
  );
}
