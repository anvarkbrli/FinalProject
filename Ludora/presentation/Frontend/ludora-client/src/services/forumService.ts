import { api } from "../lib/api";
import type {
  DiscussionListItemDTO,
  PagedResultDTO,
} from "../types/home";
import type {
  CreateDiscussionInput,
  DiscussionDetailDTO,
  DiscussionRepliesPageDTO,
  DiscussionReplyDTO,
  UpdateDiscussionInput,
} from "../types/forum";

export type ForumFeedSort = "newest" | "popular";

export async function getForumDiscussions(
  sortBy: ForumFeedSort,
  categorySlug?: string,
  signal?: AbortSignal,
  communitySlug?: string,
  search?: string,
  pageNumber = 1,
  pageSize = 20,
): Promise<PagedResultDTO<DiscussionListItemDTO>> {
  const response = await api.get<
    PagedResultDTO<DiscussionListItemDTO>
  >("/discussions", {
    params: {
      sortBy,
      categorySlug: categorySlug || undefined,
      communitySlug: communitySlug || undefined,
      search: search?.trim() || undefined,
      pageNumber,
      pageSize,
    },
    signal,
  });

  return response.data;
}

export async function getDiscussionBySlug(
  slug: string,
  signal?: AbortSignal,
): Promise<DiscussionDetailDTO> {
  const response = await api.get<DiscussionDetailDTO>(
    `/discussions/slug/${encodeURIComponent(slug)}`,
    { signal },
  );
  return response.data;
}

export async function createDiscussion(
  input: CreateDiscussionInput,
): Promise<DiscussionDetailDTO> {
  const formData = new FormData();
  formData.append("title", input.title);
  formData.append("content", input.content);
  formData.append("categoryId", String(input.categoryId));
  if (input.communityId !== null) {
    formData.append("communityId", String(input.communityId));
  }
  input.mediaFiles.forEach((file) => {
    formData.append("mediaFiles", file);
  });

  const response = await api.post<DiscussionDetailDTO>(
    "/discussions",
    formData,
    {
      headers: { "Content-Type": "multipart/form-data" },
    },
  );
  return response.data;
}


export async function updateDiscussion(
  input: UpdateDiscussionInput,
): Promise<DiscussionDetailDTO> {
  const formData = new FormData();
  formData.append("title", input.title);
  formData.append("content", input.content);
  formData.append("categoryId", String(input.categoryId));
  input.keptMediaIds.forEach((mediaId) => {
    formData.append("keptMediaIds", String(mediaId));
  });
  input.mediaFiles.forEach((file) => {
    formData.append("mediaFiles", file);
  });

  const response = await api.put<DiscussionDetailDTO>(
    `/discussions/${input.discussionId}/media`,
    formData,
    {
      headers: { "Content-Type": "multipart/form-data" },
    },
  );
  return response.data;
}

export async function deleteDiscussion(
  discussionId: number,
): Promise<void> {
  await api.delete(`/discussions/${discussionId}`);
}

export async function getDiscussionReplies(
  discussionId: number,
  signal?: AbortSignal,
): Promise<DiscussionRepliesPageDTO> {
  const response = await api.get<DiscussionRepliesPageDTO>(
    `/discussions/${discussionId}/replies`,
    {
      params: { pageNumber: 1, pageSize: 100 },
      signal,
    },
  );
  return response.data;
}

export async function createDiscussionReply(
  discussionId: number,
  content: string,
  parentReplyId?: number,
): Promise<DiscussionReplyDTO> {
  const response = await api.post<DiscussionReplyDTO>(
    `/discussions/${discussionId}/replies`,
    {
      content,
      parentReplyId: parentReplyId ?? null,
    },
  );
  return response.data;
}


export async function updateDiscussionReply(
  replyId: number,
  content: string,
): Promise<DiscussionReplyDTO> {
  const response = await api.put<DiscussionReplyDTO>(
    `/discussion-replies/${replyId}`,
    { content },
  );
  return response.data;
}

export async function voteDiscussionReply(
  replyId: number,
  value: -1 | 0 | 1,
): Promise<DiscussionReplyDTO> {
  const response = await api.put<DiscussionReplyDTO>(
    `/discussion-replies/${replyId}/vote`,
    { value },
  );
  return response.data;
}

export async function deleteDiscussionReply(
  replyId: number,
): Promise<void> {
  await api.delete(`/discussion-replies/${replyId}`);
}

export async function setDiscussionLike(
  discussionId: number,
  isLiked: boolean,
): Promise<void> {
  if (isLiked) {
    await api.post(`/discussions/${discussionId}/like`);
  } else {
    await api.delete(`/discussions/${discussionId}/like`);
  }
}

export async function setDiscussionSaved(
  discussionId: number,
  isSaved: boolean,
): Promise<void> {
  if (isSaved) {
    await api.put(`/discussions/${discussionId}/save`);
  } else {
    await api.delete(`/discussions/${discussionId}/save`);
  }
}

export async function setDiscussionReplyFollowed(
  replyId: number,
  isFollowed: boolean,
): Promise<void> {
  if (isFollowed) {
    await api.put(`/discussion-replies/${replyId}/follow`);
  } else {
    await api.delete(`/discussion-replies/${replyId}/follow`);
  }
}
