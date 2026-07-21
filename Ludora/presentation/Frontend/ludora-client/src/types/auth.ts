import type { UserProfileStatisticsDTO, UserSocialLinkDTO } from "./profile";

export interface LoginRequestDTO {
  emailOrUserName: string;
  password: string;
}

export interface RegisterRequestDTO {
  userName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export interface ResendEmailConfirmationRequestDTO {
  email: string;
}

export interface CurrentUserDTO {
  id: string;
  userName: string;
  email: string;
  emailConfirmed: boolean;
  bio: string | null;
  profileImageUrl: string | null;
  socialLinks: UserSocialLinkDTO[];
  isPrivate: boolean;
  createdAt: string;
  followerCount: number;
  followingCount: number;
  postCount: number;
  statistics: UserProfileStatisticsDTO;
}

export interface AuthResponseDTO {
  accessToken: string;
  expiresAtUtc: string;
  user: CurrentUserDTO;
}

export interface RegisterResultDTO {
  message: string;
  emailVerificationRequired: boolean;
}

export interface EmailOperationResultDTO {
  message: string;
}
