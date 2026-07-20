import { api } from "../lib/api";
import type { CatalogSource } from "../types/home";
import type {
  GameListCreateDTO,
  GameListDetailDTO,
  GameListUpdateDTO,
} from "../types/gameLists";

export async function createGameList(
  request: GameListCreateDTO,
): Promise<number> {
  const response = await api.post<{ id: number }>(
    "/game-lists",
    request,
  );

  return response.data.id;
}

export async function getGameList(
  listId: number,
): Promise<GameListDetailDTO> {
  const response = await api.get<GameListDetailDTO>(
    `/game-lists/${listId}`,
  );

  return response.data;
}

export async function updateGameList(
  listId: number,
  request: GameListUpdateDTO,
): Promise<void> {
  await api.patch(`/game-lists/${listId}`, request);
}

export async function deleteGameList(
  listId: number,
): Promise<void> {
  await api.delete(`/game-lists/${listId}`);
}

export async function addGameToList(
  listId: number,
  source: CatalogSource,
  gameId: number,
): Promise<void> {
  await api.post(
    `/game-lists/${listId}/games/${source}/${gameId}`,
  );
}

export async function removeGameFromList(
  listId: number,
  source: CatalogSource,
  gameId: number,
): Promise<void> {
  await api.delete(
    `/game-lists/${listId}/games/${source}/${gameId}`,
  );
}

export async function updateGameListPosition(
  listId: number,
  source: CatalogSource,
  gameId: number,
  position: number,
): Promise<void> {
  await api.patch(
    `/game-lists/${listId}/games/${source}/${gameId}/position`,
    { position },
  );
}
