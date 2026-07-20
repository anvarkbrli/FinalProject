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

  return new Intl.DateTimeFormat("az-AZ", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    timeZone: azerbaijanTimeZone,
  }).format(date);
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

  return new Intl.DateTimeFormat("az-AZ", {
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
): string {
  return formatDateTime(value);
}
