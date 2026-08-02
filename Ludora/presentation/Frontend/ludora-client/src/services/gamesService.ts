import { api } from "../lib/api";
import { deduplicateGames } from "../lib/deduplicateGames";
import type { CatalogSource } from "../types/home";
import type {
  CatalogEntityKind,
  CatalogFilterItemDTO,
  GameCatalogFilterDTO,
  GameCatalogItemDTO,
  GameDetailDTO,
  PagedResultDTO,
} from "../types/games";

export async function getGames(
  filter: GameCatalogFilterDTO,
  signal?: AbortSignal,
): Promise<PagedResultDTO<GameCatalogItemDTO>> {
  const response =
    await api.get<PagedResultDTO<GameCatalogItemDTO>>(
      "/games",
      {
        params: {
          source: filter.source,
          searchName:
            filter.searchName || undefined,

          companySource:
            filter.companySource,
          companyId:
            filter.companyId,

          genreSource:
            filter.genreSource,
          genreId:
            filter.genreId,

          platformSource:
            filter.platformSource,
          platformId:
            filter.platformId,

          releaseYear:
            filter.releaseYear,

          releasedOnly:
            filter.releasedOnly || undefined,

          sortBy:
            filter.sortBy || undefined,

          includeRelatedContent:
            filter.includeRelatedContent || undefined,

          pageNumber:
            filter.pageNumber,

          pageSize:
            filter.pageSize,
        },
        signal,
      },
    );

  if (!filter.searchName?.trim()) {
    return response.data;
  }

  return {
    ...response.data,
    items: deduplicateGames(response.data.items),
  };
}

export async function getGameById(
  source: "Local" | "Igdb",
  id: number,
  signal?: AbortSignal,
): Promise<GameDetailDTO> {
  const response =
    await api.get<GameDetailDTO>(
      `/games/${source}/${id}`,
      { signal },
    );

  return response.data;
}

export async function getGameCompanies(
  searchName?: string,
  signal?: AbortSignal,
): Promise<CatalogFilterItemDTO[]> {
  const response =
    await api.get<CatalogFilterItemDTO[]>(
      "/companies",
      {
        params: {
          searchName:
            searchName?.trim() || undefined,
        },
        signal,
      },
    );

  return response.data;
}

export async function getGameGenres(
  signal?: AbortSignal,
): Promise<
  CatalogFilterItemDTO[]
> {
  const response =
    await api.get<CatalogFilterItemDTO[]>(
      "/genres",
      { signal },
    );

  return response.data;
}

export async function getIgdbGameGenres(
  signal?: AbortSignal,
): Promise<CatalogFilterItemDTO[]> {
  const response =
    await api.get<CatalogFilterItemDTO[]>(
      "/genres",
      {
        params: {
          source: "Igdb",
        },
        signal,
      },
    );

  return response.data;
}

export async function getGamePlatforms(
  signal?: AbortSignal,
): Promise<
  CatalogFilterItemDTO[]
> {
  const response =
    await api.get<CatalogFilterItemDTO[]>(
      "/platforms",
      { signal },
    );

  return response.data;
}

export async function getCatalogEntityById(
  kind: CatalogEntityKind,
  source: "Local" | "Igdb",
  id: number,
  signal?: AbortSignal,
): Promise<CatalogFilterItemDTO> {
  const endpointByKind: Record<
    CatalogEntityKind,
    string
  > = {
    genre: "genres",
    platform: "platforms",
    company: "companies",
  };

  const response =
    await api.get<CatalogFilterItemDTO>(
      `/${endpointByKind[kind]}/${source}/${id}`,
      { signal },
    );

  return response.data;
}

export async function deleteGame(
  source: CatalogSource,
  gameId: number,
): Promise<void> {
  await api.delete(`/games/${source}/${gameId}`);
}
