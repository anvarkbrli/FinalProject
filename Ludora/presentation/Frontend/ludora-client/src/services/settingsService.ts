import { api } from "../lib/api";
import type { CurrentUserDTO } from "../types/auth";
import type {
  BlockedUserDTO,
  ChangeEmailRequestDTO,
  EmailOperationResultDTO,
  FollowRequestDTO,
  UpdateProfileRequestDTO,
  UpdateUserSettingsRequestDTO,
  UserSettingsDTO,
} from "../types/settings";

export async function getCurrentUser(): Promise<CurrentUserDTO> {
  const response =
    await api.get<CurrentUserDTO>("/users/me");

  return response.data;
}

export async function updateProfile(
  request: UpdateProfileRequestDTO,
): Promise<CurrentUserDTO> {
  const response =
    await api.put<CurrentUserDTO>(
      "/users/me",
      request,
    );

  return response.data;
}

export async function updateProfileImage(
  image: File,
): Promise<CurrentUserDTO> {
  const formData = new FormData();

  formData.append("image", image);

  const response =
    await api.put<CurrentUserDTO>(
      "/users/me/profile-image",
      formData,
      {
        headers: {
          "Content-Type": "multipart/form-data",
        },
      },
    );

  return response.data;
}

export async function getUserSettings(): Promise<UserSettingsDTO> {
  const response =
    await api.get<UserSettingsDTO>(
      "/users/me/settings",
    );

  return response.data;
}

export async function updateUserSettings(
  request: UpdateUserSettingsRequestDTO,
): Promise<UserSettingsDTO> {
  const response =
    await api.patch<UserSettingsDTO>(
      "/users/me/settings",
      request,
    );

  return response.data;
}

export async function getBlockedUsers(): Promise<BlockedUserDTO[]> {
  const response =
    await api.get<BlockedUserDTO[]>(
      "/users/me/settings/blocked-users",
    );

  return response.data;
}

export async function unblockUser(
  userId: string,
): Promise<void> {
  await api.delete(`/users/${userId}/unblock`);
}

export async function requestEmailChange(
  request: ChangeEmailRequestDTO,
): Promise<EmailOperationResultDTO> {
  const response =
    await api.post<EmailOperationResultDTO>(
      "/users/me/email-change",
      request,
    );

  return response.data;
}

export async function getFollowRequests(): Promise<FollowRequestDTO[]> {
  const response =
    await api.get<FollowRequestDTO[]>(
      "/users/me/follow-requests",
    );

  return response.data;
}

export async function acceptFollowRequest(
  requesterId: string,
): Promise<void> {
  await api.post(
    `/users/me/follow-requests/${requesterId}/accept-follow`,
  );
}

export async function rejectFollowRequest(
  requesterId: string,
): Promise<void> {
  await api.delete(
    `/users/me/follow-requests/${requesterId}/reject-follow`,
  );
}
