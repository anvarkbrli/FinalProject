import type {
  DiscussionMediaDTO,
  PagedResultDTO,
} from "./home";

export interface DiscussionDetailDTO {
  id: number;
  title: string;
  slug: string;
  content: string;
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

export interface DiscussionReplyDTO {
  id: number;
  discussionId: number;
  parentReplyId: number | null;
  authorId: string;
  authorUserName: string;
  authorProfileImageUrl: string | null;
  authorCommunityRole: "Owner" | "Moderator" | "Member" | null;
  content: string;
  netScore: number;
  currentUserVote: -1 | 0 | 1;
  isFollowedByCurrentUser: boolean;
  childCount: number;
  children: DiscussionReplyDTO[];
  isDeleted: boolean;
  canEdit: boolean;
  canDelete: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export type DiscussionRepliesPageDTO =
  PagedResultDTO<DiscussionReplyDTO>;

export interface CreateDiscussionInput {
  title: string;
  content: string;
  categoryId: number;
  communityId: number | null;
  mediaFiles: File[];
}

export interface UpdateDiscussionInput {
  discussionId: number;
  title: string;
  content: string;
  categoryId: number;
  keptMediaIds: number[];
  mediaFiles: File[];
}
