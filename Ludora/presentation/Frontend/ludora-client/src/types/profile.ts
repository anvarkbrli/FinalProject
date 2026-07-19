import type {
  CatalogSource,
  PagedResultDTO,
} from "./home";

export interface UserProfileDTO {
  id: string;
  userName: string;
  bio: string | null;
  profileImageUrl: string | null;
  isPrivate: boolean;
  createdAt: string;
  followerCount: number;
  followingCount: number;
  postCount: number;
}

export interface FollowStatusDTO {
  userId: string;
  isFollowing: boolean;
  isPending: boolean;
}

export interface UserListItemDTO {
  id: string;
  userName: string;
  profileImageUrl: string | null;
  isFollowing: boolean;
}

export interface FollowRequestDTO {
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  requestedAt: string;
}

export interface PostDTO {
  id: number;
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  content: string;
  createdAt: string;
  updatedAt: string | null;
  isMine: boolean;
}

export interface PostCreateDTO {
  content: string;
}

export interface PostUpdateDTO {
  content: string;
}

export type ListVisibility =
  | "Public"
  | "Private"
  | "FollowersOnly";

export interface GameListSummaryDTO {
  id: number;
  name: string;
  description: string | null;
  visibility: ListVisibility;
  ownerId: string;
  ownerUserName: string;
  gameCount: number;
  createdAt: string;
  updatedAt: string | null;
}

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

export type UserReviewsResultDTO =
  PagedResultDTO<ReviewDTO>;

export type UserActivityType =
  | "GameStatusChanged"
  | "GameStatusRemoved"
  | "GameAddedToFavorites"
  | "GameRemovedFromFavorites"
  | "GameAddedToWishlist"
  | "GameRemovedFromWishlist"
  | "GameRated"
  | "GameRatingRemoved"
  | "ReviewCreated"
  | "ReviewUpdated"
  | "ReviewDeleted"
  | "ReviewLiked"
  | "ReviewUnliked"
  | "ReviewCommented"
  | "ReviewCommentUpdated"
  | "ReviewCommentDeleted"
  | "UserFollowRequested"
  | "UserFollowed"
  | "UserUnfollowed"
  | "FollowRequestCancelled"
  | "UserBlocked"
  | "UserUnblocked"
  | "PostCreated"
  | "PostUpdated"
  | "PostDeleted"
  | "GameListCreated"
  | "GameListUpdated"
  | "GameListDeleted";

export interface UserActivityDTO {
  id: number;
  type: UserActivityType;
  createdAt: string;
  targetUserId: string | null;
  targetUserName: string | null;
  targetUserProfileImageUrl: string | null;
  gameSource: CatalogSource | null;
  gameId: number | null;
  gameName: string | null;
  gameSlug: string | null;
  gameCoverImageUrl: string | null;
  reviewId: number | null;
  status:
    | "Playing"
    | "PlanToPlay"
    | "Played"
    | "Dropped"
    | null;
  rating: number | null;
  postId: number | null;
  gameListId: number | null;
  subjectTitle: string | null;
}

export type UserActivitiesResultDTO =
  PagedResultDTO<UserActivityDTO>;
