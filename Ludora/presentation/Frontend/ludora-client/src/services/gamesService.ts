import { api } from "../lib/api";
import { deduplicateGames } from "../lib/deduplicateGames";
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

          sortBy:
            filter.sortBy || undefined,

          includeRelatedContent:
            filter.includeRelatedContent || undefined,

          pageNumber:
            filter.pageNumber,

          pageSize:
            filter.pageSize,
        },
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
): Promise<GameDetailDTO> {
  const response =
    await api.get<GameDetailDTO>(
      `/games/${source}/${id}`,
    );

  return response.data;
}

export async function getGameCompanies(
  searchName?: string,
): Promise<CatalogFilterItemDTO[]> {
  const response =
    await api.get<CatalogFilterItemDTO[]>(
      "/companies",
      {
        params: {
          searchName:
            searchName?.trim() || undefined,
        },
      },
    );

  return response.data;
}

export async function getGameGenres(): Promise<
  CatalogFilterItemDTO[]
> {
  const response =
    await api.get<CatalogFilterItemDTO[]>(
      "/genres",
    );

  return response.data;
}

export async function getGamePlatforms(): Promise<
  CatalogFilterItemDTO[]
> {
  const response =
    await api.get<CatalogFilterItemDTO[]>(
      "/platforms",
    );

  return response.data;
}

export async function getCatalogEntityById(
  kind: CatalogEntityKind,
  source: "Local" | "Igdb",
  id: number,
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
    );

  return response.data;
}
