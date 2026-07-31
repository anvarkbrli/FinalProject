import { api } from "../lib/api";
import type { PagedResultDTO } from "../types/home";
import type { CurrentUserDTO } from "../types/auth";
import type {
  BlockedUserDTO,
  ChangeEmailRequestDTO,
  ConfirmEmailChangeRequestDTO,
  ChangePasswordRequestDTO,
  DeleteAccountRequestDTO,
  DeleteAccountResultDTO,
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

export async function updatePreferredLanguage(
  language: "az" | "en",
): Promise<CurrentUserDTO> {
  const response = await api.put<CurrentUserDTO>(
    "/users/me/language",
    { language },
  );

  return response.data;
}

export async function getBlockedUsers(
  signal?: AbortSignal,
): Promise<BlockedUserDTO[]> {
  const response =
    await api.get<PagedResultDTO<BlockedUserDTO>>(
      "/users/me/settings/blocked-users",
      {
        params: {
          pageNumber: 1,
          pageSize: 100,
        },
        signal,
      },
    );

  return response.data.items;
}

export async function unblockUser(
  userId: string,
): Promise<void> {
  await api.delete(`/users/${userId}/unblock`);
}


export async function changePassword(
  request: ChangePasswordRequestDTO,
): Promise<EmailOperationResultDTO> {
  const response =
    await api.post<EmailOperationResultDTO>(
      "/users/me/password-change",
      request,
    );

  return response.data;
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

export async function getFollowRequests(
  signal?: AbortSignal,
): Promise<FollowRequestDTO[]> {
  const response =
    await api.get<PagedResultDTO<FollowRequestDTO>>(
      "/users/me/follow-requests",
      {
        params: {
          pageNumber: 1,
          pageSize: 100,
        },
        signal,
      },
    );

  return response.data.items;
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

export async function deleteAccount(
  request: DeleteAccountRequestDTO,
): Promise<DeleteAccountResultDTO> {
  const response = await api.delete<DeleteAccountResultDTO>(
    "/users/me",
    { data: request },
  );

  return response.data;
}

export async function confirmEmailChange(
  request: ConfirmEmailChangeRequestDTO,
): Promise<EmailOperationResultDTO> {
  const response = await api.post<EmailOperationResultDTO>(
    "/users/email-change/confirm",
    request,
  );
  return response.data;
}
