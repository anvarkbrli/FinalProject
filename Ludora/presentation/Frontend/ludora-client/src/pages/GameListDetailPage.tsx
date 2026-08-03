import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ArrowLeft,
  CalendarDays,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Copy,
  Crown,
  EyeOff,
  Gamepad2,
  Grid2X2,
  GripVertical,
  Heart,
  ListChecks,
  ListOrdered,
  LoaderCircle,
  LockKeyhole,
  MessageCircle,
  Pencil,
  Plus,
  Rows3,
  Search,
  Star,
  Trash2,
  UserRoundPlus,
  Users,
  X,
} from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { CopyShareButton } from "../components/common/CopyShareButton";
import { InteractionCommentsPanel } from "../components/interactions/InteractionCommentsPanel";
import { InteractionLikesModal } from "../components/interactions/InteractionLikesModal";
import { GameListCloneModal } from "../components/lists/GameListCloneModal";
import "../components/lists/game-list-detail-theme.css";
import { GameListCollaboratorsModal } from "../components/lists/GameListCollaboratorsModal";
import {
  GameListEditorModal,
  type GameListEditorValues,
} from "../components/lists/GameListEditorModal";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { ApiErrorStatusPage } from "../components/status/ApiErrorStatusPage";
import { useI18n } from "../i18n/useI18n";
import { getApiErrorMessage } from "../lib/api";
import {
  formatDate,
  formatLongDate,
  getReleaseYear,
  resolveAssetUrl,
} from "../lib/helpers";
import { useConfirmation } from "../lib/confirmation";
import {
  likeContent,
  unlikeContent,
} from "../services/contentInteractionsService";
import {
  acceptGameListCollaboration,
  addGameToList,
  cloneGameList,
  declineGameListCollaboration,
  deleteGameList,
  getGameList,
  getGameListPlayedProgress,
  removeGameFromList,
  reorderGameList,
  updateGameList,
} from "../services/gameListsService";
import { getGames } from "../services/gamesService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../stores/authStore";
import type { GameListDetailDTO, GameListEntryDTO } from "../types/gameLists";
import type { GameListItemDTO } from "../types/home";
import type { UserGameStatus } from "../types/userGames";

interface NoticeState {
  type: "success" | "error";
  message: string;
}

type DisplayMode = "grid" | "detail";

const GAMES_PER_PAGE = 16;

function GameCover({
  title,
  coverImageUrl,
}: {
  title: string;
  coverImageUrl: string | null;
}) {
  const imageUrl = resolveAssetUrl(coverImageUrl);

  return (
    <span className="flex h-28 w-20 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-white/[0.08] bg-zinc-900 text-zinc-700 shadow-lg shadow-black/20">
      {imageUrl ? (
        <img
          src={imageUrl}
          alt={title}
          className="h-full w-full object-cover"
        />
      ) : (
        <Gamepad2 size={24} />
      )}
    </span>
  );
}

function OwnerAvatar({
  userName,
  profileImageUrl,
}: {
  userName: string;
  profileImageUrl: string | null;
}) {
  const imageUrl = resolveAssetUrl(profileImageUrl);

  return (
    <span className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-white/[0.08] bg-violet-500/10 text-sm font-black text-violet-200 shadow-lg shadow-black/20">
      {imageUrl ? (
        <img
          src={imageUrl}
          alt={`${userName} profil şəkli`}
          className="h-full w-full object-cover"
        />
      ) : (
        userName.trim().charAt(0).toUpperCase() || "?"
      )}
    </span>
  );
}

function getRankBadgeClasses(position: number) {
  switch (position) {
    case 1:
      return "border-amber-300/25 bg-amber-500/[0.11] text-amber-300 shadow-[0_0_28px_rgba(245,158,11,0.14)]";
    case 2:
      return "game-list-rank-silver";
    case 3:
      return "border-orange-400/25 bg-orange-500/[0.10] text-orange-300 shadow-[0_0_28px_rgba(194,100,45,0.12)]";
    default:
      return "border-white/[0.08] bg-white/[0.04] text-zinc-400";
  }
}


function getStatusBadgeStyles(status: UserGameStatus) {
  switch (status) {
    case "Playing":
      return {
        badge: "border-emerald-300/20 bg-[#161a1a]/95 text-emerald-100",
        dot: "bg-emerald-400",
      };
    case "Played":
      return {
        badge: "border-violet-300/20 bg-[#181720]/95 text-violet-100",
        dot: "bg-violet-400",
      };
    case "PlanToPlay":
      return {
        badge: "border-sky-300/20 bg-[#151a20]/95 text-sky-100",
        dot: "bg-sky-400",
      };
    case "Dropped":
      return {
        badge: "border-orange-300/20 bg-[#1d1916]/95 text-orange-100",
        dot: "bg-orange-400",
      };
  }
}

function gameKey(game: Pick<GameListEntryDTO, "source" | "id">) {
  return `${game.source}:${game.id}`;
}

export function GameListDetailPage() {
  const { language, t } = useI18n();
  const { id = "" } = useParams();
  const listId = Number(id);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const requestConfirmation = useConfirmation();
  const accessToken = useAuthStore((state) => state.accessToken);
  const expiresAtUtc = useAuthStore((state) => state.expiresAtUtc);
  const storedUser = useAuthStore((state) => state.user);
  const isAuthInitialized = useAuthStore((state) => state.isInitialized);
  const isAuthenticated =
    isAuthSessionValid(accessToken, expiresAtUtc) && storedUser !== null;
  const currentUserCacheKey = isAuthenticated && storedUser
    ? storedUser.id
    : "anonymous";
  const gameListQueryKey = [
    "game-list",
    listId,
    currentUserCacheKey,
  ] as const;

  const [isEditorOpen, setIsEditorOpen] = useState(false);
  const [isCloneOpen, setIsCloneOpen] = useState(false);
  const [isCollaboratorsOpen, setIsCollaboratorsOpen] = useState(false);
  const [searchText, setSearchText] = useState("");
  const [notice, setNotice] = useState<NoticeState | null>(null);
  const [isCommentsOpen, setIsCommentsOpen] = useState(false);
  const [isLikesOpen, setIsLikesOpen] = useState(false);
  const [draggedGameKey, setDraggedGameKey] = useState<string | null>(null);
  const [pageState, setPageState] = useState({ listId, page: 1 });
  const [displayMode, setDisplayMode] = useState<DisplayMode>("grid");
  const [fadePlayedGames, setFadePlayedGames] = useState(false);
  const [isDisplayMenuOpen, setIsDisplayMenuOpen] = useState(false);
  const [isProgressExpanded, setIsProgressExpanded] = useState(false);
  const displayMenuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!isDisplayMenuOpen) return;

    const closeMenu = (event: MouseEvent) => {
      if (!displayMenuRef.current?.contains(event.target as Node)) {
        setIsDisplayMenuOpen(false);
      }
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsDisplayMenuOpen(false);
      }
    };

    document.addEventListener("mousedown", closeMenu);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("mousedown", closeMenu);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [isDisplayMenuOpen]);

  const listQuery = useQuery({
    queryKey: gameListQueryKey,
    queryFn: () => getGameList(listId),
    enabled:
      isAuthInitialized &&
      Number.isInteger(listId) &&
      listId > 0,
    staleTime: 0,
    refetchOnMount: "always",
  });


  const playedProgressQuery = useQuery({
    queryKey: [
      "game-list-played-progress",
      listId,
      currentUserCacheKey,
    ],
    queryFn: () => getGameListPlayedProgress(listId),
    enabled:
      isAuthInitialized &&
      isAuthenticated &&
      Number.isInteger(listId) &&
      listId > 0,
    staleTime: 0,
    refetchOnMount: "always",
  });

  const normalizedSearch = searchText.trim();
  const searchQuery = useQuery({
    queryKey: ["game-list-search", normalizedSearch],
    queryFn: () =>
      getGames({
        searchName: normalizedSearch,
        includeRelatedContent: false,
        pageNumber: 1,
        pageSize: 8,
      }),
    enabled:
      listQuery.data?.canEditGames === true && normalizedSearch.length >= 2,
  });

  const invalidateList = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: gameListQueryKey }),
      queryClient.invalidateQueries({ queryKey: ["user-game-lists"] }),
    ]);
  };

  const likeMutation = useMutation({
    mutationFn: async () => {
      const list = listQuery.data!;
      if (list.isLikedByCurrentUser) {
        await unlikeContent("game-list", listId);
      } else {
        await likeContent("game-list", listId);
      }
    },
    onSuccess: async () => {
      await Promise.all([
        invalidateList(),
        queryClient.invalidateQueries({
          queryKey: ["content-likes", "game-list", listId],
        }),
      ]);
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(error, t("lists:errors.like")),
      });
    },
  });

  const updateMutation = useMutation({
    mutationFn: (values: GameListEditorValues) =>
      updateGameList(listId, values),
    onSuccess: async () => {
      setIsEditorOpen(false);
      await invalidateList();
      setNotice({ type: "success", message: t("lists:notices.updated") });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteGameList(listId),
    onSuccess: () => {
      const ownerUserName = listQuery.data?.ownerUserName;
      navigate(ownerUserName ? `/users/${ownerUserName}` : "/", {
        replace: true,
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(error, t("lists:errors.delete")),
      });
    },
  });

  const addMutation = useMutation({
    mutationFn: (game: GameListItemDTO) =>
      addGameToList(listId, game.source, game.id),
    onSuccess: async () => {
      await invalidateList();
      setNotice({ type: "success", message: t("lists:notices.gameAdded") });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(error, t("lists:errors.addGame")),
      });
    },
  });

  const removeMutation = useMutation({
    mutationFn: (game: GameListEntryDTO) =>
      removeGameFromList(listId, game.source, game.id),
    onSuccess: async () => {
      await invalidateList();
      setNotice({ type: "success", message: t("lists:notices.gameRemoved") });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(error, t("lists:errors.removeGame")),
      });
    },
  });

  const reorderMutation = useMutation({
    mutationFn: (games: GameListEntryDTO[]) =>
      reorderGameList(
        listId,
        games.map((game) => ({ source: game.source, gameId: game.id })),
      ),
    onSuccess: invalidateList,
    onError: async (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(error, t("lists:errors.reorder")),
      });
      await invalidateList();
    },
  });

  const cloneMutation = useMutation({
    mutationFn: (request: { name: string; visibility: "Public" | "Private" | "FollowersOnly" }) =>
      cloneGameList(listId, request),
    onSuccess: (cloneId) => {
      setIsCloneOpen(false);
      navigate(`/lists/${cloneId}`);
    },
  });

  const acceptMutation = useMutation({
    mutationFn: () => acceptGameListCollaboration(listId),
    onSuccess: async () => {
      await invalidateList();
      setNotice({ type: "success", message: t("lists:collaboration.acceptedNotice") });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(error, t("lists:collaboration.acceptError")),
      });
    },
  });

  const declineMutation = useMutation({
    mutationFn: () => declineGameListCollaboration(listId),
    onSuccess: () => navigate(`/users/${listQuery.data?.ownerUserName ?? ""}`),
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(error, t("lists:collaboration.declineError")),
      });
    },
  });

  const currentGameKeys = useMemo(
    () => new Set((listQuery.data?.games ?? []).map((game) => gameKey(game))),
    [listQuery.data?.games],
  );

  const totalGamePages = Math.max(
    1,
    Math.ceil((listQuery.data?.games.length ?? 0) / GAMES_PER_PAGE),
  );
  const currentPage = Math.min(
    pageState.listId === listId ? pageState.page : 1,
    totalGamePages,
  );
  const pageStartIndex = (currentPage - 1) * GAMES_PER_PAGE;
  const visibleGames = useMemo(
    () =>
      (listQuery.data?.games ?? []).slice(
        pageStartIndex,
        pageStartIndex + GAMES_PER_PAGE,
      ),
    [listQuery.data?.games, pageStartIndex],
  );

  function goToPage(page: number) {
    setPageState({
      listId,
      page: Math.max(1, Math.min(totalGamePages, page)),
    });
  }

  if (!isAuthInitialized || listQuery.isPending) {
    return (
      <div className="min-h-screen wellplayd-page-bg text-zinc-100">
        <Navbar />
        <main className="flex min-h-[70vh] items-center justify-center gap-3 text-sm text-zinc-500">
          <LoaderCircle size={20} className="animate-spin" />
          {t("lists:detail.loading")}
        </main>
      </div>
    );
  }

  if (listQuery.isError || !listQuery.data) {
    return <ApiErrorStatusPage error={listQuery.error} entityName={t("lists:detail.entityName")} />;
  }

  const list = listQuery.data;
  const VisibilityIcon =
    list.visibility === "Public"
      ? Users
      : list.visibility === "FollowersOnly"
        ? Check
        : LockKeyhole;
  const searchResults = searchQuery.data?.items ?? [];
  const backgroundUrl = resolveAssetUrl(list.backgroundCoverImageUrl);
  const isPendingInvite = list.currentUserCollaborationStatus === "Pending";
  const isAcceptedCollaborator =
    list.currentUserCollaborationStatus === "Accepted";
  const cloneName =
    language === "en" ? `Copy of ${list.name}` : `${list.name} — surəti`;
  const progressCountFromEndpoint = Number(
    playedProgressQuery.data?.playedGameCount,
  );
  const progressCountFromDetail = Number(
    list.currentUserPlayedGameCount,
  );
  const rawPlayedGameCount = Number.isFinite(progressCountFromEndpoint)
    ? progressCountFromEndpoint
    : Number.isFinite(progressCountFromDetail)
      ? progressCountFromDetail
      : 0;
  const playedGameCount = Math.max(
    0,
    Math.min(list.gameCount, Math.trunc(rawPlayedGameCount)),
  );
  const playedPercentage = list.gameCount === 0
    ? 0
    : Math.round((playedGameCount / list.gameCount) * 100);
  const lastUpdatedAt = list.updatedAt ?? list.createdAt;
  const statusCounts = list.games.reduce<Record<UserGameStatus, number>>(
    (counts, game) => {
      if (game.currentUserStatus) {
        counts[game.currentUserStatus] += 1;
      }
      return counts;
    },
    {
      Playing: 0,
      PlanToPlay: 0,
      Played: 0,
      Dropped: 0,
    },
  );
  const userRatings = list.games
    .map((game) => game.currentUserRating)
    .filter((rating): rating is number => Number.isFinite(rating));
  const averageUserRating = userRatings.length > 0
    ? userRatings.reduce((total, rating) => total + rating, 0) /
      userRatings.length
    : null;
  const statusBadgeLabels: Record<UserGameStatus, string> = {
    Playing: t("lists:progress.status.playing"),
    Played: t("lists:progress.status.played"),
    PlanToPlay: t("lists:progress.status.planToPlay"),
    Dropped: t("lists:progress.status.dropped"),
  };
  const statusProgressItems: Array<{
    status: UserGameStatus;
    label: string;
    count: number;
    barClassName: string;
  }> = [
    {
      status: "Playing",
      label: t("lists:progress.status.playing"),
      count: statusCounts.Playing,
      barClassName: "bg-emerald-400",
    },
    {
      status: "Played",
      label: t("lists:progress.status.played"),
      count: statusCounts.Played,
      barClassName: "bg-violet-400",
    },
    {
      status: "PlanToPlay",
      label: t("lists:progress.status.planToPlay"),
      count: statusCounts.PlanToPlay,
      barClassName: "bg-sky-400",
    },
    {
      status: "Dropped",
      label: t("lists:progress.status.dropped"),
      count: statusCounts.Dropped,
      barClassName: "bg-rose-400",
    },
  ];

  function handleDrop(targetIndex: number) {
    if (!draggedGameKey || !list.isRanked || !list.canEditGames) return;
    const sourceIndex = list.games.findIndex(
      (game) => gameKey(game) === draggedGameKey,
    );
    if (sourceIndex < 0 || sourceIndex === targetIndex) {
      setDraggedGameKey(null);
      return;
    }

    const nextGames = [...list.games];
    const [moved] = nextGames.splice(sourceIndex, 1);
    nextGames.splice(targetIndex, 0, moved);
    queryClient.setQueryData<GameListDetailDTO>(
      gameListQueryKey,
      (current) => current
        ? {
            ...current,
            games: nextGames,
            backgroundCoverImageUrl:
              nextGames[0]?.coverImageUrl ?? current.backgroundCoverImageUrl,
          }
        : current,
    );
    setDraggedGameKey(null);
    reorderMutation.mutate(nextGames);
  }


  async function handleDeleteList() {
    const confirmed = await requestConfirmation({
      title: language === "en" ? "Delete list?" : "Siyahı silinsin?",
      description: language === "en"
        ? "This list will be permanently removed from your profile."
        : "Bu siyahı profilindən birdəfəlik silinəcək.",
      detail: list.name,
      confirmLabel: t("lists:actions.delete"),
      cancelLabel: t("lists:common.cancel"),
      kind: "delete",
      tone: "danger",
    });

    if (confirmed) {
      deleteMutation.mutate();
    }
  }

  async function handleRemoveGame(game: GameListEntryDTO) {
    const confirmed = await requestConfirmation({
      title: language === "en" ? "Remove game?" : "Oyun siyahıdan çıxarılsın?",
      description: t("lists:confirm.removeGame", { name: game.title }),
      detail: game.title,
      confirmLabel: t("lists:actions.removeGame"),
      cancelLabel: t("lists:common.cancel"),
      kind: "delete",
      tone: "danger",
    });

    if (confirmed) {
      removeMutation.mutate(game);
    }
  }

  return (
    <div className="min-h-screen wellplayd-page-bg text-zinc-100">
      <Navbar />

      <main>
        <section className="game-list-hero relative overflow-hidden border-b">
          {backgroundUrl && (
            <div className="pointer-events-none absolute inset-0 overflow-hidden">
              <img
                src={backgroundUrl}
                alt=""
                className="game-list-hero-image h-full w-full scale-110 object-cover blur-2xl"
              />
              <div className="game-list-hero-overlay-horizontal absolute inset-0" />
              <div className="game-list-hero-overlay-vertical absolute inset-0" />
            </div>
          )}
          <div className="game-list-hero-accents pointer-events-none absolute inset-0" />

          <div className="relative mx-auto max-w-7xl px-5 py-10 lg:px-8 lg:py-16">
            <Link
              to={`/users/${list.ownerUserName}`}
              className="game-list-glass inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition hover:text-[var(--wp-accent)]"
            >
              <ArrowLeft size={16} />
              {t("lists:detail.backToProfile", { name: list.ownerUserName })}
            </Link>

            <div className="mt-8 flex flex-col gap-8 lg:flex-row lg:items-end lg:justify-between">
              <div className="min-w-0 max-w-4xl">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="game-list-chip-accent inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-xs font-bold">
                    {list.isRanked ? <ListOrdered size={14} /> : <ListChecks size={14} />}
                    {list.isRanked
                      ? t("lists:type.ranked")
                      : t("lists:type.unranked")}
                  </span>
                  <span className="game-list-chip-neutral inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-xs font-semibold">
                    <VisibilityIcon size={14} />
                    {t(`lists:visibility.${list.visibility}`)}
                  </span>
                </div>

                <h1 className="mt-5 text-3xl font-black tracking-tight text-[var(--wp-text-strong)] sm:text-5xl">
                  {list.name}
                </h1>
                <p data-user-content className="mt-4 max-w-3xl whitespace-pre-wrap text-sm leading-7 text-[color:var(--wp-text-muted)]">
                  {list.description ?? t("lists:detail.noDescription")}
                </p>
                <div className="mt-6 flex flex-wrap items-center gap-5 text-xs text-[color:var(--wp-text-muted)]">
                  <span className="inline-flex items-center gap-2">
                    <Gamepad2 size={15} />
                    {t("lists:detail.gameCount", { count: list.gameCount })}
                  </span>
                  <span className="inline-flex items-center gap-2">
                    <CalendarDays size={15} />
                    {formatDate(list.createdAt)}
                  </span>
                  <button
                    type="button"
                    onClick={() => setIsCollaboratorsOpen(true)}
                    className="inline-flex items-center gap-2 transition hover:text-[var(--wp-accent-strong)]"
                  >
                    <Users size={15} />
                    {t("lists:detail.collaboratorCount", {
                      count: list.collaborators.filter((item) => item.status === "Accepted").length,
                    })}
                  </button>
                </div>
              </div>

              <div className="flex shrink-0 flex-wrap gap-3">
                <CopyShareButton
                  path={`/lists/${list.id}`}
                  className="game-list-action flex h-11 items-center gap-2 rounded-xl px-4 text-sm font-bold transition"
                />
                {isAuthenticated && !isPendingInvite && (
                  <button
                    type="button"
                    onClick={() => setIsCloneOpen(true)}
                    className="game-list-action flex h-11 items-center gap-2 rounded-xl px-4 text-sm font-bold transition"
                  >
                    <Copy size={16} />
                    {t("lists:actions.clone")}
                  </button>
                )}
                {(list.canManageCollaborators || isAcceptedCollaborator) && (
                  <button
                    type="button"
                    onClick={() => setIsCollaboratorsOpen(true)}
                    className="game-list-action-accent flex h-11 items-center gap-2 rounded-xl px-4 text-sm font-bold transition"
                  >
                    <UserRoundPlus size={16} />
                    {t("lists:actions.collaborators")}
                  </button>
                )}
                {list.isOwner && (
                  <>
                    <button
                      type="button"
                      onClick={() => setIsEditorOpen(true)}
                      className="game-list-action flex h-11 items-center gap-2 rounded-xl px-4 text-sm font-bold transition"
                    >
                      <Pencil size={16} />
                      {t("lists:actions.edit")}
                    </button>
                    <button
                      type="button"
                      onClick={() => {
                        void handleDeleteList();
                      }}
                      disabled={deleteMutation.isPending}
                      className="game-list-action-danger flex h-11 items-center gap-2 rounded-xl px-4 text-sm font-bold transition disabled:opacity-50"
                    >
                      <Trash2 size={16} />
                      {t("lists:actions.delete")}
                    </button>
                  </>
                )}
              </div>
            </div>
          </div>
        </section>

        <section className="mx-auto max-w-7xl px-5 py-10 lg:px-8">
          {notice && (
            <div
              className={`mb-6 flex items-center justify-between gap-4 rounded-2xl border px-4 py-3 text-sm ${
                notice.type === "success"
                  ? "border-emerald-400/15 bg-emerald-500/[0.07] text-emerald-200"
                  : "border-red-400/15 bg-red-500/[0.07] text-red-200"
              }`}
            >
              <span>{notice.message}</span>
              <button
                type="button"
                onClick={() => setNotice(null)}
                aria-label={t("lists:common.close")}
              >
                <X size={16} />
              </button>
            </div>
          )}

          {isPendingInvite && (
            <section className="mb-8 overflow-hidden rounded-3xl border border-violet-300/20 bg-gradient-to-r from-violet-500/[0.12] to-fuchsia-500/[0.06] p-5 sm:p-6">
              <div className="flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <div className="flex items-center gap-2 text-sm font-black text-violet-100">
                    <UserRoundPlus size={19} />
                    {t("lists:collaboration.invitationTitle")}
                  </div>
                  <p className="mt-2 max-w-2xl text-sm leading-6 text-zinc-400">
                    {t("lists:collaboration.invitationDescription", {
                      owner: list.ownerUserName,
                    })}
                  </p>
                </div>
                <div className="flex gap-3">
                  <button
                    type="button"
                    disabled={declineMutation.isPending || acceptMutation.isPending}
                    onClick={() => {
                      if (window.confirm(t("lists:collaboration.declineConfirm"))) {
                        declineMutation.mutate();
                      }
                    }}
                    className="h-11 rounded-xl border border-white/[0.09] px-4 text-sm font-bold text-zinc-300 transition hover:bg-white/[0.05] disabled:opacity-50"
                  >
                    {t("lists:collaboration.decline")}
                  </button>
                  <button
                    type="button"
                    disabled={acceptMutation.isPending || declineMutation.isPending}
                    onClick={() => acceptMutation.mutate()}
                    className="inline-flex h-11 items-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-bold text-white transition hover:bg-violet-500 disabled:opacity-50"
                  >
                    {acceptMutation.isPending && (
                      <LoaderCircle size={16} className="animate-spin" />
                    )}
                    {t("lists:collaboration.accept")}
                  </button>
                </div>
              </div>
            </section>
          )}

          <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start">
            <div className="min-w-0">
              {list.canEditGames && (
                <section className="mb-8 rounded-3xl border border-white/[0.07] bg-[#101015] p-5 shadow-xl shadow-black/10 sm:p-6">
                  <div className="flex items-center gap-2">
                    <Plus size={19} className="text-violet-300" />
                    <h2 className="font-black text-white">{t("lists:addGame.title")}</h2>
                  </div>
                  <p className="mt-2 text-sm leading-6 text-zinc-600">
                    {t("lists:addGame.description")}
                  </p>

                  <div className="relative mt-5">
                    <Search
                      size={18}
                      className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600"
                    />
                    <input
                      type="search"
                      autoComplete="off"
                      value={searchText}
                      onChange={(event) => setSearchText(event.target.value)}
                      placeholder={t("lists:addGame.placeholder")}
                      className="h-12 w-full rounded-2xl border border-white/[0.08] bg-white/[0.035] pl-11 pr-4 text-sm text-zinc-100 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/45 focus:ring-4 focus:ring-violet-500/10"
                    />
                  </div>

                  {normalizedSearch.length >= 2 && (
                    <div className="mt-4 grid gap-3 md:grid-cols-2">
                      {searchQuery.isLoading ? (
                        <div className="col-span-full flex items-center justify-center gap-3 py-8 text-sm text-zinc-600">
                          <LoaderCircle size={17} className="animate-spin" />
                          {t("lists:addGame.searching")}
                        </div>
                      ) : searchQuery.isError ? (
                        <p className="col-span-full py-6 text-center text-sm text-red-300">
                          {t("lists:addGame.searchError")}
                        </p>
                      ) : searchResults.length === 0 ? (
                        <p className="col-span-full py-6 text-center text-sm text-zinc-600">
                          {t("lists:addGame.noResults")}
                        </p>
                      ) : (
                        searchResults.map((game) => {
                          const isAdded = currentGameKeys.has(`${game.source}:${game.id}`);
                          return (
                            <div
                              key={`${game.source}-${game.id}`}
                              className="flex items-center gap-3 rounded-2xl border border-white/[0.07] bg-white/[0.025] p-3"
                            >
                              <GameCover title={game.title} coverImageUrl={game.coverImageUrl} />
                              <div className="min-w-0 flex-1">
                                <p className="line-clamp-2 text-sm font-bold text-zinc-100">
                                  {game.title}
                                </p>
                                <p className="mt-2 text-xs text-zinc-600">
                                  {getReleaseYear(game.releaseDate)}
                                </p>
                              </div>
                              <button
                                type="button"
                                onClick={() => addMutation.mutate(game)}
                                disabled={isAdded || addMutation.isPending}
                                className="flex h-10 shrink-0 items-center gap-2 rounded-xl bg-violet-600 px-3 text-xs font-bold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:bg-white/[0.05] disabled:text-zinc-600"
                              >
                                {isAdded ? <Check size={15} /> : <Plus size={15} />}
                                {isAdded
                                  ? t("lists:addGame.added")
                                  : t("lists:addGame.add")}
                              </button>
                            </div>
                          );
                        })
                      )}
                    </div>
                  )}
                </section>
              )}

              {list.games.length > 0 && (
                <div className="relative z-40 mb-5 flex flex-wrap items-center justify-between gap-3 border-b border-white/[0.08] pb-3">
                  <span className="text-sm font-medium text-zinc-500">
                    {t("lists:detail.gameCount", { count: list.gameCount })}
                  </span>

                  <div
                    ref={displayMenuRef}
                    className="relative flex items-center gap-2"
                  >
                    <span className="text-xs font-semibold text-zinc-600">
                      {t("lists:display.label")}
                    </span>
                    <button
                      type="button"
                      onClick={() => setIsDisplayMenuOpen((current) => !current)}
                      className="inline-flex h-10 items-center gap-2 rounded-xl border border-white/[0.08] bg-[#101015] px-3 text-sm font-bold text-zinc-200 transition hover:border-violet-300/25 hover:bg-white/[0.045]"
                      aria-expanded={isDisplayMenuOpen}
                      aria-haspopup="menu"
                    >
                      {displayMode === "grid" ? (
                        <Grid2X2 size={16} className="text-violet-300" />
                      ) : (
                        <Rows3 size={17} className="text-violet-300" />
                      )}
                      {displayMode === "grid"
                        ? t("lists:display.grid")
                        : t("lists:display.detail")}
                      <ChevronDown
                        size={15}
                        className={`text-zinc-500 transition ${
                          isDisplayMenuOpen ? "rotate-180" : ""
                        }`}
                      />
                    </button>

                    {isDisplayMenuOpen && (
                      <div
                        role="menu"
                        className="game-list-display-menu absolute right-0 top-12 z-50 w-56 overflow-hidden rounded-2xl border border-white/[0.09] bg-[#14141a] p-2 shadow-[0_24px_70px_rgba(0,0,0,0.55)]"
                      >
                        <button
                          type="button"
                          role="menuitemradio"
                          aria-checked={displayMode === "grid"}
                          onClick={() => {
                            setDisplayMode("grid");
                            setIsDisplayMenuOpen(false);
                          }}
                          className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.055]"
                        >
                          <Grid2X2 size={17} className="text-violet-300" />
                          <span className="flex-1">{t("lists:display.grid")}</span>
                          {displayMode === "grid" && (
                            <Check size={16} className="text-violet-300" />
                          )}
                        </button>
                        <button
                          type="button"
                          role="menuitemradio"
                          aria-checked={displayMode === "detail"}
                          onClick={() => {
                            setDisplayMode("detail");
                            setIsDisplayMenuOpen(false);
                          }}
                          className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.055]"
                        >
                          <Rows3 size={18} className="text-violet-300" />
                          <span className="flex-1">{t("lists:display.detail")}</span>
                          {displayMode === "detail" && (
                            <Check size={16} className="text-violet-300" />
                          )}
                        </button>

                        <div className="my-2 border-t border-white/[0.07]" />

                        <button
                          type="button"
                          role="menuitemcheckbox"
                          aria-checked={fadePlayedGames}
                          disabled={!isAuthenticated}
                          onClick={() => setFadePlayedGames((current) => !current)}
                          className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.055] disabled:cursor-not-allowed disabled:opacity-40"
                        >
                          <EyeOff size={17} className="text-zinc-400" />
                          <span className="flex-1">{t("lists:display.fadePlayed")}</span>
                          <span
                            className={`flex h-5 w-5 items-center justify-center rounded-md border transition ${
                              fadePlayedGames
                                ? "border-violet-400/50 bg-violet-500 text-white"
                                : "border-white/[0.14] bg-white/[0.025]"
                            }`}
                          >
                            {fadePlayedGames && <Check size={13} />}
                          </span>
                        </button>
                      </div>
                    )}
                  </div>
                </div>
              )}

              {list.games.length === 0 ? (
                <div className="rounded-3xl border border-dashed border-white/[0.08] p-14 text-center">
                  <ListChecks size={34} className="mx-auto text-zinc-700" />
                  <h2 className="mt-4 font-bold text-zinc-300">
                    {t("lists:empty.title")}
                  </h2>
                  <p className="mt-2 text-sm text-zinc-600">
                    {list.canEditGames
                      ? t("lists:empty.editor")
                      : t("lists:empty.viewer")}
                  </p>
                </div>
              ) : (
                <>
                  {displayMode === "grid" ? (
                    <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 xl:grid-cols-4">
                      {visibleGames.map((game, index) => {
                        const globalIndex = pageStartIndex + index;
                        const rankPosition = globalIndex + 1;
                        const key = gameKey(game);
                        const isDragging = draggedGameKey === key;
                        const imageUrl = resolveAssetUrl(game.coverImageUrl);
                        const shouldFade =
                          fadePlayedGames && game.currentUserStatus === "Played";
                        const statusBadgeStyles = game.currentUserStatus
                          ? getStatusBadgeStyles(game.currentUserStatus)
                          : null;

                        return (
                          <article
                            key={key}
                            draggable={list.isRanked && list.canEditGames && !reorderMutation.isPending}
                            onDragStart={(event) => {
                              setDraggedGameKey(key);
                              event.dataTransfer.effectAllowed = "move";
                              event.dataTransfer.setData("text/plain", key);
                            }}
                            onDragOver={(event) => {
                              if (list.isRanked && list.canEditGames) {
                                event.preventDefault();
                                event.dataTransfer.dropEffect = "move";
                              }
                            }}
                            onDrop={(event) => {
                              event.preventDefault();
                              handleDrop(globalIndex);
                            }}
                            onDragEnd={() => setDraggedGameKey(null)}
                            className={`group relative flex h-full flex-col ${
                              list.isRanked ? "pt-6" : ""
                            } transition duration-200 ${
                              list.isRanked && list.canEditGames
                                ? "cursor-grab active:cursor-grabbing"
                                : ""
                            } ${
                              isDragging
                                ? "scale-[0.98] opacity-45"
                                : "hover:-translate-y-1"
                            }`}
                          >
                            {list.isRanked && rankPosition === 1 && (
                              <span className="pointer-events-none absolute left-1/2 top-0 z-20 flex h-6 w-6 -translate-x-1/2 items-center justify-center text-amber-300 drop-shadow-[0_0_7px_rgba(245,158,11,0.45)]">
                                <Crown size={23} strokeWidth={2.1} />
                              </span>
                            )}

                            <div className="relative">
                              <Link
                                to={`/games/${game.source}/${game.id}`}
                                className={`relative block aspect-[3/4] w-full overflow-hidden rounded-2xl border bg-[#101015] shadow-xl shadow-black/20 transition duration-300 ${
                                  isDragging
                                    ? "border-violet-400/40"
                                    : "border-white/[0.08] group-hover:border-violet-300/30 group-hover:shadow-violet-950/30"
                                } ${
                                  shouldFade
                                    ? "opacity-[0.34] grayscale brightness-[0.72] saturate-0 group-hover:opacity-100 group-hover:grayscale-0 group-hover:brightness-100 group-hover:saturate-100"
                                    : ""
                                }`}
                                aria-label={game.title}
                              >
                                {imageUrl ? (
                                  <img
                                    src={imageUrl}
                                    alt={game.title}
                                    className="h-full w-full object-cover transition duration-500 group-hover:scale-105 group-hover:blur-[2px]"
                                  />
                                ) : (
                                  <span className="flex h-full w-full items-center justify-center text-zinc-700 transition duration-300 group-hover:blur-[2px]">
                                    <Gamepad2 size={36} />
                                  </span>
                                )}

                                <span className="absolute inset-0 bg-black/0 transition duration-300 group-hover:bg-black/65" />
                                <span className="absolute inset-0 flex items-center justify-center p-5 text-center">
                                  <span className="game-cover-hover-title line-clamp-4 translate-y-2 text-sm font-black leading-6 text-white opacity-0 drop-shadow-lg transition duration-300 group-hover:translate-y-0 group-hover:opacity-100">
                                    {game.title}
                                  </span>
                                </span>
                              </Link>

                              {isProgressExpanded &&
                                game.currentUserStatus &&
                                statusBadgeStyles && (
                                  <span
                                    className={`pointer-events-none absolute right-2 top-2 z-20 inline-flex max-w-[calc(100%-1rem)] items-center gap-1.5 rounded-lg border px-2.5 py-1 text-xs font-bold shadow-lg shadow-black/25 backdrop-blur-md ${statusBadgeStyles.badge}`}
                                  >
                                    <span
                                      className={`h-2 w-2 shrink-0 rounded-full ${statusBadgeStyles.dot}`}
                                    />
                                    <span className="truncate">
                                      {statusBadgeLabels[game.currentUserStatus]}
                                    </span>
                                  </span>
                                )}

                              {list.canEditGames && (
                                <button
                                  type="button"
                                  onClick={() => {
                                    void handleRemoveGame(game);
                                  }}
                                  disabled={removeMutation.isPending}
                                  className={`absolute right-2 z-30 flex h-9 w-9 items-center justify-center rounded-xl border border-red-300/15 bg-black/65 text-red-200 opacity-0 backdrop-blur-md transition hover:bg-red-500/25 group-hover:opacity-100 focus:opacity-100 disabled:opacity-40 ${
                                    isProgressExpanded && game.currentUserStatus
                                      ? "top-11"
                                      : "top-2"
                                  }`}
                                  aria-label={t("lists:actions.removeGame")}
                                >
                                  <Trash2 size={15} />
                                </button>
                              )}

                              {list.isRanked && list.canEditGames && (
                                <span
                                  className="pointer-events-none absolute bottom-2 right-2 z-20 flex h-9 w-9 items-center justify-center rounded-xl border border-white/[0.08] bg-black/55 text-zinc-300 opacity-0 backdrop-blur-md transition group-hover:opacity-100"
                                  aria-hidden="true"
                                >
                                  <GripVertical size={17} />
                                </span>
                              )}
                            </div>

                            {list.isRanked && (
                              <div className="mt-2 flex h-9 shrink-0 items-center justify-center">
                                <span
                                  className={`inline-flex h-9 min-w-9 items-center justify-center rounded-xl border px-3 text-sm font-black ${getRankBadgeClasses(rankPosition)}`}
                                >
                                  {rankPosition}
                                </span>
                              </div>
                            )}
                          </article>
                        );
                      })}
                    </div>
                  ) : (
                    <div className="divide-y divide-white/[0.075] border-y border-white/[0.08]">
                      {visibleGames.map((game, index) => {
                        const globalIndex = pageStartIndex + index;
                        const rankPosition = globalIndex + 1;
                        const key = gameKey(game);
                        const isDragging = draggedGameKey === key;
                        const imageUrl = resolveAssetUrl(game.coverImageUrl);
                        const shouldFade =
                          fadePlayedGames && game.currentUserStatus === "Played";
                        const statusBadgeStyles = game.currentUserStatus
                          ? getStatusBadgeStyles(game.currentUserStatus)
                          : null;

                        return (
                          <article
                            key={key}
                            draggable={list.isRanked && list.canEditGames && !reorderMutation.isPending}
                            onDragStart={(event) => {
                              setDraggedGameKey(key);
                              event.dataTransfer.effectAllowed = "move";
                              event.dataTransfer.setData("text/plain", key);
                            }}
                            onDragOver={(event) => {
                              if (list.isRanked && list.canEditGames) {
                                event.preventDefault();
                                event.dataTransfer.dropEffect = "move";
                              }
                            }}
                            onDrop={(event) => {
                              event.preventDefault();
                              handleDrop(globalIndex);
                            }}
                            onDragEnd={() => setDraggedGameKey(null)}
                            className={`relative flex min-h-36 items-center gap-4 py-4 ${
                              list.isRanked && list.canEditGames
                                ? "cursor-grab active:cursor-grabbing"
                                : ""
                            } ${
                              shouldFade
                                ? "opacity-[0.34] grayscale brightness-[0.72] saturate-0 transition duration-200 hover:opacity-100 hover:grayscale-0 hover:brightness-100 hover:saturate-100"
                                : ""
                            } ${isDragging ? "opacity-45" : ""}`}
                          >
                            <Link
                              to={`/games/${game.source}/${game.id}`}
                              className="flex h-28 w-20 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-white/[0.09] bg-[#101015]"
                              aria-label={game.title}
                            >
                              {imageUrl ? (
                                <img
                                  src={imageUrl}
                                  alt={game.title}
                                  className="h-full w-full object-cover"
                                />
                              ) : (
                                <Gamepad2 size={25} className="text-zinc-700" />
                              )}
                            </Link>

                            {list.isRanked && (
                              <div className="relative flex h-14 w-12 shrink-0 items-end justify-center pb-1">
                                {rankPosition === 1 && (
                                  <span className="pointer-events-none absolute left-1/2 top-0 -translate-x-1/2 text-amber-300 drop-shadow-[0_0_6px_rgba(245,158,11,0.4)]">
                                    <Crown size={18} strokeWidth={2.1} />
                                  </span>
                                )}
                                <span
                                  className={`inline-flex h-9 min-w-9 items-center justify-center rounded-xl border px-2 text-sm font-black ${getRankBadgeClasses(rankPosition)}`}
                                >
                                  {rankPosition}
                                </span>
                              </div>
                            )}

                            <Link
                              to={`/games/${game.source}/${game.id}`}
                              className="min-w-0 flex-1 text-lg font-black leading-snug text-zinc-100 sm:text-xl"
                            >
                              {game.title}
                            </Link>

                            {isProgressExpanded &&
                              game.currentUserStatus &&
                              statusBadgeStyles && (
                                <span
                                  className={`inline-flex shrink-0 items-center gap-1.5 rounded-lg border px-2.5 py-1.5 text-xs font-bold shadow-lg shadow-black/20 ${statusBadgeStyles.badge}`}
                                >
                                  <span
                                    className={`h-2 w-2 shrink-0 rounded-full ${statusBadgeStyles.dot}`}
                                  />
                                  {statusBadgeLabels[game.currentUserStatus]}
                                </span>
                              )}

                            {list.canEditGames && (
                              <div className="ml-auto flex shrink-0 items-center gap-2">
                                {list.isRanked && (
                                  <span
                                    className="flex h-10 w-10 items-center justify-center rounded-xl border border-white/[0.08] bg-white/[0.025] text-zinc-500"
                                    aria-label={t("lists:drag.handle")}
                                  >
                                    <GripVertical size={17} />
                                  </span>
                                )}
                                <button
                                  type="button"
                                  onClick={() => {
                                    void handleRemoveGame(game);
                                  }}
                                  disabled={removeMutation.isPending}
                                  className="flex h-10 w-10 items-center justify-center rounded-xl border border-red-300/15 bg-red-500/[0.055] text-red-200 disabled:opacity-40"
                                  aria-label={t("lists:actions.removeGame")}
                                >
                                  <Trash2 size={16} />
                                </button>
                              </div>
                            )}
                          </article>
                        );
                      })}
                    </div>
                  )}

                  {totalGamePages > 1 && (
                    <nav
                      className="mt-8 flex items-center justify-center gap-3"
                      aria-label={t("lists:pagination.label")}
                    >
                      <button
                        type="button"
                        onClick={() => goToPage(currentPage - 1)}
                        disabled={currentPage === 1}
                        className="flex h-10 w-10 items-center justify-center rounded-xl border border-white/[0.08] bg-[#101015] text-zinc-300 transition hover:border-violet-300/25 hover:text-violet-200 disabled:cursor-not-allowed disabled:opacity-35"
                        aria-label={t("lists:pagination.previous")}
                      >
                        <ChevronLeft size={18} />
                      </button>

                      <span className="min-w-28 text-center text-xs font-bold text-zinc-500">
                        {t("lists:pagination.page", {
                          current: currentPage,
                          total: totalGamePages,
                        })}
                      </span>

                      <button
                        type="button"
                        onClick={() => goToPage(currentPage + 1)}
                        disabled={currentPage === totalGamePages}
                        className="flex h-10 w-10 items-center justify-center rounded-xl border border-white/[0.08] bg-[#101015] text-zinc-300 transition hover:border-violet-300/25 hover:text-violet-200 disabled:cursor-not-allowed disabled:opacity-35"
                        aria-label={t("lists:pagination.next")}
                      >
                        <ChevronRight size={18} />
                      </button>
                    </nav>
                  )}
                </>
              )}
            </div>

            <aside className="order-first min-w-0 lg:order-none lg:sticky lg:top-24">
              <section className="overflow-hidden rounded-3xl border border-white/[0.08] bg-[#101015] shadow-xl shadow-black/15">
                <div className="p-5">
                  <Link
                    to={`/users/${list.ownerUserName}`}
                    className="flex items-center gap-3 rounded-2xl transition hover:bg-white/[0.025]"
                  >
                    <OwnerAvatar
                      userName={list.ownerUserName}
                      profileImageUrl={list.ownerProfileImageUrl}
                    />
                    <span className="min-w-0">
                      <span className="block truncate text-sm font-black text-zinc-100">
                        {list.ownerUserName}
                      </span>
                      <span className="mt-1 block text-[11px] font-semibold uppercase tracking-[0.15em] text-zinc-600">
                        {t("lists:detail.listOwner")}
                      </span>
                    </span>
                  </Link>

                  <div className="mt-5 grid grid-cols-2 border-y border-white/[0.08] py-3 text-sm text-zinc-500">
                    <span className="flex items-center justify-center gap-2 border-r border-white/[0.08]">
                      <button
                        type="button"
                        disabled={!isAuthenticated || list.isOwner || likeMutation.isPending}
                        onClick={() => likeMutation.mutate()}
                        className={`group inline-flex items-center transition disabled:cursor-not-allowed disabled:opacity-45 ${
                          list.isLikedByCurrentUser ? "text-rose-300" : "hover:text-rose-300"
                        }`}
                        aria-label={t("lists:actions.like")}
                      >
                        <Heart
                          size={17}
                          className={`transition ${
                            list.isLikedByCurrentUser
                              ? "fill-current"
                              : "fill-transparent group-hover:fill-current"
                          }`}
                        />
                      </button>
                      <button
                        type="button"
                        onClick={() => setIsLikesOpen(true)}
                        className="font-semibold transition hover:text-rose-300"
                      >
                        {t("lists:detail.likeCount", { count: list.likeCount })}
                      </button>
                    </span>

                    <button
                      type="button"
                      onClick={() => setIsCommentsOpen((current) => !current)}
                      className={`inline-flex items-center justify-center gap-2 font-semibold transition hover:text-violet-300 ${
                        isCommentsOpen ? "text-violet-300" : ""
                      }`}
                    >
                      <MessageCircle size={17} />
                      {t("lists:detail.commentCount", { count: list.commentCount })}
                    </button>
                  </div>

                  {isAuthenticated ? (
                    <section className="game-list-progress-card mt-5 overflow-hidden rounded-2xl">
                      <button
                        type="button"
                        onClick={() => setIsProgressExpanded((current) => !current)}
                        className="flex w-full items-center justify-between gap-4 p-4 text-left"
                        aria-expanded={isProgressExpanded}
                      >
                        <div className="min-w-0">
                          <p className="game-list-progress-label text-xs font-semibold">
                            {t("lists:progress.played")}
                          </p>
                          <p className="game-list-progress-value mt-1 text-sm font-black">
                            {t("lists:progress.count", {
                              played: playedGameCount,
                              total: list.gameCount,
                            })}
                          </p>
                          <span className="game-list-progress-toggle mt-2 inline-flex items-center gap-1.5 text-[11px] font-semibold">
                            {isProgressExpanded
                              ? t("lists:progress.hideDetails")
                              : t("lists:progress.showDetails")}
                            <ChevronDown
                              size={13}
                              className={`transition ${
                                isProgressExpanded ? "rotate-180" : ""
                              }`}
                            />
                          </span>
                        </div>

                        <div
                          className="game-list-progress-ring flex h-16 w-16 shrink-0 items-center justify-center rounded-full p-[2px]"
                          style={{
                            background: `conic-gradient(var(--wp-accent) ${playedPercentage}%, color-mix(in srgb, var(--wp-border) 78%, transparent) ${playedPercentage}% 100%)`,
                          }}
                          aria-label={t("lists:progress.percentage", {
                            percentage: playedPercentage,
                          })}
                        >
                          <span className="game-list-progress-ring-center flex h-full w-full items-center justify-center rounded-full text-sm font-black">
                            {playedPercentage}%
                          </span>
                        </div>
                      </button>

                      {isProgressExpanded && (
                        <div className="game-list-progress-details border-t px-4 pb-4 pt-3">
                          <div className="space-y-3">
                            {statusProgressItems.map((item) => {
                              const percentage = list.gameCount === 0
                                ? 0
                                : Math.round((item.count / list.gameCount) * 100);

                              return (
                                <div key={item.status}>
                                  <div className="mb-1.5 flex items-center justify-between gap-3 text-xs">
                                    <span className="game-list-progress-status-label font-semibold">
                                      {item.label}
                                    </span>
                                    <span className="game-list-progress-status-count font-black">
                                      {item.count}
                                    </span>
                                  </div>
                                  <div className="game-list-progress-track h-1.5 overflow-hidden rounded-full">
                                    <div
                                      className={`h-full rounded-full ${item.barClassName}`}
                                      style={{ width: `${percentage}%` }}
                                    />
                                  </div>
                                </div>
                              );
                            })}
                          </div>

                          <div className="game-list-progress-rating mt-4 flex items-center justify-between gap-3 border-t pt-3">
                            <span className="game-list-progress-label text-xs font-semibold">
                              {t("lists:progress.averageRating")}
                            </span>
                            <span className="game-list-progress-value inline-flex items-center gap-1.5 text-sm font-black">
                              <Star
                                size={16}
                                className="fill-fuchsia-400 text-fuchsia-400"
                              />
                              {(averageUserRating ?? 0).toFixed(2)}
                            </span>
                          </div>
                        </div>
                      )}
                    </section>
                  ) : (
                    <div className="mt-5 rounded-2xl border border-white/[0.07] bg-white/[0.025] px-4 py-4 text-xs leading-5 text-zinc-500">
                      <Link
                        to="/login"
                        className="font-bold text-violet-300 transition hover:text-violet-200"
                      >
                        {t("lists:progress.signIn")}
                      </Link>{" "}
                      {t("lists:progress.signInDescription")}
                    </div>
                  )}

                  <div className="game-list-updated-row mt-4 flex items-center justify-between gap-4 text-xs">
                    <span>{t("lists:detail.lastUpdated")}</span>
                    <span className="game-list-updated-value font-semibold">
                      {formatLongDate(lastUpdatedAt)}
                    </span>
                  </div>
                </div>

                {isCommentsOpen && (
                  <InteractionCommentsPanel
                    contentType="game-list"
                    contentId={listId}
                    isAuthenticated={isAuthenticated}
                    placeholder={t("lists:comments.placeholder")}
                    emptyText={t("lists:comments.empty")}
                    invalidateQueryKeys={[
                      gameListQueryKey,
                      ["user-game-lists"],
                    ]}
                  />
                )}
              </section>
            </aside>
          </div>
        </section>
      </main>

      <Footer />

      <InteractionLikesModal
        contentType="game-list"
        contentId={isLikesOpen ? listId : null}
        likeCount={list.likeCount}
        onClose={() => setIsLikesOpen(false)}
      />

      {isEditorOpen && (
        <GameListEditorModal
          open
          title={t("lists:editor.editTitle")}
          submitLabel={t("lists:editor.save")}
          initialValues={{
            name: list.name,
            description: list.description,
            visibility: list.visibility,
            isRanked: list.isRanked,
          }}
          isPending={updateMutation.isPending}
          errorMessage={
            updateMutation.isError
              ? getApiErrorMessage(updateMutation.error, t("lists:errors.update"))
              : null
          }
          onClose={() => setIsEditorOpen(false)}
          onSubmit={(values) => updateMutation.mutate(values)}
        />
      )}

      {isCloneOpen && (
        <GameListCloneModal
          open
          sourceName={list.name}
          initialName={cloneName}
          isPending={cloneMutation.isPending}
          errorMessage={
            cloneMutation.isError
              ? getApiErrorMessage(cloneMutation.error, t("lists:clone.error"))
              : null
          }
          onClose={() => setIsCloneOpen(false)}
          onSubmit={(values) => cloneMutation.mutate(values)}
        />
      )}

      {isCollaboratorsOpen && (
        <GameListCollaboratorsModal
          open
          listId={listId}
          currentUserId={storedUser?.id ?? null}
          isOwner={list.isOwner}
          collaborators={list.collaborators}
          onClose={() => setIsCollaboratorsOpen(false)}
          onUpdated={invalidateList}
        />
      )}
    </div>
  );
}
