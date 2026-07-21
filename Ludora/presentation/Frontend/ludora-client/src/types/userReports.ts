export type UserReportCategory =
  | "Spam"
  | "HarassmentOrInsult"
  | "HateSpeech"
  | "InappropriateContent"
  | "Impersonation"
  | "ScamOrFraud"
  | "ThreatsOrViolence"
  | "PrivacyViolation"
  | "Other";

export type UserReportStatus =
  | "Pending"
  | "UnderReview"
  | "Resolved"
  | "Rejected";

export interface UserReportDTO {
  id: number;
  reportedUserId: string;
  reportedUserName: string;
  category: UserReportCategory;
  description: string | null;
  hasEvidenceImage: boolean;
  status: UserReportStatus;
  createdAtUtc: string;
}

export interface CreateUserReportRequest {
  reportedUserId: string;
  category: UserReportCategory;
  description: string;
  evidenceImage: File | null;
}
