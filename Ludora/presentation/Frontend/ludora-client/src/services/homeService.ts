import { api } from "../lib/api";
import type {
  AnnouncementDTO,
  DiscussionListItemDTO,
  ForumCategoryDTO,
  GameListItemDTO,
  PagedResultDTO,
} from "../types/home";

export async function getFeaturedGames(): Promise<
  GameListItemDTO[]
> {
  const response = await api.get<GameListItemDTO[]>("/games", {
    params: {
      sortBy: "rating_desc",
      pageNumber: 1,
      pageSize: 6,
    },
  });

  return response.data;
}

export async function getTrendingDiscussions(): Promise<
  PagedResultDTO<DiscussionListItemDTO>
> {
  const response = await api.get<
    PagedResultDTO<DiscussionListItemDTO>
  >("/discussions", {
    params: {
      sortBy: "popular",
      pageNumber: 1,
      pageSize: 5,
    },
  });

  return response.data;
}

export async function getForumCategories(): Promise<
  ForumCategoryDTO[]
> {
  const response = await api.get<ForumCategoryDTO[]>(
    "/forum/categories",
  );

  return [...response.data]
    .sort(
      (firstCategory, secondCategory) =>
        secondCategory.discussionCount -
        firstCategory.discussionCount,
    )
    .slice(0, 6);
}

export async function getLatestAnnouncement(): Promise<
  PagedResultDTO<AnnouncementDTO>
> {
  const response = await api.get<
    PagedResultDTO<AnnouncementDTO>
  >("/announcements", {
    params: {
      pageNumber: 1,
      pageSize: 1,
    },
  });

  return response.data;
}