import type { CatalogSource } from "./home";
import type { ListVisibility } from "./profile";

export interface GameListCreateDTO {
  name: string;
  description: string | null;
  visibility: ListVisibility;
  isRanked: boolean;
}

export interface GameListUpdateDTO {
  name?: string;
  description?: string | null;
  visibility?: ListVisibility;
  isRanked?: boolean;
}

export interface GameListEntryDTO {
  source: CatalogSource;
  id: number;
  title: string;
  coverImageUrl: string | null;
  releaseDate: string | null;
  averageRating: number | null;
  position: number | null;
  addedAt: string;
}

export interface GameListDetailDTO {
  id: number;
  name: string;
  description: string | null;
  visibility: ListVisibility;
  isRanked: boolean;
  ownerId: string;
  ownerUserName: string;
  isOwner: boolean;
  gameCount: number;
  createdAt: string;
  updatedAt: string | null;
  likeCount: number;
  commentCount: number;
  isLikedByCurrentUser: boolean;
  games: GameListEntryDTO[];
}
