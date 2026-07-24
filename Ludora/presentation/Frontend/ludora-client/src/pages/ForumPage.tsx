import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ArrowUpRight,
  ChevronRight,
  Flame,
  Heart,
  MessageCircle,
  Plus,
  Search,
  Sparkles,
  UsersRound,
} from "lucide-react";
import { useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { CreateDiscussionModal } from "../components/forum/CreateDiscussionModal";
import { CreateCommunityModal } from "../components/forum/CreateCommunityModal";
import { ForumContentMenu } from "../components/forum/ForumContentMenu";
import { ForumInterestOnboarding } from "../components/forum/ForumInterestOnboarding";
import { ForumMediaGallery } from "../components/forum/ForumMediaGallery";
import {
  ForumSidebar,
  type ForumSection,
} from "../components/forum/ForumSidebar";
import { Navbar } from "../components/layout/Navbar";
import {
  formatRelativeDate,
  resolveAssetUrl,
} from "../lib/helpers";
import {
  getForumDiscussions,
  setDiscussionLike,
  type ForumFeedSort,
} from "../services/forumService";
import {
  getForumCommunities,
  getMyForumCommunities,
} from "../services/forumCommunityService";
import { getForumCategories } from "../services/homeService";
import { getMyUserGames } from "../services/userGamesService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../stores/authStore";
import type {
  DiscussionListItemDTO,
  PagedResultDTO,
} from "../types/home";
import type {
  ForumCommunityCategory,
  ForumCommunityDTO,
} from "../types/community";

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

function getCommunityPath(slug: string) {
  return `/forum/communities/${encodeURIComponent(slug.trim())}`;
}

function CommunityCard({
  community,
}: {
  community: ForumCommunityDTO;
}) {
  const iconUrl = resolveAssetUrl(community.iconUrl);
  const coverImageUrl = resolveAssetUrl(community.coverImageUrl);

  return (
    <Link
      to={getCommunityPath(community.slug)}
      className="group flex h-full min-w-0 flex-col overflow-hidden rounded-[22px] border border-white/[0.075] bg-[#0d1716]/80 text-left transition hover:-translate-y-0.5 hover:border-teal-200/15 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-300/40"
      aria-label={`${community.name} icmasını aç`}
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

        <div className="mt-3 min-w-0">
          <p className="truncate font-bold text-white">
            {community.name}
          </p>
          <p className="mt-1 text-[10px] font-semibold text-teal-300/70">
            {communityCategoryLabels[community.category]}
          </p>
        </div>

        <p className="mt-2 line-clamp-2 min-h-10 text-xs leading-5 text-zinc-600">
          {community.description}
        </p>

        <div className="mt-auto flex items-center justify-between gap-3 pt-4 text-[11px]">
          <span className="truncate text-zinc-600">
            {community.memberCount} üzv · {community.discussionCount} müzakirə
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

function DiscussionFeedCard({
  discussion,
  isAuthenticated,
}: {
  discussion: DiscussionListItemDTO;
  isAuthenticated: boolean;
}) {
  const queryClient = useQueryClient();
  const avatarUrl = resolveAssetUrl(
    discussion.communityIconUrl ??
      discussion.authorProfileImageUrl,
  );
  const displayName =
    discussion.communityName ?? discussion.authorUserName;
  const likeMutation = useMutation({
    mutationFn: (isLiked: boolean) =>
      setDiscussionLike(discussion.id, isLiked),
    onMutate: async (isLiked) => {
      await queryClient.cancelQueries({
        queryKey: ["forum", "feed"],
      });

      const previousFeeds = queryClient.getQueriesData<
        PagedResultDTO<DiscussionListItemDTO>
      >({
        queryKey: ["forum", "feed"],
      });

      queryClient.setQueriesData<
        PagedResultDTO<DiscussionListItemDTO>
      >(
        { queryKey: ["forum", "feed"] },
        (currentFeed) => {
          if (!currentFeed) return currentFeed;

          return {
            ...currentFeed,
            items: currentFeed.items.map((item) =>
              item.id === discussion.id
                ? {
                    ...item,
                    isLikedByCurrentUser: isLiked,
                    likeCount: Math.max(
                      0,
                      item.likeCount + (isLiked ? 1 : -1),
                    ),
                  }
                : item,
            ),
          };
        },
      );

      return { previousFeeds };
    },
    onError: (_error, _isLiked, context) => {
      context?.previousFeeds.forEach(([queryKey, data]) => {
        queryClient.setQueryData(queryKey, data);
      });
    },
    onSettled: () => {
      void queryClient.invalidateQueries({
        queryKey: ["forum", "feed"],
      });
    },
  });

  return (
    <article className="group relative rounded-[22px] border border-white/[0.075] bg-[#0d1716]/80 p-4 transition hover:border-teal-200/15 hover:bg-[#101c1b] sm:p-5">
      <ForumContentMenu
        kind="discussion"
        contentId={discussion.id}
        contentLabel={discussion.title}
        authorId={discussion.authorId}
        authorUserName={discussion.authorUserName}
        initialIsActive={discussion.isSavedByCurrentUser}
        className="absolute right-3 top-3 sm:right-4 sm:top-4"
      />
      <div className="flex gap-3.5">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-2xl bg-gradient-to-br from-violet-500/25 to-teal-300/15 text-sm font-black text-teal-100">
          {avatarUrl ? (
            <img
              src={avatarUrl}
              alt=""
              className="h-full w-full object-cover"
            />
          ) : (
            displayName.charAt(0).toUpperCase()
          )}
        </div>

        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1 pr-9 text-xs">
            {discussion.communitySlug ? (
              <Link
                to={`/forum/communities/${discussion.communitySlug}`}
                className="font-semibold text-teal-200 transition hover:text-teal-100"
              >
                {displayName}
              </Link>
            ) : (
              <span className="font-semibold text-zinc-300">
                {displayName}
              </span>
            )}
            <span className="text-zinc-700">•</span>
            <span className="text-zinc-600">
              {formatRelativeDate(discussion.createdAt)}
            </span>
            {discussion.isPinned && (
              <span className="rounded-full bg-teal-300/10 px-2 py-0.5 font-semibold text-teal-300">
                Bərkidilib
              </span>
            )}
          </div>

          <Link
            to={`/forum/discussions/${discussion.slug}`}
            className="mt-2 block text-base font-bold leading-6 text-zinc-100 transition group-hover:text-white sm:text-lg"
          >
            {discussion.title}
          </Link>

          {discussion.contentPreview && (
            <p className="mt-2 line-clamp-3 text-sm leading-6 text-zinc-500">
              {discussion.contentPreview}
            </p>
          )}

          <ForumMediaGallery
            media={discussion.media}
            title={discussion.title}
            compact
          />

          <div className="mt-2.5">
            <span className="rounded-lg border border-white/[0.07] bg-white/[0.035] px-2.5 py-1 text-[11px] font-semibold text-zinc-500">
              {discussion.categoryName}
            </span>
          </div>

          <div className="mt-4 flex items-center gap-1">
            <button
              type="button"
              disabled={
                !isAuthenticated || likeMutation.isPending
              }
              aria-pressed={discussion.isLikedByCurrentUser}
              title={
                isAuthenticated
                  ? discussion.isLikedByCurrentUser
                    ? "Bəyənməni geri götür"
                    : "Müzakirəni bəyən"
                  : "Bəyənmək üçün hesabınıza daxil olun"
              }
              onClick={(event) => {
                event.preventDefault();
                event.stopPropagation();
                likeMutation.mutate(
                  !discussion.isLikedByCurrentUser,
                );
              }}
              className={`flex items-center gap-1.5 rounded-xl px-2.5 py-1.5 text-xs transition ${
                discussion.isLikedByCurrentUser
                  ? "bg-rose-400/10 text-rose-300"
                  : "text-zinc-600 hover:bg-white/[0.045] hover:text-zinc-300"
              } disabled:cursor-not-allowed disabled:opacity-50`}
            >
              <Heart
                size={14}
                fill={
                  discussion.isLikedByCurrentUser
                    ? "currentColor"
                    : "none"
                }
              />
              {discussion.likeCount}
            </button>
            <Link
              to={`/forum/discussions/${discussion.slug}`}
              className="flex items-center gap-1.5 rounded-xl px-2.5 py-1.5 text-xs text-zinc-600 transition hover:bg-white/[0.045] hover:text-zinc-300"
            >
              <MessageCircle size={14} />
              {discussion.replyCount} cavab
            </Link>
            <span className="ml-auto text-[11px] text-zinc-700">
              {discussion.viewCount} baxış
            </span>
          </div>
        </div>
      </div>
    </article>
  );
}

function FeedSkeleton() {
  return (
    <div className="space-y-3">
      {Array.from({ length: 5 }).map((_, index) => (
        <div
          key={index}
          className="h-44 animate-pulse rounded-[22px] border border-white/[0.05] bg-white/[0.025]"
        />
      ))}
    </div>
  );
}

export function ForumPage() {
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const accessToken = useAuthStore((state) => state.accessToken);
  const expiresAtUtc = useAuthStore(
    (state) => state.expiresAtUtc,
  );
  const isAuthenticated =
    user !== null &&
    isAuthSessionValid(accessToken, expiresAtUtc);

  const [activeSection, setActiveSection] =
    useState<ForumSection>("home");
  const [selectedCategory, setSelectedCategory] = useState("");
  const [feedSearch, setFeedSearch] = useState("");
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

  const userGamesQuery = useQuery({
    queryKey: ["forum", "user-game-count", user?.id],
    queryFn: ({ signal }) => getMyUserGames(1, 1, signal),
    enabled: isAuthenticated,
  });

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

  const sortBy: ForumFeedSort =
    activeSection === "popular" ? "popular" : "newest";

  const discussionsQuery = useQuery({
    queryKey: [
      "forum",
      "feed",
      sortBy,
      selectedCategory,
      activeSection,
    ],
    queryFn: ({ signal }) =>
      getForumDiscussions(sortBy, selectedCategory, signal),
    enabled:
      activeSection !== "communities" &&
      activeSection !== "explore",
  });

  function changeSection(section: ForumSection) {
    setIsCreateModalOpen(false);
    setIsCreateCommunityOpen(false);
    setActiveSection(section);
    setSelectedCategory("");
    setFeedSearch("");
  }

  const storedInterests = user
    ? localStorage.getItem(
        `ludora-forum-interests:${user.id}`,
      )
    : null;

  const shouldShowOnboarding =
    isAuthenticated &&
    userGamesQuery.isSuccess &&
    userGamesQuery.data.totalCount < 25 &&
    !storedInterests &&
    !onboardingCompleted &&
    !onboardingDismissed;

  const categories = categoriesQuery.data ?? [];
  const communities = communitiesQuery.data ?? [];
  const filteredDiscussions = useMemo(() => {
    const discussions = discussionsQuery.data?.items ?? [];
    const normalizedSearch = feedSearch.trim().toLocaleLowerCase(
      "az-AZ",
    );
    if (!normalizedSearch) return discussions;
    return discussions.filter((discussion) =>
      discussion.title
        .toLocaleLowerCase("az-AZ")
        .includes(normalizedSearch),
    );
  }, [discussionsQuery.data?.items, feedSearch]);

  const sectionTitle = {
    home: "Sənin üçün",
    discussions: "Ən yeni müzakirələr",
    popular: "İcmanın gündəmi",
    communities: "Oyun icmaları",
    explore: "Yeni mövzular kəşf et",
  }[activeSection];

  return (
    <div className="min-h-screen bg-[#07100f] text-zinc-100">
      <Navbar />

      <div className="relative flex min-h-[calc(100vh-72px)]">
        <div className="pointer-events-none fixed inset-0 top-[72px] overflow-hidden">
          <div className="absolute -right-40 top-0 h-[34rem] w-[34rem] rounded-full bg-teal-400/[0.055] blur-3xl" />
          <div className="absolute bottom-0 left-1/3 h-[28rem] w-[28rem] rounded-full bg-violet-500/[0.045] blur-3xl" />
          <div
            className="absolute inset-0 opacity-[0.035]"
            style={{
              backgroundImage:
                "radial-gradient(circle, rgba(153,246,228,.9) 1px, transparent 1px)",
              backgroundSize: "28px 28px",
            }}
          />
        </div>

        <ForumSidebar
          activeSection={activeSection}
          onSectionChange={changeSection}
        />

        <main className="relative min-w-0 flex-1">
          <div className="mx-auto max-w-[1180px] px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
            <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_300px]">
              <section className="min-w-0">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                  <div>
                    <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-teal-300/70">
                      Canlı lent
                    </p>
                    <h2 className="mt-1.5 text-xl font-bold text-white">
                      {sectionTitle}
                    </h2>
                  </div>

                  <div className="flex flex-wrap items-center gap-2">
                    {activeSection !== "communities" &&
                      activeSection !== "explore" && (
                      <div className="relative">
                        <Search
                          size={15}
                          className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-600"
                        />
                        <input
                          type="search"
                          value={feedSearch}
                          onChange={(event) =>
                            setFeedSearch(event.target.value)
                          }
                          placeholder="Lentdə axtar..."
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

                {(activeSection === "home" ||
                  activeSection === "discussions" ||
                  activeSection === "popular") && (
                  <>
                    <div className="mt-4 flex gap-2 overflow-x-auto pb-1">
                      <button
                        type="button"
                        onClick={() => setSelectedCategory("")}
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
                          onClick={() =>
                            setSelectedCategory(category.slug)
                          }
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

                    <div className="mt-4 space-y-3">
                      {discussionsQuery.isLoading ? (
                        <FeedSkeleton />
                      ) : discussionsQuery.isError ? (
                        <div className="rounded-[22px] border border-rose-400/15 bg-rose-400/[0.04] p-8 text-center">
                          <p className="text-sm font-semibold text-rose-200">
                            Müzakirələr yüklənmədi
                          </p>
                          <button
                            type="button"
                            onClick={() =>
                              discussionsQuery.refetch()
                            }
                            className="mt-3 text-xs font-bold text-teal-300"
                          >
                            Yenidən yoxla
                          </button>
                        </div>
                      ) : filteredDiscussions.length === 0 ? (
                        <div className="rounded-[22px] border border-dashed border-white/10 bg-white/[0.02] p-10 text-center">
                          <MessageCircle
                            size={26}
                            className="mx-auto text-zinc-700"
                          />
                          <p className="mt-3 text-sm font-semibold text-zinc-400">
                            Burada hələ müzakirə yoxdur
                          </p>
                          <p className="mt-1 text-xs text-zinc-600">
                            İlk mövzunu sən başlat.
                          </p>
                        </div>
                      ) : (
                        filteredDiscussions.map((discussion) => (
                          <DiscussionFeedCard
                            key={discussion.id}
                            discussion={discussion}
                            isAuthenticated={isAuthenticated}
                          />
                        ))
                      )}
                    </div>
                  </>
                )}

                {activeSection === "communities" && (
                  <div className="mt-5 grid gap-3 lg:grid-cols-2">
                    {communitiesQuery.isLoading ? (
                      <FeedSkeleton />
                    ) : communities.length === 0 ? (
                      <div className="col-span-full rounded-[22px] border border-dashed border-white/10 bg-white/[0.02] p-10 text-center">
                        <UsersRound
                          size={28}
                          className="mx-auto text-zinc-700"
                        />
                        <p className="mt-3 text-sm font-semibold text-zinc-400">
                          Hələ heç bir icma yaradılmayıb
                        </p>
                        <p className="mt-1 text-xs text-zinc-600">
                          İlk icmanı yaradaraq insanları bir mövzu ətrafında topla.
                        </p>
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
                )}

                {activeSection === "explore" && (
                  <div className="mt-5 rounded-[24px] border border-white/[0.075] bg-[#0d1716]/80 p-6">
                    <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-violet-400/10 text-violet-300">
                      <Sparkles size={21} />
                    </span>
                    <h3 className="mt-5 text-lg font-bold text-white">
                      Kəşf bölməsi hazırlanır
                    </h3>
                    <p className="mt-2 max-w-lg text-sm leading-6 text-zinc-500">
                      Oyun, janr və platformalara görə yeni müzakirələri
                      burada kəşf edə biləcəksən. İndilik icmalara göz at.
                    </p>
                    <button
                      type="button"
                      onClick={() => changeSection("communities")}
                      className="mt-5 flex items-center gap-2 text-sm font-bold text-teal-300"
                    >
                      İcmalara bax
                      <ArrowUpRight size={16} />
                    </button>
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
                      <Link
                        key={community.id}
                        to={getCommunityPath(community.slug)}
                        className="group flex w-full items-center gap-3 rounded-2xl p-2 text-left transition hover:bg-white/[0.035]"
                      >
                        <span className="flex h-9 w-9 items-center justify-center overflow-hidden rounded-xl bg-teal-300/10 text-xs font-black text-teal-100">
                          {community.iconUrl ? (
                            <img
                              src={resolveAssetUrl(community.iconUrl) ?? ""}
                              alt=""
                              className="h-full w-full object-cover"
                            />
                          ) : (
                            community.name.charAt(0).toUpperCase()
                          )}
                        </span>
                        <span className="min-w-0 flex-1">
                          <span className="block truncate text-xs font-semibold text-zinc-300">
                            {community.name}
                          </span>
                          <span className="mt-0.5 block text-[10px] text-zinc-600">
                            {community.memberCount} üzv
                          </span>
                        </span>
                        <ChevronRight
                          size={14}
                          className="text-zinc-700 group-hover:text-teal-300"
                        />
                      </Link>
                    ))}
                  </div>
                  <button
                    type="button"
                    onClick={() => changeSection("communities")}
                    className="mt-3 flex w-full items-center justify-center gap-2 rounded-xl border border-white/[0.07] py-2.5 text-xs font-bold text-zinc-400 transition hover:bg-white/[0.035] hover:text-white"
                  >
                    Bütün icmalar
                    <ArrowUpRight size={14} />
                  </button>
                </div>

                {(!isAuthenticated || !storedInterests) && (
                  <div className="rounded-[22px] border border-violet-300/10 bg-gradient-to-br from-violet-500/[0.09] via-[#101719] to-teal-300/[0.05] p-5">
                    <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-violet-400/10 text-violet-300">
                      <Sparkles size={17} />
                    </span>
                    <h2 className="mt-4 text-sm font-bold text-white">
                      Daha şəxsi lent
                    </h2>
                    <p className="mt-2 text-xs leading-5 text-zinc-500">
                      Maraqlarını seçdikcə Ludora sənin üçün daha uyğun
                      mövzular göstərəcək.
                    </p>
                    {isAuthenticated && user && (
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
                )}
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
          userId={user.id}
          onComplete={() => {
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
        open={
          isCreateModalOpen &&
          activeSection === "discussions"
        }
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
