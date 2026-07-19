import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  Ban,
  Bookmark,
  CalendarDays,
  Check,
  Clock3,
  Eye,
  Gamepad2,
  Heart,
  History,
  ListChecks,
  LoaderCircle,
  LockKeyhole,
  MessageCircle,
  MessageSquareText,
  Pencil,
  Send,
  Settings,
  Shield,
  Star,
  Trash2,
  UserCheck,
  UserPlus,
  UserX,
  Users,
  X,
} from "lucide-react";
import {
  useMemo,
  useState,
} from "react";
import {
  Link,
  useNavigate,
  useParams,
} from "react-router-dom";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { ProfileActivityJournal } from "../components/profile/ProfileActivityJournal";
import { ProfileGameCollection } from "../components/profile/ProfileGameCollection";
import { getApiErrorMessage } from "../lib/api";
import {
  formatDate,
  formatRelativeDate,
  resolveAssetUrl,
} from "../lib/helpers";
import {
  blockUser,
  createPost,
  deletePost,
  followUser,
  getFollowers,
  getFollowing,
  getFollowStatus,
  getMyActivities,
  getUserGameLists,
  getUserGames,
  getUserPosts,
  getUserProfile,
  getUserReviews,
  removeFollower,
  unfollowUser,
  updatePost,
} from "../services/profileService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../stores/authStore";
import type {
  PostDTO,
  UserListItemDTO,
} from "../types/profile";

type ProfileTab =
  | "posts"
  | "lists"
  | "reviews"
  | "wishlist"
  | "favorites"
  | "journal";

type ConnectionModal =
  | "followers"
  | "following"
  | null;

interface NoticeState {
  type: "success" | "error";
  message: string;
}

const profileTabs: Array<{
  id: ProfileTab;
  label: string;
  icon: typeof MessageSquareText;
  ownerOnly?: boolean;
}> = [
  {
    id: "posts",
    label: "Postlar",
    icon: MessageSquareText,
  },
  {
    id: "lists",
    label: "Siyahılar",
    icon: ListChecks,
  },
  {
    id: "reviews",
    label: "Rəylər",
    icon: Star,
  },
  {
    id: "wishlist",
    label: "Wishlist",
    icon: Bookmark,
  },
  {
    id: "favorites",
    label: "Favorilər",
    icon: Heart,
  },
  {
    id: "journal",
    label: "Jurnal",
    icon: History,
    ownerOnly: true,
  },
];

const visibilityLabels = {
  Public: "Hamı üçün",
  Private: "Gizli",
  FollowersOnly: "Yalnız izləyicilər",
} as const;

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
    <div className="min-h-screen bg-[#07070a] text-zinc-100">
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
  const { userName = "" } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const storedUser = useAuthStore(
    (state) => state.user,
  );
  const accessToken = useAuthStore(
    (state) => state.accessToken,
  );
  const expiresAtUtc = useAuthStore(
    (state) => state.expiresAtUtc,
  );

  const [activeTab, setActiveTab] =
    useState<ProfileTab>("posts");
  const [connectionModal, setConnectionModal] =
    useState<ConnectionModal>(null);
  const [notice, setNotice] =
    useState<NoticeState | null>(null);
  const [newPostContent, setNewPostContent] =
    useState("");
  const [editingPostId, setEditingPostId] =
    useState<number | null>(null);
  const [editingPostContent, setEditingPostContent] =
    useState("");
  const [revealedSpoilers, setRevealedSpoilers] =
    useState<Set<number>>(() => new Set());
  const [wishlistPage, setWishlistPage] =
    useState(1);
  const [favoritesPage, setFavoritesPage] =
    useState(1);
  const [journalPage, setJournalPage] =
    useState(1);

  const isAuthenticated =
    isAuthSessionValid(
      accessToken,
      expiresAtUtc,
    ) && storedUser !== null;

  const profileQuery = useQuery({
    queryKey: ["user-profile", userName],
    queryFn: () => getUserProfile(userName),
    enabled: userName.trim().length > 0,
  });

  const profile = profileQuery.data;
  const isOwnProfile =
    profile !== undefined &&
    storedUser?.id === profile.id;
  const visibleActiveTab =
    !isOwnProfile && activeTab === "journal"
      ? "posts"
      : activeTab;

  const followStatusQuery = useQuery({
    queryKey: [
      "follow-status",
      profile?.id,
    ],
    queryFn: () =>
      getFollowStatus(profile!.id),
    enabled:
      isAuthenticated &&
      profile !== undefined &&
      !isOwnProfile,
  });

  const postsQuery = useQuery({
    queryKey: ["user-posts", profile?.id],
    queryFn: () => getUserPosts(profile!.id),
    enabled:
      profile !== undefined &&
      visibleActiveTab === "posts",
  });

  const listsQuery = useQuery({
    queryKey: ["user-game-lists", profile?.id],
    queryFn: () =>
      getUserGameLists(profile!.id),
    enabled:
      profile !== undefined &&
      visibleActiveTab === "lists",
  });

  const reviewsQuery = useQuery({
    queryKey: ["user-reviews", profile?.id],
    queryFn: () => getUserReviews(profile!.id),
    enabled:
      profile !== undefined &&
      visibleActiveTab === "reviews",
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
      profile !== undefined &&
      visibleActiveTab === "wishlist",
  });

  const favoritesQuery = useQuery({
    queryKey: [
      "user-games",
      profile?.id,
      "favorites",
      favoritesPage,
    ],
    queryFn: () =>
      getUserGames(profile!.id, {
        isFavorite: true,
        pageNumber: favoritesPage,
        pageSize: 12,
      }),
    enabled:
      profile !== undefined &&
      visibleActiveTab === "favorites",
  });

  const journalQuery = useQuery({
    queryKey: ["user-activities", profile?.id, journalPage],
    queryFn: () =>
      getMyActivities(journalPage, 20),
    enabled:
      profile !== undefined &&
      isOwnProfile &&
      visibleActiveTab === "journal",
  });

  const connectionsQuery = useQuery({
    queryKey: [
      "user-connections",
      profile?.id,
      connectionModal,
    ],
    queryFn: () => {
      if (!profile || !connectionModal) {
        return Promise.resolve<UserListItemDTO[]>([]);
      }

      return connectionModal === "followers"
        ? getFollowers(profile.id)
        : getFollowing(profile.id);
    },
    enabled:
      profile !== undefined &&
      connectionModal !== null,
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
    }: {
      postId: number;
      content: string;
    }) =>
      updatePost(postId, {
        content,
      }),
    onSuccess: async () => {
      setEditingPostId(null);
      setEditingPostContent("");
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
    mutationFn: deletePost,
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
  const gameLists = listsQuery.data ?? [];
  const reviews = reviewsQuery.data?.items ?? [];

  const connectionTitle = useMemo(() => {
    return connectionModal === "followers"
      ? "İzləyicilər"
      : "İzlənilənlər";
  }, [connectionModal]);

  if (profileQuery.isLoading) {
    return <ProfileLoadingState />;
  }

  if (profileQuery.isError || !profile) {
    return (
      <div className="min-h-screen bg-[#07070a] text-zinc-100">
        <Navbar />

        <main className="mx-auto flex min-h-[70vh] max-w-2xl items-center justify-center px-5 py-16 text-center">
          <div className="w-full rounded-3xl border border-red-400/15 bg-red-500/[0.05] p-8">
            <X
              size={32}
              className="mx-auto text-red-300"
            />

            <h1 className="mt-5 text-2xl font-bold text-white">
              Profil açılmadı
            </h1>

            <p className="mt-3 text-sm leading-7 text-red-200/80">
              {getApiErrorMessage(
                profileQuery.error,
                "İstifadəçi tapılmadı və ya bu profilə baxmaq mümkün deyil.",
              )}
            </p>

            <Link
              to="/"
              className="mx-auto mt-6 flex h-11 w-fit items-center justify-center rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white transition hover:bg-violet-500"
            >
              Ana səhifəyə qayıt
            </Link>
          </div>
        </main>
      </div>
    );
  }

  const loadedProfile = profile;

  function handleFollowAction() {
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
      unfollowMutation.mutate(loadedProfile.id);
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
    const normalizedContent =
      newPostContent.trim();

    if (!normalizedContent) {
      setNotice({
        type: "error",
        message: "Post mətni boş ola bilməz.",
      });
      return;
    }

    createPostMutation.mutate({
      content: normalizedContent,
    });
  }

  function beginPostEdit(post: PostDTO) {
    setEditingPostId(post.id);
    setEditingPostContent(post.content);
  }

  function handlePostUpdate(postId: number) {
    const normalizedContent =
      editingPostContent.trim();

    if (!normalizedContent) {
      setNotice({
        type: "error",
        message: "Post mətni boş ola bilməz.",
      });
      return;
    }

    updatePostMutation.mutate({
      postId,
      content: normalizedContent,
    });
  }

  function handlePostDelete(postId: number) {
    const shouldDelete = window.confirm(
      "Bu postu silmək istəyirsən?",
    );

    if (shouldDelete) {
      deletePostMutation.mutate(postId);
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
    <div className="min-h-screen bg-[#07070a] text-zinc-100">
      <Navbar />

      <main>
        <section className="relative overflow-hidden border-b border-white/[0.06] bg-[#0a0a0e]">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_20%,rgba(124,58,237,0.17),transparent_30%),radial-gradient(circle_at_90%_30%,rgba(217,70,239,0.07),transparent_25%)]" />

          <div className="relative mx-auto max-w-7xl px-5 py-12 lg:px-8 lg:py-16">
            <div className="flex flex-col gap-7 md:flex-row md:items-start md:justify-between">
              <div className="flex min-w-0 flex-col gap-6 sm:flex-row sm:items-start">
                <UserAvatar
                  userName={profile.userName}
                  profileImageUrl={
                    profile.profileImageUrl
                  }
                  className="h-28 w-28 rounded-3xl border border-white/10 text-3xl shadow-2xl shadow-black/30"
                />

                <div className="min-w-0 pt-1">
                  <div className="flex flex-wrap items-center gap-3">
                    <h1 className="truncate text-3xl font-bold tracking-tight text-white sm:text-4xl">
                      {profile.userName}
                    </h1>

                    {profile.isPrivate && (
                      <span className="inline-flex items-center gap-1.5 rounded-full border border-amber-400/20 bg-amber-500/10 px-3 py-1 text-xs font-semibold text-amber-200">
                        <LockKeyhole size={13} />
                        Private
                      </span>
                    )}
                  </div>

                  <p className="mt-4 max-w-2xl whitespace-pre-wrap text-sm leading-7 text-zinc-400">
                    {profile.bio ??
                      "Bu istifadəçi hələ bio əlavə etməyib."}
                  </p>

                  <div className="mt-5 flex flex-wrap items-center gap-x-5 gap-y-2 text-xs text-zinc-500">
                    <span className="inline-flex items-center gap-2">
                      <CalendarDays size={15} />
                      {formatDate(
                        profile.createdAt,
                      )}{" "}
                      tarixindən üzvdür
                    </span>

                    {profile.isPrivate && (
                      <span className="inline-flex items-center gap-2">
                        <Shield size={15} />
                        Məxfi hesab
                      </span>
                    )}
                  </div>
                </div>
              </div>

              <div className="flex shrink-0 flex-wrap gap-3">
                {isOwnProfile ? (
                  <Link
                    to="/settings"
                    className="flex h-11 items-center justify-center gap-2 rounded-xl border border-white/[0.09] bg-white/[0.05] px-5 text-sm font-semibold text-zinc-200 transition hover:bg-white/[0.09] hover:text-white"
                  >
                    <Settings size={17} />
                    Profili düzəlt
                  </Link>
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
                        ? "İzləyirsən"
                        : followStatus?.isPending
                          ? "Sorğu göndərilib"
                          : "İzlə"}
                    </button>

                    <button
                      type="button"
                      onClick={handleBlock}
                      disabled={actionIsPending}
                      className="flex h-11 items-center justify-center gap-2 rounded-xl border border-red-400/15 bg-red-500/[0.06] px-4 text-sm font-semibold text-red-300 transition hover:bg-red-500/15 disabled:opacity-60"
                    >
                      <Ban size={17} />
                      Blokla
                    </button>
                  </>
                )}
              </div>
            </div>

            <div className="mt-9 grid max-w-xl grid-cols-3 overflow-hidden rounded-2xl border border-white/[0.07] bg-white/[0.025]">
              <button
                type="button"
                onClick={() => {
                  setConnectionModal("followers");
                }}
                className="px-4 py-4 text-center transition hover:bg-white/[0.04]"
              >
                <span className="block text-xl font-bold text-white">
                  {profile.followerCount}
                </span>
                <span className="mt-1 block text-xs text-zinc-500">
                  İzləyici
                </span>
              </button>

              <button
                type="button"
                onClick={() => {
                  setConnectionModal("following");
                }}
                className="border-x border-white/[0.06] px-4 py-4 text-center transition hover:bg-white/[0.04]"
              >
                <span className="block text-xl font-bold text-white">
                  {profile.followingCount}
                </span>
                <span className="mt-1 block text-xs text-zinc-500">
                  İzlənilən
                </span>
              </button>

              <div className="px-4 py-4 text-center">
                <span className="block text-xl font-bold text-white">
                  {profile.postCount}
                </span>
                <span className="mt-1 block text-xs text-zinc-500">
                  Post
                </span>
              </div>
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

          <div className="flex gap-2 overflow-x-auto border-b border-white/[0.07] pb-3">
            {visibleProfileTabs.map((tab) => {
              const Icon = tab.icon;
              const isActive = visibleActiveTab === tab.id;

              return (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => {
                    setActiveTab(tab.id);
                  }}
                  className={`flex shrink-0 items-center gap-2 rounded-xl px-4 py-2.5 text-sm font-semibold transition ${
                    isActive
                      ? "bg-violet-500/12 text-violet-200"
                      : "text-zinc-500 hover:bg-white/[0.04] hover:text-zinc-200"
                  }`}
                >
                  <Icon size={17} />
                  {tab.label}
                </button>
              );
            })}
          </div>

          <div className="mt-7">
            {visibleActiveTab === "posts" && (
              <div className="mx-auto max-w-3xl space-y-5">
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

                        <div className="mt-3 flex items-center justify-between gap-4">
                          <span className="text-xs text-zinc-600">
                            {newPostContent.length}/2000
                          </span>

                          <button
                            type="button"
                            onClick={handleCreatePost}
                            disabled={
                              createPostMutation.isPending ||
                              newPostContent.trim().length === 0
                            }
                            className="flex h-10 items-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-50"
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
                    <MessageSquareText
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
                      className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6"
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

                        {post.isMine && (
                          <div className="flex shrink-0 gap-1">
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
                            <button
                              type="button"
                              onClick={() => {
                                handlePostDelete(post.id);
                              }}
                              disabled={
                                deletePostMutation.isPending
                              }
                              className="flex h-9 w-9 items-center justify-center rounded-lg text-zinc-600 transition hover:bg-red-500/[0.08] hover:text-red-300 disabled:opacity-50"
                              aria-label="Postu sil"
                            >
                              <Trash2 size={16} />
                            </button>
                          </div>
                        )}
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
                            className="w-full resize-none rounded-2xl border border-white/[0.08] bg-white/[0.03] px-4 py-3 text-sm leading-7 text-zinc-100 outline-none transition focus:border-violet-400/50 focus:ring-4 focus:ring-violet-500/10"
                          />

                          <div className="mt-3 flex justify-end gap-2">
                            <button
                              type="button"
                              onClick={() => {
                                setEditingPostId(null);
                                setEditingPostContent("");
                              }}
                              className="rounded-xl px-4 py-2 text-sm font-medium text-zinc-500 transition hover:bg-white/[0.05] hover:text-zinc-300"
                            >
                              Ləğv et
                            </button>
                            <button
                              type="button"
                              onClick={() => {
                                handlePostUpdate(post.id);
                              }}
                              disabled={
                                updatePostMutation.isPending ||
                                editingPostContent.trim()
                                  .length === 0
                              }
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
                      ) : (
                        <p className="mt-5 whitespace-pre-wrap break-words text-sm leading-7 text-zinc-300">
                          {post.content}
                        </p>
                      )}
                    </article>
                  ))
                )}
              </div>
            )}

            {visibleActiveTab === "lists" && (
              <div>
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
                      Görünən oyun siyahısı yoxdur.
                    </p>
                  </div>
                ) : (
                  <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                    {gameLists.map((list) => (
                      <article
                        key={list.id}
                        className="rounded-3xl border border-white/[0.07] bg-[#101015] p-6"
                      >
                        <div className="flex items-start justify-between gap-4">
                          <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                            <ListChecks size={20} />
                          </span>

                          <span className="rounded-full border border-white/[0.07] bg-white/[0.03] px-3 py-1 text-[11px] font-semibold text-zinc-500">
                            {
                              visibilityLabels[
                                list.visibility
                              ]
                            }
                          </span>
                        </div>

                        <h2 className="mt-5 text-lg font-semibold text-white">
                          {list.name}
                        </h2>

                        <p className="mt-2 line-clamp-3 min-h-[4.5rem] text-sm leading-6 text-zinc-500">
                          {list.description ??
                            "Açıqlama əlavə edilməyib."}
                        </p>

                        <div className="mt-5 flex items-center justify-between border-t border-white/[0.06] pt-4 text-xs text-zinc-600">
                          <span className="inline-flex items-center gap-2">
                            <Gamepad2 size={15} />
                            {list.gameCount} oyun
                          </span>
                          <span>
                            {formatRelativeDate(
                              list.createdAt,
                            )}
                          </span>
                        </div>
                      </article>
                    ))}
                  </div>
                )}
              </div>
            )}

            {visibleActiveTab === "reviews" && (
              <div className="mx-auto max-w-4xl space-y-5">
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
                      Hələ heç bir rəy yoxdur.
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
                        key={review.id}
                        className="overflow-hidden rounded-3xl border border-white/[0.07] bg-[#101015]"
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
                            <p className="whitespace-pre-wrap text-sm leading-7 text-zinc-300">
                              {review.content}
                            </p>
                          )}

                          <div className="mt-5 flex items-center gap-5 border-t border-white/[0.06] pt-4 text-xs text-zinc-600">
                            <span className="inline-flex items-center gap-2">
                              <Heart size={15} />
                              {review.likeCount}
                            </span>
                            <span className="inline-flex items-center gap-2">
                              <MessageCircle size={15} />
                              {review.commentCount}
                            </span>
                          </div>
                        </div>
                      </article>
                    );
                  })
                )}
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

            {visibleActiveTab === "journal" &&
              isOwnProfile && (
                <ProfileActivityJournal
                  result={journalQuery.data}
                  isLoading={journalQuery.isLoading}
                  isError={journalQuery.isError}
                  error={journalQuery.error}
                  page={journalPage}
                  onPageChange={setJournalPage}
                />
              )}
          </div>
        </section>
      </main>

      <Footer />

      {connectionModal && (
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
                            onClick={() => {
                              removeFollowerMutation.mutate(
                                connectionUser.id,
                              );
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
                            onClick={() => {
                              unfollowMutation.mutate(
                                connectionUser.id,
                              );
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
    </div>
  );
}
