import { api } from "../lib/api";
import type {
  GameCommunityUserCategory,
  GameCommunityUserDTO,
  GameCommunityUsersFilter,
} from "../types/gameCommunity";
import type {
  PagedResultDTO,
} from "../types/games";
import type {
  CatalogSource,
} from "../types/home";

const defaultFilter: GameCommunityUsersFilter = {
  pageNumber: 1,
  pageSize: 20,
};

export async function getGameCategoryUsers(
  source: CatalogSource,
  gameId: number,
  category: GameCommunityUserCategory,
  filter: GameCommunityUsersFilter =
    defaultFilter,
): Promise<
  PagedResultDTO<GameCommunityUserDTO>
> {
  const response =
    await api.get<
      PagedResultDTO<GameCommunityUserDTO>
    >(
      `/games/${source}/${gameId}/community/categories/${category}/users`,
      {
        params: {
          pageNumber:
            filter.pageNumber,
          pageSize:
            filter.pageSize,
        },
      },
    );

  return response.data;
}

export async function getGameRatingUsers(
  source: CatalogSource,
  gameId: number,
  rating: number,
  filter: GameCommunityUsersFilter =
    defaultFilter,
): Promise<
  PagedResultDTO<GameCommunityUserDTO>
> {
  const response =
    await api.get<
      PagedResultDTO<GameCommunityUserDTO>
    >(
      `/games/${source}/${gameId}/community/ratings/${rating}/users`,
      {
        params: {
          pageNumber:
            filter.pageNumber,
          pageSize:
            filter.pageSize,
        },
      },
    );

  return response.data;
}
