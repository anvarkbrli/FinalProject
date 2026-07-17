import type {
  CatalogSource,
  GameListItemDTO,
} from "./home";

export type GameSort =
  | ""
  | "name_asc"
  | "name_desc"
  | "rating_asc"
  | "rating_desc"
  | "play_asc"
  | "play_desc"
  | "year_asc"
  | "year_desc";

export interface CatalogFilterItemDTO {
  source: CatalogSource;
  id: number;
  name: string;
  isOverridden: boolean;
  createdAt: string | null;
  updatedAt: string | null;
}

export interface GameCatalogFilterDTO {
  source?: CatalogSource;
  searchName?: string;

  companySource?: CatalogSource;
  companyId?: number;

  genreSource?: CatalogSource;
  genreId?: number;

  platformSource?: CatalogSource;
  platformId?: number;

  releaseYear?: number;
  sortBy?: GameSort;

  pageNumber: number;
  pageSize: number;
}

export type GameCatalogItemDTO = GameListItemDTO;