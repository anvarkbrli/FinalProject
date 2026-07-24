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
  isFollowedByCurrentUser: boolean;
  canManage: boolean;
  createdAt: string;
}

export interface ForumCommunityMemberDTO {
  membershipId: number;
  userId: string;
  userName: string;
  profileImageUrl: string | null;
  role: "Owner" | "Member";
  status: ForumCommunityMembershipStatus;
  createdAt: string;
}

export interface CreateForumCommunityInput {
  name: string;
  description: string;
  category: ForumCommunityCategory;
  visibility: ForumCommunityVisibility;
  icon: File | null;
  coverImage: File | null;
}
