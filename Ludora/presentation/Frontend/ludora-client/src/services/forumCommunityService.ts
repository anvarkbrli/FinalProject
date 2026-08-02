import { api } from "../lib/api";
import type {
  CreateForumCommunityInput,
  ForumCommunityDTO,
  ForumCommunityInviteCandidateDTO,
  ForumCommunityLeaveResultDTO,
  ForumCommunityMemberDTO,
  ForumCommunityMemberRole,
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

export async function leaveForumCommunity(
  id: number,
): Promise<ForumCommunityLeaveResultDTO> {
  const response = await api.delete<ForumCommunityLeaveResultDTO>(
    `/forum-communities/${id}/membership`,
  );
  return response.data;
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

export async function getCommunityMembers(
  communityId: number,
  signal?: AbortSignal,
): Promise<ForumCommunityMemberDTO[]> {
  const response = await api.get<ForumCommunityMemberDTO[]>(
    `/forum-communities/${communityId}/members`,
    { signal },
  );
  return response.data;
}

export async function searchCommunityInviteCandidates(
  communityId: number,
  query: string,
  signal?: AbortSignal,
): Promise<ForumCommunityInviteCandidateDTO[]> {
  const response = await api.get<ForumCommunityInviteCandidateDTO[]>(
    `/forum-communities/${communityId}/invite-candidates`,
    { params: { query }, signal },
  );
  return response.data;
}

export async function removeCommunityMember(
  communityId: number,
  membershipId: number,
): Promise<void> {
  await api.delete(
    `/forum-communities/${communityId}/members/${membershipId}`,
  );
}


export async function updateCommunityMemberRole(
  communityId: number,
  membershipId: number,
  role: ForumCommunityMemberRole,
): Promise<void> {
  await api.put(
    `/forum-communities/${communityId}/members/${membershipId}/role`,
    { role },
  );
}

export async function acceptCommunityOwnershipOffer(
  communityId: number,
): Promise<void> {
  await api.put(
    `/forum-communities/${communityId}/ownership-offer/accept`,
  );
}

export async function declineCommunityOwnershipOffer(
  communityId: number,
): Promise<void> {
  await api.delete(
    `/forum-communities/${communityId}/ownership-offer`,
  );
}

export async function deleteForumCommunity(
  communityId: number,
): Promise<void> {
  await api.delete(`/forum-communities/${communityId}`);
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

export async function getCommunityRules(
  communityId: number,
  signal?: AbortSignal,
) {
  const response = await api.get<import("../types/community").ForumCommunityRuleDTO[]>(
    `/forum-communities/${communityId}/rules`,
    { signal },
  );
  return response.data;
}

export async function replaceCommunityRules(
  communityId: number,
  rules: import("../types/community").ForumCommunityRuleInput[],
) {
  const response = await api.put<import("../types/community").ForumCommunityRuleDTO[]>(
    `/forum-communities/${communityId}/rules`,
    { rules },
  );
  return response.data;
}

export async function getCommunityModerators(
  communityId: number,
  signal?: AbortSignal,
) {
  const response = await api.get<import("../types/community").ForumCommunityMemberSearchDTO[]>(
    `/forum-communities/${communityId}/moderators`,
    { signal },
  );
  return response.data;
}

export async function getCommunityFlairs(
  communityId: number,
  signal?: AbortSignal,
) {
  const response = await api.get<import("../types/community").ForumCommunityFlairDTO[]>(
    `/forum-communities/${communityId}/flairs`,
    { signal },
  );
  return response.data;
}

export async function replaceCommunityFlairs(
  communityId: number,
  flairs: import("../types/community").ForumCommunityFlairInput[],
) {
  const response = await api.put<import("../types/community").ForumCommunityFlairDTO[]>(
    `/forum-communities/${communityId}/flairs`,
    { flairs },
  );
  return response.data;
}

export async function setMyCommunityFlair(
  communityId: number,
  flairId: number | null,
): Promise<void> {
  await api.put(`/forum-communities/${communityId}/my-flair`, { flairId });
}

export async function searchCommunityMembers(
  communityId: number,
  query: string,
  pageNumber: number,
  pageSize: number,
  signal?: AbortSignal,
) {
  const response = await api.get<import("../types/community").CommunityPagedResult<import("../types/community").ForumCommunityMemberSearchDTO>>(
    `/forum-communities/${communityId}/member-search`,
    { params: { query, pageNumber, pageSize }, signal },
  );
  return response.data;
}

export async function getCommunityEvents(
  communityId: number,
  past: boolean,
  pageNumber = 1,
  pageSize = 12,
  signal?: AbortSignal,
) {
  const response = await api.get<import("../types/community").CommunityPagedResult<import("../types/community").ForumCommunityEventDTO>>(
    `/forum-communities/${communityId}/events`,
    { params: { past, pageNumber, pageSize }, signal },
  );
  return response.data;
}

export async function createCommunityEvent(
  communityId: number,
  input: import("../types/community").ForumCommunityEventInput,
) {
  const response = await api.post<import("../types/community").ForumCommunityEventDTO>(
    `/forum-communities/${communityId}/events`,
    input,
  );
  return response.data;
}

export async function updateCommunityEvent(
  communityId: number,
  eventId: number,
  input: import("../types/community").ForumCommunityEventInput,
) {
  const response = await api.put<import("../types/community").ForumCommunityEventDTO>(
    `/forum-communities/${communityId}/events/${eventId}`,
    input,
  );
  return response.data;
}

export async function cancelCommunityEvent(
  communityId: number,
  eventId: number,
): Promise<void> {
  await api.delete(`/forum-communities/${communityId}/events/${eventId}`);
}

export async function setCommunityEventParticipation(
  communityId: number,
  eventId: number,
  joined: boolean,
): Promise<void> {
  const url = `/forum-communities/${communityId}/events/${eventId}/participation`;
  if (joined) await api.put(url);
  else await api.delete(url);
}
