export interface SteamConnectStartDTO {
  authorizationUrl: string;
  expiresAtUtc: string;
}

export interface SteamConnectionStatusDTO {
  isConnected: boolean;
  steamId?: string | null;
  steamName?: string | null;
  avatarUrl?: string | null;
  profileUrl?: string | null;
  linkedAtUtc?: string | null;
  lastSyncedAtUtc?: string | null;
}

export interface SteamConnectionMessage {
  type: "wellplayd:steam-link-result";
  success: boolean;
  steamId?: string | null;
  steamName?: string | null;
  avatarUrl?: string | null;
  profileUrl?: string | null;
  linkedAtUtc?: string | null;
  error?: string | null;
}

export interface SteamSyncResultDTO {
  steamOwnedGameCount: number;
  matchedGameCount: number;
  unmatchedGameCount: number;
  createdUserGameCount: number;
  updatedUserGameCount: number;
  createdAutoLogCount: number;
  updatedAutoLogCount: number;
  updatedReviewPlaytimeCount: number;
  createdPlayLogCount: number;
  updatedPlayLogCount: number;
  syncedAtUtc: string;
}

export interface SteamOwnedGameDTO {
  steamAppId: number;
  name: string;
  playtimeMinutes: number;
  playtimeHours: number;
  playtimeWindowsMinutes: number;
  playtimeMacMinutes: number;
  playtimeLinuxMinutes: number;
  playtimeTwoWeeksMinutes?: number | null;
  lastPlayedAtUtc?: string | null;
  steamIconUrl?: string | null;
  isMatched: boolean;
  localGameId?: number | null;
  localGameName?: string | null;
  localGameSlug?: string | null;
  localCoverImageUrl?: string | null;
  localReleaseDate?: string | null;
  syncedAtUtc: string;
}
