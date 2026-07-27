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

  likeCount: number;
  replyCount: number;
  viewCount: number;

  isLikedByCurrentUser: boolean;
  isSavedByCurrentUser: boolean;
  isPinned: boolean;
  isLocked: boolean;
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

export interface AnnouncementDTO {
  id: number;
  title: string;
  content: string;
  createdByUserId: string;
  createdByUserName: string;
  isPublished: boolean;
  publishedAtUtc: string | null;
  isDeleted: boolean;
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

export interface PagedResultDTO<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
