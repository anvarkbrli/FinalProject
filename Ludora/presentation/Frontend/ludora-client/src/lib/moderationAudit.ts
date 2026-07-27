import type {
  ModeratorActionLog,
  UserReportStatus,
} from "../types/admin";

const statusLabels: Record<UserReportStatus, string> = {
  Pending: "Gözləyir",
  UnderReview: "Araşdırılır",
  Resolved: "Həll edilib",
  Rejected: "Rədd edilib",
};

const actionLabels: Record<string, string> = {
  "1": "Şikayətin statusu dəyişdirildi",
  "2": "İstifadəçiyə xəbərdarlıq göndərildi",
  "3": "İstifadəçiyə müddətli ban verildi",
  "4": "İstifadəçiyə müddətsiz ban verildi",
  "5": "İstifadəçinin banı açıldı",
  ReportStatusChanged: "Şikayətin statusu dəyişdirildi",
  WarningIssued: "İstifadəçiyə xəbərdarlıq göndərildi",
  TemporaryBanIssued: "İstifadəçiyə müddətli ban verildi",
  PermanentBanIssued: "İstifadəçiyə müddətsiz ban verildi",
  UserUnbanned: "İstifadəçinin banı açıldı",
};

export function getModerationActionTitle(
  entry: ModeratorActionLog,
): string {
  const actionType = String(entry.actionType);
  const isReportStatusChange =
    actionType === "1" ||
    actionType === "ReportStatusChanged";

  if (isReportStatusChange && entry.newReportStatus) {
    const newStatus = statusLabels[entry.newReportStatus];

    if (entry.previousReportStatus) {
      const previousStatus =
        statusLabels[entry.previousReportStatus];

      return `Şikayətin statusu “${previousStatus}” vəziyyətindən “${newStatus}” vəziyyətinə dəyişdirildi`;
    }

    return `Şikayətin statusu “${newStatus}” olaraq dəyişdirildi`;
  }

  return (
    actionLabels[actionType] ??
    "Moderasiya əməliyyatı yerinə yetirildi"
  );
}
