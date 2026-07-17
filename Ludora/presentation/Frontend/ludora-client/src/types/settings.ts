export interface UpdateProfileRequestDTO {
  userName: string;
  bio: string | null;
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