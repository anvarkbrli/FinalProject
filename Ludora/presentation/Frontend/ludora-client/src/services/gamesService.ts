import { api } from "../lib/api";
import type {
  CatalogFilterItemDTO,
  GameCatalogFilterDTO,
  GameCatalogItemDTO,
} from "../types/games";

export async function getGames(
  filter: GameCatalogFilterDTO,
): Promise<GameCatalogItemDTO[]> {
  const response =
    await api.get<GameCatalogItemDTO[]>(
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

          pageNumber:
            filter.pageNumber,

          pageSize:
            filter.pageSize,
        },
      },
    );

  return response.data;
}

export async function getGameCompanies(): Promise<
  CatalogFilterItemDTO[]
> {
  const response =
    await api.get<CatalogFilterItemDTO[]>(
      "/companies",
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