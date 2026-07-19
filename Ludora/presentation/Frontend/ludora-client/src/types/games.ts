export type CatalogEntityKind =
  | "genre"
  | "platform"
  | "company";

import type {
  CatalogSource,
  GameListItemDTO,
  GameType,
  LookupDTO,
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

export type GameCompanyRole =
  | "Developer"
  | "Publisher";

export interface CompanyLookupDTO extends LookupDTO {
  role: GameCompanyRole;
}

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
  includeRelatedContent?: boolean;

  pageNumber: number;
  pageSize: number;
}

export interface RelatedGamesDTO {
  parentGame: GameListItemDTO | null;
  dlcs: GameListItemDTO[];
  expansions: GameListItemDTO[];
  standaloneExpansions: GameListItemDTO[];
  bundles: GameListItemDTO[];
  remakes: GameListItemDTO[];
  remasters: GameListItemDTO[];
}


export interface GameRatingDistributionItemDTO {
  rating: number;
  count: number;
}

export interface GameCommunityStatsDTO {
  averageRating: number | null;
  ratingCount: number;
  ratingDistribution: GameRatingDistributionItemDTO[];
  playingCount: number;
  planToPlayCount: number;
  playedCount: number;
  droppedCount: number;
  wishlistCount: number;
  favoriteCount: number;
}

export interface GameDetailDTO {
  source: CatalogSource;
  id: number;
  title: string;
  description: string | null;
  releaseDate: string | null;
  coverImageUrl: string | null;
  playCount: number;
  averageRating: number | null;
  isOverridden: boolean;
  companies: CompanyLookupDTO[];
  genres: LookupDTO[];
  platforms: LookupDTO[];
  gameType: GameType;
  communityStats: GameCommunityStatsDTO;
  relatedGames: RelatedGamesDTO;
}


export interface PagedResultDTO<TItem> {
  items: TItem[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export type GameCatalogItemDTO =
  GameListItemDTO;
