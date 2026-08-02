import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ArrowLeft,
  CalendarDays,
  Check,
  Copy,
  Gamepad2,
  GripVertical,
  Heart,
  ListChecks,
  ListOrdered,
  LoaderCircle,
  LockKeyhole,
  MessageCircle,
  Pencil,
  Plus,
  Search,
  Sparkles,
  Star,
  Trash2,
  UserRoundPlus,
  Users,
  X,
} from "lucide-react";
import { useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { InteractionCommentsPanel } from "../components/interactions/InteractionCommentsPanel";
import { InteractionLikesModal } from "../components/interactions/InteractionLikesModal";
import { GameListCloneModal } from "../components/lists/GameListCloneModal";
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
  formatDateTime,
  getReleaseYear,
  resolveAssetUrl,
} from "../lib/helpers";
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

interface NoticeState {
  type: "success" | "error";
  message: string;
}

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

function getRankBadgeClasses(position: number) {
  switch (position) {
    case 1:
      return "border-amber-300/25 bg-amber-500/[0.11] text-amber-300 shadow-[0_0_28px_rgba(245,158,11,0.14)]";
    case 2:
      return "border-slate-200/25 bg-slate-200/[0.10] text-slate-200 shadow-[0_0_28px_rgba(226,232,240,0.10)]";
    case 3:
      return "border-orange-400/25 bg-orange-500/[0.10] text-orange-300 shadow-[0_0_28px_rgba(194,100,45,0.12)]";
    default:
      return "border-white/[0.08] bg-white/[0.04] text-zinc-400";
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
  const accessToken = useAuthStore((state) => state.accessToken);
  const expiresAtUtc = useAuthStore((state) => state.expiresAtUtc);
  const storedUser = useAuthStore((state) => state.user);
  const isAuthenticated =
    isAuthSessionValid(accessToken, expiresAtUtc) && storedUser !== null;

  const [isEditorOpen, setIsEditorOpen] = useState(false);
  const [isCloneOpen, setIsCloneOpen] = useState(false);
  const [isCollaboratorsOpen, setIsCollaboratorsOpen] = useState(false);
  const [searchText, setSearchText] = useState("");
  const [notice, setNotice] = useState<NoticeState | null>(null);
  const [isCommentsOpen, setIsCommentsOpen] = useState(false);
  const [isLikesOpen, setIsLikesOpen] = useState(false);
  const [draggedGameKey, setDraggedGameKey] = useState<string | null>(null);

  const listQuery = useQuery({
    queryKey: ["game-list", listId],
    queryFn: () => getGameList(listId),
    enabled: Number.isInteger(listId) && listId > 0,
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
      queryClient.invalidateQueries({ queryKey: ["game-list", listId] }),
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

  if (listQuery.isLoading) {
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
      ["game-list", listId],
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

  return (
    <div className="min-h-screen wellplayd-page-bg text-zinc-100">
      <Navbar />

      <main>
        <section className="relative overflow-hidden border-b border-white/[0.07] bg-[#09090d]">
          {backgroundUrl && (
            <div className="pointer-events-none absolute inset-0 overflow-hidden">
              <img
                src={backgroundUrl}
                alt=""
                className="h-full w-full scale-110 object-cover opacity-30 blur-2xl"
              />
              <div className="absolute inset-0 bg-gradient-to-r from-[#09090d] via-[#09090d]/85 to-[#09090d]/65" />
              <div className="absolute inset-0 bg-gradient-to-t from-[#09090d] via-transparent to-[#09090d]/30" />
            </div>
          )}
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_10%,rgba(124,58,237,0.2),transparent_32%),radial-gradient(circle_at_85%_25%,rgba(245,158,11,0.08),transparent_25%)]" />

          <div className="relative mx-auto max-w-7xl px-5 py-10 lg:px-8 lg:py-16">
            <Link
              to={`/users/${list.ownerUserName}`}
              className="inline-flex items-center gap-2 rounded-full wp-glass-surface px-4 py-2 text-sm font-semibold text-[var(--wp-text)] transition hover:text-[var(--wp-accent)]"
            >
              <ArrowLeft size={16} />
              {t("lists:detail.backToProfile", { name: list.ownerUserName })}
            </Link>

            <div className="mt-8 flex flex-col gap-8 lg:flex-row lg:items-end lg:justify-between">
              <div className="min-w-0 max-w-4xl">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-xs font-bold wp-chip-accent">
                    {list.isRanked ? <ListOrdered size={14} /> : <ListChecks size={14} />}
                    {list.isRanked
                      ? t("lists:type.ranked")
                      : t("lists:type.unranked")}
                  </span>
                  <span className="inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-xs font-semibold wp-chip-neutral">
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
                <div className="mt-6 flex flex-wrap items-center gap-5 text-xs text-zinc-400">
                  <span className="inline-flex items-center gap-2">
                    <Gamepad2 size={15} />
                    {t("lists:detail.gameCount", { count: list.gameCount })}
                  </span>
                  <span className="inline-flex items-center gap-2">
                    <CalendarDays size={15} />
                    {formatDateTime(list.createdAt)}
                  </span>
                  <button
                    type="button"
                    onClick={() => setIsCollaboratorsOpen(true)}
                    className="inline-flex items-center gap-2 transition hover:text-violet-200"
                  >
                    <Users size={15} />
                    {t("lists:detail.collaboratorCount", {
                      count: list.collaborators.filter((item) => item.status === "Accepted").length,
                    })}
                  </button>
                </div>
              </div>

              <div className="flex shrink-0 flex-wrap gap-3">
                {isAuthenticated && !isPendingInvite && (
                  <button
                    type="button"
                    onClick={() => setIsCloneOpen(true)}
                    className="flex h-11 items-center gap-2 rounded-xl px-4 text-sm font-bold wp-action-btn transition"
                  >
                    <Copy size={16} />
                    {t("lists:actions.clone")}
                  </button>
                )}
                {(list.canManageCollaborators || isAcceptedCollaborator) && (
                  <button
                    type="button"
                    onClick={() => setIsCollaboratorsOpen(true)}
                    className="flex h-11 items-center gap-2 rounded-xl px-4 text-sm font-bold wp-action-btn-accent transition"
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
                      className="flex h-11 items-center gap-2 rounded-xl px-4 text-sm font-bold wp-action-btn transition"
                    >
                      <Pencil size={16} />
                      {t("lists:actions.edit")}
                    </button>
                    <button
                      type="button"
                      onClick={() => {
                        if (window.confirm(t("lists:confirm.deleteList"))) {
                          deleteMutation.mutate();
                        }
                      }}
                      disabled={deleteMutation.isPending}
                      className="flex h-11 items-center gap-2 rounded-xl px-4 text-sm font-bold wp-action-btn-danger transition disabled:opacity-50"
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

          <section className="mb-8 overflow-hidden rounded-3xl border border-white/[0.07] bg-[#101015] shadow-xl shadow-black/10">
            <div className="flex items-center gap-5 px-5 py-4 text-sm text-zinc-500 sm:px-6">
              <span className="inline-flex items-center gap-2">
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
                className={`inline-flex items-center gap-2 font-semibold transition hover:text-violet-300 ${
                  isCommentsOpen ? "text-violet-300" : ""
                }`}
              >
                <MessageCircle size={17} />
                {t("lists:detail.commentCount", { count: list.commentCount })}
              </button>
            </div>

            {isCommentsOpen && (
              <InteractionCommentsPanel
                contentType="game-list"
                contentId={listId}
                isAuthenticated={isAuthenticated}
                placeholder={t("lists:comments.placeholder")}
                emptyText={t("lists:comments.empty")}
                invalidateQueryKeys={[
                  ["game-list", listId],
                  ["user-game-lists"],
                ]}
              />
            )}
          </section>

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

          {list.isRanked && list.canEditGames && list.games.length > 1 && (
            <div className="mb-4 flex items-center gap-2 rounded-2xl border border-violet-300/10 bg-violet-500/[0.05] px-4 py-3 text-xs text-violet-200/75">
              <GripVertical size={16} />
              {t("lists:drag.hint")}
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
            <div className="space-y-3">
              {list.games.map((game, index) => {
                const rankPosition = list.isRanked ? index + 1 : game.position ?? index + 1;
                const key = gameKey(game);
                const isDragging = draggedGameKey === key;

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
                      handleDrop(index);
                    }}
                    onDragEnd={() => setDraggedGameKey(null)}
                    className={`group flex items-center gap-4 rounded-3xl border bg-[#101015] p-4 shadow-lg shadow-black/10 transition sm:p-5 ${
                      isDragging
                        ? "scale-[0.99] border-violet-400/35 opacity-50"
                        : "border-white/[0.07] hover:border-white/[0.12]"
                    }`}
                  >
                    {list.isRanked && list.canEditGames && (
                      <span
                        className="flex h-12 w-8 shrink-0 cursor-grab items-center justify-center rounded-xl text-zinc-700 transition group-hover:bg-white/[0.04] group-hover:text-zinc-400 active:cursor-grabbing"
                        aria-label={t("lists:drag.handle")}
                      >
                        <GripVertical size={20} />
                      </span>
                    )}

                    {list.isRanked && (
                      <div className="relative flex h-[4.5rem] w-14 shrink-0 items-end justify-center">
                        {rankPosition === 1 && (
                          <div className="absolute -top-3 left-1/2 z-10 -translate-x-1/2" aria-hidden="true">
                            <svg
                              viewBox="0 0 72 48"
                              className="h-10 w-14 -rotate-3 overflow-visible drop-shadow-[0_0_10px_rgba(245,158,11,0.55)]"
                            >
                              <defs>
                                <linearGradient id="rank-crown-gold" x1="0" y1="0" x2="0" y2="1">
                                  <stop offset="0%" stopColor="#fff3a3" />
                                  <stop offset="45%" stopColor="#fbbf24" />
                                  <stop offset="100%" stopColor="#b45309" />
                                </linearGradient>
                              </defs>
                              <path d="M8 37 4 13l17 11L36 4l15 20 17-11-4 24Z" fill="url(#rank-crown-gold)" stroke="#fde68a" strokeWidth="2" strokeLinejoin="round" />
                              <path d="M10 37h52l-3 7H13Z" fill="#d97706" stroke="#fde68a" strokeWidth="2" strokeLinejoin="round" />
                              <circle cx="21" cy="31" r="2.5" fill="#fff7cc" />
                              <circle cx="36" cy="27" r="3" fill="#fff7cc" />
                              <circle cx="51" cy="31" r="2.5" fill="#fff7cc" />
                            </svg>
                          </div>
                        )}
                        <span className={`flex h-12 w-12 items-center justify-center rounded-2xl border text-lg font-black ${getRankBadgeClasses(rankPosition)}`}>
                          {rankPosition}
                        </span>
                      </div>
                    )}

                    <Link
                      to={`/games/${game.source}/${game.id}`}
                      className="flex min-w-0 flex-1 items-center gap-4"
                    >
                      <GameCover title={game.title} coverImageUrl={game.coverImageUrl} />
                      <div className="min-w-0">
                        <h2 className="line-clamp-2 text-base font-bold text-white transition hover:text-violet-200">
                          {game.title}
                        </h2>
                        <div className="mt-3 flex flex-wrap items-center gap-4 text-xs text-zinc-600">
                          <span>{getReleaseYear(game.releaseDate)}</span>
                          {game.averageRating !== null && (
                            <span className="inline-flex items-center gap-1.5 text-amber-300/80">
                              <Star size={14} fill="currentColor" />
                              {game.averageRating.toFixed(1)}
                            </span>
                          )}
                          <span>
                            {t("lists:detail.addedOn", {
                              date: formatDate(game.addedAt),
                            })}
                          </span>
                        </div>
                      </div>
                    </Link>

                    {list.canEditGames && (
                      <button
                        type="button"
                        onClick={() => {
                          if (
                            window.confirm(
                              t("lists:confirm.removeGame", { name: game.title }),
                            )
                          ) {
                            removeMutation.mutate(game);
                          }
                        }}
                        disabled={removeMutation.isPending}
                        className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-red-400/10 text-red-300/65 transition hover:bg-red-500/[0.08] hover:text-red-300 disabled:opacity-50"
                        aria-label={t("lists:actions.removeGame")}
                      >
                        <Trash2 size={16} />
                      </button>
                    )}
                  </article>
                );
              })}
            </div>
          )}
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
