export interface UpdateProfileRequestDTO {
  userName: string;
  bio: string | null;
  socialLinks: string[];
}

export interface UserSettingsDTO {
  isPrivate: boolean;
}

export interface UpdateUserSettingsRequestDTO {
  isPrivate: boolean;
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
