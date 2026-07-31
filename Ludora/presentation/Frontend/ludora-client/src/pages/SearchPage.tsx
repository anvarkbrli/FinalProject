import { useQuery } from "@tanstack/react-query";
import {
  ChevronLeft,
  ChevronRight,
  Gamepad2,
  ListChecks,
  LoaderCircle,
  MessageSquareText,
  Search,
  Star,
  UserRound,
} from "lucide-react";
import type { ReactNode } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { useI18n } from "../i18n/useI18n";
import {
  formatDateTime,
  getReleaseYear,
  resolveAssetUrl,
} from "../lib/helpers";
import {
  searchSection,
  type SearchSection,
} from "../services/searchService";
import type {
  SearchDiscussionResultDTO,
  SearchGameListResultDTO,
  SearchGameResultDTO,
  SearchReviewResultDTO,
  SearchUserResultDTO,
} from "../types/search";

const PAGE_SIZE = 20;

function parsePage(value: string | null): number {
  const page = Number(value);
  return Number.isInteger(page) && page > 0 ? page : 1;
}

function parseTab(value: string | null): SearchSection {
  return value === "users" ||
    value === "discussions" ||
    value === "reviews" ||
    value === "lists"
    ? value
    : "games";
}

export function SearchPage() {
  const { language } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const query = searchParams.get("q")?.trim() ?? "";
  const activeTab = parseTab(searchParams.get("tab"));
  const page = parsePage(searchParams.get("page"));

  const resultQuery = useQuery({
    queryKey: ["search-page", activeTab, query, page, PAGE_SIZE],
    queryFn: ({ signal }) =>
      searchSection(activeTab, query, page, PAGE_SIZE, signal),
    enabled: query.length >= 2,
    staleTime: 30_000,
  });

  const data = resultQuery.data;
  const tabs: Array<{ id: SearchSection; label: string }> = [
    { id: "games", label: language === "en" ? "Games" : "Oyunlar" },
    { id: "users", label: language === "en" ? "Users" : "İstifadəçilər" },
    {
      id: "discussions",
      label: language === "en" ? "Discussions" : "Müzakirələr",
    },
    { id: "reviews", label: language === "en" ? "Reviews" : "Rəylər" },
    { id: "lists", label: language === "en" ? "Lists" : "Siyahılar" },
  ];

  function changeTab(tab: SearchSection) {
    const next = new URLSearchParams(searchParams);
    if (tab === "games") next.delete("tab");
    else next.set("tab", tab);
    next.delete("page");
    setSearchParams(next);
  }

  function changePage(nextPage: number) {
    if (
      nextPage < 1 ||
      nextPage > (data?.totalPages ?? 1) ||
      nextPage === page
    ) {
      return;
    }

    const next = new URLSearchParams(searchParams);
    if (nextPage === 1) next.delete("page");
    else next.set("page", String(nextPage));
    setSearchParams(next);
    window.requestAnimationFrame(() => {
      document.getElementById("search-results")?.scrollIntoView({
        behavior: "smooth",
        block: "start",
      });
    });
  }

  const activeLabel = tabs.find((tab) => tab.id === activeTab)?.label;

  return (
    <div className="min-h-screen wellplayd-page-bg text-zinc-100">
      <Navbar />

      <main className="mx-auto min-h-[70vh] max-w-7xl px-5 py-10 lg:px-8">
        <div className="flex items-center gap-3">
          <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-violet-500/10 text-violet-300">
            <Search size={22} />
          </span>

          <div>
            <h1 className="text-2xl font-black text-white">
              {language === "en" ? "Search results" : "Axtarış nəticələri"}
            </h1>
            <p className="mt-1 text-sm text-zinc-500">
              {language === "en"
                ? `Content found for “${query}”`
                : `“${query}” üçün tapılan məzmun`}
            </p>
          </div>
        </div>

        {query.length < 2 ? (
          <EmptyState
            text={
              language === "en"
                ? "Enter at least 2 characters to search."
                : "Axtarış üçün ən azı 2 simvol yaz."
            }
          />
        ) : (
          <>
            <div className="mt-8 flex gap-2 overflow-x-auto border-b border-white/[0.07] pb-3">
              {tabs.map((tab) => (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => changeTab(tab.id)}
                  className={`shrink-0 rounded-xl px-4 py-2.5 text-sm font-bold transition ${
                    activeTab === tab.id
                      ? "bg-violet-600 text-white"
                      : "bg-white/[0.035] text-zinc-400 hover:bg-white/[0.07] hover:text-white"
                  }`}
                >
                  {tab.label}
                  {activeTab === tab.id && data
                    ? ` (${data.totalCount})`
                    : ""}
                </button>
              ))}
            </div>

            <div id="search-results" className="scroll-mt-24">
              {resultQuery.isLoading ? (
                <div className="flex min-h-72 items-center justify-center">
                  <LoaderCircle
                    className="animate-spin text-violet-300"
                    size={30}
                  />
                </div>
              ) : resultQuery.isError || !data ? (
                <div className="mt-10 rounded-3xl border border-red-400/15 bg-red-500/[0.05] p-10 text-center text-red-300">
                  {language === "en"
                    ? "Search results could not be loaded."
                    : "Axtarış nəticələri yüklənmədi."}
                </div>
              ) : (
                <>
                  <div className="mt-5 flex items-center justify-between gap-4 text-xs text-zinc-600">
                    <span>
                      {activeLabel}: {data.totalCount}
                    </span>
                    {resultQuery.isFetching && !resultQuery.isLoading && (
                      <span className="inline-flex items-center gap-2">
                        <LoaderCircle size={14} className="animate-spin" />
                        {language === "en" ? "Updating" : "Yenilənir"}
                      </span>
                    )}
                  </div>

                  <div className="mt-4 grid gap-3">
                    {activeTab === "games" &&
                      (data.items as SearchGameResultDTO[]).map((item) => (
                        <ResultLink
                          key={`${item.source}-${item.id}`}
                          to={`/games/${item.source}/${item.id}`}
                          imageUrl={item.coverImageUrl}
                          icon={<Gamepad2 size={20} />}
                          title={item.title}
                          description={`${getReleaseYear(item.releaseDate)}${
                            item.averageRating !== null
                              ? ` · ${item.averageRating.toFixed(1)} ${
                                  language === "en" ? "rating" : "puan"
                                }`
                              : ""
                          }`}
                        />
                      ))}

                    {activeTab === "users" &&
                      (data.items as SearchUserResultDTO[]).map((item) => (
                        <ResultLink
                          key={item.id}
                          to={`/users/${encodeURIComponent(item.userName)}`}
                          imageUrl={item.profileImageUrl}
                          icon={<UserRound size={20} />}
                          title={item.userName}
                          description={
                            item.bio ??
                            (language === "en"
                              ? "No bio added"
                              : "Bio əlavə edilməyib")
                          }
                          rounded
                        />
                      ))}

                    {activeTab === "discussions" &&
                      (data.items as SearchDiscussionResultDTO[]).map((item) => (
                        <ResultLink
                          key={item.id}
                          to={`/forum/discussions/${encodeURIComponent(item.slug)}`}
                          icon={<MessageSquareText size={20} />}
                          title={item.title}
                          description={`${item.categoryName} · ${item.authorUserName} · ${formatDateTime(item.createdAt)}`}
                        />
                      ))}

                    {activeTab === "reviews" &&
                      (data.items as SearchReviewResultDTO[]).map((item) => (
                        <ResultLink
                          key={item.id}
                          to={`/games/${item.source}/${item.gameId}`}
                          imageUrl={item.gameCoverImageUrl}
                          icon={<Star size={20} />}
                          title={item.gameName}
                          eyebrow={
                            language === "en"
                              ? `Review by ${item.userName}`
                              : `${item.userName} tərəfindən rəy`
                          }
                          description={item.contentPreview}
                        />
                      ))}

                    {activeTab === "lists" &&
                      (data.items as SearchGameListResultDTO[]).map((item) => (
                        <ResultLink
                          key={item.id}
                          to={`/lists/${item.id}`}
                          icon={<ListChecks size={20} />}
                          title={item.name}
                          description={
                            language === "en"
                              ? `${item.ownerUserName} · ${item.gameCount} ${
                                  item.gameCount === 1 ? "game" : "games"
                                }${item.isRanked ? " · Ranked" : ""}`
                              : `${item.ownerUserName} · ${item.gameCount} oyun${
                                  item.isRanked ? " · Sıralı" : ""
                                }`
                          }
                        />
                      ))}

                    {data.items.length === 0 && (
                      <EmptyState
                        text={
                          language === "en"
                            ? "No results were found in this category."
                            : "Bu kateqoriyada nəticə tapılmadı."
                        }
                      />
                    )}
                  </div>

                  {data.totalPages > 1 && (
                    <div className="mt-6 flex items-center justify-between rounded-2xl border border-white/[0.08] bg-white/[0.025] p-2">
                      <button
                        type="button"
                        disabled={page <= 1}
                        onClick={() => changePage(page - 1)}
                        className="inline-flex h-10 items-center gap-2 rounded-xl px-3 text-xs font-bold text-zinc-400 transition hover:bg-white/[0.05] hover:text-white disabled:cursor-not-allowed disabled:opacity-35"
                      >
                        <ChevronLeft size={16} />
                        {language === "en" ? "Previous" : "Əvvəlki"}
                      </button>
                      <span className="text-xs font-semibold text-zinc-600">
                        {page} / {data.totalPages}
                      </span>
                      <button
                        type="button"
                        disabled={page >= data.totalPages}
                        onClick={() => changePage(page + 1)}
                        className="inline-flex h-10 items-center gap-2 rounded-xl px-3 text-xs font-bold text-zinc-400 transition hover:bg-white/[0.05] hover:text-white disabled:cursor-not-allowed disabled:opacity-35"
                      >
                        {language === "en" ? "Next" : "Növbəti"}
                        <ChevronRight size={16} />
                      </button>
                    </div>
                  )}
                </>
              )}
            </div>
          </>
        )}
      </main>

      <Footer />
    </div>
  );
}

function ResultLink({
  to,
  imageUrl,
  icon,
  title,
  eyebrow,
  description,
  rounded = false,
}: {
  to: string;
  imageUrl?: string | null;
  icon: ReactNode;
  title: string;
  eyebrow?: string;
  description: string;
  rounded?: boolean;
}) {
  const resolvedImageUrl = resolveAssetUrl(imageUrl);

  return (
    <Link
      to={to}
      className="flex items-center gap-4 rounded-2xl border border-white/[0.08] bg-[#111116] p-4 transition hover:border-violet-400/25 hover:bg-[#15131d]"
    >
      <span
        className={`flex shrink-0 items-center justify-center overflow-hidden border border-white/[0.08] bg-violet-500/10 text-violet-300 ${
          rounded
            ? "h-14 w-14 rounded-full"
            : "h-16 w-14 rounded-xl"
        }`}
      >
        {resolvedImageUrl ? (
          <img
            src={resolvedImageUrl}
            alt=""
            className="h-full w-full object-cover"
          />
        ) : (
          icon
        )}
      </span>

      <span className="min-w-0">
        <span className="block truncate font-bold text-white">
          {title}
        </span>
        {eyebrow && (
          <span className="mt-1 block text-xs font-semibold text-violet-300">
            {eyebrow}
          </span>
        )}
        <span className="mt-1 line-clamp-2 block text-sm text-zinc-500">
          {description}
        </span>
      </span>
    </Link>
  );
}

function EmptyState({ text }: { text: string }) {
  return (
    <div className="rounded-3xl border border-white/[0.08] bg-white/[0.025] p-10 text-center text-zinc-500">
      {text}
    </div>
  );
}
