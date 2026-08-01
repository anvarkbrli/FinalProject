import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  ArrowRight,
  ChevronRight,
  Flame,
  LogIn,
  MessageCircle,
  Plus,
  Search,
  Sparkles,
  UserPlus,
  UsersRound,
} from "lucide-react";
import { useEffect, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { CreateCommunityModal } from "../components/forum/CreateCommunityModal";
import { CreateDiscussionModal } from "../components/forum/CreateDiscussionModal";
import {
  DiscussionFeedCard,
  ForumFeedSkeleton,
  ForumPostCard,
} from "../components/forum/ForumFeedCards";
import { ForumInterestOnboarding } from "../components/forum/ForumInterestOnboarding";
import {
  ForumSidebar,
  type ForumSection,
} from "../components/forum/ForumSidebar";
import { Navbar } from "../components/layout/Navbar";
import { useI18n } from "../i18n/useI18n";
import { resolveAssetUrl } from "../lib/helpers";
import {
  getForumCommunities,
  getMyForumCommunities,
} from "../services/forumCommunityService";
import {
  getForumExploreFeed,
  getForumHomeFeed,
  getForumPopularFeed,
} from "../services/forumFeedService";
import { getForumDiscussions } from "../services/forumService";
import { getForumInterests } from "../services/forumInterestsService";
import { getForumCategories } from "../services/homeService";
import { getMyUserGames } from "../services/userGamesService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../stores/authStore";
import type {
  ForumCommunityCategory,
  ForumCommunityDTO,
} from "../types/community";
import type {
  ForumFeedItemDTO,
  ForumPopularPeriod,
} from "../types/forumFeed";
import type { ForumInterestProfileDTO } from "../types/forumInterests";

const communityCategoryLabels: Record<
  ForumCommunityCategory,
  string
> = {
  GameNews: "Oyun xəbərləri",
  BugsAndSolutions: "Buglar və həlləri",
  Speedrunning: "Speedrunning",
  HardwareAndGamingSetup: "Hardware və gaming setup",
  IndieGames: "Indie oyunlar",
  ModdingAndGameDevelopment: "Modding və oyun inkişafı",
  Esports: "Esports",
  Other: "Digər",
};

const popularPeriods: Array<{
  value: ForumPopularPeriod;
  label: string;
}> = [
  { value: "day", label: "24 saat" },
  { value: "week", label: "7 gün" },
  { value: "month", label: "30 gün" },
  { value: "all", label: "Bütün zamanlar" },
];

const forumSections: ForumSection[] = [
  "home",
  "discussions",
  "popular",
  "communities",
  "explore",
];

function parseForumSection(value: string | null): ForumSection {
  return forumSections.includes(value as ForumSection)
    ? (value as ForumSection)
    : "home";
}

function parsePopularPeriod(value: string | null): ForumPopularPeriod {
  return popularPeriods.some((period) => period.value === value)
    ? (value as ForumPopularPeriod)
    : "week";
}

function parsePage(value: string | null): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : 1;
}


function getCommunityPath(slug: string) {
  return `/forum/communities/${encodeURIComponent(slug.trim())}`;
}

function CommunityCard({
  community,
  compact = false,
}: {
  community: ForumCommunityDTO;
  compact?: boolean;
}) {
  const { language } = useI18n();
  const iconUrl = resolveAssetUrl(community.iconUrl);
  const coverImageUrl = resolveAssetUrl(community.coverImageUrl);

  if (compact) {
    return (
      <Link
        to={getCommunityPath(community.slug)}
        className="group flex min-w-0 items-center gap-3 rounded-2xl border border-white/[0.07] bg-white/[0.025] p-3 transition hover:border-teal-300/20 hover:bg-teal-300/[0.04]"
      >
        <span className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-teal-300/10 text-sm font-black text-teal-100">
          {iconUrl ? (
            <img
              src={iconUrl}
              alt=""
              className="h-full w-full object-cover"
            />
          ) : (
            community.name.charAt(0).toUpperCase()
          )}
        </span>
        <span className="min-w-0 flex-1">
          <span className="block truncate text-sm font-bold text-zinc-200">
            {community.name}
          </span>
          <span className="mt-1 block text-[11px] text-zinc-600">
            {language === "en"
              ? `${community.memberCount} ${community.memberCount === 1 ? "member" : "members"} · ${community.discussionCount} ${community.discussionCount === 1 ? "discussion" : "discussions"}`
              : `${community.memberCount} üzv · ${community.discussionCount} müzakirə`}
          </span>
        </span>
        <ChevronRight
          size={15}
          className="text-zinc-700 transition group-hover:translate-x-0.5 group-hover:text-teal-300"
        />
      </Link>
    );
  }

  return (
    <Link
      to={getCommunityPath(community.slug)}
      className="group flex h-full min-w-0 flex-col overflow-hidden rounded-[22px] border border-white/[0.075] bg-[#0d1716]/80 text-left transition hover:-translate-y-0.5 hover:border-teal-200/15"
    >
      <div
        className="h-24 shrink-0 bg-gradient-to-br from-teal-400/15 via-transparent to-violet-400/10 bg-cover bg-center"
        style={
          coverImageUrl
            ? { backgroundImage: `url("${coverImageUrl}")` }
            : undefined
        }
      />
      <div className="flex flex-1 flex-col px-5 pb-5">
        <span className="-mt-7 flex h-14 w-14 shrink-0 items-center justify-center overflow-hidden rounded-2xl border-4 border-[#0d1716] bg-teal-300/15 text-sm font-black text-teal-100">
          {iconUrl ? (
            <img
              src={iconUrl}
              alt=""
              className="h-full w-full object-cover"
            />
          ) : (
            community.name.charAt(0).toUpperCase()
          )}
        </span>
        <p className="mt-3 truncate font-bold text-white">
          {community.name}
        </p>
        <p className="mt-1 text-[10px] font-semibold text-teal-300/70">
          {communityCategoryLabels[community.category]}
        </p>
        <p data-user-content className="mt-2 line-clamp-2 min-h-10 text-xs leading-5 text-zinc-600">
          {community.description}
        </p>
        <div className="mt-auto flex items-center justify-between gap-3 pt-4 text-[11px]">
          <span className="truncate text-zinc-600">
            {language === "en"
              ? `${community.memberCount} ${community.memberCount === 1 ? "member" : "members"} · ${community.discussionCount} ${community.discussionCount === 1 ? "discussion" : "discussions"}`
              : `${community.memberCount} üzv · ${community.discussionCount} müzakirə`}
          </span>
          <ChevronRight
            size={15}
            className="shrink-0 text-zinc-700 transition group-hover:translate-x-0.5 group-hover:text-teal-300"
          />
        </div>
      </div>
    </Link>
  );
}

function FeedItem({
  item,
  isAuthenticated,
  showReason,
}: {
  item: ForumFeedItemDTO;
  isAuthenticated: boolean;
  showReason: boolean;
}) {
  const reason = showReason ? item.reason : undefined;

  if (item.itemType === "Post" && item.post) {
    return (
      <ForumPostCard
        post={item.post}
        reason={reason}
        isAuthenticated={isAuthenticated}
      />
    );
  }

  if (item.discussion) {
    return (
      <DiscussionFeedCard
        discussion={item.discussion}
        reason={reason}
        isAuthenticated={isAuthenticated}
      />
    );
  }

  return null;
}

function EmptyFeed({
  title,
  description,
}: {
  title: string;
  description: string;
}) {
  return (
    <div className="rounded-[22px] border border-dashed border-white/10 bg-white/[0.02] p-10 text-center">
      <MessageCircle
        size={26}
        className="mx-auto text-zinc-700"
      />
      <p className="mt-3 text-sm font-semibold text-zinc-400">
        {title}
      </p>
      <p className="mx-auto mt-1 max-w-md text-xs leading-5 text-zinc-600">
        {description}
      </p>
    </div>
  );
}

function Pagination({
  page,
  totalPages,
  onPageChange,
}: {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}) {
  if (totalPages <= 1) return null;

  return (
    <div className="mt-5 flex items-center justify-between rounded-2xl border border-white/[0.07] bg-white/[0.025] p-2">
      <button
        type="button"
        disabled={page <= 1}
        onClick={() => onPageChange(page - 1)}
        className="flex h-9 items-center gap-2 rounded-xl px-3 text-xs font-bold text-zinc-400 transition hover:bg-white/[0.045] hover:text-white disabled:cursor-not-allowed disabled:opacity-35"
      >
        <ArrowLeft size={14} />
        Əvvəlki
      </button>
      <span className="text-xs font-semibold text-zinc-600">
        {page} / {totalPages}
      </span>
      <button
        type="button"
        disabled={page >= totalPages}
        onClick={() => onPageChange(page + 1)}
        className="flex h-9 items-center gap-2 rounded-xl px-3 text-xs font-bold text-zinc-400 transition hover:bg-white/[0.045] hover:text-white disabled:cursor-not-allowed disabled:opacity-35"
      >
        Növbəti
        <ArrowRight size={14} />
      </button>
    </div>
  );
}

export function ForumPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const user = useAuthStore((state) => state.user);
  const accessToken = useAuthStore((state) => state.accessToken);
  const expiresAtUtc = useAuthStore((state) => state.expiresAtUtc);
  const isAuthenticated =
    user !== null && isAuthSessionValid(accessToken, expiresAtUtc);

  const activeSection = parseForumSection(searchParams.get("section"));
  const selectedCategory = searchParams.get("category")?.trim() ?? "";
  const feedSearch = searchParams.get("q")?.trim() ?? "";
  const popularPeriod = parsePopularPeriod(searchParams.get("period"));
  const page = parsePage(searchParams.get("page"));
  const [searchInput, setSearchInput] = useState(feedSearch);
  const [onboardingCompleted, setOnboardingCompleted] =
    useState(false);
  const [onboardingDismissed, setOnboardingDismissed] =
    useState(false);
  const [isOnboardingRequested, setIsOnboardingRequested] =
    useState(false);
  const [isCreateModalOpen, setIsCreateModalOpen] =
    useState(false);
  const [isCreateCommunityOpen, setIsCreateCommunityOpen] =
    useState(false);

  const serializedSearchParams = searchParams.toString();

  function updateForumUrl(
    updates: Record<string, string | null>,
    options: { replace?: boolean } = { replace: true },
  ) {
    const nextSearchParams = new URLSearchParams(searchParams);
    for (const [key, value] of Object.entries(updates)) {
      if (value?.trim()) nextSearchParams.set(key, value.trim());
      else nextSearchParams.delete(key);
    }
    setSearchParams(nextSearchParams, { replace: options.replace ?? true });
  }

  useEffect(() => {
    const timeoutId = window.setTimeout(() => setSearchInput(feedSearch), 0);
    return () => window.clearTimeout(timeoutId);
  }, [feedSearch]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      const normalizedSearch = searchInput.trim();
      if (normalizedSearch === feedSearch) return;

      const nextSearchParams = new URLSearchParams(serializedSearchParams);
      if (normalizedSearch) nextSearchParams.set("q", normalizedSearch);
      else nextSearchParams.delete("q");
      nextSearchParams.delete("page");
      setSearchParams(nextSearchParams, { replace: true });
    }, 350);

    return () => window.clearTimeout(timeoutId);
  }, [feedSearch, searchInput, serializedSearchParams, setSearchParams]);

  const userGamesQuery = useQuery({
    queryKey: ["forum", "user-game-count", user?.id],
    queryFn: ({ signal }) => getMyUserGames(1, 1, signal),
    enabled: isAuthenticated,
  });

  const interestsQuery = useQuery({
    queryKey: ["forum", "interests", user?.id],
    queryFn: ({ signal }) => getForumInterests(signal),
    enabled: isAuthenticated,
  });
  const interestProfile = interestsQuery.data ?? null;

  const categoriesQuery = useQuery({
    queryKey: ["forum", "categories"],
    queryFn: getForumCategories,
  });

  const communitiesQuery = useQuery({
    queryKey: ["forum", "communities"],
    queryFn: ({ signal }) => getForumCommunities(signal),
  });

  const myCommunitiesQuery = useQuery({
    queryKey: ["forum", "communities", "mine", user?.id],
    queryFn: ({ signal }) => getMyForumCommunities(signal),
    enabled: isAuthenticated,
  });

  const homeFeedQuery = useQuery({
    queryKey: ["forum", "home-feed", user?.id, page, feedSearch],
    queryFn: ({ signal }) =>
      getForumHomeFeed(
        {
          search: feedSearch,
          pageNumber: page,
          pageSize: 20,
        },
        signal,
      ),
    enabled: activeSection === "home" && isAuthenticated,
  });

  const popularFeedQuery = useQuery({
    queryKey: [
      "forum",
      "popular-feed",
      page,
      feedSearch,
      selectedCategory,
      popularPeriod,
    ],
    queryFn: ({ signal }) =>
      getForumPopularFeed(
        {
          search: feedSearch,
          categorySlug: selectedCategory,
          period: popularPeriod,
          pageNumber: page,
          pageSize: 20,
        },
        signal,
      ),
    enabled: activeSection === "popular",
  });

  const exploreFeedQuery = useQuery({
    queryKey: [
      "forum",
      "explore-feed",
      page,
      feedSearch,
      user?.id,
      interestProfile,
    ],
    queryFn: ({ signal }) =>
      getForumExploreFeed(
        {
          search: feedSearch,
          interestGameTitles: interestProfile?.games?.map(
            (game) => game.title,
          ),
          interestGenres: interestProfile?.genres,
          interestPlayStyles: interestProfile?.styles,
          interestPlatform: interestProfile?.platform,
          pageNumber: page,
          pageSize: 20,
        },
        signal,
      ),
    enabled:
      activeSection === "explore" &&
      (!isAuthenticated || interestsQuery.isSuccess),
  });

  const discussionsQuery = useQuery({
    queryKey: [
      "forum",
      "discussions",
      page,
      feedSearch,
      selectedCategory,
    ],
    queryFn: ({ signal }) =>
      getForumDiscussions(
        "newest",
        selectedCategory,
        signal,
        undefined,
        feedSearch,
        page,
        20,
      ),
    enabled: activeSection === "discussions",
  });

  function changeSection(section: ForumSection) {
    setIsCreateModalOpen(false);
    setIsCreateCommunityOpen(false);
    setSearchInput("");
    updateForumUrl({
      section: section === "home" ? null : section,
      category: null,
      q: null,
      page: null,
      period: section === "popular" ? popularPeriod : null,
    });
  }

  function changeCategory(category: string) {
    updateForumUrl({ category: category || null, page: null });
  }

  function changePopularPeriod(period: ForumPopularPeriod) {
    updateForumUrl({ period, page: null });
  }

  function changePage(nextPage: number) {
    updateForumUrl(
      { page: nextPage > 1 ? String(nextPage) : null },
      { replace: false },
    );
    window.requestAnimationFrame(() => {
      document.querySelector("main")?.scrollIntoView({
        behavior: "smooth",
        block: "start",
      });
    });
  }

  const shouldShowOnboarding =
    isAuthenticated &&
    userGamesQuery.isSuccess &&
    userGamesQuery.data.totalCount < 25 &&
    interestsQuery.isSuccess &&
    !interestProfile &&
    !onboardingCompleted &&
    !onboardingDismissed;

  const categories = categoriesQuery.data ?? [];
  const communities = communitiesQuery.data ?? [];
  const activeFeedQuery =
    activeSection === "home"
      ? homeFeedQuery
      : activeSection === "popular"
        ? popularFeedQuery
        : exploreFeedQuery;
  const activeFeed = activeFeedQuery.data;

  const sectionTitle: Record<ForumSection, string> = {
    home: "İzlədiklərindəki yeniliklər",
    discussions: "Ən yeni müzakirələr",
    popular: "İcmaların gündəmi",
    communities: "Oyun icmaları",
    explore: "Sənin üçün yeni kəşflər",
  };

  const sectionSubtitle: Record<ForumSection, string> = {
    home: "İzlədiyin istifadəçi və icmalardan gələn post və müzakirələr",
    discussions: "Bütün açıq mövzuları tarix, kateqoriya və axtarışla araşdır",
    popular: "İcmalarda ən çox reaksiya və cavab alan müzakirələr",
    communities: "Oyun mövzuları ətrafında qurulan məkanlar",
    explore: "Hələ izləmədiyin, amma marağına uyğun ola biləcək məzmun",
  };

  const showSearch = activeSection !== "communities";
  const showCategoryFilter =
    activeSection === "discussions" || activeSection === "popular";

  return (
    <div className="forum-theme min-h-screen text-zinc-100">
      <Navbar />

      <div className="relative flex min-h-[calc(100vh-72px)]">
        <div className="pointer-events-none fixed inset-0 top-[72px] overflow-hidden">
          <div className="absolute -right-40 top-0 h-[34rem] w-[34rem] rounded-full bg-teal-400/[0.055] blur-3xl" />
          <div className="absolute bottom-0 left-1/3 h-[28rem] w-[28rem] rounded-full bg-violet-500/[0.045] blur-3xl" />
        </div>

        <ForumSidebar
          activeSection={activeSection}
          onSectionChange={changeSection}
          onCreateCommunity={
            isAuthenticated
              ? () => setIsCreateCommunityOpen(true)
              : undefined
          }
        />

        <main className="relative min-w-0 flex-1">
          <div className="mx-auto max-w-[1180px] px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
            <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_300px]">
              <section className="min-w-0">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                  <div>
                    <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-teal-300/70">
                      Forum
                    </p>
                    <h1 className="mt-1.5 text-xl font-bold text-white">
                      {sectionTitle[activeSection]}
                    </h1>
                    <p className="mt-1 max-w-2xl text-xs leading-5 text-zinc-600">
                      {sectionSubtitle[activeSection]}
                    </p>
                  </div>

                  <div className="flex flex-wrap items-center gap-2">
                    {showSearch && (
                      <div className="relative">
                        <Search
                          size={15}
                          className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-600"
                        />
                        <input
                          type="search"
                          value={searchInput}
                          onChange={(event) => setSearchInput(event.target.value)}
                          placeholder="Bu bölmədə axtar..."
                          className="h-10 w-full rounded-xl border border-white/[0.075] bg-white/[0.025] pl-9 pr-3 text-xs text-white outline-none transition placeholder:text-zinc-700 focus:border-teal-300/25 sm:w-56"
                        />
                      </div>
                    )}

                    {isAuthenticated &&
                      activeSection === "discussions" && (
                        <button
                          type="button"
                          onClick={() => setIsCreateModalOpen(true)}
                          className="flex h-10 items-center gap-2 rounded-xl bg-teal-300 px-3.5 text-xs font-black text-[#07100f] transition hover:bg-teal-200"
                        >
                          <Plus size={15} />
                          Müzakirə yarat
                        </button>
                      )}

                    {isAuthenticated &&
                      activeSection === "communities" && (
                        <button
                          type="button"
                          onClick={() => setIsCreateCommunityOpen(true)}
                          className="flex h-10 items-center gap-2 rounded-xl bg-teal-300 px-3.5 text-xs font-black text-[#07100f] transition hover:bg-teal-200"
                        >
                          <UsersRound size={15} />
                          Öz icmanı yarat
                        </button>
                      )}
                  </div>
                </div>

                {showCategoryFilter && (
                  <div className="mt-4 flex gap-2 overflow-x-auto pb-1">
                    <button
                      type="button"
                      onClick={() => changeCategory("")}
                      className={`shrink-0 rounded-xl border px-3 py-2 text-xs font-semibold transition ${
                        selectedCategory === ""
                          ? "border-teal-300/25 bg-teal-300/[0.09] text-teal-200"
                          : "border-white/[0.07] bg-white/[0.025] text-zinc-500 hover:text-zinc-200"
                      }`}
                    >
                      Hamısı
                    </button>
                    {categories.map((category) => (
                      <button
                        key={category.id}
                        type="button"
                        onClick={() => changeCategory(category.slug)}
                        className={`shrink-0 rounded-xl border px-3 py-2 text-xs font-semibold transition ${
                          selectedCategory === category.slug
                            ? "border-teal-300/25 bg-teal-300/[0.09] text-teal-200"
                            : "border-white/[0.07] bg-white/[0.025] text-zinc-500 hover:text-zinc-200"
                        }`}
                      >
                        {category.name}
                      </button>
                    ))}
                  </div>
                )}

                {activeSection === "popular" && (
                  <div className="mt-3 flex flex-wrap gap-2">
                    {popularPeriods.map((period) => (
                      <button
                        key={period.value}
                        type="button"
                        onClick={() => changePopularPeriod(period.value)}
                        className={`rounded-xl px-3 py-1.5 text-[11px] font-bold transition ${
                          popularPeriod === period.value
                            ? "bg-amber-300/10 text-amber-200"
                            : "text-zinc-600 hover:bg-white/[0.035] hover:text-zinc-300"
                        }`}
                      >
                        {period.label}
                      </button>
                    ))}
                  </div>
                )}

                {activeSection === "home" && !isAuthenticated ? (
                  <div className="mt-5 rounded-[24px] border border-teal-300/10 bg-gradient-to-br from-teal-300/[0.07] to-violet-400/[0.05] p-7 text-center">
                    <LogIn
                      size={28}
                      className="mx-auto text-teal-300"
                    />
                    <h2 className="mt-4 text-lg font-bold text-white">
                      Ana lent izlədiklərinə əsaslanır
                    </h2>
                    <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-zinc-500">
                      İzlədiyin istifadəçilərin yeni post və müzakirələrini, həmçinin izlədiyin və ya üzv olduğun icmalardakı yeni müzakirələri görmək üçün hesabına daxil ol.
                    </p>
                    <Link
                      to="/login"
                      className="forum-login-cta mt-5 inline-flex h-10 items-center rounded-xl px-4 text-xs font-black transition"
                    >
                      Daxil ol
                    </Link>
                  </div>
                ) : activeSection === "discussions" ? (
                  <div className="mt-4 space-y-3">
                    {discussionsQuery.isLoading ? (
                      <ForumFeedSkeleton />
                    ) : discussionsQuery.isError ? (
                      <EmptyFeed
                        title="Müzakirələr yüklənmədi"
                        description="Sorğunu yenidən yoxlamaq üçün səhifəni təzələ."
                      />
                    ) : (discussionsQuery.data?.items.length ?? 0) === 0 ? (
                      <EmptyFeed
                        title="Uyğun müzakirə tapılmadı"
                        description="Axtarışı və ya kateqoriya filtrini dəyişərək yenidən yoxla."
                      />
                    ) : (
                      discussionsQuery.data?.items.map((discussion) => (
                        <DiscussionFeedCard
                          key={discussion.id}
                          discussion={discussion}
                          isAuthenticated={isAuthenticated}
                        />
                      ))
                    )}
                    <Pagination
                      page={page}
                      totalPages={discussionsQuery.data?.totalPages ?? 0}
                      onPageChange={changePage}
                    />
                  </div>
                ) : activeSection === "communities" ? (
                  <div className="mt-5 grid gap-3 lg:grid-cols-2">
                    {communitiesQuery.isLoading ? (
                      <ForumFeedSkeleton />
                    ) : communities.length === 0 ? (
                      <div className="col-span-full">
                        <EmptyFeed
                          title="Hələ heç bir icma yaradılmayıb"
                          description="İlk icmanı yaradaraq oyunçuları bir mövzu ətrafında topla."
                        />
                      </div>
                    ) : (
                      communities.map((community) => (
                        <CommunityCard
                          key={community.id}
                          community={community}
                        />
                      ))
                    )}
                  </div>
                ) : (
                  <div className="mt-5">
                    {activeSection === "explore" &&
                      (activeFeed?.suggestedCommunities.length ||
                        activeFeed?.suggestedUsers.length) ? (
                        <div className="mb-5 space-y-4 rounded-[24px] border border-violet-300/10 bg-violet-500/[0.035] p-4 sm:p-5">
                          {(activeFeed?.suggestedCommunities.length ?? 0) >
                            0 && (
                            <div>
                              <div className="flex items-center gap-2">
                                <UsersRound
                                  size={16}
                                  className="text-teal-300"
                                />
                                <h2 className="text-sm font-bold text-white">
                                  Kəşf edə biləcəyin icmalar
                                </h2>
                              </div>
                              <div className="mt-3 grid gap-2 md:grid-cols-2">
                                {activeFeed?.suggestedCommunities
                                  .slice(0, 4)
                                  .map((community) => (
                                    <CommunityCard
                                      key={community.id}
                                      community={community}
                                      compact
                                    />
                                  ))}
                              </div>
                            </div>
                          )}

                          {(activeFeed?.suggestedUsers.length ?? 0) > 0 && (
                            <div className="border-t border-white/[0.06] pt-4">
                              <div className="flex items-center gap-2">
                                <UserPlus
                                  size={16}
                                  className="text-violet-300"
                                />
                                <h2 className="text-sm font-bold text-white">
                                  İzləyə biləcəyin istifadəçilər
                                </h2>
                              </div>
                              <div className="mt-3 flex gap-2 overflow-x-auto pb-1">
                                {activeFeed?.suggestedUsers.map(
                                  (suggestion) => {
                                    const avatarUrl = resolveAssetUrl(
                                      suggestion.profileImageUrl,
                                    );
                                    return (
                                      <Link
                                        key={suggestion.userId}
                                        to={`/users/${encodeURIComponent(
                                          suggestion.userName,
                                        )}`}
                                        className="flex min-w-[190px] items-center gap-3 rounded-2xl border border-white/[0.07] bg-white/[0.025] p-3 transition hover:border-violet-300/20"
                                      >
                                        <span className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-violet-400/10 text-sm font-black text-violet-200">
                                          {avatarUrl ? (
                                            <img
                                              src={avatarUrl}
                                              alt=""
                                              className="h-full w-full object-cover"
                                            />
                                          ) : (
                                            suggestion.userName
                                              .charAt(0)
                                              .toUpperCase()
                                          )}
                                        </span>
                                        <span className="min-w-0">
                                          <span className="block truncate text-xs font-bold text-zinc-200">
                                            {suggestion.userName}
                                          </span>
                                          <span className="mt-1 block text-[10px] text-zinc-600">
                                            {suggestion.reason}
                                          </span>
                                        </span>
                                      </Link>
                                    );
                                  },
                                )}
                              </div>
                            </div>
                          )}
                        </div>
                      ) : null}

                    <div className="space-y-3">
                      {activeFeedQuery.isLoading ? (
                        <ForumFeedSkeleton />
                      ) : activeFeedQuery.isError ? (
                        <EmptyFeed
                          title="Lent yüklənmədi"
                          description="Server sorğusu tamamlanmadı. Səhifəni təzələyərək yenidən yoxla."
                        />
                      ) : (activeFeed?.items.length ?? 0) === 0 ? (
                        <EmptyFeed
                          title={
                            activeSection === "home"
                              ? "İzlədiklərində yeni paylaşım yoxdur"
                              : activeSection === "popular"
                                ? "Bu müddətdə populyar icma mövzusu yoxdur"
                                : "Kəşf üçün uyğun məzmun tapılmadı"
                          }
                          description={
                            activeSection === "home"
                              ? "Yeni istifadəçi və icmaları izlədikcə bu lent dolacaq."
                              : "Axtarışı və filtrləri dəyişərək yenidən yoxla."
                          }
                        />
                      ) : (
                        activeFeed?.items.map((item, index) => (
                          <FeedItem
                            key={`${item.itemType}-${
                              item.post?.id ?? item.discussion?.id ?? index
                            }`}
                            item={item}
                            isAuthenticated={isAuthenticated}
                            showReason={activeSection === "explore"}
                          />
                        ))
                      )}
                    </div>
                    <Pagination
                      page={page}
                      totalPages={activeFeed?.totalPages ?? 0}
                      onPageChange={changePage}
                    />
                  </div>
                )}
              </section>

              <aside className="space-y-4">
                <div className="rounded-[22px] border border-white/[0.075] bg-[#0d1716]/80 p-5">
                  <div className="flex items-center gap-2">
                    <Flame size={16} className="text-amber-300" />
                    <h2 className="text-sm font-bold text-white">
                      Aktiv icmalar
                    </h2>
                  </div>
                  <div className="mt-4 space-y-2">
                    {communities.slice(0, 4).map((community) => (
                      <CommunityCard
                        key={community.id}
                        community={community}
                        compact
                      />
                    ))}
                  </div>
                  <button
                    type="button"
                    onClick={() => changeSection("communities")}
                    className="mt-3 flex w-full items-center justify-center gap-2 rounded-xl border border-white/[0.07] py-2.5 text-xs font-bold text-zinc-400 transition hover:bg-white/[0.035] hover:text-white"
                  >
                    Bütün icmalar
                    <ChevronRight size={14} />
                  </button>
                </div>

                <div className="rounded-[22px] border border-violet-300/10 bg-gradient-to-br from-violet-500/[0.09] via-[#101719] to-teal-300/[0.05] p-5">
                  <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-violet-400/10 text-violet-300">
                    <Sparkles size={17} />
                  </span>
                  <h2 className="mt-4 text-sm font-bold text-white">
                    Lentlərin fərqi
                  </h2>
                  <div className="mt-3 space-y-2 text-[11px] leading-5 text-zinc-500">
                    <p>
                      <strong className="text-zinc-300">Ana lent:</strong>{" "}
                      izlədiyin istifadəçilər və izlədiyin və ya üzv olduğun icmalardakı yeniliklər.
                    </p>
                    <p>
                      <strong className="text-zinc-300">Populyar:</strong>{" "}
                      icmalarda trend olan mövzular.
                    </p>
                    <p>
                      <strong className="text-zinc-300">Kəşf:</strong>{" "}
                      izləmədiyin yeni məzmun və insanlar.
                    </p>
                  </div>
                  {isAuthenticated && user && interestsQuery.isSuccess && !interestProfile && (
                    <button
                      type="button"
                      onClick={() => {
                        setOnboardingDismissed(false);
                        setIsOnboardingRequested(true);
                      }}
                      className="mt-4 text-xs font-bold text-teal-300"
                    >
                      Maraqlarımı seç
                    </button>
                  )}
                </div>
              </aside>
            </div>
          </div>
        </main>
      </div>

      {user && (
        <ForumInterestOnboarding
          open={
            shouldShowOnboarding ||
            (isAuthenticated && isOnboardingRequested)
          }
          onComplete={(profile: ForumInterestProfileDTO) => {
            queryClient.setQueryData(
              ["forum", "interests", user.id],
              profile,
            );
            setOnboardingCompleted(true);
            setIsOnboardingRequested(false);
          }}
          onDismiss={() => {
            setOnboardingDismissed(true);
            setIsOnboardingRequested(false);
          }}
        />
      )}

      <CreateDiscussionModal
        open={isCreateModalOpen && activeSection === "discussions"}
        categories={categories}
        communities={myCommunitiesQuery.data ?? []}
        onClose={() => setIsCreateModalOpen(false)}
        onCreated={(discussion) => {
          setIsCreateModalOpen(false);
          navigate(`/forum/discussions/${discussion.slug}`);
        }}
      />

      <CreateCommunityModal
        open={isCreateCommunityOpen}
        onClose={() => setIsCreateCommunityOpen(false)}
        onCreated={(community) => {
          setIsCreateCommunityOpen(false);
          navigate(getCommunityPath(community.slug));
        }}
      />
    </div>
  );
}
