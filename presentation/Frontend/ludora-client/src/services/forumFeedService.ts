import { api } from "../lib/api";
import type {
  ForumFeedQuery,
  ForumFeedResultDTO,
} from "../types/forumFeed";

function buildParams(query: ForumFeedQuery) {
  return {
    search: query.search?.trim() || undefined,
    categorySlug: query.categorySlug || undefined,
    period: query.period,
    interestGameTitles: query.interestGameTitles?.join("|") || undefined,
    interestGenres: query.interestGenres?.join("|") || undefined,
    interestPlayStyles: query.interestPlayStyles?.join("|") || undefined,
    interestPlatform: query.interestPlatform || undefined,
    pageNumber: query.pageNumber ?? 1,
    pageSize: query.pageSize ?? 20,
  };
}

export async function getForumHomeFeed(
  query: ForumFeedQuery,
  signal?: AbortSignal,
): Promise<ForumFeedResultDTO> {
  const response = await api.get<ForumFeedResultDTO>(
    "/forum-feed/home",
    {
      params: buildParams(query),
      signal,
    },
  );

  return response.data;
}

export async function getForumPopularFeed(
  query: ForumFeedQuery,
  signal?: AbortSignal,
): Promise<ForumFeedResultDTO> {
  const response = await api.get<ForumFeedResultDTO>(
    "/forum-feed/popular",
    {
      params: buildParams(query),
      signal,
    },
  );

  return response.data;
}

export async function getForumExploreFeed(
  query: ForumFeedQuery,
  signal?: AbortSignal,
): Promise<ForumFeedResultDTO> {
  const response = await api.get<ForumFeedResultDTO>(
    "/forum-feed/explore",
    {
      params: buildParams(query),
      signal,
    },
  );

  return response.data;
}
