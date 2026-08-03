import { api } from "../lib/api";
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
          companyFilters:
            filter.companyReferences?.length
              ? filter.companyReferences.join(",")
              : undefined,

          genreSource:
            filter.genreSource,
          genreId:
            filter.genreId,
          genreFilters:
            filter.genreReferences?.length
              ? filter.genreReferences.join(",")
              : undefined,

          platformSource:
            filter.platformSource,
          platformId:
            filter.platformId,
          platformFilters:
            filter.platformReferences?.length
              ? filter.platformReferences.join(",")
              : undefined,

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

  return response.data;
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
