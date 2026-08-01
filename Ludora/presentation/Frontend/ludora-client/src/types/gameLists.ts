import type { CatalogSource } from "./home";
import type { ListVisibility } from "./profile";

export type GameListCollaborationStatus = "Pending" | "Accepted";

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

export interface GameListCollaboratorDTO {
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  status: GameListCollaborationStatus;
  invitedAtUtc: string;
  respondedAtUtc: string | null;
}

export interface GameListCloneDTO {
  name: string;
  visibility: ListVisibility;
}

export interface GameListReorderItemDTO {
  source: CatalogSource;
  gameId: number;
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
  canEditGames: boolean;
  canManageCollaborators: boolean;
  currentUserCollaborationStatus: GameListCollaborationStatus | null;
  gameCount: number;
  createdAt: string;
  updatedAt: string | null;
  likeCount: number;
  commentCount: number;
  isLikedByCurrentUser: boolean;
  backgroundCoverImageUrl: string | null;
  collaborators: GameListCollaboratorDTO[];
  games: GameListEntryDTO[];
}
