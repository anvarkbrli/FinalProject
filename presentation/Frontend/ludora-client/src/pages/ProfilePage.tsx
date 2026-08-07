import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ArrowDown,
  ArrowUp,
  Ban,
  CalendarDays,
  Check,
  ChevronDown,
  ChevronRight,
  Clock3,
  Eye,
  EllipsisVertical,
  FileText,
  Flag,
  Gamepad2,
  Heart,
  ImagePlus,
  ListChecks,
  ListOrdered,
  LoaderCircle,
  LockKeyhole,
  MessageCircle,
  MessageSquareWarning,
  Pencil,
  Plus,
  Send,
  SlidersHorizontal,
  Star,
  Trash2,
  UserCheck,
  UserPlus,
  UserX,
  Users,
  X,
} from "lucide-react";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import {
  Link,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import { CopyShareButton } from "../components/common/CopyShareButton";
import { InteractionCommentsPanel } from "../components/interactions/InteractionCommentsPanel";
import { InteractionLikesModal } from "../components/interactions/InteractionLikesModal";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { useI18n } from "../i18n/useI18n";
import { GameListEditorModal } from "../components/lists/GameListEditorModal";
import { ProfileActivityFeed } from "../components/profile/ProfileActivityFeed";
import { ProfileDiscussions } from "../components/profile/ProfileDiscussions";
import { FollowRequestsModal } from "../components/profile/FollowRequestsModal";
import { ProfileGamesGallery } from "../components/profile/ProfileGamesGallery";
import { ProfileOverview } from "../components/profile/ProfileOverview";
import { ProfileSteamLibrary } from "../components/profile/ProfileSteamLibrary";
import { UserReportModal } from "../components/profile/UserReportModal";
import { ContentReportButton } from "../components/reporting/ContentReportButton";
import { ApiErrorStatusPage } from "../components/status/ApiErrorStatusPage";
import { SocialPlatformIcon } from "../components/profile/SocialPlatformIcon";
import { ReviewCommentsPanel } from "../components/reviews/ReviewCommentsPanel";
import { ReviewHardwareSummary } from "../components/reviews/ReviewHardwareSummary";
import { ReviewLikesModal } from "../components/reviews/ReviewLikesModal";
import { ProfileGameCollection } from "../components/profile/ProfileGameCollection";
import { SteamPlaytimeReconciliationModal } from "../components/steam/SteamPlaytimeReconciliationModal";
import { getApiErrorMessage } from "../lib/api";
import { hasAuthCapability } from "../lib/authRoles";
import { useConfirmation } from "../lib/confirmation";
import { socialPlatformLabels } from "../lib/socialPlatforms";
import {
  formatDate,
  formatRelativeDate,
  resolveAssetUrl,
} from "../lib/helpers";
import { createGameList } from "../services/gameListsService";
import { deleteAdminPost, warnAdminUser } from "../services/adminService";
import {
  likeContent,
  unlikeContent,
} from "../services/contentInteractionsService";
import {
  likeReview,
  unlikeReview,
} from "../services/reviewsService";
import {
  getSteamOwnedGames,
  syncSteamOwnedGames,
} from "../services/steamService";
import {
  blockUser,
  createPost,
  deletePost,
  followUser,
  getFollowers,
  getFollowing,
  getFollowStatus,
  getMyCollaboratingGameLists,
  getMyFeed,
  getUserDiscussions,
  getUserGameLists,
  getUserFavoriteGames,
  getUserGames,
  getUserProfileGames,
  getUserPlaytimeStatistics,
  getUserPosts,
  getUserProfile,
  getUserReviews,
  removeFollower,
  unfollowUser,
  updatePost,
} from "../services/profileService";
import { getFollowRequests } from "../services/settingsService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../stores/authStore";
import type {
  PostDTO,
  ProfileGameEntryDTO,
  ReviewDTO as ProfileReviewDTO,
  UserActivityFeedCategory,
  UserActivityFeedScope,
  UserListItemDTO,
  UserPlaytimePeriod,
  UserProfileGameSortBy,
} from "../types/profile";
import type { ReviewSort } from "../types/reviews";

type ProfileTab =
  | "overview"
  | "games"
  | "posts"
  | "discussions"
  | "lists"
  | "reviews"
  | "owned-games"
  | "wishlist"
  | "favorites"
  | "feed";

type ProfileGameStatus = NonNullable<
  ProfileGameEntryDTO["status"]
>;

type ConnectionModal =
  | "followers"
  | "following"
  | "requests"
  | null;

interface NoticeState {
  type: "success" | "error";
  message: string;
}

const profileTabs: Array<{
  id: ProfileTab;
  label: string;
  ownerOnly?: boolean;
}> = [
  { id: "overview", label: "Profil" },
  { id: "games", label: "Oyunlar" },
  { id: "posts", label: "Postlar" },
  { id: "discussions", label: "Müzakirələr" },
  { id: "lists", label: "Siyahılar" },
  { id: "reviews", label: "Rəylər" },
  { id: "owned-games", label: "Sahib olunanlar" },
  { id: "wishlist", label: "Wishlist", ownerOnly: true },
  { id: "favorites", label: "Sevimlilər", ownerOnly: true },
  { id: "feed", label: "Axın", ownerOnly: true },
];

type ReviewSortCategory =
  | "date"
  | "comments"
  | "rating";

type ReviewSortDirection = "asc" | "desc";

const reviewSortCategories: Array<{
  value: ReviewSortCategory;
  label: string;
  description: string;
}> = [
  {
    value: "date",
    label: "Tarix",
    description: "Rəylərin paylaşılma tarixinə görə",
  },
  {
    value: "comments",
    label: "Şərh sayı",
    description: "Rəylərə yazılan şərhlərin sayına görə",
  },
  {
    value: "rating",
    label: "Ulduz",
    description: "Dəqiq ulduz dəyərini slider ilə seç",
  },
];

const validReviewSorts: ReviewSort[] = [
  "Newest",
  "Oldest",
  "HighestRated",
  "LowestRated",
  "MostLiked",
  "LeastLiked",
  "MostCommented",
  "LeastCommented",
];

const reviewRatingSteps = [
  0.5,
  1,
  1.5,
  2,
  2.5,
  3,
  3.5,
  4,
  4.5,
  5,
];

function parseProfileTab(value: string | null): ProfileTab | null {
  return profileTabs.some((tab) => tab.id === value)
    ? (value as ProfileTab)
    : null;
}

function parseConnectionModal(value: string | null): ConnectionModal {
  return value === "followers" ||
    value === "following" ||
    value === "requests"
    ? value
    : null;
}


function parseProfileGameStatus(
  value: string | null,
): ProfileGameStatus | null {
  const validStatuses: ProfileGameStatus[] = [
    "Playing",
    "PlanToPlay",
    "Played",
    "Dropped",
  ];

  return validStatuses.includes(value as ProfileGameStatus)
    ? (value as ProfileGameStatus)
    : null;
}

function parseReviewSort(value: string | null): ReviewSort {
  return validReviewSorts.includes(value as ReviewSort)
    ? (value as ReviewSort)
    : "Newest";
}

function getReviewSortState(sort: ReviewSort): {
  category: ReviewSortCategory;
  direction: ReviewSortDirection;
} {
  switch (sort) {
    case "Oldest":
      return { category: "date", direction: "asc" };
    case "HighestRated":
      return { category: "rating", direction: "desc" };
    case "LowestRated":
      return { category: "rating", direction: "asc" };
    case "MostCommented":
      return { category: "comments", direction: "desc" };
    case "LeastCommented":
      return { category: "comments", direction: "asc" };
    default:
      return { category: "date", direction: "desc" };
  }
}

function resolveReviewSort(
  category: ReviewSortCategory,
  direction: ReviewSortDirection,
): ReviewSort {
  if (category === "comments") {
    return direction === "desc"
      ? "MostCommented"
      : "LeastCommented";
  }

  if (category === "rating") {
    return direction === "desc"
      ? "HighestRated"
      : "LowestRated";
  }

  return direction === "desc" ? "Newest" : "Oldest";
}

function parseReviewRating(value: string | null): number | null {
  if (!value) {
    return null;
  }

  const rating = Number(value);

  return reviewRatingSteps.includes(rating)
    ? rating
    : null;
}

function formatReviewRating(value: number): string {
  return Number.isInteger(value)
    ? value.toFixed(0)
    : value.toFixed(1);
}

function useObjectUrl(file: File | null): string | null {
  const objectUrl = useMemo(
    () => (file ? URL.createObjectURL(file) : null),
    [file],
  );

  useEffect(() => {
    return () => {
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [objectUrl]);

  return objectUrl;
}

function UserAvatar({
  userName,
  profileImageUrl,
  className,
}: {
  userName: string;
  profileImageUrl: string | null;
  className: string;
}) {
  const [failedImageUrl, setFailedImageUrl] =
    useState<string | null>(null);

  const imageUrl = resolveAssetUrl(
    profileImageUrl,
  );

  return (
    <span
      className={`flex shrink-0 items-center justify-center overflow-hidden bg-violet-500/15 font-bold text-violet-300 ${className}`}
    >
      {imageUrl && imageUrl !== failedImageUrl ? (
        <img
          src={imageUrl}
          alt={`${userName} profil şəkli`}
          className="h-full w-full object-cover"
          onError={() => {
            setFailedImageUrl(imageUrl);
          }}
        />
      ) : (
        userName.trim().charAt(0).toUpperCase() || "?"
      )}
    </span>
  );
}

function ProfileLoadingState() {
  return (
    <div className="profile-page min-h-screen wellplayd-page-bg text-[var(--wp-text)]">
      <Navbar />

      <main className="flex min-h-[70vh] items-center justify-center px-5">
        <div className="flex items-center gap-3 text-sm text-zinc-400">
          <LoaderCircle
            size={20}
            className="animate-spin"
          />
          Profil yüklənir...
        </div>
      </main>
    </div>
  );
}

export function ProfilePage() {
  const { language, t } = useI18n();
  const { userName = "" } = useParams();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const requestConfirmation = useConfirmation();

  const storedUser = useAuthStore(
    (state) => state.user,
  );
  const canDeleteAnyPost = hasAuthCapability(
    storedUser?.capabilities,
    "admin.posts.delete",
  );
  const canWarnUsers = hasAuthCapability(
    storedUser?.capabilities,
    "admin.users.warn",
  );
  const accessToken = useAuthStore(
    (state) => state.accessToken,
  );
  const expiresAtUtc = useAuthStore(
    (state) => state.expiresAtUtc,
  );
  const isAuthInitialized = useAuthStore(
    (state) => state.isInitialized,
  );

  const activeTab =
    parseProfileTab(searchParams.get("tab")) ?? "overview";
  const reviewSort = parseReviewSort(
    searchParams.get("sort"),
  );
  const reviewRating = parseReviewRating(
    searchParams.get("rating"),
  );
  const gameRatingFromUrl =
    activeTab === "games"
      ? parseReviewRating(searchParams.get("gameRating"))
      : null;
  const gameStatusFromUrl =
    activeTab === "games"
      ? parseProfileGameStatus(searchParams.get("gameStatus"))
      : null;
  const gamesStatus = gameStatusFromUrl;
  const gamesRating = gameRatingFromUrl;
  const reviewSortState = getReviewSortState(reviewSort);
  const activeReviewSortCategory: ReviewSortCategory =
    reviewRating !== null
      ? "rating"
      : reviewSortState.category;
  const connectionModal = parseConnectionModal(
    searchParams.get("connections"),
  );
  const setConnectionModal = useCallback(
    (value: ConnectionModal) => {
      const nextParams = new URLSearchParams(searchParams);

      if (value) {
        nextParams.set("connections", value);
      } else {
        nextParams.delete("connections");
      }

      setSearchParams(nextParams, { replace: true });
    },
    [searchParams, setSearchParams],
  );
  const [notice, setNotice] =
    useState<NoticeState | null>(null);
  const [newPostContent, setNewPostContent] =
    useState("");
  const [newPostImage, setNewPostImage] =
    useState<File | null>(null);
  const [editingPostId, setEditingPostId] =
    useState<number | null>(null);
  const [editingPostContent, setEditingPostContent] =
    useState("");
  const [editingPostImage, setEditingPostImage] =
    useState<File | null>(null);
  const [removeEditingPostImage, setRemoveEditingPostImage] =
    useState(false);
  const [isCreateListOpen, setIsCreateListOpen] =
    useState(false);
  const [revealedSpoilers, setRevealedSpoilers] =
    useState<Set<number>>(() => new Set());
  const [likesReview, setLikesReview] = useState<{
    id: number;
    count: number;
  } | null>(null);
  const [likesPost, setLikesPost] = useState<{
    id: number;
    count: number;
  } | null>(null);
  const [openPostCommentIds, setOpenPostCommentIds] =
    useState<Set<number>>(() => new Set());
  const [openReviewCommentIds, setOpenReviewCommentIds] =
    useState<Set<number>>(() => new Set());
  const [wishlistPage, setWishlistPage] =
    useState(1);
  const [favoritesPage, setFavoritesPage] =
    useState(1);
  const [discussionsPage, setDiscussionsPage] =
    useState(1);
  const [isFavoritesModalOpen, setIsFavoritesModalOpen] =
    useState(false);
  const [gamesPage, setGamesPage] = useState(1);
  const [gamesSearch, setGamesSearch] = useState("");
  const [gamesGenre, setGamesGenre] = useState<string | null>(null);
  const [gamesCompany, setGamesCompany] = useState<string | null>(null);
  const [gamesPlatform, setGamesPlatform] = useState<string | null>(null);
  const [gamesPlayedPlatform, setGamesPlayedPlatform] =
    useState<string | null>(null);
  const [gamesReleaseYear, setGamesReleaseYear] =
    useState<number | null>(null);
  const [gamesSortBy, setGamesSortBy] =
    useState<UserProfileGameSortBy>("RecentlyUpdated");
  const [isGamesFilterOpen, setIsGamesFilterOpen] = useState(false);
  const [playtimePeriod, setPlaytimePeriod] =
    useState<UserPlaytimePeriod>("Weekly");
  const [feedPage, setFeedPage] = useState(1);
  const [feedCategory, setFeedCategory] =
    useState<UserActivityFeedCategory>("All");
  const [feedScope, setFeedScope] =
    useState<UserActivityFeedScope>("Mine");
  const [isReviewFilterOpen, setIsReviewFilterOpen] =
    useState(false);
  const [isProfileActionOpen, setIsProfileActionOpen] =
    useState(false);
  const profileActionMenuRef = useRef<HTMLDivElement>(null);
  const [isReportOpen, setIsReportOpen] = useState(false);
  const [isWarningOpen, setIsWarningOpen] = useState(false);
  const [warningMessage, setWarningMessage] = useState("");
  const [isPlaytimeReconciliationOpen, setIsPlaytimeReconciliationOpen] =
    useState(false);

  const newPostImagePreview = useObjectUrl(newPostImage);
  const editingPostImagePreview = useObjectUrl(editingPostImage);

  useEffect(() => {
    if (!isProfileActionOpen) return;

    const handlePointerDown = (event: PointerEvent) => {
      if (
        profileActionMenuRef.current &&
        !profileActionMenuRef.current.contains(event.target as Node)
      ) {
        setIsProfileActionOpen(false);
      }
    };
    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsProfileActionOpen(false);
      }
    };

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleEscape);
    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleEscape);
    };
  }, [isProfileActionOpen]);

  useEffect(() => {
    if (!isFavoritesModalOpen) return;

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsFavoritesModalOpen(false);
      }
    };

    document.addEventListener("keydown", handleEscape);

    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", handleEscape);
    };
  }, [isFavoritesModalOpen]);

  const isAuthenticated =
    isAuthSessionValid(
      accessToken,
      expiresAtUtc,
    ) && storedUser !== null;

  const profileQuery = useQuery({
    queryKey: [
      "user-profile",
      userName,
      storedUser?.id ?? "anonymous",
    ],
    queryFn: ({ signal }) =>
      getUserProfile(userName, signal),
    enabled:
      isAuthInitialized &&
      userName.trim().length > 0,
  });

  const profile = profileQuery.data;
  const isOwnProfile =
    profile !== undefined &&
    storedUser?.id === profile.id;

  const followStatusQuery = useQuery({
    queryKey: [
      "follow-status",
      profile?.id,
      storedUser?.id ?? "anonymous",
    ],
    queryFn: ({ signal }) =>
      getFollowStatus(profile!.id, signal),
    enabled:
      isAuthInitialized &&
      isAuthenticated &&
      profile !== undefined &&
      !isOwnProfile,
  });

  const canViewProfileContent =
    profile !== undefined &&
    (profile.canViewContent === true ||
      !profile.isPrivate ||
      isOwnProfile ||
      followStatusQuery.data?.isFollowing === true);
  const requestedTab = profileTabs.find((tab) => tab.id === activeTab);
  const visibleActiveTab =
    !isOwnProfile && requestedTab?.ownerOnly
      ? "overview"
      : activeTab;

  const postsQuery = useQuery({
    queryKey: ["user-posts", profile?.id],
    queryFn: ({ signal }) =>
      getUserPosts(profile!.id, signal),
    enabled:
      canViewProfileContent &&
      visibleActiveTab === "posts",
  });

  const discussionsQuery = useQuery({
    queryKey: [
      "user-discussions",
      profile?.id,
      discussionsPage,
      storedUser?.id ?? "anonymous",
    ],
    queryFn: ({ signal }) =>
      getUserDiscussions(
        profile!.id,
        discussionsPage,
        20,
        signal,
      ),
    enabled:
      canViewProfileContent &&
      visibleActiveTab === "discussions",
  });

  const listsQuery = useQuery({
    queryKey: ["user-game-lists", profile?.id],
    queryFn: () =>
      getUserGameLists(profile!.id),
    enabled:
      canViewProfileContent &&
      visibleActiveTab === "lists",
  });

  const collaboratingListsQuery = useQuery({
    queryKey: ["my-collaborating-game-lists"],
    queryFn: getMyCollaboratingGameLists,
    enabled:
      canViewProfileContent &&
      isAuthenticated &&
      isOwnProfile &&
      visibleActiveTab === "lists",
  });

  const reviewsQuery = useQuery({
    queryKey: [
      "user-reviews",
      profile?.id,
      reviewSort,
      reviewRating,
    ],
    queryFn: () =>
      getUserReviews(profile!.id, {
        sortBy: reviewSort,
        rating: reviewRating,
        pageNumber: 1,
        pageSize: 100,
      }),
    enabled:
      canViewProfileContent &&
      visibleActiveTab === "reviews",
  });

  const profileGamesQuery = useQuery({
    queryKey: [
      "user-profile-games",
      profile?.id,
      gamesSearch,
      gamesGenre,
      gamesCompany,
      gamesPlatform,
      gamesPlayedPlatform,
      gamesStatus,
      gamesReleaseYear,
      gamesRating,
      gamesSortBy,
      gamesPage,
    ],
    queryFn: () =>
      getUserProfileGames(profile!.id, {
        search: gamesSearch.trim() || undefined,
        genre: gamesGenre ?? undefined,
        company: gamesCompany ?? undefined,
        platform: gamesPlatform ?? undefined,
        playedPlatform: gamesPlayedPlatform ?? undefined,
        status: gamesStatus,
        releaseYear: gamesReleaseYear,
        rating: gamesRating,
        sortBy: gamesSortBy,
        pageNumber: gamesPage,
        pageSize: 20,
      }),
    enabled:
      canViewProfileContent &&
      visibleActiveTab === "games",
  });

  const recentPlayedGamesQuery = useQuery({
    queryKey: ["user-profile-recent-played-games", profile?.id],
    queryFn: () =>
      getUserProfileGames(profile!.id, {
        status: "Played",
        sortBy: "RecentlyPlayed",
        pageNumber: 1,
        pageSize: 4,
      }),
    enabled:
      isAuthInitialized &&
      canViewProfileContent &&
      visibleActiveTab === "overview",
    staleTime: 2 * 60 * 1000,
    refetchOnMount: "always",
  });

  const recentFavoriteGamesQuery = useQuery({
    queryKey: [
      "user-profile-recent-favorite-games",
      profile?.id,
      storedUser?.id ?? "anonymous",
    ],
    queryFn: () =>
      getUserFavoriteGames(profile!.id, 1, 4),
    enabled:
      isAuthInitialized &&
      isAuthenticated &&
      canViewProfileContent &&
      visibleActiveTab === "overview",
    staleTime: 2 * 60 * 1000,
  });

  const playtimeStatisticsQuery = useQuery({
    queryKey: [
      "user-playtime-statistics",
      profile?.id,
      playtimePeriod,
    ],
    queryFn: () =>
      getUserPlaytimeStatistics(
        profile!.id,
        playtimePeriod,
      ),
    enabled:
      canViewProfileContent &&
      visibleActiveTab === "overview",
    staleTime: 2 * 60 * 1000,
  });

  const profileGamesOptionsQuery = useQuery({
    queryKey: ["user-profile-games-options", profile?.id],
    queryFn: () =>
      getUserProfileGames(profile!.id, {
        pageNumber: 1,
        pageSize: 50,
        sortBy: "NameAsc",
      }),
    enabled:
      canViewProfileContent &&
      visibleActiveTab === "games",
    staleTime: 5 * 60 * 1000,
  });

  const wishlistQuery = useQuery({
    queryKey: [
      "user-games",
      profile?.id,
      "wishlist",
      wishlistPage,
    ],
    queryFn: () =>
      getUserGames(profile!.id, {
        isWishlisted: true,
        pageNumber: wishlistPage,
        pageSize: 12,
      }),
    enabled:
      canViewProfileContent &&
      isOwnProfile &&
      visibleActiveTab === "wishlist",
  });

  const favoritesQuery = useQuery({
    queryKey: [
      "user-games",
      profile?.id,
      "favorites",
      favoritesPage,
      storedUser?.id ?? "anonymous",
    ],
    queryFn: () =>
      getUserFavoriteGames(
        profile!.id,
        favoritesPage,
        12,
      ),
    enabled:
      isAuthInitialized &&
      isAuthenticated &&
      canViewProfileContent &&
      (isFavoritesModalOpen ||
        (isOwnProfile && visibleActiveTab === "favorites")),
    refetchOnMount: "always",
  });

  const ownedGamesQuery = useQuery({
    queryKey: ["steam-owned-games", profile?.id],
    queryFn: () => getSteamOwnedGames(profile!.id, isOwnProfile),
    enabled:
      canViewProfileContent &&
      visibleActiveTab === "owned-games",
    staleTime: 2 * 60 * 1000,
  });

  const steamSyncMutation = useMutation({
    mutationFn: syncSteamOwnedGames,
    onSuccess: async (result) => {
      setNotice({
        type: "success",
        message: `Steam kitabxanası yeniləndi. ${result.createdAutoLogCount} yeni avto-log yaradıldı, ${result.updatedAutoLogCount} avto-log yeniləndi.`,
      });

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["steam-owned-games", profile?.id] }),
        queryClient.invalidateQueries({ queryKey: ["user-profile", userName] }),
        queryClient.invalidateQueries({ queryKey: ["user-profile-games", profile?.id] }),
        queryClient.invalidateQueries({ queryKey: ["user-profile-recent-played-games", profile?.id] }),
        queryClient.invalidateQueries({ queryKey: ["user-playtime-statistics", profile?.id] }),
        queryClient.invalidateQueries({ queryKey: ["user-reviews", profile?.id] }),
      ]);
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "Steam kitabxanasını sinxronlaşdırmaq mümkün olmadı.",
        ),
      });
    },
  });

  const feedQuery = useQuery({
    queryKey: [
      "user-feed",
      profile?.id,
      feedCategory,
      feedScope,
      feedPage,
    ],
    queryFn: () => getMyFeed(feedCategory, feedScope, feedPage, 20),
    enabled:
      canViewProfileContent &&
      isOwnProfile &&
      visibleActiveTab === "feed",
  });

  const connectionsQuery = useQuery({
    queryKey: [
      "user-connections",
      profile?.id,
      connectionModal,
    ],
    queryFn: ({ signal }) => {
      if (
        !profile ||
        (connectionModal !== "followers" &&
          connectionModal !== "following")
      ) {
        return Promise.resolve<UserListItemDTO[]>([]);
      }

      return connectionModal === "followers"
        ? getFollowers(profile.id, signal)
        : getFollowing(profile.id, signal);
    },
    enabled:
      canViewProfileContent &&
      (connectionModal === "followers" ||
        connectionModal === "following"),
  });

  const followRequestCountQuery = useQuery({
    queryKey: ["follow-requests", "count"],
    queryFn: ({ signal }) =>
      getFollowRequests({
        pageNumber: 1,
        pageSize: 1,
        signal,
      }),
    enabled: isOwnProfile && connectionModal === "followers",
    staleTime: 15_000,
  });

  const invalidateProfileData = async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ["user-profile", userName],
      }),
      queryClient.invalidateQueries({
        queryKey: [
          "follow-status",
          profile?.id,
        ],
      }),
      queryClient.invalidateQueries({
        queryKey: ["current-user"],
      }),
      queryClient.invalidateQueries({
        queryKey: ["user-connections"],
      }),
    ]);
  };

  const followMutation = useMutation({
    mutationFn: followUser,
    onSuccess: async () => {
      await invalidateProfileData();

      setNotice({
        type: "success",
        message: profile?.isPrivate
          ? "İzləmə sorğusu göndərildi."
          : "İstifadəçini izləməyə başladın.",
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "İzləmə əməliyyatı tamamlanmadı.",
        ),
      });
    },
  });

  const unfollowMutation = useMutation({
    mutationFn: unfollowUser,
    onSuccess: async () => {
      await invalidateProfileData();

      setNotice({
        type: "success",
        message:
          "İzləmə əlaqəsi və ya göndərilmiş sorğu ləğv edildi.",
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "İzləməni dayandırmaq mümkün olmadı.",
        ),
      });
    },
  });

  const blockMutation = useMutation({
    mutationFn: blockUser,
    onSuccess: () => {
      navigate("/", {
        replace: true,
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "İstifadəçini bloklamaq mümkün olmadı.",
        ),
      });
    },
  });

  const warningMutation = useMutation({
    mutationFn: (message: string) => warnAdminUser(profile!.id, message),
    onSuccess: async () => {
      setWarningMessage("");
      setIsWarningOpen(false);
      setNotice({
        type: "success",
        message:
          language === "en"
            ? "The warning was sent to the user."
            : "Xəbərdarlıq istifadəçiyə göndərildi.",
      });
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["admin", "user", profile?.id],
        }),
        queryClient.invalidateQueries({
          queryKey: ["admin", "user", profile?.id, "warnings"],
        }),
        queryClient.invalidateQueries({
          queryKey: ["admin", "user", profile?.id, "audit"],
        }),
      ]);
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          language === "en"
            ? "The warning could not be sent."
            : "Xəbərdarlıq göndərilmədi.",
        ),
      });
    },
  });

  const removeFollowerMutation = useMutation({
    mutationFn: removeFollower,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["user-profile", userName],
        }),
        queryClient.invalidateQueries({
          queryKey: ["user-connections"],
        }),
        queryClient.invalidateQueries({
          queryKey: ["current-user"],
        }),
      ]);

      setNotice({
        type: "success",
        message: "İstifadəçi izləyicilərindən çıxarıldı.",
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "İzləyicini çıxarmaq mümkün olmadı.",
        ),
      });
    },
  });

  const createPostMutation = useMutation({
    mutationFn: createPost,
    onSuccess: async () => {
      setNewPostContent("");
      setNewPostImage(null);
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["user-posts", profile?.id],
        }),
        queryClient.invalidateQueries({
          queryKey: ["user-profile", userName],
        }),
        queryClient.invalidateQueries({
          queryKey: ["current-user"],
        }),
      ]);

      setNotice({
        type: "success",
        message: "Post paylaşıldı.",
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "Postu paylaşmaq mümkün olmadı.",
        ),
      });
    },
  });

  const updatePostMutation = useMutation({
    mutationFn: ({
      postId,
      content,
      image,
      removeImage,
    }: {
      postId: number;
      content: string;
      image: File | null;
      removeImage: boolean;
    }) =>
      updatePost(postId, {
        content,
        image,
        removeImage,
      }),
    onSuccess: async () => {
      setEditingPostId(null);
      setEditingPostContent("");
      setEditingPostImage(null);
      setRemoveEditingPostImage(false);
      await queryClient.invalidateQueries({
        queryKey: ["user-posts", profile?.id],
      });

      setNotice({
        type: "success",
        message: "Post yeniləndi.",
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "Postu yeniləmək mümkün olmadı.",
        ),
      });
    },
  });

  const deletePostMutation = useMutation({
    mutationFn: ({
      postId,
      isStaffDelete,
      reason,
    }: {
      postId: number;
      isStaffDelete: boolean;
      reason?: string;
    }) =>
      isStaffDelete
        ? deleteAdminPost(postId, reason ?? "Profil səhifəsindən staff tərəfindən silindi.")
        : deletePost(postId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["user-posts", profile?.id],
        }),
        queryClient.invalidateQueries({
          queryKey: ["user-profile", userName],
        }),
        queryClient.invalidateQueries({
          queryKey: ["current-user"],
        }),
      ]);

      setNotice({
        type: "success",
        message: "Post silindi.",
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "Postu silmək mümkün olmadı.",
        ),
      });
    },
  });

  const postLikeMutation = useMutation({
    mutationFn: async (post: PostDTO) => {
      if (post.isLikedByCurrentUser) {
        await unlikeContent("post", post.id);
      } else {
        await likeContent("post", post.id);
      }
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["user-posts", profile?.id],
        }),
        queryClient.invalidateQueries({
          queryKey: ["content-likes", "post"],
        }),
      ]);
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "Post bəyənmə əməliyyatı tamamlanmadı.",
        ),
      });
    },
  });

  const createListMutation = useMutation({
    mutationFn: createGameList,
    onSuccess: async (listId) => {
      setIsCreateListOpen(false);
      await queryClient.invalidateQueries({
        queryKey: ["user-game-lists", profile?.id],
      });
      navigate(`/lists/${listId}`);
    },
  });


  const reviewLikeMutation = useMutation({
    mutationFn: async (review: ProfileReviewDTO) => {
      if (review.isLikedByCurrentUser) {
        await unlikeReview(review.id);
      } else {
        await likeReview(review.id);
      }
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["user-reviews", profile?.id],
        }),
        queryClient.invalidateQueries({
          queryKey: ["game-reviews"],
        }),
        queryClient.invalidateQueries({
          queryKey: ["review-likes"],
        }),
      ]);
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "Rəy bəyənmə əməliyyatı tamamlanmadı.",
        ),
      });
    },
  });

  function updateProfileTab(tab: ProfileTab) {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("tab", tab);

    if (tab !== "reviews") {
      nextParams.delete("sort");
      nextParams.delete("rating");
    }

    if (tab !== "games") {
      nextParams.delete("gameRating");
      nextParams.delete("gameStatus");
    }

    setSearchParams(nextParams);
  }

  function updateReviewSort(sort: ReviewSort) {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("tab", "reviews");

    if (sort === "Newest") {
      nextParams.delete("sort");
    } else {
      nextParams.set("sort", sort);
    }

    setSearchParams(nextParams, { replace: true });
  }

  function updateReviewSortCategory(
    category: ReviewSortCategory,
  ) {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("tab", "reviews");

    if (category === "rating") {
      // Ulduz kateqoriyası sıralama deyil, dəqiq puan filtridir.
      // Buna görə artan/azalan istiqaməti deaktiv edilir və
      // nəticələr standart olaraq ən yeni tarixə görə göstərilir.
      nextParams.delete("sort");

      if (reviewRating === null) {
        nextParams.set("rating", "2.5");
      }
    } else {
      nextParams.delete("rating");

      const nextSort = resolveReviewSort(
        category,
        reviewSortState.direction,
      );

      if (nextSort === "Newest") {
        nextParams.delete("sort");
      } else {
        nextParams.set("sort", nextSort);
      }
    }

    setSearchParams(nextParams, { replace: true });
  }

  function toggleReviewSortDirection() {
    if (activeReviewSortCategory === "rating") {
      return;
    }

    const nextDirection: ReviewSortDirection =
      reviewSortState.direction === "desc" ? "asc" : "desc";

    updateReviewSort(
      resolveReviewSort(
        activeReviewSortCategory,
        nextDirection,
      ),
    );
  }

  function updateReviewRating(rating: number | null) {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("tab", "reviews");

    if (rating === null) {
      nextParams.delete("rating");
    } else {
      nextParams.set("rating", rating.toString());
    }

    setSearchParams(nextParams, { replace: true });
  }

  function clearGameFilters() {
    setGamesSearch("");
    setGamesGenre(null);
    setGamesCompany(null);
    setGamesPlatform(null);
    setGamesPlayedPlatform(null);
    setGamesReleaseYear(null);
    setGamesSortBy("RecentlyUpdated");
    setGamesPage(1);

    const nextParams = new URLSearchParams(searchParams);
    nextParams.delete("gameRating");
    nextParams.delete("gameStatus");
    setSearchParams(nextParams, { replace: true });
  }

  function updateGamesStatus(
    status: ProfileGameEntryDTO["status"],
  ) {
    setGamesPage(1);

    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("tab", "games");

    if (status === null) {
      nextParams.delete("gameStatus");
    } else {
      nextParams.set("gameStatus", status);
    }

    setSearchParams(nextParams, { replace: true });
  }

  function updateGamesRating(rating: number | null) {
    setGamesPage(1);

    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("tab", "games");

    if (rating === null) {
      nextParams.delete("gameRating");
    } else {
      nextParams.set("gameRating", rating.toString());
    }

    setSearchParams(nextParams, { replace: true });
  }

  function openGamesByRating(rating: number) {
    setGamesSearch("");
    setGamesGenre(null);
    setGamesCompany(null);
    setGamesPlatform(null);
    setGamesPlayedPlatform(null);
    setGamesReleaseYear(null);
    setGamesSortBy("RecentlyUpdated");
    setGamesPage(1);
    setIsGamesFilterOpen(true);

    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("tab", "games");
    nextParams.set("gameRating", rating.toString());
    nextParams.delete("gameStatus");
    nextParams.delete("sort");
    nextParams.delete("rating");

    setSearchParams(nextParams);
  }

  function openRecentlyPlayedGames() {
    setGamesSearch("");
    setGamesGenre(null);
    setGamesCompany(null);
    setGamesPlatform(null);
    setGamesPlayedPlatform(null);
    setGamesReleaseYear(null);
    setGamesSortBy("RecentlyPlayed");
    setGamesPage(1);
    setIsGamesFilterOpen(true);

    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("tab", "games");
    nextParams.set("gameStatus", "Played");
    nextParams.delete("gameRating");
    nextParams.delete("sort");
    nextParams.delete("rating");

    setSearchParams(nextParams);
  }

  function openProfileGamesByGenre(genre: string) {
    setGamesSearch("");
    setGamesGenre(genre);
    setGamesCompany(null);
    setGamesPlatform(null);
    setGamesPlayedPlatform(null);
    setGamesReleaseYear(null);
    setGamesSortBy("RecentlyUpdated");
    setGamesPage(1);
    setIsGamesFilterOpen(true);

    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("tab", "games");
    nextParams.delete("gameStatus");
    nextParams.delete("gameRating");
    nextParams.delete("sort");
    nextParams.delete("rating");

    setSearchParams(nextParams);
  }

  function openProfileGamesByPlayedPlatform(platform: string) {
    setGamesSearch("");
    setGamesGenre(null);
    setGamesCompany(null);
    setGamesPlatform(null);
    setGamesPlayedPlatform(platform);
    setGamesReleaseYear(null);
    setGamesSortBy("RecentlyUpdated");
    setGamesPage(1);
    setIsGamesFilterOpen(true);

    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("tab", "games");
    nextParams.delete("gameStatus");
    nextParams.delete("gameRating");
    nextParams.delete("sort");
    nextParams.delete("rating");

    setSearchParams(nextParams);
  }

  function openProfileReview(reviewId: number) {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("tab", "reviews");
    nextParams.delete("sort");
    nextParams.delete("rating");

    navigate({
      search: `?${nextParams.toString()}`,
      hash: `#review-${reviewId}`,
    });
  }

  const visibleProfileTabs = useMemo(
    () =>
      profileTabs.filter(
        (tab) => !tab.ownerOnly || isOwnProfile,
      ),
    [isOwnProfile],
  );

  const followStatus =
    followStatusQuery.data;

  const actionIsPending =
    followMutation.isPending ||
    unfollowMutation.isPending ||
    blockMutation.isPending ||
    removeFollowerMutation.isPending;

  const posts = postsQuery.data ?? [];
  const sharedPostId = Number(searchParams.get("post"));

  useEffect(() => {
    if (
      visibleActiveTab !== "posts" ||
      !Number.isSafeInteger(sharedPostId) ||
      sharedPostId < 1 ||
      postsQuery.isLoading
    ) {
      return;
    }

    const frameId = window.requestAnimationFrame(() => {
      document.getElementById(`post-${sharedPostId}`)?.scrollIntoView({
        behavior: "smooth",
        block: "center",
      });
    });

    return () => window.cancelAnimationFrame(frameId);
  }, [postsQuery.data, postsQuery.isLoading, sharedPostId, visibleActiveTab]);
  const gameLists = listsQuery.data ?? [];
  const collaboratingGameLists = collaboratingListsQuery.data ?? [];
  const reviews = reviewsQuery.data?.items ?? [];
  const profileGames = profileGamesQuery.data?.items ?? [];

  const profileGameFilterOptions = useMemo(() => {
    const profileGameOptionItems =
      profileGamesOptionsQuery.data?.items ?? [];
    const uniqueStrings = (values: string[]) =>
      Array.from(new Set(values.filter(Boolean))).sort((left, right) =>
        left.localeCompare(right, "az"),
      );

    return {
      genres: uniqueStrings([
        ...profileGameOptionItems.flatMap((game) => game.genres),
        ...(gamesGenre ? [gamesGenre] : []),
      ]),
      companies: uniqueStrings(
        profileGameOptionItems.flatMap((game) => game.companies),
      ),
      platforms: uniqueStrings(
        profileGameOptionItems.flatMap((game) => game.platforms),
      ),
      playedPlatforms: uniqueStrings([
        ...profileGameOptionItems
          .map((game) => game.playedPlatformName)
          .filter((value): value is string => Boolean(value)),
        ...(gamesPlayedPlatform ? [gamesPlayedPlatform] : []),
      ]),
      releaseYears: Array.from(
        new Set(
          profileGameOptionItems
            .map((game) =>
              game.releaseDate
                ? new Date(`${game.releaseDate}T00:00:00`).getFullYear()
                : null,
            )
            .filter((value): value is number => value !== null),
        ),
      ).sort((left, right) => right - left),
    };
  }, [
    gamesGenre,
    gamesPlayedPlatform,
    profileGamesOptionsQuery.data?.items,
  ]);

  useEffect(() => {
    if (visibleActiveTab !== "reviews" || reviewsQuery.isLoading) {
      return;
    }

    const targetId = window.location.hash.replace(/^#/, "");
    if (!targetId) {
      return;
    }

    const frame = window.requestAnimationFrame(() => {
      document.getElementById(targetId)?.scrollIntoView({
        behavior: "smooth",
        block: "start",
      });
    });

    return () => window.cancelAnimationFrame(frame);
  }, [visibleActiveTab, reviewsQuery.isLoading, reviews.length]);

  const connectionTitle =
    connectionModal === "followers"
      ? language === "en"
        ? "Followers"
        : "İzləyicilər"
      : language === "en"
        ? "Following"
        : "İzlənilənlər";

  if (profileQuery.isLoading) {
    return <ProfileLoadingState />;
  }

  if (profileQuery.isError || !profile) {
    return (
      <ApiErrorStatusPage
        error={profileQuery.error}
        entityName="Profil"
      />
    );
  }

  const loadedProfile = profile;
  const isRestrictedPrivateProfile = !canViewProfileContent;

  async function handleFollowAction() {
    setNotice(null);

    if (!isAuthenticated) {
      navigate("/login", {
        state: {
          from: `/users/${loadedProfile.userName}`,
        },
      });
      return;
    }

    if (
      followStatus?.isFollowing ||
      followStatus?.isPending
    ) {
      const confirmed = await requestConfirmation({
        title: `${loadedProfile.userName} izlənməsin?`,
        description:
          "Bu istifadəçinin yeni paylaşımları və fəaliyyətləri artıq lentində görünməyəcək.",
        detail: followStatus.isPending
          ? "Gözləyən izləmə sorğun da ləğv ediləcək."
          : "İstəsən, bu profili daha sonra yenidən izləyə bilərsən.",
        confirmLabel: followStatus.isPending
          ? "Sorğunu ləğv et"
          : "İzləməni dayandır",
        kind: "remove-user",
        tone: "warning",
      });

      if (confirmed) {
        unfollowMutation.mutate(loadedProfile.id);
      }
      return;
    }

    followMutation.mutate(loadedProfile.id);
  }

  function handleBlock() {
    setNotice(null);

    if (!isAuthenticated) {
      navigate("/login", {
        state: {
          from: `/users/${loadedProfile.userName}`,
        },
      });
      return;
    }

    const shouldBlock = window.confirm(
      `${loadedProfile.userName} istifadəçisini bloklamaq istəyirsən? İki tərəf arasındakı izləmə əlaqələri də silinəcək.`,
    );

    if (shouldBlock) {
      blockMutation.mutate(loadedProfile.id);
    }
  }

  function handleCreatePost() {
    const normalizedContent = newPostContent.trim();

    if (!normalizedContent && !newPostImage) {
      setNotice({
        type: "error",
        message: "Post üçün mətn və ya şəkil əlavə et.",
      });
      return;
    }

    createPostMutation.mutate({
      content: normalizedContent,
      image: newPostImage,
    });
  }

  function beginPostEdit(post: PostDTO) {
    setEditingPostId(post.id);
    setEditingPostContent(post.content ?? "");
    setEditingPostImage(null);
    setRemoveEditingPostImage(false);
  }

  function cancelPostEdit() {
    setEditingPostId(null);
    setEditingPostContent("");
    setEditingPostImage(null);
    setRemoveEditingPostImage(false);
  }

  function handlePostUpdate(post: PostDTO) {
    const normalizedContent = editingPostContent.trim();
    const keepsExistingImage =
      Boolean(post.imageUrl) && !removeEditingPostImage;

    if (
      !normalizedContent &&
      !editingPostImage &&
      !keepsExistingImage
    ) {
      setNotice({
        type: "error",
        message: "Post üçün mətn və ya şəkil saxlanmalıdır.",
      });
      return;
    }

    updatePostMutation.mutate({
      postId: post.id,
      content: normalizedContent,
      image: editingPostImage,
      removeImage:
        removeEditingPostImage && !editingPostImage,
    });
  }

  async function handlePostDelete(post: PostDTO) {
    const isStaffDelete = !post.isMine && canDeleteAnyPost;
    let reason: string | undefined;

    if (isStaffDelete) {
      const enteredReason = window.prompt(
        language === "en"
          ? "Enter the moderation reason for deleting this post:"
          : "Postun silinmə səbəbini daxil et:",
      );

      if (enteredReason === null) return;
      reason = enteredReason.trim();

      if (reason.length < 3) {
        setNotice({
          type: "error",
          message:
            language === "en"
              ? "The moderation reason must contain at least 3 characters."
              : "Moderasiya səbəbi ən azı 3 simvol olmalıdır.",
        });
        return;
      }
    }

    const shouldDelete = await requestConfirmation({
      title: language === "en" ? "Delete post?" : "Post silinsin?",
      description: isStaffDelete
        ? language === "en"
          ? `This post by ${post.userName} will be removed from the platform.`
          : `${post.userName} tərəfindən paylaşılmış post platformadan silinəcək.`
        : language === "en"
          ? "Your post will be removed from your profile."
          : "Post profilindən silinəcək.",
      detail: reason,
      confirmLabel: language === "en" ? "Delete" : "Sil",
      kind: "delete",
      tone: "danger",
    });

    if (shouldDelete) {
      deletePostMutation.mutate({
        postId: post.id,
        isStaffDelete,
        reason,
      });
    }
  }

  function toggleSpoiler(reviewId: number) {
    setRevealedSpoilers((currentValue) => {
      const nextValue = new Set(currentValue);

      if (nextValue.has(reviewId)) {
        nextValue.delete(reviewId);
      } else {
        nextValue.add(reviewId);
      }

      return nextValue;
    });
  }

  return (
    <div className="profile-page min-h-screen wellplayd-page-bg text-zinc-100">
      <Navbar />

      <main>
        <section className="wp-profile-hero relative overflow-hidden border-b border-white/[0.06] bg-[#0a0a0e]">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_20%,rgba(124,58,237,0.17),transparent_30%),radial-gradient(circle_at_90%_30%,rgba(217,70,239,0.07),transparent_25%)]" />

          <div className="profile-hero-inner relative mx-auto max-w-7xl px-5 py-12 lg:px-8 lg:py-16">
            <div className="flex flex-col gap-7 md:flex-row md:items-start md:justify-between">
              <div className="profile-identity flex min-w-0 flex-col gap-6 sm:flex-row sm:items-start">
                <UserAvatar
                  userName={profile.userName}
                  profileImageUrl={
                    profile.profileImageUrl
                  }
                  className="profile-avatar h-28 w-28 rounded-3xl border border-white/10 text-3xl shadow-2xl shadow-black/30"
                />

                <div className="min-w-0 pt-1">
                  <div className="flex flex-wrap items-center gap-3">
                    <h1 className="truncate text-3xl font-bold tracking-tight text-white sm:text-4xl">
                      {profile.userName}
                    </h1>

                  </div>

                  <>
                      <p data-user-content className="mt-4 max-w-2xl whitespace-pre-wrap text-sm leading-7 text-zinc-400">
                        {profile.bio ??
                          (language === "en"
                            ? "This user has not added a bio yet."
                            : "Bu istifadəçi hələ bio əlavə etməyib.")}
                      </p>

                      {profile.socialLinks.length > 0 && (
                        <div className="mt-4 flex flex-wrap items-center gap-2">
                          {profile.socialLinks.map((link) => (
                            <a
                              key={link.platform}
                              href={link.url}
                              target="_blank"
                              rel="noreferrer"
                              title={socialPlatformLabels[link.platform]}
                              aria-label={
                                language === "en"
                                  ? `Open ${socialPlatformLabels[link.platform]} profile`
                                  : `${socialPlatformLabels[link.platform]} profilini aç`
                              }
                              className="inline-flex h-9 items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.035] px-3 text-xs font-semibold text-zinc-400 transition hover:border-violet-400/25 hover:bg-violet-500/10 hover:text-white"
                            >
                              <SocialPlatformIcon
                                platform={link.platform}
                                size={16}
                              />
                              <span className="hidden sm:inline">
                                {socialPlatformLabels[link.platform]}
                              </span>
                            </a>
                          ))}
                        </div>
                      )}

                      <div className="mt-4 flex flex-wrap items-center gap-x-5 gap-y-2 text-xs text-zinc-500">
                        <span className="inline-flex items-center gap-2">
                          <CalendarDays size={15} />
                          {language === "en"
                            ? `Member since ${formatDate(profile.createdAt)}`
                            : `${formatDate(profile.createdAt)} tarixindən üzvdür`}
                        </span>
                      </div>
                  </>
                </div>
              </div>

              <div className="profile-actions flex shrink-0 flex-wrap gap-3">
                {isOwnProfile ? (
                  <>
                    <Link
                      to="/settings?section=profile"
                      className="flex h-11 items-center justify-center gap-2 rounded-xl border border-white/[0.09] bg-white/[0.05] px-5 text-sm font-semibold text-zinc-200 transition hover:bg-white/[0.09] hover:text-white"
                    >
                      <Pencil size={17} />
                      {language === "en" ? "Edit" : "Redaktə et"}
                    </Link>
                    <CopyShareButton
                      path={`/users/${encodeURIComponent(loadedProfile.userName)}`}
                      title={`${loadedProfile.userName} · WellPlayd`}
                      text={
                        language === "en"
                          ? `View ${loadedProfile.userName}'s WellPlayd profile.`
                          : `${loadedProfile.userName} istifadəçisinin WellPlayd profilinə bax.`
                      }
                      className="flex h-11 items-center justify-center gap-2 rounded-xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] px-4 text-sm font-semibold text-[var(--wp-text)] transition hover:bg-[var(--wp-surface-hover)] hover:text-[var(--wp-text-strong)]"
                    />
                  </>
                ) : (
                  <>
                    <button
                      type="button"
                      onClick={handleFollowAction}
                      disabled={actionIsPending}
                      className={`flex h-11 items-center justify-center gap-2 rounded-xl px-5 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-60 ${
                        followStatus?.isFollowing ||
                        followStatus?.isPending
                          ? "border border-white/[0.09] bg-white/[0.05] text-zinc-200 hover:bg-white/[0.09]"
                          : "bg-violet-600 text-white hover:bg-violet-500"
                      }`}
                    >
                      {actionIsPending ? (
                        <LoaderCircle
                          size={17}
                          className="animate-spin"
                        />
                      ) : followStatus?.isFollowing ? (
                        <UserCheck size={17} />
                      ) : followStatus?.isPending ? (
                        <Clock3 size={17} />
                      ) : (
                        <UserPlus size={17} />
                      )}

                      {followStatus?.isFollowing
                        ? language === "en"
                          ? "Following"
                          : "İzləyirsən"
                        : followStatus?.isPending
                          ? language === "en"
                            ? "Request sent"
                            : "Sorğu göndərilib"
                          : language === "en"
                            ? "Follow"
                            : "İzlə"}
                    </button>

                    <CopyShareButton
                      path={`/users/${encodeURIComponent(loadedProfile.userName)}`}
                      title={`${loadedProfile.userName} · WellPlayd`}
                      text={
                        language === "en"
                          ? `View ${loadedProfile.userName}'s WellPlayd profile.`
                          : `${loadedProfile.userName} istifadəçisinin WellPlayd profilinə bax.`
                      }
                      iconOnly
                      className="flex h-11 w-11 items-center justify-center rounded-xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] text-[var(--wp-text-muted)] transition hover:bg-[var(--wp-surface-hover)] hover:text-[var(--wp-text-strong)]"
                    />

                    <div ref={profileActionMenuRef} className="relative">
                        <button
                          type="button"
                          onClick={() =>
                            setIsProfileActionOpen((current) => !current)
                          }
                          aria-expanded={isProfileActionOpen}
                          aria-haspopup="menu"
                          aria-label={
                            language === "en"
                              ? "Profile actions"
                              : "Profil əməliyyatları"
                          }
                          className="flex h-11 w-11 items-center justify-center rounded-xl border border-white/[0.09] bg-white/[0.05] text-zinc-300 transition hover:bg-white/[0.09] hover:text-white"
                        >
                          <EllipsisVertical size={19} />
                        </button>

                        {isProfileActionOpen && (
                          <div
                            role="menu"
                            className="wp-profile-action-menu absolute right-0 top-[calc(100%+8px)] z-30 w-48 rounded-2xl border border-white/[0.09] bg-[#15151b] p-2 shadow-2xl shadow-black/50"
                          >
                            <button
                              type="button"
                              role="menuitem"
                              onClick={() => {
                                setIsProfileActionOpen(false);
                                if (!isAuthenticated) {
                                  navigate("/login");
                                  return;
                                }
                                navigate(
                                  `/messages?userId=${loadedProfile.id}&userName=${encodeURIComponent(loadedProfile.userName)}`,
                                );
                              }}
                              className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-zinc-300 transition hover:bg-white/[0.06] hover:text-white"
                            >
                              <MessageCircle size={16} />
                              {language === "en" ? "Message" : "Mesaj yaz"}
                            </button>
                            <button
                              type="button"
                              role="menuitem"
                              onClick={() => {
                                setIsProfileActionOpen(false);
                                handleBlock();
                              }}
                              disabled={actionIsPending}
                              className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-red-300 transition hover:bg-red-500/[0.08] disabled:opacity-50"
                            >
                              <Ban size={16} />
                              {language === "en" ? "Block" : "Blokla"}
                            </button>
                            <button
                              type="button"
                              role="menuitem"
                              onClick={() => {
                                setIsProfileActionOpen(false);
                                if (!isAuthenticated) {
                                  navigate("/login");
                                  return;
                                }
                                setIsReportOpen(true);
                              }}
                              className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-zinc-300 transition hover:bg-white/[0.06] hover:text-white"
                            >
                              <Flag size={16} />
                              {language === "en" ? "Report" : "Şikayət et"}
                            </button>
                            {canWarnUsers && (
                              <button
                                type="button"
                                role="menuitem"
                                onClick={() => {
                                  setIsProfileActionOpen(false);
                                  setWarningMessage("");
                                  setIsWarningOpen(true);
                                }}
                                className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-amber-300 transition hover:bg-amber-500/[0.08]"
                              >
                                <MessageSquareWarning size={16} />
                                {language === "en" ? "Send warning" : "Xəbərdarlıq et"}
                              </button>
                            )}
                          </div>
                        )}
                    </div>
                  </>
                )}
              </div>
            </div>

            <div className="profile-stats-grid mt-8 grid grid-cols-2 gap-3 border-t border-white/[0.06] pt-6 sm:grid-cols-4">
              <button
                type="button"
                onClick={() => setConnectionModal("followers")}
                disabled={isRestrictedPrivateProfile}
                title={isRestrictedPrivateProfile ? t("profile:private") : undefined}
                className={`wp-profile-stat-card group flex min-w-0 items-center gap-3 rounded-2xl px-4 py-3 text-left transition ${
                  isRestrictedPrivateProfile ? "is-disabled" : ""
                }`}
              >
                <span className="wp-profile-stat-icon flex h-10 w-10 shrink-0 items-center justify-center rounded-xl">
                  <Users size={17} />
                </span>
                <span className="min-w-0">
                  <strong className="wp-profile-stat-value block text-lg font-bold">{profile.followerCount}</strong>
                  <span className="wp-profile-stat-label block truncate text-xs">
                    {t("profile:followers")}
                  </span>
                </span>
              </button>

              <button
                type="button"
                onClick={() => setConnectionModal("following")}
                disabled={isRestrictedPrivateProfile}
                title={isRestrictedPrivateProfile ? t("profile:private") : undefined}
                className={`wp-profile-stat-card group flex min-w-0 items-center gap-3 rounded-2xl px-4 py-3 text-left transition ${
                  isRestrictedPrivateProfile ? "is-disabled" : ""
                }`}
              >
                <span className="wp-profile-stat-icon flex h-10 w-10 shrink-0 items-center justify-center rounded-xl">
                  <UserPlus size={17} />
                </span>
                <span className="min-w-0">
                  <strong className="wp-profile-stat-value block text-lg font-bold">{profile.followingCount}</strong>
                  <span className="wp-profile-stat-label block truncate text-xs">
                    {t("profile:following")}
                  </span>
                </span>
              </button>

              {isRestrictedPrivateProfile ? (
                <div
                  aria-disabled="true"
                  title={t("profile:private")}
                  className="wp-profile-stat-card is-disabled group flex min-w-0 items-center gap-3 rounded-2xl px-4 py-3"
                >
                  <span className="wp-profile-stat-icon flex h-10 w-10 shrink-0 items-center justify-center rounded-xl">
                    <FileText size={17} />
                  </span>
                  <span className="min-w-0">
                    <strong className="wp-profile-stat-value block text-lg font-bold">{profile.postCount}</strong>
                    <span className="wp-profile-stat-label block truncate text-xs">
                      {t("profile:posts")}
                    </span>
                  </span>
                </div>
              ) : (
                <Link
                  to="?tab=posts#posts"
                  className="wp-profile-stat-card group flex min-w-0 items-center gap-3 rounded-2xl px-4 py-3 transition"
                >
                  <span className="wp-profile-stat-icon flex h-10 w-10 shrink-0 items-center justify-center rounded-xl">
                    <FileText size={17} />
                  </span>
                  <span className="min-w-0">
                    <strong className="wp-profile-stat-value block text-lg font-bold">{profile.postCount}</strong>
                    <span className="wp-profile-stat-label block truncate text-xs">
                      {t("profile:posts")}
                    </span>
                  </span>
                </Link>
              )}

              {isRestrictedPrivateProfile ? (
                <div
                  aria-disabled="true"
                  title={t("profile:private")}
                  className="wp-profile-stat-card is-disabled group flex min-w-0 items-center gap-3 rounded-2xl px-4 py-3"
                >
                  <span className="wp-profile-stat-icon flex h-10 w-10 shrink-0 items-center justify-center rounded-xl">
                    <Star size={17} />
                  </span>
                  <span className="min-w-0">
                    <strong className="wp-profile-stat-value block text-lg font-bold">{profile.statistics.reviewCount}</strong>
                    <span className="wp-profile-stat-label block truncate text-xs">
                      {t("profile:reviews")}
                    </span>
                  </span>
                </div>
              ) : (
                <Link
                  to="?tab=reviews#reviews"
                  className="wp-profile-stat-card group flex min-w-0 items-center gap-3 rounded-2xl px-4 py-3 transition"
                >
                  <span className="wp-profile-stat-icon flex h-10 w-10 shrink-0 items-center justify-center rounded-xl">
                    <Star size={17} />
                  </span>
                  <span className="min-w-0">
                    <strong className="wp-profile-stat-value block text-lg font-bold">{profile.statistics.reviewCount}</strong>
                    <span className="wp-profile-stat-label block truncate text-xs">
                      {t("profile:reviews")}
                    </span>
                  </span>
                </Link>
              )}
            </div>
          </div>
        </section>

        <section className="mx-auto max-w-7xl px-5 py-10 lg:px-8">
          {notice && (
            <div
              className={`mb-6 flex items-start gap-3 rounded-2xl border px-4 py-3 text-sm ${
                notice.type === "success"
                  ? "border-emerald-400/15 bg-emerald-500/[0.07] text-emerald-200"
                  : "border-red-400/15 bg-red-500/[0.07] text-red-200"
              }`}
            >
              {notice.type === "success" ? (
                <Check
                  size={18}
                  className="mt-0.5 shrink-0"
                />
              ) : (
                <X
                  size={18}
                  className="mt-0.5 shrink-0"
                />
              )}

              <span>{notice.message}</span>
            </div>
          )}

          {isRestrictedPrivateProfile ? (
            <section className="wp-private-profile-inline mx-auto flex max-w-3xl flex-col items-center px-6 py-12 text-center sm:px-10 sm:py-16">
              <span className="wp-private-profile-lock flex h-16 w-16 items-center justify-center rounded-2xl">
                <LockKeyhole size={28} />
              </span>
              <h2 className="mt-5 text-xl font-bold text-[var(--wp-text-strong)] sm:text-2xl">
                {t("profile:private")}
              </h2>
            </section>
          ) : (
            <>
              <div className="profile-tabs flex gap-2 overflow-x-auto border-b border-[var(--wp-border)]">
            {visibleProfileTabs.map((tab) => {
              const isActive = visibleActiveTab === tab.id;

              return (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => {
                    updateProfileTab(tab.id);
                  }}
                  className={`relative flex shrink-0 items-center px-3 py-3 text-sm font-semibold transition-colors duration-300 after:absolute after:inset-x-3 after:-bottom-[1px] after:h-0.5 after:origin-center after:rounded-full after:bg-[var(--wp-accent)] after:transition-transform after:duration-300 after:ease-out ${
                    isActive
                      ? "text-[var(--wp-text-strong)] after:scale-x-100"
                      : "text-[var(--wp-text-muted)] after:scale-x-0 hover:text-[var(--wp-text-strong)] hover:after:scale-x-100"
                  }`}
                >
                  {t(
                    `profile:tabs.${
                      tab.id === "owned-games"
                        ? "ownedGames"
                        : tab.id === "feed"
                          ? "activity"
                          : tab.id
                    }`,
                  )}
                </button>
              );
            })}
          </div>

          <div className="mt-7">
            {visibleActiveTab === "overview" && (
              <ProfileOverview
                profile={profile}
                recentPlayedGames={recentPlayedGamesQuery.data?.items ?? []}
                recentPlayedGamesLoading={recentPlayedGamesQuery.isLoading}
                favoriteGames={recentFavoriteGamesQuery.data?.items ?? []}
                favoriteGamesLoading={recentFavoriteGamesQuery.isLoading}
                onOpenFavorites={() => {
                  setFavoritesPage(1);
                  queryClient.removeQueries({
                    queryKey: [
                      "user-games",
                      profile.id,
                      "favorites",
                    ],
                  });
                  setIsFavoritesModalOpen(true);
                }}
                onOpenRecentPlayed={openRecentlyPlayedGames}
                onOpenTopGenre={openProfileGamesByGenre}
                onOpenTopPlatform={openProfileGamesByPlayedPlatform}
                playtimeStatistics={playtimeStatisticsQuery.data}
                playtimePeriod={playtimePeriod}
                playtimeStatisticsLoading={playtimeStatisticsQuery.isLoading}
                playtimeStatisticsError={
                  playtimeStatisticsQuery.isError
                    ? getApiErrorMessage(
                        playtimeStatisticsQuery.error,
                        "Oyun vaxtı statistikasını yükləmək mümkün olmadı.",
                      )
                    : null
                }
                onPlaytimePeriodChange={setPlaytimePeriod}
                onRatingSelect={openGamesByRating}
              />
            )}

            {visibleActiveTab === "games" && (
              <ProfileGamesGallery
                items={profileGames}
                totalCount={profileGamesQuery.data?.totalCount ?? 0}
                pageNumber={profileGamesQuery.data?.pageNumber ?? gamesPage}
                totalPages={profileGamesQuery.data?.totalPages ?? 0}
                pageSize={profileGamesQuery.data?.pageSize ?? 20}
                isLoading={profileGamesQuery.isLoading}
                errorMessage={
                  profileGamesQuery.isError
                    ? getApiErrorMessage(
                        profileGamesQuery.error,
                        "İstifadəçinin oyun kolleksiyasını yükləmək mümkün olmadı.",
                      )
                    : null
                }
                isFilterOpen={
                  isGamesFilterOpen || gamesStatus !== null
                }
                onToggleFilter={() => {
                  setIsGamesFilterOpen((currentValue) => !currentValue);
                }}
                searchValue={gamesSearch}
                onSearchChange={(value) => {
                  setGamesSearch(value);
                  setGamesPage(1);
                }}
                selectedGenre={gamesGenre}
                selectedCompany={gamesCompany}
                selectedPlatform={gamesPlatform}
                selectedPlayedPlatform={gamesPlayedPlatform}
                selectedStatus={gamesStatus}
                selectedReleaseYear={gamesReleaseYear}
                selectedRating={gamesRating}
                sortBy={gamesSortBy}
                genreOptions={profileGameFilterOptions.genres}
                companyOptions={profileGameFilterOptions.companies}
                platformOptions={profileGameFilterOptions.platforms}
                playedPlatformOptions={
                  profileGameFilterOptions.playedPlatforms
                }
                releaseYearOptions={profileGameFilterOptions.releaseYears}
                onGenreChange={(value) => {
                  setGamesGenre(value);
                  setGamesPage(1);
                }}
                onCompanyChange={(value) => {
                  setGamesCompany(value);
                  setGamesPage(1);
                }}
                onPlatformChange={(value) => {
                  setGamesPlatform(value);
                  setGamesPage(1);
                }}
                onPlayedPlatformChange={(value) => {
                  setGamesPlayedPlatform(value);
                  setGamesPage(1);
                }}
                onStatusChange={(value) => {
                  updateGamesStatus(value);
                }}
                onReleaseYearChange={(value) => {
                  setGamesReleaseYear(value);
                  setGamesPage(1);
                }}
                onRatingChange={(value) => {
                  updateGamesRating(value);
                }}
                onSortChange={(value) => {
                  setGamesSortBy(value);
                  setGamesPage(1);
                }}
                onClearFilters={clearGameFilters}
                onPageChange={setGamesPage}
                onOpenReview={openProfileReview}
              />
            )}

            {visibleActiveTab === "discussions" && (
              <ProfileDiscussions
                result={discussionsQuery.data}
                isLoading={discussionsQuery.isLoading}
                isError={discussionsQuery.isError}
                error={discussionsQuery.error}
                page={discussionsPage}
                onPageChange={setDiscussionsPage}
              />
            )}

            {visibleActiveTab === "owned-games" && (
              <ProfileSteamLibrary
                games={ownedGamesQuery.data ?? []}
                isLoading={ownedGamesQuery.isLoading}
                isError={ownedGamesQuery.isError}
                error={ownedGamesQuery.error}
                isOwnProfile={isOwnProfile}
                isSyncing={steamSyncMutation.isPending}
                onSync={() => {
                  setNotice(null);
                  steamSyncMutation.mutate();
                }}
                onResolvePlaytime={() => {
                  setIsPlaytimeReconciliationOpen(true);
                }}
              />
            )}

            {visibleActiveTab === "posts" && (
              <div id="posts" className="mx-auto max-w-3xl scroll-mt-28 space-y-5">
                {isOwnProfile && (
                  <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
                    <div className="flex items-start gap-4">
                      <UserAvatar
                        userName={profile.userName}
                        profileImageUrl={
                          profile.profileImageUrl
                        }
                        className="h-11 w-11 rounded-xl text-sm"
                      />

                      <div className="min-w-0 flex-1">
                        <textarea
                          value={newPostContent}
                          onChange={(event) => {
                            setNewPostContent(
                              event.target.value,
                            );
                          }}
                          rows={4}
                          maxLength={2000}
                          placeholder="Oyunlarla bağlı nə düşünürsən?"
                          className="w-full resize-none rounded-2xl border border-white/[0.08] bg-white/[0.03] px-4 py-3 text-sm leading-7 text-zinc-100 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/50 focus:ring-4 focus:ring-violet-500/10"
                        />

                        {newPostImagePreview && (
                          <div className="relative mt-3 overflow-hidden rounded-2xl border border-white/[0.08] bg-black/20">
                            <img
                              src={newPostImagePreview}
                              alt="Post şəkli önizləməsi"
                              className="max-h-[28rem] w-full object-contain"
                            />
                            <button
                              type="button"
                              onClick={() => {
                                setNewPostImage(null);
                              }}
                              className="absolute right-3 top-3 flex h-9 w-9 items-center justify-center rounded-xl border border-white/10 bg-black/70 text-zinc-200 backdrop-blur transition hover:bg-red-500/80 hover:text-white"
                              aria-label="Seçilmiş şəkli sil"
                            >
                              <X size={17} />
                            </button>
                          </div>
                        )}

                        <div className="mt-3 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                          <div className="flex flex-wrap items-center gap-3">
                            <label className="inline-flex h-10 cursor-pointer items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.035] px-4 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.07] hover:text-white">
                              <ImagePlus size={17} />
                              {newPostImage
                                ? "Şəkli dəyiş"
                                : "Şəkil əlavə et"}
                              <input
                                type="file"
                                accept="image/jpeg,image/png,image/webp"
                                className="hidden"
                                onChange={(event) => {
                                  setNewPostImage(
                                    event.target.files?.[0] ?? null,
                                  );
                                  event.currentTarget.value = "";
                                }}
                              />
                            </label>
                            <span className="text-xs text-zinc-600">
                              {newPostContent.length}/2000 · maksimum 10 MB
                            </span>
                          </div>

                          <button
                            type="button"
                            onClick={handleCreatePost}
                            disabled={
                              createPostMutation.isPending ||
                              (newPostContent.trim().length === 0 &&
                                !newPostImage)
                            }
                            className="flex h-10 items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-50"
                          >
                            {createPostMutation.isPending ? (
                              <LoaderCircle
                                size={16}
                                className="animate-spin"
                              />
                            ) : (
                              <Send size={16} />
                            )}
                            Paylaş
                          </button>
                        </div>
                      </div>
                    </div>
                  </section>
                )}

                {postsQuery.isLoading ? (
                  <div className="flex items-center justify-center gap-3 py-16 text-sm text-zinc-500">
                    <LoaderCircle
                      size={19}
                      className="animate-spin"
                    />
                    Postlar yüklənir...
                  </div>
                ) : postsQuery.isError ? (
                  <div className="rounded-3xl border border-amber-400/15 bg-amber-500/[0.05] p-8 text-center">
                    <LockKeyhole
                      size={30}
                      className="mx-auto text-amber-300"
                    />
                    <h2 className="mt-4 font-semibold text-white">
                      Postlar göstərilmir
                    </h2>
                    <p className="mt-2 text-sm leading-6 text-amber-100/70">
                      {getApiErrorMessage(
                        postsQuery.error,
                        "Bu hesabın postlarına baxmaq mümkün deyil.",
                      )}
                    </p>
                  </div>
                ) : posts.length === 0 ? (
                  <div className="rounded-3xl border border-dashed border-white/[0.08] p-12 text-center">
                    <FileText
                      size={31}
                      className="mx-auto text-zinc-700"
                    />
                    <p className="mt-4 text-sm text-zinc-500">
                      Hələ heç bir post yoxdur.
                    </p>
                  </div>
                ) : (
                  posts.map((post) => (
                    <article
                      key={post.id}
                      id={`post-${post.id}`}
                      className="scroll-mt-28 rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6"
                    >
                      <div className="flex items-start justify-between gap-4">
                        <div className="flex min-w-0 items-center gap-3">
                          <UserAvatar
                            userName={post.userName}
                            profileImageUrl={
                              post.profileImageUrl
                            }
                            className="h-11 w-11 rounded-xl text-sm"
                          />

                          <div className="min-w-0">
                            <p className="truncate text-sm font-semibold text-white">
                              {post.userName}
                            </p>
                            <p className="mt-1 text-xs text-zinc-600">
                              {formatRelativeDate(
                                post.createdAt,
                              )}
                              {post.updatedAt
                                ? " · redaktə edilib"
                                : ""}
                            </p>
                          </div>
                        </div>

                        <div className="flex shrink-0 gap-1">
                          <CopyShareButton
                            path={`/users/${encodeURIComponent(post.userName)}?tab=posts&post=${post.id}#post-${post.id}`}
                            title={`${post.userName} · WellPlayd`}
                            text={
                              post.content?.trim() ||
                              (language === "en" ? "WellPlayd post" : "WellPlayd postu")
                            }
                            iconOnly
                            className="flex h-9 w-9 items-center justify-center rounded-lg text-[var(--wp-text-muted)] transition hover:bg-[var(--wp-surface-hover)] hover:text-[var(--wp-accent)]"
                          />
                          <ContentReportButton
                            reportedUserId={post.userId}
                            reportedUserName={post.userName}
                            contentType="Post"
                            contentId={post.id}
                            contentLabel={
                              post.content?.slice(0, 80) ||
                              "Şəkilli post"
                            }
                            isOwnContent={post.isMine}
                            className="flex h-9 w-9 items-center justify-center rounded-lg text-zinc-600 transition hover:bg-red-500/[0.08] hover:text-red-300"
                          />
                          {post.isMine && (
                            <button
                              type="button"
                              onClick={() => {
                                beginPostEdit(post);
                              }}
                              className="flex h-9 w-9 items-center justify-center rounded-lg text-zinc-600 transition hover:bg-white/[0.06] hover:text-zinc-300"
                              aria-label="Postu redaktə et"
                            >
                              <Pencil size={16} />
                            </button>
                          )}
                          {(post.isMine || canDeleteAnyPost) && (
                            <button
                              type="button"
                              onClick={() => {
                                void handlePostDelete(post);
                              }}
                              disabled={
                                deletePostMutation.isPending
                              }
                              className="flex h-9 w-9 items-center justify-center rounded-lg text-zinc-600 transition hover:bg-red-500/[0.08] hover:text-red-300 disabled:opacity-50"
                              aria-label="Postu sil"
                            >
                              <Trash2 size={16} />
                            </button>
                          )}
                        </div>
                      </div>

                      {editingPostId === post.id ? (
                        <div className="mt-5">
                          <textarea
                            value={editingPostContent}
                            onChange={(event) => {
                              setEditingPostContent(
                                event.target.value,
                              );
                            }}
                            rows={5}
                            maxLength={2000}
                            placeholder="Post mətni"
                            className="w-full resize-none rounded-2xl border border-white/[0.08] bg-white/[0.03] px-4 py-3 text-sm leading-7 text-zinc-100 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/50 focus:ring-4 focus:ring-violet-500/10"
                          />

                          {(editingPostImagePreview ||
                            (!removeEditingPostImage &&
                              resolveAssetUrl(post.imageUrl))) && (
                            <div className="relative mt-3 overflow-hidden rounded-2xl border border-white/[0.08] bg-black/20">
                              <img
                                src={
                                  editingPostImagePreview ??
                                  resolveAssetUrl(post.imageUrl) ??
                                  undefined
                                }
                                alt="Post şəkli"
                                className="max-h-[28rem] w-full object-contain"
                              />
                              <button
                                type="button"
                                onClick={() => {
                                  setEditingPostImage(null);
                                  setRemoveEditingPostImage(true);
                                }}
                                className="absolute right-3 top-3 flex h-9 w-9 items-center justify-center rounded-xl border border-white/10 bg-black/70 text-zinc-200 backdrop-blur transition hover:bg-red-500/80 hover:text-white"
                                aria-label="Post şəklini sil"
                              >
                                <X size={17} />
                              </button>
                            </div>
                          )}

                          <div className="mt-3 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                            <div className="flex flex-wrap items-center gap-3">
                              <label className="inline-flex h-10 cursor-pointer items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.035] px-4 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.07] hover:text-white">
                                <ImagePlus size={17} />
                                {editingPostImage ||
                                (!removeEditingPostImage && post.imageUrl)
                                  ? "Şəkli dəyiş"
                                  : "Şəkil əlavə et"}
                                <input
                                  type="file"
                                  accept="image/jpeg,image/png,image/webp"
                                  className="hidden"
                                  onChange={(event) => {
                                    const nextFile =
                                      event.target.files?.[0] ?? null;
                                    setEditingPostImage(nextFile);
                                    if (nextFile) {
                                      setRemoveEditingPostImage(false);
                                    }
                                    event.currentTarget.value = "";
                                  }}
                                />
                              </label>
                              <span className="text-xs text-zinc-600">
                                {editingPostContent.length}/2000
                              </span>
                            </div>

                            <div className="flex justify-end gap-2">
                              <button
                                type="button"
                                onClick={cancelPostEdit}
                                className="rounded-xl px-4 py-2 text-sm font-medium text-zinc-500 transition hover:bg-white/[0.05] hover:text-zinc-300"
                              >
                                Ləğv et
                              </button>
                              <button
                                type="button"
                                onClick={() => {
                                  handlePostUpdate(post);
                                }}
                                disabled={updatePostMutation.isPending}
                                className="flex items-center gap-2 rounded-xl bg-violet-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:opacity-50"
                              >
                                {updatePostMutation.isPending && (
                                  <LoaderCircle
                                    size={15}
                                    className="animate-spin"
                                  />
                                )}
                                Yadda saxla
                              </button>
                            </div>
                          </div>
                        </div>
                      ) : (
                        <div className="mt-5 space-y-4">
                          {post.content && (
                            <p data-user-content className="whitespace-pre-wrap break-words text-sm leading-7 text-zinc-300">
                              {post.content}
                            </p>
                          )}
                          {resolveAssetUrl(post.imageUrl) && (
                            <img
                              src={resolveAssetUrl(post.imageUrl) ?? undefined}
                              alt={`${post.userName} post şəkli`}
                              className="max-h-[36rem] w-full rounded-2xl border border-white/[0.07] bg-black/20 object-contain"
                            />
                          )}
                        </div>
                      )}

                      <div className="mt-5 flex items-center gap-5 border-t border-white/[0.06] pt-4 text-xs text-zinc-600">
                        <span className="inline-flex items-center gap-2">
                          <button
                            type="button"
                            disabled={
                              !isAuthenticated ||
                              post.isMine ||
                              postLikeMutation.isPending
                            }
                            onClick={() => {
                              postLikeMutation.mutate(post);
                            }}
                            className={`group inline-flex items-center font-semibold transition disabled:cursor-not-allowed disabled:opacity-45 ${
                              post.isLikedByCurrentUser
                                ? "text-rose-300"
                                : "hover:text-rose-300"
                            }`}
                            aria-label="Postu bəyən"
                          >
                            <Heart
                              size={15}
                              className={`transition ${
                                post.isLikedByCurrentUser
                                  ? "fill-current"
                                  : "fill-transparent group-hover:fill-current"
                              }`}
                            />
                          </button>
                          <button
                            type="button"
                            onClick={() => {
                              setLikesPost({
                                id: post.id,
                                count: post.likeCount,
                              });
                            }}
                            className="font-semibold transition hover:text-rose-300"
                          >
                            {post.likeCount} bəyəni
                          </button>
                        </span>

                        <button
                          type="button"
                          onClick={() => {
                            setOpenPostCommentIds((currentValue) => {
                              const nextValue = new Set(currentValue);
                              if (nextValue.has(post.id)) {
                                nextValue.delete(post.id);
                              } else {
                                nextValue.add(post.id);
                              }
                              return nextValue;
                            });
                          }}
                          className={`inline-flex items-center gap-2 font-semibold transition hover:text-violet-300 ${
                            openPostCommentIds.has(post.id)
                              ? "text-violet-300"
                              : ""
                          }`}
                        >
                          <MessageCircle size={15} />
                          {post.commentCount}
                        </button>
                      </div>

                      {openPostCommentIds.has(post.id) && (
                        <div className="-mx-5 -mb-5 mt-4 overflow-hidden rounded-b-3xl sm:-mx-6 sm:-mb-6">
                          <InteractionCommentsPanel
                            contentType="post"
                            contentId={post.id}
                            isAuthenticated={isAuthenticated}
                            placeholder="Posta şərh yaz..."
                            emptyText="Bu posta hələ şərh yazılmayıb."
                            invalidateQueryKeys={[
                              ["user-posts", profile?.id],
                            ]}
                          />
                        </div>
                      )}
                    </article>
                  ))
                )}
              </div>
            )}

            {visibleActiveTab === "lists" && (
              <div>
                <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <h2 className="text-xl font-bold text-[var(--wp-text-strong)]">
                      Oyun siyahıları
                    </h2>
                    <p className="mt-1 text-sm text-[color:var(--wp-text-muted)]">
                      Ranklanmış siyahılar şəxsi sıralamanı, sırasız siyahılar isə kolleksiyanı göstərir.
                    </p>
                  </div>

                  {isOwnProfile && (
                    <button
                      type="button"
                      onClick={() => {
                        createListMutation.reset();
                        setIsCreateListOpen(true);
                      }}
                      className="inline-flex h-11 shrink-0 items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-semibold text-white transition hover:bg-violet-500"
                    >
                      <Plus size={17} />
                      Yeni siyahı
                    </button>
                  )}
                </div>

                {createListMutation.isError && (
                  <div className="mb-5 rounded-2xl border border-red-400/15 bg-red-500/[0.06] px-4 py-3 text-sm text-red-200">
                    {getApiErrorMessage(
                      createListMutation.error,
                      "Siyahını yaratmaq mümkün olmadı.",
                    )}
                  </div>
                )}

                {listsQuery.isLoading ? (
                  <div className="flex items-center justify-center gap-3 py-16 text-sm text-zinc-500">
                    <LoaderCircle
                      size={19}
                      className="animate-spin"
                    />
                    Siyahılar yüklənir...
                  </div>
                ) : listsQuery.isError ? (
                  <div className="rounded-3xl border border-amber-400/15 bg-amber-500/[0.05] p-8 text-center text-sm text-amber-100/70">
                    {getApiErrorMessage(
                      listsQuery.error,
                      "Bu istifadəçinin oyun siyahılarına baxmaq mümkün deyil.",
                    )}
                  </div>
                ) : gameLists.length === 0 ? (
                  <div className="rounded-3xl border border-dashed border-white/[0.08] p-12 text-center">
                    <ListChecks
                      size={31}
                      className="mx-auto text-zinc-700"
                    />
                    <p className="mt-4 text-sm text-zinc-500">
                      {isOwnProfile
                        ? "Hələ oyun siyahısı yaratmamısan."
                        : "Görünən oyun siyahısı yoxdur."}
                    </p>
                    {isOwnProfile && (
                      <button
                        type="button"
                        onClick={() => {
                          createListMutation.reset();
                          setIsCreateListOpen(true);
                        }}
                        className="mx-auto mt-5 inline-flex h-10 items-center gap-2 rounded-xl border border-violet-300/20 bg-violet-500/10 px-4 text-sm font-semibold text-violet-200 transition hover:bg-violet-500/20"
                      >
                        <Plus size={16} />
                        İlk siyahını yarat
                      </button>
                    )}
                  </div>
                ) : (
                  <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                    {gameLists.map((list) => {
                      const backgroundUrl = resolveAssetUrl(
                        list.backgroundCoverImageUrl,
                      );

                      return (
                        <Link
                          key={list.id}
                          to={`/lists/${list.id}`}
                          className="wp-list-card group relative min-h-[22rem] overflow-hidden rounded-[1.75rem] transition duration-300 hover:-translate-y-1 hover:border-violet-300/25 hover:shadow-2xl"
                        >
                          {backgroundUrl && (
                            <div className="pointer-events-none absolute inset-0 overflow-hidden">
                              <img
                                src={backgroundUrl}
                                alt=""
                                className="wp-list-card-image h-full w-full scale-110 object-cover opacity-35 blur-xl transition duration-500 group-hover:scale-[1.16] group-hover:opacity-45"
                              />
                              <div className="wp-list-card-overlay absolute inset-0" />
                              <div className="wp-list-card-accent absolute inset-0" />
                            </div>
                          )}

                          <div className="relative flex h-full min-h-[22rem] flex-col p-6">
                            <div className="flex items-start justify-between gap-4">
                              <span className="flex h-12 w-12 items-center justify-center rounded-2xl wp-chip-accent">
                                {list.isRanked ? (
                                  <ListOrdered size={21} />
                                ) : (
                                  <ListChecks size={21} />
                                )}
                              </span>

                              <span className="rounded-full px-3 py-1 text-[11px] font-semibold wp-chip-neutral">
                                {t(`lists:visibility.${list.visibility}`)}
                              </span>
                            </div>

                            <div className="mt-8 flex items-center gap-2 text-[11px] font-black uppercase tracking-[0.14em] text-[var(--wp-accent-strong)]">
                              {list.isRanked
                                ? t("lists:profile.ranked")
                                : t("lists:profile.unranked")}
                            </div>

                            <h2 className="mt-2 line-clamp-2 text-xl font-black text-[var(--wp-text-strong)] transition group-hover:text-[var(--wp-accent-strong)]">
                              {list.name}
                            </h2>

                            <p data-user-content className="mt-3 line-clamp-3 text-sm leading-6 text-[color:var(--wp-text-muted)]">
                              {list.description ??
                                t("lists:profile.noDescription")}
                            </p>

                            <div className="mt-auto pt-8">
                              {list.collaboratorCount > 0 && (
                                <span className="mb-3 inline-flex items-center gap-2 rounded-full px-3 py-1 text-[11px] font-bold wp-chip-success">
                                  <Users size={13} />
                                  {t("lists:profile.collaborators", {
                                    count: list.collaboratorCount,
                                  })}
                                </span>
                              )}

                              <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[color:var(--wp-border)] pt-4 text-xs text-[color:var(--wp-text-muted)]">
                                <div className="flex flex-wrap items-center gap-4">
                                  <span className="inline-flex items-center gap-2">
                                    <Gamepad2 size={15} />
                                    {list.gameCount}
                                  </span>
                                  <span className="inline-flex items-center gap-1.5">
                                    <Heart size={14} />
                                    {list.likeCount}
                                  </span>
                                  <span className="inline-flex items-center gap-1.5">
                                    <MessageCircle size={14} />
                                    {list.commentCount}
                                  </span>
                                </div>
                                <span>{formatRelativeDate(list.createdAt)}</span>
                              </div>
                            </div>
                          </div>
                        </Link>
                      );
                    })}
                  </div>
                )}

                {isOwnProfile && collaboratingGameLists.length > 0 && (
                  <section className="mt-10 border-t border-white/[0.07] pt-8">
                    <div className="mb-5">
                      <h3 className="text-lg font-black text-[var(--wp-text-strong)]">
                        {t("lists:profile.sharedWithMe")}
                      </h3>
                      <p className="mt-1 text-sm text-[color:var(--wp-text-muted)]">
                        {t("lists:profile.sharedWithMeDescription")}
                      </p>
                    </div>
                    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                      {collaboratingGameLists.map((list) => {
                        const backgroundUrl = resolveAssetUrl(
                          list.backgroundCoverImageUrl,
                        );
                        const isPending =
                          list.currentUserCollaborationStatus === "Pending";

                        return (
                          <Link
                            key={`collaboration-${list.id}`}
                            to={`/lists/${list.id}`}
                            className="wp-collaboration-list-card group relative min-h-[18rem] overflow-hidden rounded-[1.75rem] border border-white/[0.08] bg-[#101015] transition duration-300 hover:-translate-y-1 hover:border-violet-300/25"
                          >
                            {backgroundUrl && (
                              <div className="pointer-events-none absolute inset-0 overflow-hidden">
                                <img
                                  src={backgroundUrl}
                                  alt=""
                                  className="wp-collaboration-list-card-image h-full w-full scale-110 object-cover opacity-30 blur-xl transition duration-500 group-hover:scale-[1.16]"
                                />
                                <div className="wp-collaboration-list-card-overlay absolute inset-0 bg-gradient-to-t from-[#0c0c12] via-[#0c0c12]/88 to-[#0c0c12]/50" />
                              </div>
                            )}
                            <div className="relative flex h-full min-h-[18rem] flex-col p-6">
                              <div className="flex items-start justify-between gap-3">
                                <span className="flex h-11 w-11 items-center justify-center rounded-2xl border border-violet-300/15 bg-violet-500/15 text-violet-200">
                                  <Users size={20} />
                                </span>
                                <span className={`rounded-full border px-3 py-1 text-[11px] font-bold ${
                                  isPending
                                    ? "border-amber-300/15 bg-amber-500/[0.1] text-amber-200"
                                    : "border-emerald-300/15 bg-emerald-500/[0.1] text-emerald-200"
                                }`}>
                                  {isPending
                                    ? t("lists:profile.invitationPending")
                                    : t("lists:profile.collaborating")}
                                </span>
                              </div>
                              <p className="mt-7 text-xs font-bold text-violet-200/80">
                                @{list.ownerUserName}
                              </p>
                              <h4 className="mt-2 line-clamp-2 text-xl font-black text-white">
                                {list.name}
                              </h4>
                              <p data-user-content className="mt-3 line-clamp-2 text-sm leading-6 text-zinc-400">
                                {list.description ?? t("lists:profile.noDescription")}
                              </p>
                              <div className="mt-auto flex items-center gap-4 border-t border-white/[0.08] pt-4 text-xs text-zinc-500">
                                <span className="inline-flex items-center gap-2">
                                  <Gamepad2 size={14} />
                                  {list.gameCount}
                                </span>
                                <span className="inline-flex items-center gap-2">
                                  <Users size={14} />
                                  {list.collaboratorCount}
                                </span>
                              </div>
                            </div>
                          </Link>
                        );
                      })}
                    </div>
                  </section>
                )}
              </div>
            )}

            {visibleActiveTab === "reviews" && (
              <div id="reviews" className="mx-auto max-w-6xl scroll-mt-28 space-y-5">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <button
                      type="button"
                      onClick={() => {
                        setIsReviewFilterOpen((currentValue) =>
                          !currentValue,
                        );
                      }}
                      aria-expanded={isReviewFilterOpen}
                      className={`group inline-flex h-11 items-center gap-2 rounded-xl border px-4 text-sm font-bold transition ${
                        isReviewFilterOpen
                          ? "border-violet-400/30 bg-violet-500/15 text-violet-200"
                          : "border-white/[0.08] bg-[#101015] text-zinc-300 hover:border-white/[0.14] hover:text-white"
                      }`}
                    >
                      <SlidersHorizontal size={17} />
                      Filtr
                      <ChevronDown
                        size={16}
                        className={`transition-transform duration-300 ${
                          isReviewFilterOpen ? "rotate-180" : ""
                        }`}
                      />
                    </button>

                    {reviewRating !== null && (
                      <div className="inline-flex h-11 items-center gap-2 rounded-xl border border-amber-300/15 bg-amber-500/[0.08] px-3 text-sm font-bold text-amber-300">
                        <Star size={14} fill="currentColor" />
                        {formatReviewRating(reviewRating)}
                        <button
                          type="button"
                          onClick={() => {
                            updateReviewRating(null);
                          }}
                          className="ml-1 flex h-6 w-6 items-center justify-center rounded-md text-amber-200/65 transition hover:bg-amber-400/10 hover:text-amber-100"
                          aria-label="Ulduz filtrini sil"
                        >
                          <X size={13} />
                        </button>
                      </div>
                    )}
                  </div>

                  <span className="rounded-full border border-white/[0.07] bg-white/[0.035] px-3 py-1.5 text-xs font-semibold text-zinc-400">
                    {reviewsQuery.data?.totalCount ?? 0} rəy
                  </span>
                </div>

                <div
                  className={`grid items-start gap-5 transition-[grid-template-columns] duration-300 ease-out ${
                    isReviewFilterOpen
                      ? "lg:grid-cols-[18rem_minmax(0,1fr)]"
                      : "lg:grid-cols-[0px_minmax(0,1fr)]"
                  }`}
                >
                  <aside
                    className={`overflow-hidden transition-[max-height,opacity,transform] duration-300 ease-out ${
                      isReviewFilterOpen
                        ? "max-h-[40rem] translate-y-0 opacity-100 lg:translate-x-0"
                        : "max-h-0 -translate-y-2 opacity-0 lg:max-h-[40rem] lg:-translate-x-3"
                    }`}
                  >
                    <div className="rounded-3xl border border-white/[0.07] bg-[#101015] p-4 sm:p-5">
                      <div>
                        <p className="text-sm font-black text-white">
                          Sıralama meyarı
                        </p>
                        <p className="mt-1 text-xs leading-5 text-zinc-600">
                          Tarix və şərh sayı üçün sıra istiqamətini seç. Ulduz bölməsində isə dəqiq puanı slider ilə müəyyən et.
                        </p>
                      </div>

                      <div className="mt-4 space-y-2">
                        {reviewSortCategories.map((option) => {
                          const isActive =
                            activeReviewSortCategory === option.value;

                          return (
                            <button
                              key={option.value}
                              type="button"
                              onClick={() => {
                                updateReviewSortCategory(option.value);
                              }}
                              className={`w-full rounded-2xl border px-4 py-3 text-left transition ${
                                isActive
                                  ? "border-violet-400/30 bg-violet-500/15"
                                  : "border-white/[0.07] bg-white/[0.025] hover:border-white/[0.12] hover:bg-white/[0.04]"
                              }`}
                            >
                              <span
                                className={`block text-sm font-bold ${
                                  isActive
                                    ? "text-violet-200"
                                    : "text-zinc-300"
                                }`}
                              >
                                {language === "en"
                                  ? {
                                      date: "Date",
                                      comments: "Comment count",
                                      rating: "Star",
                                    }[option.value]
                                  : option.label}
                              </span>
                              <span className="mt-1 block text-[11px] leading-5 text-zinc-600">
                                {option.description}
                              </span>
                            </button>
                          );
                        })}
                      </div>

                      {activeReviewSortCategory === "rating" ? (
                        <div className="mt-5 border-t border-white/[0.06] pt-5">
                          <div className="flex items-center justify-between gap-3">
                            <div>
                              <p className="text-xs font-bold uppercase tracking-[0.12em] text-zinc-600">
                                Ulduz seçimi
                              </p>
                              <p className="mt-1 text-[11px] leading-5 text-zinc-600">
                                Sürüşdürərək yalnız seçdiyin puana aid rəyləri göstər.
                              </p>
                            </div>
                            <span className="inline-flex shrink-0 items-center rounded-xl border border-amber-300/15 bg-amber-500/[0.08] px-3 py-2 text-sm font-black text-amber-300">
                              {formatReviewRating(reviewRating ?? 2.5)}
                            </span>
                          </div>

                          <div className="mt-5">
                            <input
                              type="range"
                              min="0.5"
                              max="5"
                              step="0.5"
                              value={reviewRating ?? 2.5}
                              onChange={(event) => {
                                updateReviewRating(
                                  Number(event.target.value),
                                );
                              }}
                              aria-label="Ulduza görə rəy filtri"
                              className="block h-5 w-full cursor-pointer accent-violet-500"
                            />

                            <div className="pointer-events-none mx-[9px] mt-1 flex h-5 items-start justify-between text-[10px] font-semibold text-zinc-700">
                              <span>0.5</span>
                              <span>5</span>
                            </div>
                          </div>

                          <button
                            type="button"
                            onClick={() => {
                              const nextParams = new URLSearchParams(
                                searchParams,
                              );
                              nextParams.set("tab", "reviews");
                              nextParams.delete("rating");
                              nextParams.delete("sort");
                              setSearchParams(nextParams, {
                                replace: true,
                              });
                            }}
                            className="mt-4 h-10 w-full rounded-xl border border-white/[0.08] bg-white/[0.025] text-xs font-bold text-zinc-500 transition hover:border-white/[0.14] hover:bg-white/[0.05] hover:text-white"
                          >
                            Ulduz filtrini təmizlə
                          </button>
                        </div>
                      ) : (
                        <div className="mt-5 border-t border-white/[0.06] pt-5">
                          <p className="text-xs font-bold uppercase tracking-[0.12em] text-zinc-600">
                            Sıra istiqaməti
                          </p>
                          <button
                            type="button"
                            onClick={toggleReviewSortDirection}
                            className="mt-3 flex h-11 w-full items-center justify-between rounded-xl border border-white/[0.08] bg-white/[0.03] px-4 text-sm font-bold text-zinc-200 transition hover:border-violet-400/25 hover:bg-violet-500/[0.08] hover:text-violet-200"
                          >
                            <span>
                              {reviewSortState.direction === "desc"
                                ? "Azalan sıra"
                                : "Artan sıra"}
                            </span>
                            {reviewSortState.direction === "desc" ? (
                              <ArrowDown size={18} />
                            ) : (
                              <ArrowUp size={18} />
                            )}
                          </button>
                          <p className="mt-2 text-[11px] leading-5 text-zinc-600">
                            {activeReviewSortCategory === "date"
                              ? reviewSortState.direction === "desc"
                                ? "Ən son yazılmış rəylər əvvəl göstərilir."
                                : "Ən köhnə rəylər əvvəl göstərilir."
                              : reviewSortState.direction === "desc"
                                ? "Ən çox şərh alan rəylər əvvəl göstərilir."
                                : "Ən az şərh alan rəylər əvvəl göstərilir."}
                          </p>
                        </div>
                      )}
                    </div>
                  </aside>

                  <div className="min-w-0 space-y-5">
                {reviewsQuery.isLoading ? (
                  <div className="flex items-center justify-center gap-3 py-16 text-sm text-zinc-500">
                    <LoaderCircle
                      size={19}
                      className="animate-spin"
                    />
                    Rəylər yüklənir...
                  </div>
                ) : reviewsQuery.isError ? (
                  <div className="rounded-3xl border border-amber-400/15 bg-amber-500/[0.05] p-8 text-center text-sm text-amber-100/70">
                    {getApiErrorMessage(
                      reviewsQuery.error,
                      "Bu istifadəçinin rəylərinə baxmaq mümkün deyil.",
                    )}
                  </div>
                ) : reviews.length === 0 ? (
                  <div className="rounded-3xl border border-dashed border-white/[0.08] p-12 text-center">
                    <Star
                      size={31}
                      className="mx-auto text-zinc-700"
                    />
                    <p className="mt-4 text-sm text-zinc-500">
                      {reviewRating === null
                        ? "Hələ heç bir rəy yoxdur."
                        : `${formatReviewRating(reviewRating)} ulduza uyğun rəy tapılmadı.`}
                    </p>
                  </div>
                ) : (
                  reviews.map((review) => {
                    const coverUrl = resolveAssetUrl(
                      review.gameCoverImageUrl,
                    );
                    const spoilerIsRevealed =
                      revealedSpoilers.has(review.id);

                    return (
                      <article
                        id={`review-${review.id}`}
                        key={review.id}
                        className="scroll-mt-28 overflow-hidden rounded-3xl border border-white/[0.07] bg-[#101015]"
                      >
                        <div className="flex gap-4 border-b border-white/[0.06] p-5 sm:p-6">
                          <Link
                            to={`/games/${review.source}/${review.gameId}`}
                            className="flex min-w-0 flex-1 items-center gap-4"
                          >
                            <span className="flex h-20 w-16 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-zinc-900 text-zinc-700">
                              {coverUrl ? (
                                <img
                                  src={coverUrl}
                                  alt={`${review.gameName} örtüyü`}
                                  className="h-full w-full object-cover"
                                />
                              ) : (
                                <Gamepad2 size={22} />
                              )}
                            </span>

                            <div className="min-w-0">
                              <p className="truncate font-semibold text-white">
                                {review.gameName}
                              </p>
                              <p className="mt-2 text-xs text-zinc-600">
                                {formatRelativeDate(
                                  review.createdAt,
                                )}
                              </p>
                            </div>
                          </Link>

                          {review.rating !== null && (
                            <span className="flex h-10 shrink-0 items-center gap-1.5 rounded-xl bg-amber-500/10 px-3 text-sm font-bold text-amber-300">
                              <Star
                                size={16}
                                fill="currentColor"
                              />
                              {review.rating.toFixed(1)}
                            </span>
                          )}
                        </div>

                        <div className="p-5 sm:p-6">
                          {review.containsSpoilers &&
                          !review.isMine &&
                          !spoilerIsRevealed ? (
                            <button
                              type="button"
                              onClick={() => {
                                toggleSpoiler(review.id);
                              }}
                              className="flex w-full flex-col items-center rounded-2xl border border-amber-400/15 bg-amber-500/[0.05] px-5 py-8 text-center"
                            >
                              <Eye
                                size={24}
                                className="text-amber-300"
                              />
                              <span className="mt-3 text-sm font-semibold text-amber-200">
                                Spoiler olan rəyi göstər
                              </span>
                            </button>
                          ) : (
                            <p data-user-content className="whitespace-pre-wrap text-sm leading-7 text-zinc-300">
                              {review.content}
                            </p>
                          )}

                          {review.showHardwareProfile && (
                            <ReviewHardwareSummary
                              gpu={review.hardwareGpu}
                              cpu={review.hardwareCpu}
                              ramGb={review.hardwareRamGb}
                              vramGb={review.hardwareVramGb}
                              language={language}
                            />
                          )}

                          <div className="mt-5 flex items-center gap-5 border-t border-white/[0.06] pt-4 text-xs text-zinc-600">
                            <span className="inline-flex items-center gap-2">
                              <button
                                type="button"
                                disabled={
                                  !isAuthenticated ||
                                  review.isMine ||
                                  reviewLikeMutation.isPending
                                }
                                onClick={() => {
                                  reviewLikeMutation.mutate(review);
                                }}
                                className={`group inline-flex items-center font-semibold transition disabled:cursor-not-allowed disabled:opacity-45 ${
                                  review.isLikedByCurrentUser
                                    ? "text-rose-300"
                                    : "hover:text-rose-300"
                                }`}
                                aria-label="Rəyi bəyən"
                              >
                                <Heart
                                  size={15}
                                  className={`transition ${
                                    review.isLikedByCurrentUser
                                      ? "fill-current"
                                      : "fill-transparent group-hover:fill-current"
                                  }`}
                                />
                              </button>
                              <button
                                type="button"
                                onClick={() => {
                                  setLikesReview({
                                    id: review.id,
                                    count: review.likeCount,
                                  });
                                }}
                                className="font-semibold transition hover:text-rose-300"
                              >
                                {review.likeCount} bəyəni
                              </button>
                            </span>
                            <button
                              type="button"
                              onClick={() => {
                                setOpenReviewCommentIds((currentValue) => {
                                  const nextValue = new Set(currentValue);
                                  if (nextValue.has(review.id)) {
                                    nextValue.delete(review.id);
                                  } else {
                                    nextValue.add(review.id);
                                  }
                                  return nextValue;
                                });
                              }}
                              className={`inline-flex items-center gap-2 font-semibold transition hover:text-violet-300 ${
                                openReviewCommentIds.has(review.id)
                                  ? "text-violet-300"
                                  : ""
                              }`}
                            >
                              <MessageCircle size={15} />
                              {review.commentCount}
                            </button>
                          </div>
                        </div>

                        {openReviewCommentIds.has(review.id) && (
                          <ReviewCommentsPanel
                            reviewId={review.id}
                            isAuthenticated={isAuthenticated}
                          />
                        )}
                      </article>
                    );
                  })
                )}

                  </div>
                </div>
              </div>
            )}

            {visibleActiveTab === "wishlist" && (
              <ProfileGameCollection
                kind="wishlist"
                result={wishlistQuery.data}
                isLoading={wishlistQuery.isLoading}
                isError={wishlistQuery.isError}
                error={wishlistQuery.error}
                page={wishlistPage}
                onPageChange={setWishlistPage}
              />
            )}

            {visibleActiveTab === "favorites" && (
              <ProfileGameCollection
                kind="favorites"
                result={favoritesQuery.data}
                isLoading={favoritesQuery.isLoading}
                isError={favoritesQuery.isError}
                error={favoritesQuery.error}
                page={favoritesPage}
                onPageChange={setFavoritesPage}
              />
            )}

            {visibleActiveTab === "feed" &&
              isOwnProfile && storedUser && (
                <ProfileActivityFeed
                  result={feedQuery.data}
                  isLoading={feedQuery.isLoading}
                  isError={feedQuery.isError}
                  error={feedQuery.error}
                  page={feedPage}
                  category={feedCategory}
                  scope={feedScope}
                  currentUserId={storedUser.id}
                  onCategoryChange={(category) => {
                    setFeedCategory(category);
                    setFeedPage(1);
                  }}
                  onScopeChange={(scope) => {
                    setFeedScope(scope);
                    setFeedPage(1);
                  }}
                  onPageChange={setFeedPage}
                />
              )}
              </div>
            </>
          )}
        </section>
      </main>

      <Footer />

      <ReviewLikesModal
        reviewId={canViewProfileContent ? likesReview?.id ?? null : null}
        likeCount={likesReview?.count ?? 0}
        onClose={() => setLikesReview(null)}
      />

      <InteractionLikesModal
        contentType="post"
        contentId={canViewProfileContent ? likesPost?.id ?? null : null}
        likeCount={likesPost?.count ?? 0}
        onClose={() => setLikesPost(null)}
      />

      {isCreateListOpen && (
        <GameListEditorModal
          open
          title="Yeni oyun siyahısı"
          submitLabel="Siyahını yarat"
          isPending={createListMutation.isPending}
          errorMessage={
            createListMutation.isError
              ? getApiErrorMessage(
                  createListMutation.error,
                  "Siyahını yaratmaq mümkün olmadı.",
                )
              : null
          }
          onClose={() => {
            if (!createListMutation.isPending) {
              setIsCreateListOpen(false);
            }
          }}
          onSubmit={(values) => {
            createListMutation.mutate(values);
          }}
        />
      )}

      {canViewProfileContent && isFavoritesModalOpen && (
        <div
          className="fixed inset-0 z-[85] flex items-center justify-center bg-black/75 px-4 py-6 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          aria-labelledby="favorite-games-modal-title"
          onMouseDown={(event) => {
            if (event.currentTarget === event.target) {
              setIsFavoritesModalOpen(false);
            }
          }}
        >
          <section className="flex max-h-[88vh] w-full max-w-6xl flex-col overflow-hidden rounded-3xl border border-violet-400/15 bg-[#101015] shadow-2xl shadow-violet-950/40">
            <div className="flex items-start justify-between gap-4 border-b border-white/[0.07] px-5 py-4 sm:px-6 sm:py-5">
              <div>
                <p className="text-[11px] font-bold uppercase tracking-[0.18em] text-fuchsia-300">
                  {language === "en" ? "Favorite collection" : "Sevimli kolleksiya"}
                </p>
                <h2
                  id="favorite-games-modal-title"
                  className="mt-2 flex items-center gap-2 text-xl font-bold text-white"
                >
                  <Heart size={19} className="fill-rose-300 text-rose-300" />
                  {language === "en" ? "Favorite games" : "Sevimli oyunlar"}
                </h2>
                <p className="mt-2 text-sm text-zinc-500">
                  {isOwnProfile
                    ? language === "en"
                      ? "Your favorite game collection."
                      : "Sənin sevimli oyun kolleksiyan."
                    : language === "en"
                      ? `${profile.userName}'s favorite game collection.`
                      : `${profile.userName} istifadəçisinin sevimli oyun kolleksiyası.`}
                </p>
              </div>

              <button
                type="button"
                onClick={() => setIsFavoritesModalOpen(false)}
                className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-white/[0.08] bg-white/[0.04] text-zinc-400 transition hover:bg-white/[0.08] hover:text-white"
                aria-label={language === "en" ? "Close favorite games dialog" : "Sevimli oyunlar pəncərəsini bağla"}
              >
                <X size={18} />
              </button>
            </div>

            <div className="overflow-y-auto p-5 sm:p-6">
              <ProfileGameCollection
                kind="favorites"
                result={favoritesQuery.data}
                isLoading={favoritesQuery.isLoading}
                isError={favoritesQuery.isError}
                error={favoritesQuery.error}
                page={favoritesPage}
                onPageChange={setFavoritesPage}
              />
            </div>
          </section>
        </div>
      )}

      {canViewProfileContent &&
        (connectionModal === "followers" ||
          connectionModal === "following") && (
        <div
          className="fixed inset-0 z-[80] flex items-center justify-center bg-black/70 px-5 py-8 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          aria-label={connectionTitle}
          onMouseDown={(event) => {
            if (event.currentTarget === event.target) {
              setConnectionModal(null);
            }
          }}
        >
          <div className="w-full max-w-lg overflow-hidden rounded-3xl border border-white/[0.09] bg-[#111116] shadow-2xl shadow-black/60">
            <div className="flex items-center justify-between border-b border-white/[0.07] px-5 py-4">
              <div className="flex items-center gap-3">
                <Users
                  size={19}
                  className="text-violet-300"
                />
                <h2 className="font-semibold text-white">
                  {connectionTitle}
                </h2>
              </div>

              <button
                type="button"
                onClick={() => {
                  setConnectionModal(null);
                }}
                className="flex h-9 w-9 items-center justify-center rounded-xl text-zinc-500 transition hover:bg-white/[0.06] hover:text-white"
                aria-label="Pəncərəni bağla"
              >
                <X size={18} />
              </button>
            </div>

            <div className="max-h-[65vh] overflow-y-auto p-3">
              {isOwnProfile && connectionModal === "followers" && (
                <button
                  type="button"
                  onClick={() => setConnectionModal("requests")}
                  className="mb-3 flex w-full items-center gap-3 rounded-2xl border border-violet-400/15 bg-violet-500/[0.07] px-4 py-3 text-left transition hover:border-violet-400/25 hover:bg-violet-500/[0.12]"
                >
                  <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-violet-500/12 text-violet-300">
                    <UserPlus size={17} />
                  </span>
                  <span className="min-w-0 flex-1 text-sm font-semibold text-zinc-200">
                    {t("profile:followRequests.open")}
                  </span>
                  {(followRequestCountQuery.data?.totalCount ?? 0) > 0 && (
                    <span className="text-sm font-bold text-violet-200">
                      {followRequestCountQuery.data?.totalCount}
                    </span>
                  )}
                  <ChevronRight size={17} className="text-zinc-600" />
                </button>
              )}

              {connectionsQuery.isLoading ? (
                <div className="flex items-center justify-center gap-3 py-14 text-sm text-zinc-500">
                  <LoaderCircle
                    size={18}
                    className="animate-spin"
                  />
                  Siyahı yüklənir...
                </div>
              ) : connectionsQuery.isError ? (
                <div className="rounded-2xl border border-amber-400/15 bg-amber-500/[0.05] p-5 text-center text-sm leading-6 text-amber-100/70">
                  {getApiErrorMessage(
                    connectionsQuery.error,
                    "Bu siyahıya baxmaq mümkün deyil.",
                  )}
                </div>
              ) : !connectionsQuery.data?.length ? (
                <div className="py-14 text-center text-sm text-zinc-600">
                  Siyahı boşdur.
                </div>
              ) : (
                <div className="space-y-2">
                  {connectionsQuery.data.map(
                    (connectionUser) => (
                      <div
                        key={connectionUser.id}
                        className="flex items-center gap-3 rounded-2xl px-3 py-3 transition hover:bg-white/[0.05]"
                      >
                        <Link
                          to={`/users/${connectionUser.userName}`}
                          onClick={() => {
                            setConnectionModal(null);
                          }}
                          className="flex min-w-0 flex-1 items-center gap-3"
                        >
                          <UserAvatar
                            userName={
                              connectionUser.userName
                            }
                            profileImageUrl={
                              connectionUser.profileImageUrl
                            }
                            className="h-11 w-11 rounded-xl text-sm"
                          />

                          <div className="min-w-0">
                            <p className="truncate text-sm font-semibold text-zinc-200">
                              {connectionUser.userName}
                            </p>
                            {connectionUser.isFollowing && (
                              <p className="mt-1 text-xs text-violet-400">
                                İzləyirsən
                              </p>
                            )}
                          </div>
                        </Link>

                        {isOwnProfile &&
                        connectionModal === "followers" ? (
                          <button
                            type="button"
                            onClick={async () => {
                              const confirmed =
                                await requestConfirmation({
                                  title: `${connectionUser.userName} izləyicilərdən çıxarılsın?`,
                                  description:
                                    "Bu istifadəçi artıq səni izləməyəcək və izləyicilər siyahında görünməyəcək.",
                                  detail:
                                    "İstifadəçi sonradan səni yenidən izləyə və ya izləmə sorğusu göndərə bilər.",
                                  confirmLabel: "İzləyicidən çıxar",
                                  kind: "remove-user",
                                  tone: "warning",
                                });

                              if (confirmed) {
                                removeFollowerMutation.mutate(
                                  connectionUser.id,
                                );
                              }
                            }}
                            disabled={
                              removeFollowerMutation.isPending
                            }
                            className="flex h-9 shrink-0 items-center gap-1.5 rounded-xl border border-red-400/15 bg-red-500/[0.06] px-3 text-xs font-semibold text-red-300 transition hover:bg-red-500/15 disabled:opacity-50"
                          >
                            <UserX size={14} />
                            Çıxar
                          </button>
                        ) : isOwnProfile &&
                          connectionModal === "following" ? (
                          <button
                            type="button"
                            onClick={async () => {
                              const confirmed =
                                await requestConfirmation({
                                  title: `${connectionUser.userName} izlənməsin?`,
                                  description:
                                    "Bu istifadəçinin yeni paylaşımları və fəaliyyətləri artıq lentində görünməyəcək.",
                                  detail:
                                    "İstəsən, bu profili daha sonra yenidən izləyə bilərsən.",
                                  confirmLabel:
                                    "İzləməni dayandır",
                                  kind: "remove-user",
                                  tone: "warning",
                                });

                              if (confirmed) {
                                unfollowMutation.mutate(
                                  connectionUser.id,
                                );
                              }
                            }}
                            disabled={
                              unfollowMutation.isPending
                            }
                            className="flex h-9 shrink-0 items-center gap-1.5 rounded-xl border border-white/[0.08] bg-white/[0.04] px-3 text-xs font-semibold text-zinc-400 transition hover:bg-white/[0.08] hover:text-white disabled:opacity-50"
                          >
                            <UserX size={14} />
                            İzləməni dayandır
                          </button>
                        ) : (
                          <span className="shrink-0 text-xs font-semibold text-zinc-600">
                            Profilə bax
                          </span>
                        )}
                      </div>
                    ),
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {connectionModal === "requests" && isOwnProfile && (
        <FollowRequestsModal
          profileUserName={profile.userName}
          onBack={() => setConnectionModal("followers")}
          onClose={() => setConnectionModal(null)}
        />
      )}

      <SteamPlaytimeReconciliationModal
        isOpen={isPlaytimeReconciliationOpen}
        onClose={() => setIsPlaytimeReconciliationOpen(false)}
        onApplied={async (result) => {
          setNotice({
            type: "success",
            message: `${result.appliedGameCount} oyunun saatları yeniləndi.`,
          });
          await queryClient.invalidateQueries();
        }}
      />

      {isWarningOpen && canWarnUsers && (
        <div
          className="fixed inset-0 z-[180] flex items-center justify-center bg-slate-950/65 px-4 py-8 backdrop-blur-sm"
          role="presentation"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget && !warningMutation.isPending) {
              setIsWarningOpen(false);
              setWarningMessage("");
            }
          }}
        >
          <form
            role="dialog"
            aria-modal="true"
            aria-labelledby="profile-warning-title"
            className="wp-warning-modal w-full max-w-lg rounded-3xl p-5 sm:p-6"
            onSubmit={(event) => {
              event.preventDefault();
              const normalizedMessage = warningMessage.trim();
              if (normalizedMessage.length < 3) {
                setNotice({
                  type: "error",
                  message:
                    language === "en"
                      ? "Write a warning of at least 3 characters."
                      : "Ən azı 3 simvoldan ibarət xəbərdarlıq yaz.",
                });
                return;
              }
              warningMutation.mutate(normalizedMessage);
            }}
          >
            <div className="flex items-start justify-between gap-4">
              <div className="flex min-w-0 items-start gap-3">
                <span className="wp-warning-modal-icon flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl">
                  <MessageSquareWarning size={21} />
                </span>
                <div className="min-w-0">
                  <h2
                    id="profile-warning-title"
                    className="text-lg font-bold text-[var(--wp-text-strong)]"
                  >
                    {language === "en" ? "Send warning" : "Xəbərdarlıq göndər"}
                  </h2>
                  <p className="mt-1 truncate text-sm text-[var(--wp-text-muted)]">
                    @{loadedProfile.userName}
                  </p>
                </div>
              </div>
              <button
                type="button"
                disabled={warningMutation.isPending}
                onClick={() => {
                  setIsWarningOpen(false);
                  setWarningMessage("");
                }}
                className="wp-warning-modal-close flex h-9 w-9 shrink-0 items-center justify-center rounded-xl transition disabled:opacity-50"
                aria-label={language === "en" ? "Close" : "Bağla"}
              >
                <X size={18} />
              </button>
            </div>

            <p className="mt-5 text-sm leading-6 text-[var(--wp-text-muted)]">
              {language === "en"
                ? "The user will receive this message as an official moderation warning."
                : "İstifadəçi bu mesajı rəsmi moderasiya xəbərdarlığı kimi alacaq."}
            </p>

            <label className="mt-4 block">
              <span className="mb-2 block text-xs font-bold uppercase tracking-[0.14em] text-[var(--wp-text-muted)]">
                {language === "en" ? "Warning message" : "Xəbərdarlıq mətni"}
              </span>
              <textarea
                autoFocus
                value={warningMessage}
                onChange={(event) => setWarningMessage(event.target.value)}
                maxLength={1000}
                rows={6}
                placeholder={
                  language === "en"
                    ? "Explain which rule was violated and what the user should change..."
                    : "Hansı qaydanın pozulduğunu və istifadəçinin nəyi dəyişməli olduğunu izah et..."
                }
                className="wp-warning-textarea w-full resize-y rounded-2xl px-4 py-3 text-sm leading-6 outline-none transition"
              />
            </label>

            <div className="mt-2 flex items-center justify-between gap-3 text-xs text-[var(--wp-text-muted)]">
              <span>
                {language === "en"
                  ? "Minimum 3 characters"
                  : "Minimum 3 simvol"}
              </span>
              <span>{warningMessage.length}/1000</span>
            </div>

            <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
              <button
                type="button"
                disabled={warningMutation.isPending}
                onClick={() => {
                  setIsWarningOpen(false);
                  setWarningMessage("");
                }}
                className="wp-warning-secondary h-11 rounded-xl px-5 text-sm font-semibold transition disabled:opacity-50"
              >
                {language === "en" ? "Cancel" : "Ləğv et"}
              </button>
              <button
                type="submit"
                disabled={warningMutation.isPending || warningMessage.trim().length < 3}
                className="wp-warning-primary flex h-11 items-center justify-center gap-2 rounded-xl px-5 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-50"
              >
                {warningMutation.isPending ? (
                  <LoaderCircle size={17} className="animate-spin" />
                ) : (
                  <MessageSquareWarning size={17} />
                )}
                {warningMutation.isPending
                  ? language === "en"
                    ? "Sending..."
                    : "Göndərilir..."
                  : language === "en"
                    ? "Send warning"
                    : "Xəbərdarlığı göndər"}
              </button>
            </div>
          </form>
        </div>
      )}

      <UserReportModal
        isOpen={isReportOpen}
        reportedUserId={loadedProfile.id}
        reportedUserName={loadedProfile.userName}
        onClose={() => setIsReportOpen(false)}
        onSubmitted={() => {
          setIsReportOpen(false);
          setNotice({
            type: "success",
            message: "Şikayət moderasiya komandasına göndərildi.",
          });
        }}
      />
    </div>
  );
}
