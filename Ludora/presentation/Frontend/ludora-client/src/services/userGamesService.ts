import { api } from "../lib/api";
import type { CatalogSource } from "../types/home";
import type {
  UserGameDTO,
  UserGameStatus,
  WishlistedFriendDTO,
} from "../types/userGames";

export async function getMyUserGames(): Promise<UserGameDTO[]> {
  const response = await api.get<UserGameDTO[]>(
    "/usergames",
  );

  return response.data;
}

function resolveNullableState(
  status: number,
  data: UserGameDTO | null,
): UserGameDTO | null {
  return status === 204 ? null : data;
}

export async function getWishlistedFriends(
  source: CatalogSource,
  gameId: number,
): Promise<WishlistedFriendDTO[]> {
  const response = await api.get<WishlistedFriendDTO[]>(
    `/usergames/${source}/${gameId}/wishlisted-friends`,
  );

  return response.data;
}

export async function getUserGameState(
  source: CatalogSource,
  gameId: number,
): Promise<UserGameDTO | null> {
  const response = await api.get<UserGameDTO | null>(
    `/usergames/${source}/${gameId}`,
  );

  return response.data;
}

export async function setUserGameStatus(
  source: CatalogSource,
  gameId: number,
  status: UserGameStatus,
): Promise<UserGameDTO> {
  const response = await api.put<UserGameDTO>(
    `/usergames/${source}/${gameId}/status`,
    { status },
  );

  return response.data;
}

export async function removeUserGameStatus(
  source: CatalogSource,
  gameId: number,
): Promise<UserGameDTO | null> {
  const response = await api.delete<UserGameDTO | null>(
    `/usergames/${source}/${gameId}/status`,
  );

  return resolveNullableState(
    response.status,
    response.data,
  );
}

export async function setUserGameFavorite(
  source: CatalogSource,
  gameId: number,
  isFavorite: boolean,
): Promise<UserGameDTO | null> {
  const response = await api.put<UserGameDTO | null>(
    `/usergames/${source}/${gameId}/favorite`,
    { isFavorite },
  );

  return resolveNullableState(
    response.status,
    response.data,
  );
}

export async function setUserGameWishlist(
  source: CatalogSource,
  gameId: number,
  isWishlisted: boolean,
): Promise<UserGameDTO | null> {
  const response = await api.put<UserGameDTO | null>(
    `/usergames/${source}/${gameId}/wishlist`,
    { isWishlisted },
  );

  return resolveNullableState(
    response.status,
    response.data,
  );
}

export async function setUserGameRating(
  source: CatalogSource,
  gameId: number,
  rating: number | null,
): Promise<UserGameDTO | null> {
  const response = await api.put<UserGameDTO | null>(
    `/usergames/${source}/${gameId}/rating`,
    { rating },
  );

  return resolveNullableState(
    response.status,
    response.data,
  );
}
