import { api } from "../lib/api";
import type {
  ForumInterestProfileDTO,
  UpdateForumInterestProfileDTO,
} from "../types/forumInterests";

export async function getForumInterests(
  signal?: AbortSignal,
): Promise<ForumInterestProfileDTO | null> {
  const response = await api.get<ForumInterestProfileDTO | null>(
    "/users/me/forum-interests",
    { signal },
  );

  const data = response.data as ForumInterestProfileDTO | null | "";
  return response.status === 204 || data === "" ? null : data;
}

export async function updateForumInterests(
  request: UpdateForumInterestProfileDTO,
): Promise<ForumInterestProfileDTO> {
  const response = await api.put<ForumInterestProfileDTO>(
    "/users/me/forum-interests",
    request,
  );

  return response.data;
}
