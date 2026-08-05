import { api } from "../lib/api";
import type {
  SteamConnectStartDTO,
  SteamConnectionStatusDTO,
  SteamApplyPlaytimeReconciliationDTO,
  SteamApplyPlaytimeResultDTO,
  SteamOwnedGameDTO,
  SteamPlaytimeReconciliationDTO,
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


export async function getSteamPlaytimeReconciliation(): Promise<SteamPlaytimeReconciliationDTO> {
  const response = await api.get<SteamPlaytimeReconciliationDTO>(
    "/steam/playtime-reconciliation",
  );

  return response.data;
}

export async function applySteamPlaytimeReconciliation(
  payload: SteamApplyPlaytimeReconciliationDTO,
): Promise<SteamApplyPlaytimeResultDTO> {
  const response = await api.post<SteamApplyPlaytimeResultDTO>(
    "/steam/playtime-reconciliation/apply",
    payload,
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
