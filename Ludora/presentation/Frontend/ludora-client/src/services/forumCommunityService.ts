import { api } from "../lib/api";
import type {
  CreateForumCommunityInput,
  ForumCommunityDTO,
  ForumCommunityMemberDTO,
} from "../types/community";

export async function getForumCommunities(
  signal?: AbortSignal,
): Promise<ForumCommunityDTO[]> {
  const response = await api.get<ForumCommunityDTO[]>(
    "/forum-communities",
    { signal },
  );
  return response.data;
}

export async function getMyForumCommunities(
  signal?: AbortSignal,
): Promise<ForumCommunityDTO[]> {
  const response = await api.get<ForumCommunityDTO[]>(
    "/forum-communities/mine",
    { signal },
  );
  return response.data;
}

export async function getForumCommunityBySlug(
  slug: string,
  signal?: AbortSignal,
): Promise<ForumCommunityDTO> {
  const response = await api.get<ForumCommunityDTO>(
    `/forum-communities/${encodeURIComponent(slug)}`,
    { signal },
  );
  return response.data;
}

export async function createForumCommunity(
  input: CreateForumCommunityInput,
): Promise<ForumCommunityDTO> {
  const formData = new FormData();
  formData.append("name", input.name);
  formData.append("description", input.description);
  formData.append("category", input.category);
  formData.append("visibility", input.visibility);
  formData.append("hasIcon", String(input.icon !== null));
  formData.append(
    "hasCoverImage",
    String(input.coverImage !== null),
  );
  if (input.icon) formData.append("imageFiles", input.icon);
  if (input.coverImage) {
    formData.append("imageFiles", input.coverImage);
  }

  const response = await api.post<ForumCommunityDTO>(
    "/forum-communities",
    formData,
    { headers: { "Content-Type": "multipart/form-data" } },
  );
  return response.data;
}

export async function joinForumCommunity(id: number): Promise<void> {
  await api.put(`/forum-communities/${id}/join`);
}

export async function leaveForumCommunity(id: number): Promise<void> {
  await api.delete(`/forum-communities/${id}/membership`);
}

export async function setForumCommunityFollowed(
  id: number,
  isFollowed: boolean,
): Promise<void> {
  if (isFollowed) {
    await api.put(`/forum-communities/${id}/follow`);
  } else {
    await api.delete(`/forum-communities/${id}/follow`);
  }
}

export async function getCommunityMembershipRequests(
  id: number,
  signal?: AbortSignal,
): Promise<ForumCommunityMemberDTO[]> {
  const response = await api.get<ForumCommunityMemberDTO[]>(
    `/forum-communities/${id}/membership-requests`,
    { signal },
  );
  return response.data;
}

export async function approveCommunityMembership(
  communityId: number,
  membershipId: number,
): Promise<void> {
  await api.put(
    `/forum-communities/${communityId}/membership-requests/${membershipId}/approve`,
  );
}

export async function rejectCommunityMembership(
  communityId: number,
  membershipId: number,
): Promise<void> {
  await api.delete(
    `/forum-communities/${communityId}/membership-requests/${membershipId}`,
  );
}

export async function inviteToCommunity(
  communityId: number,
  userName: string,
): Promise<void> {
  await api.post(`/forum-communities/${communityId}/invitations`, {
    userName,
  });
}

export async function acceptCommunityInvite(id: number): Promise<void> {
  await api.put(`/forum-communities/${id}/invitations/accept`);
}
