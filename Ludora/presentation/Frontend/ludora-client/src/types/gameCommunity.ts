export type GameCommunityUserCategory =
  | "Played"
  | "Playing"
  | "PlanToPlay"
  | "Wishlist"
  | "Favorite"
  | "Dropped";

export interface GameCommunityUserDTO {
  id: string;
  userName: string;
  profileImageUrl: string | null;
}

export interface GameCommunityUsersFilter {
  pageNumber: number;
  pageSize: number;
}
