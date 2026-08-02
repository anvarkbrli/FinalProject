import {
  getCurrentLanguage,
  getCurrentLocale,
  t,
} from "../i18n/localization";

const apiOrigin = import.meta.env.VITE_API_ORIGIN;
const azerbaijanTimeZone = "Asia/Baku";
const dateOnlyPattern = /^(\d{4})-(\d{2})-(\d{2})$/;
const utcOffsetPattern = /(Z|[+-]\d{2}:?\d{2})$/i;

export function resolveAssetUrl(
  value: string | null | undefined,
): string | null {
  if (!value) {
    return null;
  }

  if (
    value.startsWith("http://") ||
    value.startsWith("https://")
  ) {
    return value;
  }

  if (value.startsWith("//")) {
    return `https:${value}`;
  }

  if (!apiOrigin) {
    return value;
  }

  const normalizedPath = value.startsWith("/")
    ? value
    : `/${value}`;

  return `${apiOrigin}${normalizedPath}`;
}

export function getReleaseYear(
  value: string | null,
): string {
  if (!value) {
    return "Tarix yoxdur";
  }

  return value.slice(0, 4);
}

function parseBackendDateTime(
  value: string,
): Date | null {
  const normalizedValue =
    value.includes("T") && !utcOffsetPattern.test(value)
      ? `${value}Z`
      : value;
  const date = new Date(normalizedValue);

  return Number.isNaN(date.getTime())
    ? null
    : date;
}

export function formatDate(
  value: string | null | undefined,
): string {
  if (!value) {
    return "";
  }

  const dateOnlyMatch = dateOnlyPattern.exec(value);

  if (dateOnlyMatch) {
    const [, year, month, day] = dateOnlyMatch;
    return `${day}.${month}.${year}`;
  }

  const date = parseBackendDateTime(value);

  if (!date) {
    return value;
  }

  if (getCurrentLanguage() === "az") {
    const parts = new Intl.DateTimeFormat("en-GB", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      timeZone: azerbaijanTimeZone,
    }).formatToParts(date);
    const day = parts.find((part) => part.type === "day")?.value ?? "";
    const month = parts.find((part) => part.type === "month")?.value ?? "";
    const year = parts.find((part) => part.type === "year")?.value ?? "";

    return day && month && year ? `${day}.${month}.${year}` : value;
  }

  return new Intl.DateTimeFormat(getCurrentLocale(), {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    timeZone: azerbaijanTimeZone,
  }).format(date);
}

export function formatLongDate(
  value: string | null | undefined,
): string {
  if (!value) {
    return "";
  }

  const dateOnlyMatch = dateOnlyPattern.exec(value);
  let date: Date | null;
  let timeZone = azerbaijanTimeZone;

  if (dateOnlyMatch) {
    const [, year, month, day] = dateOnlyMatch;
    date = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day), 12));
    timeZone = "UTC";
  } else {
    date = parseBackendDateTime(value);
  }

  if (!date) {
    return value;
  }

  const parts = new Intl.DateTimeFormat("en-US", {
    day: "numeric",
    month: "numeric",
    year: "numeric",
    timeZone,
  }).formatToParts(date);

  const day = Number(parts.find((part) => part.type === "day")?.value ?? 0);
  const monthIndex = Number(
    parts.find((part) => part.type === "month")?.value ?? 0,
  ) - 1;
  const year = parts.find((part) => part.type === "year")?.value ?? "";

  const azerbaijaniMonths = [
    "yanvar",
    "fevral",
    "mart",
    "aprel",
    "may",
    "iyun",
    "iyul",
    "avqust",
    "sentyabr",
    "oktyabr",
    "noyabr",
    "dekabr",
  ];
  const englishMonths = [
    "January",
    "February",
    "March",
    "April",
    "May",
    "June",
    "July",
    "August",
    "September",
    "October",
    "November",
    "December",
  ];

  const month = getCurrentLanguage() === "az"
    ? azerbaijaniMonths[monthIndex]
    : englishMonths[monthIndex];

  if (!day || !month || !year) {
    return value;
  }

  return getCurrentLanguage() === "az"
    ? `${day} ${month}, ${year}`
    : `${month} ${day}, ${year}`;
}

export function formatDateTime(
  value: string | null | undefined,
): string {
  if (!value) {
    return "";
  }

  if (dateOnlyPattern.test(value)) {
    return formatDate(value);
  }

  const date = parseBackendDateTime(value);

  if (!date) {
    return formatDate(value);
  }

  return new Intl.DateTimeFormat(getCurrentLocale(), {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: azerbaijanTimeZone,
  }).format(date);
}

function parseDateOnly(
  value: string | null,
): Date | null {
  if (!value) {
    return null;
  }

  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(
    value,
  );

  if (!match) {
    return null;
  }

  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  const date = new Date(year, month - 1, day);

  if (
    date.getFullYear() !== year ||
    date.getMonth() !== month - 1 ||
    date.getDate() !== day
  ) {
    return null;
  }

  return date;
}

export function isFutureReleaseDate(
  value: string | null,
  now = new Date(),
): boolean {
  const releaseDate = parseDateOnly(value);

  if (!releaseDate) {
    return false;
  }

  const today = new Date(
    now.getFullYear(),
    now.getMonth(),
    now.getDate(),
  );

  return releaseDate.getTime() > today.getTime();
}

export function formatRelativeDate(
  value: string,
  now = new Date(),
): string {
  const date = parseBackendDateTime(value);

  if (!date) {
    return formatDateTime(value);
  }

  const differenceInMilliseconds = now.getTime() - date.getTime();
  const absoluteDifference = Math.abs(differenceInMilliseconds);

  if (absoluteDifference < 45_000) {
    return t("common:date.now");
  }

  const isFuture = differenceInMilliseconds < 0;
  const absoluteMinutes = Math.max(1, Math.floor(absoluteDifference / 60_000));

  if (getCurrentLanguage() === "az") {
    const formatUnit = (amount: number, unit: string) =>
      isFuture
        ? `${amount} ${unit} sonra`
        : `${amount} ${unit} əvvəl`;

    if (absoluteMinutes < 60) {
      return formatUnit(absoluteMinutes, "dəqiqə");
    }

    const hours = Math.floor(absoluteMinutes / 60);
    if (hours < 24) {
      return formatUnit(hours, "saat");
    }

    const days = Math.floor(hours / 24);
    if (days < 7) {
      return formatUnit(days, "gün");
    }

    const weeks = Math.floor(days / 7);
    if (weeks < 5) {
      return formatUnit(weeks, "həftə");
    }

    const months = Math.floor(days / 30);
    if (months < 12) {
      return formatUnit(months, "ay");
    }

    return formatUnit(Math.floor(days / 365), "il");
  }

  const relativeTime = new Intl.RelativeTimeFormat(getCurrentLocale(), {
    numeric: "always",
  });

  if (absoluteMinutes < 60) {
    return relativeTime.format(
      isFuture ? absoluteMinutes : -absoluteMinutes,
      "minute",
    );
  }

  const hours = Math.floor(absoluteMinutes / 60);
  if (hours < 24) {
    return relativeTime.format(isFuture ? hours : -hours, "hour");
  }

  const days = Math.floor(hours / 24);
  if (days < 7) {
    return relativeTime.format(isFuture ? days : -days, "day");
  }

  const weeks = Math.floor(days / 7);
  if (weeks < 5) {
    return relativeTime.format(isFuture ? weeks : -weeks, "week");
  }

  const months = Math.floor(days / 30);
  if (months < 12) {
    return relativeTime.format(isFuture ? months : -months, "month");
  }

  const years = Math.floor(days / 365);
  return relativeTime.format(isFuture ? years : -years, "year");
}

export function formatPlaytime(
  totalMinutes: number | null | undefined,
): string {
  const normalizedMinutes = Math.max(0, Math.round(totalMinutes ?? 0));
  const hours = Math.floor(normalizedMinutes / 60);
  const minutes = normalizedMinutes % 60;
  const language = getCurrentLanguage();

  const formatUnit = (
    amount: number,
    singular: string,
    plural: string,
  ) => `${new Intl.NumberFormat(getCurrentLocale()).format(amount)} ${
    language === "en" && amount === 1 ? singular : plural
  }`;

  const hourText =
    language === "en"
      ? formatUnit(hours, "hour", "hours")
      : `${new Intl.NumberFormat(getCurrentLocale()).format(hours)} saat`;
  const minuteText =
    language === "en"
      ? formatUnit(minutes, "minute", "minutes")
      : `${new Intl.NumberFormat(getCurrentLocale()).format(minutes)} dəqiqə`;

  if (hours === 0) {
    return minuteText;
  }

  if (minutes === 0) {
    return hourText;
  }

  return `${hourText} ${minuteText}`;
}
