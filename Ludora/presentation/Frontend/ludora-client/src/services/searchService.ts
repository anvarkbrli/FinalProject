import { api } from "../lib/api";
import type { PagedResultDTO } from "../types/home";
import type {
  GlobalSearchResultDTO,
  SearchDiscussionResultDTO,
  SearchGameListResultDTO,
  SearchGameResultDTO,
  SearchReviewResultDTO,
  SearchUserResultDTO,
} from "../types/search";

export type SearchSection =
  | "games"
  | "users"
  | "discussions"
  | "reviews"
  | "lists";

export type SearchSectionResult =
  | SearchGameResultDTO
  | SearchUserResultDTO
  | SearchDiscussionResultDTO
  | SearchReviewResultDTO
  | SearchGameListResultDTO;

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

export async function searchSection(
  section: SearchSection,
  query: string,
  pageNumber: number,
  pageSize = 20,
  signal?: AbortSignal,
): Promise<PagedResultDTO<SearchSectionResult>> {
  const response = await api.get<PagedResultDTO<SearchSectionResult>>(
    `/search/${section}`,
    {
      params: { query, pageNumber, pageSize },
      signal,
    },
  );

  return response.data;
}
