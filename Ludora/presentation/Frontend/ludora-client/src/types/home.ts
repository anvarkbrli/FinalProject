export type CatalogSource = "Local" | "Igdb";

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
  genres: LookupDTO[];
  platforms: LookupDTO[];
}

export interface DiscussionListItemDTO {
  id: number;
  title: string;
  slug: string;

  categoryId: number;
  categoryName: string;
  categorySlug: string;

  authorId: string;
  authorUserName: string;
  authorProfileImageUrl: string | null;

  likeCount: number;
  replyCount: number;
  viewCount: number;

  isLikedByCurrentUser: boolean;
  isPinned: boolean;
  isLocked: boolean;
  isDeleted: boolean;
  canEdit: boolean;
  canDelete: boolean;

  createdAt: string;
  updatedAt: string | null;
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

export interface PagedResultDTO<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}