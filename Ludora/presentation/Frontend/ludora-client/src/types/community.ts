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
