import type { PagedResultDTO } from "./home";

export type InteractionContentType = "post" | "game-list";

export interface InteractionLikeUserDTO {
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  likedAt: string;
}

export type InteractionLikesResultDTO =
  PagedResultDTO<InteractionLikeUserDTO>;

export interface InteractionCommentDTO {
  id: number;
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  content: string;
  createdAt: string;
  updatedAt: string | null;
  isMine: boolean;
}

export type InteractionCommentsResultDTO =
  PagedResultDTO<InteractionCommentDTO>;
