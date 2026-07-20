import { api } from "../lib/api";
import type {
  InteractionCommentDTO,
  InteractionCommentsResultDTO,
  InteractionContentType,
  InteractionLikesResultDTO,
} from "../types/interactions";

function getBasePath(
  contentType: InteractionContentType,
): string {
  return contentType === "post"
    ? "/posts"
    : "/game-lists";
}

export async function likeContent(
  contentType: InteractionContentType,
  contentId: number,
): Promise<void> {
  await api.post(
    `${getBasePath(contentType)}/${contentId}/likes`,
  );
}

export async function unlikeContent(
  contentType: InteractionContentType,
  contentId: number,
): Promise<void> {
  await api.delete(
    `${getBasePath(contentType)}/${contentId}/likes`,
  );
}

export async function getContentLikes(
  contentType: InteractionContentType,
  contentId: number,
  pageNumber = 1,
  pageSize = 100,
): Promise<InteractionLikesResultDTO> {
  const response =
    await api.get<InteractionLikesResultDTO>(
      `${getBasePath(contentType)}/${contentId}/likes`,
      {
        params: {
          pageNumber,
          pageSize,
        },
      },
    );

  return response.data;
}

export async function getContentComments(
  contentType: InteractionContentType,
  contentId: number,
  pageNumber: number,
  pageSize = 8,
): Promise<InteractionCommentsResultDTO> {
  const response =
    await api.get<InteractionCommentsResultDTO>(
      `${getBasePath(contentType)}/${contentId}/comments`,
      {
        params: {
          pageNumber,
          pageSize,
        },
      },
    );

  return response.data;
}

export async function createContentComment(
  contentType: InteractionContentType,
  contentId: number,
  content: string,
): Promise<InteractionCommentDTO> {
  const response = await api.post<InteractionCommentDTO>(
    `${getBasePath(contentType)}/${contentId}/comments`,
    { content },
  );

  return response.data;
}

export async function updateContentComment(
  contentType: InteractionContentType,
  commentId: number,
  content: string,
): Promise<InteractionCommentDTO> {
  const response = await api.put<InteractionCommentDTO>(
    `${getBasePath(contentType)}/comments/${commentId}`,
    { content },
  );

  return response.data;
}

export async function deleteContentComment(
  contentType: InteractionContentType,
  commentId: number,
): Promise<void> {
  await api.delete(
    `${getBasePath(contentType)}/comments/${commentId}`,
  );
}
