import { api } from "../lib/api";
import type { CatalogSource } from "../types/home";
import type {
  ReviewCommentDTO,
  ReviewCommentsResultDTO,
  ReviewDTO,
  ReviewLikesResultDTO,
  ReviewSort,
  ReviewsResultDTO,
  ReviewWriteDTO,
} from "../types/reviews";

export async function getGameReviews(
  source: CatalogSource,
  gameId: number,
  sortBy: ReviewSort,
  pageNumber: number,
  pageSize = 8,
): Promise<ReviewsResultDTO> {
  const response =
    await api.get<ReviewsResultDTO>(
      `/reviews/game/${source}/${gameId}`,
      {
        params: {
          sortBy,
          pageNumber,
          pageSize,
        },
      },
    );

  return response.data;
}

export async function getMyGameReview(
  source: CatalogSource,
  gameId: number,
): Promise<ReviewDTO | null> {
  const response = await api.get<ReviewDTO | null>(
    `/reviews/mine/${source}/${gameId}`,
  );

  return response.status === 204
    ? null
    : response.data;
}

export async function createReview(
  source: CatalogSource,
  gameId: number,
  request: ReviewWriteDTO,
): Promise<ReviewDTO> {
  const response = await api.post<ReviewDTO>(
    `/reviews/${source}/${gameId}`,
    request,
  );

  return response.data;
}

export async function updateReview(
  reviewId: number,
  request: ReviewWriteDTO,
): Promise<ReviewDTO> {
  const response = await api.put<ReviewDTO>(
    `/reviews/${reviewId}`,
    request,
  );

  return response.data;
}

export async function likeReview(
  reviewId: number,
): Promise<void> {
  await api.post(`/reviews/${reviewId}/likes`);
}

export async function unlikeReview(
  reviewId: number,
): Promise<void> {
  await api.delete(`/reviews/${reviewId}/likes`);
}


export async function getReviewLikes(
  reviewId: number,
  pageNumber = 1,
  pageSize = 50,
): Promise<ReviewLikesResultDTO> {
  const response = await api.get<ReviewLikesResultDTO>(
    `/reviews/${reviewId}/likes`,
    { params: { pageNumber, pageSize } },
  );

  return response.data;
}

export async function getReviewComments(
  reviewId: number,
  pageNumber: number,
  pageSize = 10,
): Promise<ReviewCommentsResultDTO> {
  const response = await api.get<ReviewCommentsResultDTO>(
    `/reviews/${reviewId}/comments`,
    {
      params: {
        pageNumber,
        pageSize,
      },
    },
  );

  return response.data;
}

export async function createReviewComment(
  reviewId: number,
  content: string,
): Promise<ReviewCommentDTO> {
  const response = await api.post<ReviewCommentDTO>(
    `/reviews/${reviewId}/comments`,
    { content },
  );

  return response.data;
}

export async function updateReviewComment(
  commentId: number,
  content: string,
): Promise<ReviewCommentDTO> {
  const response = await api.put<ReviewCommentDTO>(
    `/reviews/comments/${commentId}`,
    { content },
  );

  return response.data;
}

export async function deleteReviewComment(
  commentId: number,
): Promise<void> {
  await api.delete(`/reviews/comments/${commentId}`);
}
