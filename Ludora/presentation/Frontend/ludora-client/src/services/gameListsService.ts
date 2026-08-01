import { api } from "../lib/api";
import type { CatalogSource } from "../types/home";
import type {
  GameListCloneDTO,
  GameListCreateDTO,
  GameListDetailDTO,
  GameListReorderItemDTO,
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


export async function reorderGameList(
  listId: number,
  items: GameListReorderItemDTO[],
): Promise<void> {
  await api.put(`/game-lists/${listId}/games/order`, { items });
}

export async function cloneGameList(
  listId: number,
  request: GameListCloneDTO,
): Promise<number> {
  const response = await api.post<{ id: number }>(
    `/game-lists/${listId}/clone`,
    request,
  );
  return response.data.id;
}

export async function inviteGameListCollaborator(
  listId: number,
  userName: string,
): Promise<void> {
  await api.post(`/game-lists/${listId}/collaborators`, { userName });
}

export async function acceptGameListCollaboration(
  listId: number,
): Promise<void> {
  await api.post(`/game-lists/${listId}/collaboration/accept`);
}

export async function declineGameListCollaboration(
  listId: number,
): Promise<void> {
  await api.delete(`/game-lists/${listId}/collaboration/decline`);
}

export async function removeGameListCollaborator(
  listId: number,
  userId: string,
): Promise<void> {
  await api.delete(`/game-lists/${listId}/collaborators/${userId}`);
}
