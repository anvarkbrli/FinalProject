import type { CatalogSource } from "./home";
import type { ListVisibility } from "./profile";

export interface GlobalSearchCountsDTO {
  games: number;
  users: number;
  discussions: number;
  reviews: number;
  lists: number;
  total: number;
}

export interface SearchGameResultDTO {
  source: CatalogSource;
  id: number;
  title: string;
  coverImageUrl: string | null;
  releaseDate: string | null;
  averageRating: number | null;
  score: number;
}

export interface SearchUserResultDTO {
  id: string;
  userName: string;
  profileImageUrl: string | null;
  bio: string | null;
  score: number;
}

export interface SearchDiscussionResultDTO {
  id: number;
  title: string;
  slug: string;
  categoryName: string;
  authorUserName: string;
  createdAt: string;
  score: number;
}

export interface SearchReviewResultDTO {
  id: number;
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  source: CatalogSource;
  gameId: number;
  gameName: string;
  gameCoverImageUrl: string | null;
  contentPreview: string;
  containsSpoilers: boolean;
  isMine: boolean;
  createdAt: string;
  score: number;
}

export interface SearchGameListResultDTO {
  id: number;
  name: string;
  description: string | null;
  visibility: ListVisibility;
  isRanked: boolean;
  ownerId: string;
  ownerUserName: string;
  gameCount: number;
  createdAt: string;
  score: number;
}

export interface GlobalSearchResultDTO {
  query: string;
  counts: GlobalSearchCountsDTO;
  games: SearchGameResultDTO[];
  users: SearchUserResultDTO[];
  discussions: SearchDiscussionResultDTO[];
  reviews: SearchReviewResultDTO[];
  lists: SearchGameListResultDTO[];
}
