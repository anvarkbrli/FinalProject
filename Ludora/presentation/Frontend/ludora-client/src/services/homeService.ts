import { api } from "../lib/api";
import { filterDisplayableGames } from "../lib/gameQuality";
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
  /*
   * Bəzi nəticələr keyfiyyət filtrindən keçməyə
   * bilər. Buna görə 6 yox, 24 nəticə gətiririk
   * və uyğun ilk 6 oyunu saxlayırıq.
   */
  const response = await api.get<
    GameListItemDTO[]
  >("/games", {
    params: {
      sortBy: "rating_desc",
      pageNumber: 1,
      pageSize: 24,
    },
  });

  return filterDisplayableGames(
    response.data,
  ).slice(0, 6);
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