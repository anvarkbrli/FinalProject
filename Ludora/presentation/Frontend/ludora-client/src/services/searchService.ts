import { api } from "../lib/api";
import type { GlobalSearchResultDTO } from "../types/search";

export async function globalSearch(
  query: string,
  limit = 20,
): Promise<GlobalSearchResultDTO> {
  const response = await api.get<GlobalSearchResultDTO>(
    "/search",
    { params: { query, limit } },
  );

  return response.data;
}
