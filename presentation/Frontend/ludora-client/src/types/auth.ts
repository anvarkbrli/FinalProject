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
  acceptedTermsAndPrivacy: boolean;
}

export interface UsernameAvailabilityDTO {
  userName: string;
  isAvailable: boolean;
  message: string;
}


export interface ConfirmEmailRequestDTO {
  userId: string;
  token: string;
}

export interface ResendEmailConfirmationRequestDTO {
  email: string;
}

export interface CurrentUserDTO {
  id: string;
  userName: string;
  email: string;
  roles?: string[];
  capabilities?: string[];
  emailConfirmed: boolean;
  bio: string | null;
  profileImageUrl: string | null;
  socialLinks: UserSocialLinkDTO[];
  isPrivate: boolean;
  preferredLanguage: "az" | "en";
  preferredTheme: "light" | "dark";
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
  emailDeliveryPending: boolean;
}

export interface EmailOperationResultDTO {
  message: string;
}

export interface ForgotPasswordRequestDTO {
  email: string;
}

export interface ResetPasswordRequestDTO {
  email: string;
  token: string;
  newPassword: string;
  confirmNewPassword: string;
}

export interface AuthDeviceSessionDTO {
  id: string;
  deviceName: string;
  browser: string;
  operatingSystem: string;
  ipAddress: string | null;
  signedInAtUtc: string;
  lastActiveAtUtc: string;
  expiresAtUtc: string;
  isCurrent: boolean;
}

export interface RevokeAuthDeviceSessionResultDTO {
  currentSessionRevoked: boolean;
  message: string;
}

export interface GoogleAvailabilityDTO {
  enabled: boolean;
}

export interface ExternalAuthExchangeRequestDTO {
  code: string;
}

export interface ExternalAuthExchangeResultDTO {
  requiresProfileCompletion: boolean;
  session: AuthResponseDTO | null;
  registrationToken: string | null;
  suggestedUserName: string | null;
  email: string | null;
  displayName: string | null;
  pictureUrl: string | null;
  returnPath: string;
}

export interface CompleteExternalRegistrationRequestDTO {
  registrationToken: string;
  userName: string;
  acceptedTermsAndPrivacy: boolean;
  useGoogleProfileImage: boolean;
}

export interface ExternalLoginStatusDTO {
  googleEnabled: boolean;
  googleLinked: boolean;
  hasPassword: boolean;
  canUnlinkGoogle: boolean;
  googleEmail: string | null;
}

export interface ExternalLoginOperationResultDTO {
  message: string;
}

export interface GoogleLinkIntentDTO {
  url: string;
}
