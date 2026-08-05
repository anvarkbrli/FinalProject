export interface ForumInterestGameDTO {
  key: string;
  title: string;
}

export interface ForumInterestProfileDTO {
  styles: string[];
  games: ForumInterestGameDTO[];
  genres: string[];
  platform: string;
  completedAtUtc: string;
}

export interface UpdateForumInterestProfileDTO {
  styles: string[];
  games: ForumInterestGameDTO[];
  genres: string[];
  platform: string;
}
