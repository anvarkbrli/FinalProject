export type ContactMessageStatus =
  | "New"
  | "InProgress"
  | "ReplyQueued"
  | "Replied"
  | "ReplyFailed"
  | "Closed";

export type ContactMessageSubject =
  | "general"
  | "account"
  | "bug"
  | "feedback"
  | "moderation";

export interface ContactMessageCreateRequest {
  name: string;
  email: string;
  subject: ContactMessageSubject;
  message: string;
  language: "az" | "en";
}

export interface ContactMessageReceipt {
  id: number;
  status: ContactMessageStatus;
  createdAtUtc: string;
}
