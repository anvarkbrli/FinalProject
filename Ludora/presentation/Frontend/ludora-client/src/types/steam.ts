import type { CatalogSource } from "./home";

export type SteamGameMatchMethod =
  | "None"
  | "SteamAppId"
  | "IgdbExternalId"
  | "ExactName"
  | "NormalizedEditionName"
  | "SteamFallback";

export type SteamPlaytimeResolution =
  | "Unresolved"
  | "SteamOnly"
  | "PendingUserDecision"
  | "SteamIncludesManual"
  | "SeparateFromSteam"
  | "ConservativeMaximum"
  | "SeparatePlatformAutomatic";

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
  pendingResolutionCount: number;
  readyToApplyCount: number;
  autoUpdatedResolvedLogCount: number;
  requiresConfirmation: boolean;
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
  matchedGameSource?: CatalogSource | null;
  matchedGameId?: number | null;
  matchedGameName?: string | null;
  matchedCoverImageUrl?: string | null;
  matchedReleaseDate?: string | null;
  matchMethod: SteamGameMatchMethod;
  matchConfidence: number;
  isSteamFallback: boolean;
  playtimeResolution: SteamPlaytimeResolution;
  resolvedPlaytimeMinutes?: number | null;
  needsResolution: boolean;
  detailUrl?: string | null;
  localGameId?: number | null;
  localGameName?: string | null;
  localGameSlug?: string | null;
  localCoverImageUrl?: string | null;
  localReleaseDate?: string | null;
  syncedAtUtc: string;
}

export interface SteamPlaytimeReconciliationItemDTO {
  steamAppId: number;
  steamName: string;
  steamIconUrl?: string | null;
  steamPlaytimeMinutes: number;
  matchedGameSource: CatalogSource;
  matchedGameId: number;
  matchedGameName: string;
  matchedCoverImageUrl?: string | null;
  matchMethod: SteamGameMatchMethod;
  manualPlaytimeMinutes: number;
  manualPlatformName?: string | null;
  currentResolution: SteamPlaytimeResolution;
  suggestedResolution: SteamPlaytimeResolution;
  needsUserDecision: boolean;
  previewResolvedPlaytimeMinutes: number;
}

export interface SteamPlaytimeReconciliationDTO {
  totalPlayedGames: number;
  pendingCount: number;
  readyToApplyCount: number;
  alreadyConfirmedCount: number;
  items: SteamPlaytimeReconciliationItemDTO[];
}

export interface SteamPlaytimeDecisionDTO {
  steamAppId: number;
  resolution: SteamPlaytimeResolution;
}

export interface SteamApplyPlaytimeReconciliationDTO {
  defaultPendingResolution: SteamPlaytimeResolution;
  decisions: SteamPlaytimeDecisionDTO[];
}

export interface SteamApplyPlaytimeResultDTO {
  appliedGameCount: number;
  createdAutoLogCount: number;
  updatedAutoLogCount: number;
  assignedPcPlatformCount: number;
  appliedAtUtc: string;
}
