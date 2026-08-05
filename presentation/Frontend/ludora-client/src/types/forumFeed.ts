import type { ForumCommunityDTO } from "./community";
import type { DiscussionListItemDTO } from "./home";
import type { PostDTO } from "./profile";

export type ForumFeedItemType = "Post" | "Discussion";
export type ForumPopularPeriod = "day" | "week" | "month" | "all";

export interface ForumFeedItemDTO {
  itemType: ForumFeedItemType;
  reason: string;
  publishedAt: string;
  post: PostDTO | null;
  discussion: DiscussionListItemDTO | null;
}

export interface ForumUserSuggestionDTO {
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  sharedGameCount: number;
  postCount: number;
  discussionCount: number;
  followerCount: number;
  reason: string;
}

export interface ForumFeedResultDTO {
  items: ForumFeedItemDTO[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  suggestedCommunities: ForumCommunityDTO[];
  suggestedUsers: ForumUserSuggestionDTO[];
}

export interface ForumFeedQuery {
  search?: string;
  categorySlug?: string;
  period?: ForumPopularPeriod;
  interestGameTitles?: string[];
  interestGenres?: string[];
  interestPlayStyles?: string[];
  interestPlatform?: string;
  pageNumber?: number;
  pageSize?: number;
}
