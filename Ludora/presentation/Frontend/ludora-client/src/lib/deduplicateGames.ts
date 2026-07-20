interface SearchableGame {
  source: "Local" | "Igdb";
  id: number;
  title: string;
  coverImageUrl: string | null;
  releaseDate: string | null;
  averageRating: number | null;
}

function normalizeTitle(value: string): string {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLocaleLowerCase("az-AZ")
    .replace(/[^a-z0-9əğıöşüç]+/gi, "")
    .trim();
}

function qualityScore(game: SearchableGame): number {
  return (
    (game.releaseDate ? 4 : 0) +
    (game.coverImageUrl ? 3 : 0) +
    (game.averageRating !== null ? 2 : 0) +
    (game.source === "Igdb" ? 1 : 0)
  );
}

export function deduplicateGames<T extends SearchableGame>(
  games: T[],
): T[] {
  const uniqueGames = new Map<string, T>();

  for (const game of games) {
    const key = normalizeTitle(game.title) || `${game.source}-${game.id}`;
    const current = uniqueGames.get(key);

    if (!current || qualityScore(game) > qualityScore(current)) {
      uniqueGames.set(key, game);
    }
  }

  return Array.from(uniqueGames.values());
}
