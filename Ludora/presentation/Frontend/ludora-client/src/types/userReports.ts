export type UserReportContentType =
  | "UserProfile"
  | "Post"
  | "PostComment"
  | "Review"
  | "ReviewComment"
  | "Discussion"
  | "DiscussionReply"
  | "DirectMessage"
  | "Community";

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
  reportedContentType: UserReportContentType;
  reportedContentId: number | null;
  reportedContentSnapshot: string | null;
  reportedContentUrl: string | null;
  status: UserReportStatus;
  createdAtUtc: string;
}

export interface CreateUserReportRequest {
  reportedUserId: string;
  category: UserReportCategory;
  description: string;
  evidenceFiles: File[];
  reportedContentType: UserReportContentType;
  reportedContentId: number | null;
}
