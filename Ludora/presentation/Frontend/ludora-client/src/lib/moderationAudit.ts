import type { SupportedLanguage } from "../i18n/localization";
import type {
  ModeratorActionLog,
  UserReportStatus,
} from "../types/admin";

const statusLabels: Record<
  SupportedLanguage,
  Record<UserReportStatus, string>
> = {
  az: {
    Pending: "Gözləyir",
    UnderReview: "Araşdırılır",
    Resolved: "Həll edilib",
    Rejected: "Rədd edilib",
  },
  en: {
    Pending: "Pending",
    UnderReview: "Under review",
    Resolved: "Resolved",
    Rejected: "Rejected",
  },
};

const actionLabels: Record<
  SupportedLanguage,
  Record<string, string>
> = {
  az: {
    "1": "Şikayətin statusu dəyişdirildi",
    "2": "İstifadəçiyə xəbərdarlıq göndərildi",
    "3": "İstifadəçiyə müddətli ban verildi",
    "4": "İstifadəçiyə müddətsiz ban verildi",
    "5": "İstifadəçinin banı açıldı",
    "6": "İstifadəçinin rolu dəyişdirildi",
    "7": "Admin əməliyyatı yerinə yetirildi",
    ReportStatusChanged: "Şikayətin statusu dəyişdirildi",
    WarningIssued: "İstifadəçiyə xəbərdarlıq göndərildi",
    TemporaryBanIssued: "İstifadəçiyə müddətli ban verildi",
    PermanentBanIssued: "İstifadəçiyə müddətsiz ban verildi",
    UserUnbanned: "İstifadəçinin banı açıldı",
    RoleChanged: "İstifadəçinin rolu dəyişdirildi",
    AdministrativeAction: "Admin əməliyyatı yerinə yetirildi",
  },
  en: {
    "1": "Report status changed",
    "2": "Sent a warning to the user",
    "3": "Temporarily banned the user",
    "4": "Permanently banned the user",
    "5": "Lifted the user's ban",
    "6": "Changed the user's role",
    "7": "Administrative action completed",
    ReportStatusChanged: "Report status changed",
    WarningIssued: "Sent a warning to the user",
    TemporaryBanIssued: "Temporarily banned the user",
    PermanentBanIssued: "Permanently banned the user",
    UserUnbanned: "Lifted the user's ban",
    RoleChanged: "Changed the user's role",
    AdministrativeAction: "Administrative action completed",
  },
};

const resourceTypeLabels: Record<string, string> = {
  Oyun: "Game",
  Janr: "Genre",
  Platforma: "Platform",
  Şirkət: "Company",
  Nişan: "Badge",
  Elan: "Announcement",
  Post: "Post",
  "Forum kateqoriyası": "Forum category",
  "Forum cavabı": "Forum reply",
  "Forum müzakirəsi": "Forum discussion",
};

const operationLabels: Record<string, string> = {
  "Nişan şəklini dəyişdi": "Updated the badge image",
  "İstifadəçinin nişanlarını yenidən hesabladı":
    "Re-evaluated the user's badges",
  "İstifadəçiyə nişan verdi": "Awarded a badge to the user",
  "İstifadəçidən nişanı geri aldı": "Revoked a badge from the user",
  "Forum müzakirəsini bərkitdi": "Pinned the forum discussion",
  "Forum müzakirəsinin bərkitməsini ləğv etdi":
    "Unpinned the forum discussion",
  "Forum müzakirəsini kilidlədi": "Locked the forum discussion",
  "Forum müzakirəsinin kilidini açdı":
    "Unlocked the forum discussion",
  "Oyuna əlaqəli kontent əlavə etdi":
    "Added related content to the game",
  "Oyundan əlaqəli kontenti sildi":
    "Removed related content from the game",
};

function getEnglishResourceNoun(resourceType?: string | null): string {
  if (!resourceType) return "item";
  return (resourceTypeLabels[resourceType] ?? resourceType).toLowerCase();
}

function withIndefiniteArticle(noun: string): string {
  return /^[aeiou]/i.test(noun) ? `an ${noun}` : `a ${noun}`;
}

function localizeAdministrativeOperation(
  operation: string,
  resourceType?: string | null,
): string {
  const exactLabel = operationLabels[operation];
  if (exactLabel) return exactLabel;

  const noun = getEnglishResourceNoun(resourceType);
  const nounWithArticle = withIndefiniteArticle(noun);

  if (operation.endsWith(" yaratdı")) {
    return `Created ${nounWithArticle}`;
  }
  if (operation.endsWith(" məlumatlarını redaktə etdi")) {
    return `Edited ${noun} details`;
  }
  if (operation.endsWith(" sildi")) {
    return `Deleted ${nounWithArticle}`;
  }
  if (operation.endsWith(" bərpa etdi")) {
    return `Restored ${nounWithArticle}`;
  }

  const fallbackMatch = operation.match(
    /^.+ üzrə “(.+)” əməliyyatını yerinə yetirdi$/,
  );
  if (fallbackMatch) {
    return `Performed the “${fallbackMatch[1]}” action for the ${noun}`;
  }

  return operation;
}

export function getModerationResourceType(
  resourceType: string,
  language: SupportedLanguage,
): string {
  if (language === "az") return resourceType;
  return resourceTypeLabels[resourceType] ?? resourceType;
}

export function getModerationActionTitle(
  entry: ModeratorActionLog,
  language: SupportedLanguage,
): string {
  const actionType = String(entry.actionType);
  const isReportStatusChange =
    actionType === "1" ||
    actionType === "ReportStatusChanged";

  if (
    (actionType === "7" ||
      actionType === "AdministrativeAction") &&
    entry.operation
  ) {
    return language === "en"
      ? localizeAdministrativeOperation(
          entry.operation,
          entry.resourceType,
        )
      : entry.operation;
  }

  if (isReportStatusChange && entry.newReportStatus) {
    const newStatus = statusLabels[language][entry.newReportStatus];

    if (entry.previousReportStatus) {
      const previousStatus =
        statusLabels[language][entry.previousReportStatus];

      return language === "en"
        ? `Report status changed from “${previousStatus}” to “${newStatus}”`
        : `Şikayətin statusu “${previousStatus}” vəziyyətindən “${newStatus}” vəziyyətinə dəyişdirildi`;
    }

    return language === "en"
      ? `Report status changed to “${newStatus}”`
      : `Şikayətin statusu “${newStatus}” olaraq dəyişdirildi`;
  }

  return (
    actionLabels[language][actionType] ??
    (language === "en"
      ? "Moderation action completed"
      : "Moderasiya əməliyyatı yerinə yetirildi")
  );
}
