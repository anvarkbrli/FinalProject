import { api } from "../lib/api";
import type {
  FollowStatusDTO,
  GameListSummaryDTO,
  PostCreateDTO,
  PostDTO,
  PostUpdateDTO,
  UserActivitiesResultDTO,
  UserActivityFeedCategory,
  UserActivityFeedScope,
  UserListItemDTO,
  UserPlaytimePeriod,
  UserPlaytimeStatisticsDTO,
  UserProfileDTO,
  UserProfileGameSortBy,
  UserProfileGamesResultDTO,
  UserReviewsResultDTO,
} from "../types/profile";
import type { UserGameDTO } from "../types/userGames";
import type { PagedResultDTO } from "../types/home";
import type { ReviewSort } from "../types/reviews";

export async function getUserProfile(
  userName: string,
): Promise<UserProfileDTO> {
  const response = await api.get<UserProfileDTO>(
    `/users/${encodeURIComponent(userName)}`,
  );

  return response.data;
}

export async function getFollowStatus(
  userId: string,
): Promise<FollowStatusDTO> {
  const response = await api.get<FollowStatusDTO>(
    `/users/${userId}/follow-status`,
  );

  return response.data;
}

export async function followUser(
  userId: string,
): Promise<void> {
  await api.post(`/users/${userId}/follow`);
}

export async function unfollowUser(
  userId: string,
): Promise<void> {
  await api.delete(`/users/${userId}/unfollow`);
}

export async function blockUser(
  userId: string,
): Promise<void> {
  await api.post(`/users/${userId}/block`);
}

export async function removeFollower(
  followerId: string,
): Promise<void> {
  await api.delete(
    `/users/me/followers/${followerId}/remove-follower`,
  );
}

export async function getFollowers(
  userId: string,
): Promise<UserListItemDTO[]> {
  const response = await api.get<UserListItemDTO[]>(
    `/users/${userId}/followers`,
  );

  return response.data;
}

export async function getFollowing(
  userId: string,
): Promise<UserListItemDTO[]> {
  const response = await api.get<UserListItemDTO[]>(
    `/users/${userId}/following`,
  );

  return response.data;
}

export async function getUserPosts(
  userId: string,
): Promise<PostDTO[]> {
  const response = await api.get<PostDTO[]>(
    `/posts/user/${userId}`,
  );

  return response.data;
}

export async function createPost(
  request: PostCreateDTO,
): Promise<PostDTO> {
  const formData = new FormData();
  formData.append("content", request.content);

  if (request.image) {
    formData.append("image", request.image);
  }

  const response = await api.post<PostDTO>(
    "/posts",
    formData,
    {
      headers: {
        "Content-Type": "multipart/form-data",
      },
    },
  );

  return response.data;
}

export async function updatePost(
  postId: number,
  request: PostUpdateDTO,
): Promise<PostDTO> {
  const formData = new FormData();
  formData.append("content", request.content);
  formData.append(
    "removeImage",
    String(request.removeImage),
  );

  if (request.image) {
    formData.append("image", request.image);
  }

  const response = await api.put<PostDTO>(
    `/posts/${postId}`,
    formData,
    {
      headers: {
        "Content-Type": "multipart/form-data",
      },
    },
  );

  return response.data;
}

export async function deletePost(
  postId: number,
): Promise<void> {
  await api.delete(`/posts/${postId}`);
}

export async function getUserGameLists(
  userId: string,
): Promise<GameListSummaryDTO[]> {
  const response = await api.get<GameListSummaryDTO[]>(
    `/game-lists/user/${userId}`,
  );

  return response.data;
}

export async function getUserReviews(
  userId: string,
  filter: {
    sortBy?: ReviewSort;
    rating?: number | null;
    pageNumber?: number;
    pageSize?: number;
  } = {},
): Promise<UserReviewsResultDTO> {
  const response =
    await api.get<UserReviewsResultDTO>(
      `/reviews/user/${userId}`,
      {
        params: {
          sortBy: filter.sortBy ?? "Newest",
          rating: filter.rating ?? undefined,
          pageNumber: filter.pageNumber ?? 1,
          pageSize: filter.pageSize ?? 20,
        },
      },
    );

  return response.data;
}

export async function getUserGames(
  userId: string,
  filter: {
    isFavorite?: boolean;
    isWishlisted?: boolean;
    pageNumber: number;
    pageSize: number;
  },
): Promise<PagedResultDTO<UserGameDTO>> {
  const response = await api.get<
    PagedResultDTO<UserGameDTO>
  >(`/usergames/user/${userId}`, {
    params: filter,
  });

  return response.data;
}


export async function getUserPlaytimeStatistics(
  userId: string,
  period: UserPlaytimePeriod,
): Promise<UserPlaytimeStatisticsDTO> {
  const response = await api.get<UserPlaytimeStatisticsDTO>(
    `/usergames/user/${userId}/playtime-statistics`,
    {
      params: { period },
    },
  );

  return response.data;
}

export async function getUserProfileGames(
  userId: string,
  filter: {
    search?: string;
    genre?: string;
    company?: string;
    platform?: string;
    playedPlatform?: string;
    rating?: number | null;
    status?: "Playing" | "PlanToPlay" | "Played" | "Dropped" | null;
    releaseYear?: number | null;
    sortBy?: UserProfileGameSortBy;
    pageNumber?: number;
    pageSize?: number;
  } = {},
): Promise<UserProfileGamesResultDTO> {
  const response = await api.get<UserProfileGamesResultDTO>(
    `/usergames/user/${userId}/profile-collection`,
    {
      params: {
        search: filter.search || undefined,
        genre: filter.genre || undefined,
        company: filter.company || undefined,
        platform: filter.platform || undefined,
        playedPlatform: filter.playedPlatform || undefined,
        rating: filter.rating ?? undefined,
        status: filter.status ?? undefined,
        releaseYear: filter.releaseYear ?? undefined,
        sortBy: filter.sortBy ?? "RecentlyUpdated",
        pageNumber: filter.pageNumber ?? 1,
        pageSize: filter.pageSize ?? 20,
      },
    },
  );

  return response.data;
}

export async function getMyFeed(
  category: UserActivityFeedCategory,
  scope: UserActivityFeedScope,
  pageNumber: number,
  pageSize = 20,
): Promise<UserActivitiesResultDTO> {
  const response = await api.get<UserActivitiesResultDTO>(
    "/users/me/feed",
    {
      params: {
        category,
        scope,
        pageNumber,
        pageSize,
      },
    },
  );

  return response.data;
}
