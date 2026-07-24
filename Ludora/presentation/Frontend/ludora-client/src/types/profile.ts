import type {
  CatalogSource,
  PagedResultDTO,
} from "./home";

export type SocialPlatform =
  | "Steam"
  | "Discord"
  | "X"
  | "Twitch"
  | "YouTube"
  | "Instagram"
  | "Spotify"
  | "Letterboxd"
  | "GitHub"
  | "TikTok"
  | "Reddit"
  | "Xbox"
  | "PlayStation"
  | "EpicGames";

export interface UserSocialLinkDTO {
  platform: SocialPlatform;
  url: string;
}

export interface UserRatingDistributionDTO {
  rating: number;
  count: number;
}

export interface UserProfileStatisticsDTO {
  totalPlaytimeMinutes: number;
  reviewCount: number;
  ratingCount: number;
  averageRating: number | null;
  ratingDistribution: UserRatingDistributionDTO[];
}

export type UserPlaytimePeriod =
  | "Weekly"
  | "Monthly"
  | "Yearly";

export interface UserPlaytimeChartGameDTO {
  source: CatalogSource;
  gameId: number;
  name: string;
  coverUrl: string | null;
  playtimeMinutes: number;
}

export interface UserPlaytimeChartPointDTO {
  startDate: string;
  endDate: string;
  playtimeMinutes: number;
  gameCount: number;
  games: UserPlaytimeChartGameDTO[];
}

export interface UserTopPlayedGameDTO {
  source: CatalogSource;
  gameId: number;
  name: string;
  coverUrl: string | null;
  playtimeMinutes: number;
}

export interface UserTopPlayedCategoryDTO {
  name: string;
  playtimeMinutes: number;
  percentage: number;
}

export interface UserPlaytimeStatisticsDTO {
  period: UserPlaytimePeriod;
  periodTotalPlaytimeMinutes: number;
  allTimeTotalPlaytimeMinutes: number;
  chart: UserPlaytimeChartPointDTO[];
  topGame: UserTopPlayedGameDTO | null;
  topGenre: UserTopPlayedCategoryDTO | null;
  topPlatform: UserTopPlayedCategoryDTO | null;
}

export interface UserProfileDTO {
  id: string;
  userName: string;
  bio: string | null;
  profileImageUrl: string | null;
  socialLinks: UserSocialLinkDTO[];
  isPrivate: boolean;
  createdAt: string;
  followerCount: number;
  followingCount: number;
  postCount: number;
  statistics: UserProfileStatisticsDTO;
  badges: UserBadgeDTO[];
}

export interface UserBadgeDTO {
  id: number;
  code: string;
  name: string;
  description: string;
  icon: string;
  color: string;
  metricValueAtAward: number;
  awardedAtUtc: string;
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
  content: string | null;
  imageUrl: string | null;
  createdAt: string;
  updatedAt: string | null;
  isMine: boolean;
  likeCount: number;
  commentCount: number;
  isLikedByCurrentUser: boolean;
}

export interface PostCreateDTO {
  content: string;
  image: File | null;
}

export interface PostUpdateDTO {
  content: string;
  image: File | null;
  removeImage: boolean;
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
  isRanked: boolean;
  ownerId: string;
  ownerUserName: string;
  gameCount: number;
  createdAt: string;
  updatedAt: string | null;
  likeCount: number;
  commentCount: number;
  isLikedByCurrentUser: boolean;
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


export type UserProfileGameSortBy =
  | "RecentlyUpdated"
  | "RecentlyReleased"
  | "OldestRelease"
  | "RatingHighToLow"
  | "RatingLowToHigh"
  | "NameAsc"
  | "NameDesc"
  | "RecentlyPlayed";

export interface ProfileGameEntryDTO {
  source: CatalogSource;
  gameId: number;
  name: string;
  slug: string | null;
  coverUrl: string | null;
  releaseDate: string | null;
  status:
    | "Playing"
    | "PlanToPlay"
    | "Played"
    | "Dropped"
    | null;
  isFavorite: boolean;
  isWishlisted: boolean;
  rating: number | null;
  reviewId: number | null;
  playedPlatformName: string | null;
  playedOn: string | null;
  playtimeMinutes: number | null;
  genres: string[];
  platforms: string[];
  companies: string[];
  createdAt: string;
  updatedAt: string | null;
}

export type UserProfileGamesResultDTO =
  PagedResultDTO<ProfileGameEntryDTO>;

export type UserActivityFeedScope = "Mine" | "Following";

export type UserActivityFeedCategory =
  | "All"
  | "Statuses"
  | "Lists"
  | "Ratings"
  | "Reviews"
  | "Posts";

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
  | "GameListDeleted"
  | "GameAddedToList"
  | "GameRemovedFromList"
  | "GameListGameReordered"
  | "PostLiked"
  | "PostUnliked"
  | "PostCommented"
  | "PostCommentUpdated"
  | "PostCommentDeleted"
  | "GameListLiked"
  | "GameListUnliked"
  | "GameListCommented"
  | "GameListCommentUpdated"
  | "GameListCommentDeleted";

export interface UserActivityDTO {
  id: number;
  type: UserActivityType;
  createdAt: string;
  actorUserId: string;
  actorUserName: string;
  actorUserProfileImageUrl: string | null;
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
