import type { CatalogSource } from "./home";

export type UserGameStatus =
  | "Playing"
  | "PlanToPlay"
  | "Played"
  | "Dropped";

export interface UserGameDTO {
  source: CatalogSource;
  gameId: number;
  name: string;
  slug: string | null;
  coverUrl: string | null;
  releaseDate: string | null;
  status: UserGameStatus | null;
  isFavorite: boolean;
  isWishlisted: boolean;
  rating: number | null;
  createdAt: string;
  updatedAt: string | null;
}


export interface WishlistedFriendDTO {
  userId: string;
  userName: string;
  profileImageUrl: string | null;
}
