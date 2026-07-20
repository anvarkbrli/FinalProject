import type {
  CatalogSource,
  PagedResultDTO,
} from "./home";

export type ReviewSort =
  | "Newest"
  | "Oldest"
  | "HighestRated"
  | "LowestRated"
  | "MostLiked"
  | "LeastLiked"
  | "MostCommented"
  | "LeastCommented";

export interface ReviewDTO {
  id: number;
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  source: CatalogSource;
  gameId: number;
  gameName: string;
  gameSlug: string | null;
  gameCoverImageUrl: string | null;
  content: string;
  rating: number | null;
  containsSpoilers: boolean;
  playedOn: string | null;
  platformSource: CatalogSource | null;
  platformId: number | null;
  platformName: string | null;
  playtimeMinutes: number | null;
  createdAt: string;
  updatedAt: string | null;
  isMine: boolean;
  likeCount: number;
  commentCount: number;
  isLikedByCurrentUser: boolean;
}

export interface ReviewWriteDTO {
  content: string;
  rating: number | null;
  containsSpoilers: boolean;
  playedOn: string | null;
  platformSource: CatalogSource | null;
  platformId: number | null;
  playtimeMinutes: number | null;
}

export type ReviewsResultDTO =
  PagedResultDTO<ReviewDTO>;

export interface ReviewCommentDTO {
  id: number;
  reviewId: number;
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  content: string;
  createdAt: string;
  updatedAt: string | null;
  isMine: boolean;
}

export type ReviewCommentsResultDTO =
  PagedResultDTO<ReviewCommentDTO>;

export interface ReviewLikeUserDTO {
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  likedAt: string;
}

export type ReviewLikesResultDTO =
  PagedResultDTO<ReviewLikeUserDTO>;
