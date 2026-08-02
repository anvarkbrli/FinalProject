export interface UpdateProfileRequestDTO {
  userName: string;
  bio: string | null;
  socialLinks: string[];
}

export interface UserSettingsDTO {
  isPrivate: boolean;
  preferredLanguage: "az" | "en";
  preferredTheme: "light" | "dark";
}

export interface UpdateUserSettingsRequestDTO {
  isPrivate?: boolean;
  preferredLanguage?: "az" | "en";
  preferredTheme?: "light" | "dark";
}

export interface BlockedUserDTO {
  id: string;
  userName: string;
  profileImageUrl: string | null;
  blockedAt: string;
}
export interface ChangeEmailRequestDTO {
  newEmail: string;
  currentPassword: string;
}

export interface EmailOperationResultDTO {
  message: string;
}

export interface ChangePasswordRequestDTO {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}

export interface FollowRequestDTO {
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  requestedAt: string;
}

export interface FollowRequestBulkResultDTO {
  processedCount: number;
}

export interface DeleteAccountRequestDTO {
  currentPassword: string;
  confirmationText: string;
}

export interface DeleteAccountResultDTO {
  deletedImmediately: boolean;
  pendingOwnershipTransfers: boolean;
  pendingCommunityCount: number;
  message: string;
}

export interface ConfirmEmailChangeRequestDTO {
  userId: string;
  newEmail: string;
  token: string;
}
