import { api } from "../lib/api";
import { filterDisplayableGames } from "../lib/gameQuality";
import type {
  AnnouncementDTO,
  DiscussionListItemDTO,
  ForumCategoryDTO,
  GameListItemDTO,
  HomeDiscoveryDTO,
  PagedResultDTO,
} from "../types/home";


export async function getHomeDiscovery(): Promise<
  HomeDiscoveryDTO
> {
  const response = await api.get<HomeDiscoveryDTO>(
    "/home/discovery",
  );

  return response.data;
}

export async function getFeaturedGames(): Promise<
  GameListItemDTO[]
> {
 
  const response = await api.get<
    PagedResultDTO<GameListItemDTO>
  >("/games", {
    params: {
      sortBy: "rating_desc",
      includeRelatedContent: false,
      pageNumber: 1,
      pageSize: 24,
    },
  });

  return filterDisplayableGames(
    response.data.items,
  ).slice(0, 6);
}

export async function getTrendingDiscussions(): Promise<
  PagedResultDTO<DiscussionListItemDTO>
> {
  const response = await api.get<
    PagedResultDTO<DiscussionListItemDTO>
  >("/discussions", {
    params: {
      sortBy: "trending",
      pageNumber: 1,
      pageSize: 20,
    },
  });

  return response.data;
}

export async function getForumCategories(): Promise<
  ForumCategoryDTO[]
> {
  const response = await api.get<
    ForumCategoryDTO[]
  >("/forum/categories");

  return [...response.data]
    .sort(
      (
        firstCategory,
        secondCategory,
      ) =>
        secondCategory.discussionCount -
        firstCategory.discussionCount,
    )
    .slice(0, 6);
}

export async function getHomeAnnouncements(
  signal?: AbortSignal,
): Promise<
  PagedResultDTO<AnnouncementDTO>
> {
  const response = await api.get<
    PagedResultDTO<AnnouncementDTO>
  >("/announcements", {
    signal,
    params: {
      pageNumber: 1,
      pageSize: 5,
    },
  });

  return response.data;
}
