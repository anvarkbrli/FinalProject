export type ForumCommunityCategory =
  | "GameNews"
  | "BugsAndSolutions"
  | "Speedrunning"
  | "HardwareAndGamingSetup"
  | "IndieGames"
  | "ModdingAndGameDevelopment"
  | "Esports"
  | "Other";

export type ForumCommunityVisibility =
  | "Open"
  | "Restricted"
  | "Private";

export type ForumCommunityMembershipStatus =
  | "Pending"
  | "Active"
  | "Invited";

export type ForumCommunityMemberRole =
  | "Owner"
  | "Moderator"
  | "Member";

export interface ForumCommunityDTO {
  id: number;
  name: string;
  slug: string;
  description: string;
  iconUrl: string | null;
  coverImageUrl: string | null;
  category: ForumCommunityCategory;
  visibility: ForumCommunityVisibility;
  ownerId: string;
  ownerUserName: string;
  memberCount: number;
  followerCount: number;
  discussionCount: number;
  currentUserMembershipStatus: ForumCommunityMembershipStatus | null;
  currentUserRole: ForumCommunityMemberRole | null;
  isFollowedByCurrentUser: boolean;
  canManage: boolean;
  canModerate: boolean;
  hasOwnershipOfferForCurrentUser: boolean;
  hasPendingOwnershipTransfer: boolean;
  pendingOwnerCandidateUserName: string | null;
  createdAt: string;
}

export interface ForumCommunityMemberDTO {
  membershipId: number;
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  role: ForumCommunityMemberRole;
  status: ForumCommunityMembershipStatus;
  createdAt: string;
}

export interface ForumCommunityInviteCandidateDTO {
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  membershipStatus: ForumCommunityMembershipStatus | null;
}

export interface ForumCommunityLeaveResultDTO {
  leftCommunity: boolean;
  ownershipOfferCreated: boolean;
  candidateUserName: string | null;
}

export interface CreateForumCommunityInput {
  name: string;
  description: string;
  category: ForumCommunityCategory;
  visibility: ForumCommunityVisibility;
  icon: File | null;
  coverImage: File | null;
}

export interface ForumCommunityRuleDTO {
  id: number;
  title: string;
  description: string;
  sortOrder: number;
}

export interface ForumCommunityRuleInput {
  id?: number;
  title: string;
  description: string;
}

export interface ForumCommunityFlairDTO {
  id: number;
  name: string;
  backgroundColor: string;
  textColor: string;
  sortOrder: number;
  isSelectedByCurrentUser: boolean;
}

export interface ForumCommunityFlairInput {
  id?: number;
  name: string;
  backgroundColor: string;
  textColor: string;
}

export interface ForumCommunityMemberSearchDTO {
  membershipId: number;
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  role: ForumCommunityMemberRole;
  flairId: number | null;
  flairName: string | null;
  flairBackgroundColor: string | null;
  flairTextColor: string | null;
  joinedAt: string;
}

export type ForumCommunityEventType =
  | "Multiplayer"
  | "Community"
  | "Tournament"
  | "WatchParty";

export interface ForumCommunityEventDTO {
  id: number;
  communityId: number;
  title: string;
  description: string;
  type: ForumCommunityEventType;
  gameName: string | null;
  platform: string | null;
  joinUrl: string | null;
  startsAtUtc: string;
  endsAtUtc: string | null;
  maxParticipants: number | null;
  participantCount: number;
  isJoinedByCurrentUser: boolean;
  isCancelled: boolean;
  createdByUserId: string;
  createdByUserName: string;
  canManage: boolean;
  createdAt: string;
}

export interface ForumCommunityEventInput {
  title: string;
  description: string;
  type: ForumCommunityEventType;
  gameName: string | null;
  platform: string | null;
  joinUrl: string | null;
  startsAtUtc: string;
  endsAtUtc: string | null;
  maxParticipants: number | null;
}

export interface CommunityPagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
