import type {
  GameListItemDTO,
  GameType,
} from "../types/home";

const mainCatalogGameTypes = new Set<GameType>([
  "MainGame",
  "StandaloneExpansion",
  "Remake",
  "Remaster",
  "ExpandedGame",
  "Port",
]);

const blockedGameTitles = new Set([
  "untitled",
  "untitled game",
  "unknown",
  "unknown game",
  "test",
  "test game",
  "sample",
  "sample game",
  "placeholder",
  "asdf",
  "qwerty",
  "null",
  "undefined",
  "n/a",
  "na",
  "tbd",
]);

function normalizeGameTitle(title: string): string {
  return title
    .normalize("NFKC")
    .trim()
    .replace(/\s+/g, " ");
}

export function isDisplayableGame(
  game: GameListItemDTO,
): boolean {
  if (!mainCatalogGameTypes.has(game.gameType)) {
    return false;
  }

  const title = normalizeGameTitle(game.title);

  if (!title) {
    return false;
  }

  const normalizedTitle = title.toLocaleLowerCase();

  if (blockedGameTitles.has(normalizedTitle)) {
    return false;
  }

  const meaningfulCharacters =
    title.match(/[\p{L}\p{N}]/gu) ?? [];

  /*
   * "-", "—", ":)", "!", "." kimi
   * adların qarşısını alır.
   */
  if (meaningfulCharacters.length < 2) {
    return false;
  }

  const meaningfulTitle = meaningfulCharacters
    .join("")
    .toLocaleLowerCase();

  /*
   * "aaa", "1111", "xxxxx" kimi yalnız
   * eyni simvoldan ibarət adları gizlədir.
   */
  if (
    meaningfulTitle.length >= 3 &&
    /^(.)\1+$/u.test(meaningfulTitle)
  ) {
    return false;
  }

  /*
   * IGDB-dən gələn oyunda nə janr, nə də
   * platforma varsa, aşağı keyfiyyətli və ya
   * natamam qeyd kimi qəbul edilir.
   *
   * Local oyunlara bu məhdudiyyət tətbiq edilmir.
   */
  if (
    game.source === "Igdb" &&
    game.genres.length === 0 &&
    game.platforms.length === 0
  ) {
    return false;
  }

  return true;
}

export function filterDisplayableGames<
  TGame extends GameListItemDTO,
>(games: TGame[]): TGame[] {
  return games.filter(isDisplayableGame);
}