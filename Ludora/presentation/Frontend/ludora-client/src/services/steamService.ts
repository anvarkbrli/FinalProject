import { api } from "../lib/api";
import type {
  SteamConnectStartDTO,
  SteamConnectionStatusDTO,
  SteamOwnedGameDTO,
  SteamSyncResultDTO,
} from "../types/steam";

export async function startSteamConnection(): Promise<SteamConnectStartDTO> {
  const response = await api.get<SteamConnectStartDTO>(
    "/settings/export/steam/connect",
  );

  return response.data;
}

export async function getSteamConnectionStatus(): Promise<SteamConnectionStatusDTO> {
  const response = await api.get<SteamConnectionStatusDTO>(
    "/settings/export/steam/status",
  );

  return response.data;
}

export async function disconnectSteam(): Promise<void> {
  await api.delete("/settings/export/steam");
}

export async function syncSteamOwnedGames(): Promise<SteamSyncResultDTO> {
  const response = await api.post<SteamSyncResultDTO>(
    "/steam/sync/owned-games",
    undefined,
    { timeout: 60_000 },
  );

  return response.data;
}

export async function getSteamOwnedGames(
  userId: string,
  isOwnProfile: boolean,
  refresh = false,
): Promise<SteamOwnedGameDTO[]> {
  const endpoint = isOwnProfile
    ? "/steam/owned-games"
    : `/steam/users/${userId}/owned-games`;

  const response = await api.get<SteamOwnedGameDTO[]>(endpoint, {
    params: isOwnProfile ? { refresh } : undefined,
    timeout: refresh ? 60_000 : 20_000,
  });

  return response.data;
}
