export type WellPlaydTheme = "light" | "dark";

export const THEME_STORAGE_KEY = "wellplayd-theme";

function isWellPlaydTheme(value: unknown): value is WellPlaydTheme {
  return value === "light" || value === "dark";
}

export function getPreferredTheme(): WellPlaydTheme {
  const savedTheme = window.localStorage.getItem(THEME_STORAGE_KEY);

  if (isWellPlaydTheme(savedTheme)) {
    return savedTheme;
  }

  return window.matchMedia("(prefers-color-scheme: dark)").matches
    ? "dark"
    : "light";
}

export function getActiveTheme(): WellPlaydTheme {
  const activeTheme = document.documentElement.dataset.theme;
  return isWellPlaydTheme(activeTheme) ? activeTheme : getPreferredTheme();
}

export function applyTheme(theme: WellPlaydTheme): void {
  document.documentElement.dataset.theme = theme;
  document.documentElement.style.colorScheme = theme;
  window.localStorage.setItem(THEME_STORAGE_KEY, theme);
}
