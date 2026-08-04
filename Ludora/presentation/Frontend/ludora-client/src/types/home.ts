export type CatalogSource = "Local" | "Igdb";

export type GameType =
  | "MainGame"
  | "DlcAddon"
  | "Expansion"
  | "Bundle"
  | "StandaloneExpansion"
  | "Mod"
  | "Episode"
  | "Season"
  | "Remake"
  | "Remaster"
  | "ExpandedGame"
  | "Port"
  | "Fork"
  | "Pack"
  | "Update";

export interface LookupDTO {
  source: CatalogSource;
  id: number;
  name: string;
  isOverridden: boolean;
}

export interface GameListItemDTO {
  source: CatalogSource;
  id: number;
  title: string;
  coverImageUrl: string | null;
  releaseDate: string | null;
  averageRating: number | null;
  isOverridden: boolean;
  gameType: GameType;
  genres: LookupDTO[];
  platforms: LookupDTO[];
}

export interface DiscussionListItemDTO {
  id: number;
  title: string;
  slug: string;
  contentPreview: string;
  media: DiscussionMediaDTO[];
  poll: DiscussionPollPreviewDTO | null;

  categoryId: number;
  categoryName: string;
  categorySlug: string;

  communityId: number | null;
  communityName: string | null;
  communitySlug: string | null;
  communityIconUrl: string | null;

  authorId: string;
  authorUserName: string;
  authorProfileImageUrl: string | null;
  authorCommunityRole: "Owner" | "Moderator" | "Member" | null;
  authorCommunityFlairName: string | null;
  authorCommunityFlairBackgroundColor: string | null;
  authorCommunityFlairTextColor: string | null;

  likeCount: number;
  replyCount: number;
  viewCount: number;

  isLikedByCurrentUser: boolean;
  isSavedByCurrentUser: boolean;
  isPinned: boolean;
  isLocked: boolean;
  isSpoiler: boolean;
  isDeleted: boolean;
  canEdit: boolean;
  canDelete: boolean;

  createdAt: string;
  updatedAt: string | null;
}

export interface DiscussionMediaDTO {
  id: number;
  url: string;
  contentType: string;
  originalFileName: string;
  fileSize: number;
  sortOrder: number;
  isVideo: boolean;
}

export interface DiscussionPollPreviewOptionDTO {
  id: number;
  text: string;
  displayOrder: number;
}

export interface DiscussionPollPreviewDTO {
  id: number;
  question: string;
  endsAtUtc: string;
  isClosed: boolean;
  options: DiscussionPollPreviewOptionDTO[];
}

export type PollResultsVisibility = "Always" | "AfterVote" | "AfterEnd";

export interface DiscussionPollOptionDTO {
  id: number;
  text: string;
  displayOrder: number;
  voteCount: number | null;
  percentage: number | null;
}

export interface DiscussionPollParticipantDTO {
  userId: string;
  userName: string;
  avatarUrl: string | null;
}

export interface DiscussionPollDTO {
  id: number;
  question: string;
  endsAtUtc: string;
  isClosed: boolean;
  hasEnded: boolean;
  allowVoteChange: boolean;
  resultsVisibility: PollResultsVisibility;
  canViewResults: boolean;
  hasVoted: boolean;
  selectedOptionId: number | null;
  totalVotes: number | null;
  canEdit: boolean;
  options: DiscussionPollOptionDTO[];
  participantPreview: DiscussionPollParticipantDTO[];
}

export interface ForumCategoryDTO {
  id: number;
  name: string;
  slug: string;
  description: string | null;
  discussionCount: number;
  isDeleted: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export type AnnouncementAudience =
  | "Everyone"
  | "AzerbaijaniUsers"
  | "EnglishUsers"
  | "SteamLinkedUsers"
  | "NewUsers"
  | "RegularUsers"
  | "Moderators"
  | "Administrators";

export interface AnnouncementDTO {
  id: number;
  title: string;
  content: string;
  createdByUserId: string;
  createdByUserName: string;
  audience: AnnouncementAudience;
  isPublished: boolean;
  publishedAtUtc: string | null;
  isDeleted: boolean;
  uniqueViewCount: number;
  targetUserCount: number;
  viewRatePercent: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface HomeComingSoonGameDTO {
  anticipationRank: number;
  source: CatalogSource;
  id: number;
  title: string;
  coverImageUrl: string | null;
  releaseDate: string | null;
}

export interface HomeDiscoveryDTO {
  trendingGames: GameListItemDTO[];
  comingSoonGames: HomeComingSoonGameDTO[];
  generatedAtUtc: string;
}

export interface HomeInterestRecommendationDTO {
  game: GameListItemDTO;
  reason: string;
  matchScore: number;
}

export interface HomeInterestRecommendationsDTO {
  language: "az" | "en";
  items: HomeInterestRecommendationDTO[];
  summary: string;
  isAiGenerated: boolean;
  generatedAtUtc: string;
}

export interface PagedResultDTO<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface HomeSocialUserDTO {
  id: string;
  userName: string;
  profileImageUrl: string | null;
}

export interface HomeSocialGameDTO {
  source: CatalogSource;
  id: number;
  title: string;
  coverImageUrl: string | null;
}

export interface HomeFriendsPlayingGameDTO {
  game: HomeSocialGameDTO;
  friends: HomeSocialUserDTO[];
  friendCount: number;
  lastActivityAtUtc: string;
}

export interface HomeFriendRatingDTO {
  user: HomeSocialUserDTO;
  game: HomeSocialGameDTO;
  rating: number;
  ratedAtUtc: string;
}


export interface HomeFriendReviewDTO {
  reviewId: number;
  user: HomeSocialUserDTO;
  game: HomeSocialGameDTO;
  rating: number | null;
  content: string;
  containsSpoilers: boolean;
  createdAtUtc: string;
}

export interface HomeTasteMatchDTO {
  user: HomeSocialUserDTO;
  matchPercent: number;
  sharedGameCount: number;
  sharedFavoriteCount: number;
  sharedGames: HomeSocialGameDTO[];
}

export interface HomeFriendSuggestionDTO {
  user: HomeSocialUserDTO;
  isPrivate: boolean;
  mutualFollowCount: number;
  sharedGameCount: number;
  sharedFavoriteCount: number;
}

export interface HomeSocialDashboardDTO {
  followedUserCount: number;
  activities: import("./profile").UserActivityDTO[];
  friendsPlaying: HomeFriendsPlayingGameDTO[];
  friendRatings: HomeFriendRatingDTO[];
  friendReviews: HomeFriendReviewDTO[];
  tasteMatch: HomeTasteMatchDTO | null;
  friendSuggestions: HomeFriendSuggestionDTO[];
}
