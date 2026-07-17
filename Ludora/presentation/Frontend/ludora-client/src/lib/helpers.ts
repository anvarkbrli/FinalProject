const apiOrigin = import.meta.env.VITE_API_ORIGIN;

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

export function formatRelativeDate(
  value: string,
): string {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "";
  }

  const differenceInSeconds = Math.round(
    (date.getTime() - Date.now()) / 1000,
  );

  const formatter = new Intl.RelativeTimeFormat("az-AZ", {
    numeric: "auto",
  });

  const absoluteDifference = Math.abs(
    differenceInSeconds,
  );

  if (absoluteDifference < 60) {
    return formatter.format(
      differenceInSeconds,
      "second",
    );
  }

  if (absoluteDifference < 3_600) {
    return formatter.format(
      Math.round(differenceInSeconds / 60),
      "minute",
    );
  }

  if (absoluteDifference < 86_400) {
    return formatter.format(
      Math.round(differenceInSeconds / 3_600),
      "hour",
    );
  }

  if (absoluteDifference < 2_592_000) {
    return formatter.format(
      Math.round(differenceInSeconds / 86_400),
      "day",
    );
  }

  return new Intl.DateTimeFormat("az-AZ", {
    day: "2-digit",
    month: "short",
    year: "numeric",
  }).format(date);
}