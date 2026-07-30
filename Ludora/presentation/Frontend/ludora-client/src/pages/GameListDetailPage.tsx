import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ArrowDown,
  ArrowLeft,
  ArrowUp,
  CalendarDays,
  Check,
  Gamepad2,
  Heart,
  ListChecks,
  ListOrdered,
  LoaderCircle,
  LockKeyhole,
  MessageCircle,
  Pencil,
  Plus,
  Search,
  Star,
  Trash2,
  Users,
  X,
} from "lucide-react";
import { useMemo, useState } from "react";
import {
  Link,
  useNavigate,
  useParams,
} from "react-router-dom";
import { InteractionCommentsPanel } from "../components/interactions/InteractionCommentsPanel";
import { InteractionLikesModal } from "../components/interactions/InteractionLikesModal";
import {
  GameListEditorModal,
  type GameListEditorValues,
} from "../components/lists/GameListEditorModal";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
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
  addGameToList,
  deleteGameList,
  getGameList,
  removeGameFromList,
  updateGameList,
  updateGameListPosition,
} from "../services/gameListsService";
import { getGames } from "../services/gamesService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../stores/authStore";
import type { GameListEntryDTO } from "../types/gameLists";
import type { GameListItemDTO } from "../types/home";

const visibilityPresentation = {
  Public: {
    label: "Hamı üçün",
    icon: Users,
  },
  FollowersOnly: {
    label: "Yalnız izləyicilər",
    icon: Check,
  },
  Private: {
    label: "Gizli",
    icon: LockKeyhole,
  },
} as const;

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
    <span className="flex h-28 w-20 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-white/[0.08] bg-zinc-900 text-zinc-700">
      {imageUrl ? (
        <img
          src={imageUrl}
          alt={`${title} örtüyü`}
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

export function GameListDetailPage() {
  const { language } = useI18n();
  const { id = "" } = useParams();
  const listId = Number(id);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);
  const expiresAtUtc = useAuthStore((state) => state.expiresAtUtc);
  const storedUser = useAuthStore((state) => state.user);
  const isAuthenticated =
    isAuthSessionValid(accessToken, expiresAtUtc) &&
    storedUser !== null;
  const [isEditorOpen, setIsEditorOpen] = useState(false);
  const [searchText, setSearchText] = useState("");
  const [notice, setNotice] = useState<NoticeState | null>(null);
  const [isCommentsOpen, setIsCommentsOpen] = useState(false);
  const [isLikesOpen, setIsLikesOpen] = useState(false);

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
      listQuery.data?.isOwner === true &&
      normalizedSearch.length >= 2,
  });

  const invalidateList = async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ["game-list", listId],
      }),
      queryClient.invalidateQueries({
        queryKey: ["user-game-lists"],
      }),
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
        message: getApiErrorMessage(
          error,
          "Siyahını bəyənmə əməliyyatı tamamlanmadı.",
        ),
      });
    },
  });

  const updateMutation = useMutation({
    mutationFn: (values: GameListEditorValues) =>
      updateGameList(listId, values),
    onSuccess: async () => {
      setIsEditorOpen(false);
      await invalidateList();
      setNotice({
        type: "success",
        message: "Siyahı yeniləndi.",
      });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteGameList(listId),
    onSuccess: () => {
      const ownerUserName = listQuery.data?.ownerUserName;
      navigate(
        ownerUserName
          ? `/users/${ownerUserName}`
          : "/",
        { replace: true },
      );
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "Siyahını silmək mümkün olmadı.",
        ),
      });
    },
  });

  const addMutation = useMutation({
    mutationFn: (game: GameListItemDTO) =>
      addGameToList(listId, game.source, game.id),
    onSuccess: async () => {
      await invalidateList();
      setNotice({
        type: "success",
        message: "Oyun siyahıya əlavə edildi.",
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "Oyunu siyahıya əlavə etmək mümkün olmadı.",
        ),
      });
    },
  });

  const removeMutation = useMutation({
    mutationFn: (game: GameListEntryDTO) =>
      removeGameFromList(listId, game.source, game.id),
    onSuccess: async () => {
      await invalidateList();
      setNotice({
        type: "success",
        message: "Oyun siyahıdan çıxarıldı.",
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "Oyunu siyahıdan çıxarmaq mümkün olmadı.",
        ),
      });
    },
  });

  const positionMutation = useMutation({
    mutationFn: ({
      game,
      position,
    }: {
      game: GameListEntryDTO;
      position: number;
    }) =>
      updateGameListPosition(
        listId,
        game.source,
        game.id,
        position,
      ),
    onSuccess: invalidateList,
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "Oyunun sırasını dəyişmək mümkün olmadı.",
        ),
      });
    },
  });

  const currentGameKeys = useMemo(
    () =>
      new Set(
        (listQuery.data?.games ?? []).map(
          (game) => `${game.source}:${game.id}`,
        ),
      ),
    [listQuery.data?.games],
  );

  if (listQuery.isLoading) {
    return (
      <div className="min-h-screen wellplayd-page-bg text-zinc-100">
        <Navbar />
        <main className="flex min-h-[70vh] items-center justify-center gap-3 text-sm text-zinc-500">
          <LoaderCircle size={20} className="animate-spin" />
          Siyahı yüklənir...
        </main>
      </div>
    );
  }

  if (listQuery.isError || !listQuery.data) {
    return (
      <div className="min-h-screen wellplayd-page-bg text-zinc-100">
        <Navbar />
        <main className="mx-auto flex min-h-[70vh] max-w-2xl items-center justify-center px-5 py-12">
          <div className="w-full rounded-3xl border border-red-400/15 bg-red-500/[0.06] p-8 text-center">
            <X size={32} className="mx-auto text-red-300" />
            <h1 className="mt-5 text-2xl font-black text-white">
              Siyahı açılmadı
            </h1>
            <p className="mt-3 text-sm leading-7 text-red-100/70">
              {getApiErrorMessage(
                listQuery.error,
                "Siyahı tapılmadı və ya onu görmək icazən yoxdur.",
              )}
            </p>
            <Link
              to="/"
              className="mx-auto mt-6 flex h-11 w-fit items-center rounded-xl bg-violet-600 px-5 text-sm font-bold text-white"
            >
              Ana səhifəyə qayıt
            </Link>
          </div>
        </main>
      </div>
    );
  }

  const list = listQuery.data;
  const visibility = visibilityPresentation[list.visibility];
  const VisibilityIcon = visibility.icon;
  const searchResults = searchQuery.data?.items ?? [];

  return (
    <div className="min-h-screen wellplayd-page-bg text-zinc-100">
      <Navbar />

      <main>
        <section className="relative overflow-hidden border-b border-white/[0.07] bg-[#0a0a0e]">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_10%,rgba(124,58,237,0.18),transparent_32%),radial-gradient(circle_at_85%_25%,rgba(245,158,11,0.07),transparent_25%)]" />
          <div className="relative mx-auto max-w-7xl px-5 py-10 lg:px-8 lg:py-14">
            <Link
              to={`/users/${list.ownerUserName}`}
              className="inline-flex items-center gap-2 text-sm font-semibold text-zinc-500 transition hover:text-violet-300"
            >
              <ArrowLeft size={16} />
              {language === "en"
                ? `Back to ${list.ownerUserName}'s profile`
                : `${list.ownerUserName} profilinə qayıt`}
            </Link>

            <div className="mt-8 flex flex-col gap-7 lg:flex-row lg:items-end lg:justify-between">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="inline-flex items-center gap-2 rounded-full border border-violet-300/15 bg-violet-500/[0.09] px-3 py-1.5 text-xs font-bold text-violet-200">
                    {list.isRanked ? (
                      <ListOrdered size={14} />
                    ) : (
                      <ListChecks size={14} />
                    )}
                    {list.isRanked
                      ? "Ranklanmış siyahı"
                      : "Sırasız siyahı"}
                  </span>
                  <span className="inline-flex items-center gap-2 rounded-full border border-white/[0.08] bg-white/[0.035] px-3 py-1.5 text-xs font-semibold text-zinc-400">
                    <VisibilityIcon size={14} />
                    {visibility.label}
                  </span>
                </div>

                <h1 className="mt-5 max-w-4xl text-3xl font-black tracking-tight text-white sm:text-5xl">
                  {list.name}
                </h1>
                <p className="mt-4 max-w-3xl whitespace-pre-wrap text-sm leading-7 text-zinc-400">
                  {list.description ?? "Bu siyahı üçün açıqlama əlavə edilməyib."}
                </p>
                <div className="mt-5 flex flex-wrap items-center gap-5 text-xs text-zinc-600">
                  <span className="inline-flex items-center gap-2">
                    <Gamepad2 size={15} />
                    {list.gameCount} oyun
                  </span>
                  <span className="inline-flex items-center gap-2">
                    <CalendarDays size={15} />
                    {formatDateTime(list.createdAt)}
                  </span>
                </div>
              </div>

              {list.isOwner && (
                <div className="flex shrink-0 flex-wrap gap-3">
                  <button
                    type="button"
                    onClick={() => {
                      setIsEditorOpen(true);
                    }}
                    className="flex h-11 items-center gap-2 rounded-xl border border-white/[0.09] bg-white/[0.05] px-4 text-sm font-bold text-zinc-200 transition hover:bg-white/[0.09]"
                  >
                    <Pencil size={16} />
                    Redaktə et
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      if (
                        window.confirm(
                          "Bu siyahını silmək istəyirsən?",
                        )
                      ) {
                        deleteMutation.mutate();
                      }
                    }}
                    disabled={deleteMutation.isPending}
                    className="flex h-11 items-center gap-2 rounded-xl border border-red-400/15 bg-red-500/[0.06] px-4 text-sm font-bold text-red-300 transition hover:bg-red-500/15 disabled:opacity-50"
                  >
                    <Trash2 size={16} />
                    Sil
                  </button>
                </div>
              )}
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
                onClick={() => {
                  setNotice(null);
                }}
                aria-label="Bildirişi bağla"
              >
                <X size={16} />
              </button>
            </div>
          )}

          <section className="mb-8 overflow-hidden rounded-3xl border border-white/[0.07] bg-[#101015]">
            <div className="flex items-center gap-5 px-5 py-4 text-sm text-zinc-500 sm:px-6">
              <span className="inline-flex items-center gap-2">
                <button
                  type="button"
                  disabled={
                    !isAuthenticated ||
                    list.isOwner ||
                    likeMutation.isPending
                  }
                  onClick={() => {
                    likeMutation.mutate();
                  }}
                  className={`group inline-flex items-center transition disabled:cursor-not-allowed disabled:opacity-45 ${
                    list.isLikedByCurrentUser
                      ? "text-rose-300"
                      : "hover:text-rose-300"
                  }`}
                  aria-label="Siyahını bəyən"
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
                  onClick={() => {
                    setIsLikesOpen(true);
                  }}
                  className="font-semibold transition hover:text-rose-300"
                >
                  {list.likeCount} bəyəni
                </button>
              </span>

              <button
                type="button"
                onClick={() => {
                  setIsCommentsOpen((currentValue) => !currentValue);
                }}
                className={`inline-flex items-center gap-2 font-semibold transition hover:text-violet-300 ${
                  isCommentsOpen ? "text-violet-300" : ""
                }`}
              >
                <MessageCircle size={17} />
                {list.commentCount} şərh
              </button>
            </div>

            {isCommentsOpen && (
              <InteractionCommentsPanel
                contentType="game-list"
                contentId={listId}
                isAuthenticated={isAuthenticated}
                placeholder="Siyahıya şərh yaz..."
                emptyText="Bu siyahıya hələ şərh yazılmayıb."
                invalidateQueryKeys={[
                  ["game-list", listId],
                  ["user-game-lists"],
                ]}
              />
            )}
          </section>

          {list.isOwner && (
            <section className="mb-8 rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
              <div className="flex items-center gap-2">
                <Plus size={19} className="text-violet-300" />
                <h2 className="font-black text-white">
                  Siyahıya oyun əlavə et
                </h2>
              </div>
              <p className="mt-2 text-sm leading-6 text-zinc-600">
                Oyun adına görə axtar və nəticədən birbaşa siyahıya əlavə et.
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
                  onChange={(event) => {
                    setSearchText(event.target.value);
                  }}
                  placeholder="Oyun axtar..."
                  className="h-12 w-full rounded-2xl border border-white/[0.08] bg-white/[0.035] pl-11 pr-4 text-sm text-zinc-100 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/45 focus:ring-4 focus:ring-violet-500/10"
                />
              </div>

              {normalizedSearch.length >= 2 && (
                <div className="mt-4 grid gap-3 md:grid-cols-2">
                  {searchQuery.isLoading ? (
                    <div className="col-span-full flex items-center justify-center gap-3 py-8 text-sm text-zinc-600">
                      <LoaderCircle size={17} className="animate-spin" />
                      Oyunlar axtarılır...
                    </div>
                  ) : searchQuery.isError ? (
                    <p className="col-span-full py-6 text-center text-sm text-red-300">
                      Oyunları axtarmaq mümkün olmadı.
                    </p>
                  ) : searchResults.length === 0 ? (
                    <p className="col-span-full py-6 text-center text-sm text-zinc-600">
                      Uyğun oyun tapılmadı.
                    </p>
                  ) : (
                    searchResults.map((game) => {
                      const isAdded = currentGameKeys.has(
                        `${game.source}:${game.id}`,
                      );

                      return (
                        <div
                          key={`${game.source}-${game.id}`}
                          className="flex items-center gap-3 rounded-2xl border border-white/[0.07] bg-white/[0.025] p-3"
                        >
                          <GameCover
                            title={game.title}
                            coverImageUrl={game.coverImageUrl}
                          />
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
                            onClick={() => {
                              addMutation.mutate(game);
                            }}
                            disabled={
                              isAdded || addMutation.isPending
                            }
                            className="flex h-10 shrink-0 items-center gap-2 rounded-xl bg-violet-600 px-3 text-xs font-bold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:bg-white/[0.05] disabled:text-zinc-600"
                          >
                            {isAdded ? (
                              <Check size={15} />
                            ) : (
                              <Plus size={15} />
                            )}
                            {isAdded ? "Əlavə edilib" : "Əlavə et"}
                          </button>
                        </div>
                      );
                    })
                  )}
                </div>
              )}
            </section>
          )}

          {list.games.length === 0 ? (
            <div className="rounded-3xl border border-dashed border-white/[0.08] p-14 text-center">
              <ListChecks
                size={34}
                className="mx-auto text-zinc-700"
              />
              <h2 className="mt-4 font-bold text-zinc-300">
                Siyahı hələ boşdur
              </h2>
              <p className="mt-2 text-sm text-zinc-600">
                {list.isOwner
                  ? "Yuxarıdakı axtarışdan ilk oyunu əlavə et."
                  : "Siyahı sahibi hələ oyun əlavə etməyib."}
              </p>
            </div>
          ) : (
            <div className="space-y-3">
              {list.games.map((game, index) => {
                const rankPosition = game.position ?? index + 1;

                return (
                  <article
                    key={`${game.source}-${game.id}`}
                    className="flex items-center gap-4 rounded-3xl border border-white/[0.07] bg-[#101015] p-4 sm:p-5"
                  >
                    {list.isRanked && (
                      <div className="relative flex h-[4.5rem] w-14 shrink-0 items-end justify-center">
                        {rankPosition === 1 && (
                          <div
                            className="absolute -top-3 left-1/2 z-10 -translate-x-1/2"
                            aria-hidden="true"
                          >
                            <svg
                              viewBox="0 0 72 48"
                              className="h-10 w-14 -rotate-3 overflow-visible drop-shadow-[0_0_10px_rgba(245,158,11,0.55)]"
                            >
                              <defs>
                                <linearGradient
                                  id="rank-crown-gold"
                                  x1="0"
                                  y1="0"
                                  x2="0"
                                  y2="1"
                                >
                                  <stop offset="0%" stopColor="#fff3a3" />
                                  <stop offset="45%" stopColor="#fbbf24" />
                                  <stop offset="100%" stopColor="#b45309" />
                                </linearGradient>
                              </defs>

                              <path
                                d="M8 37 4 13l17 11L36 4l15 20 17-11-4 24Z"
                                fill="url(#rank-crown-gold)"
                                stroke="#fde68a"
                                strokeWidth="2"
                                strokeLinejoin="round"
                              />
                              <path
                                d="M10 37h52l-3 7H13Z"
                                fill="#d97706"
                                stroke="#fde68a"
                                strokeWidth="2"
                                strokeLinejoin="round"
                              />
                              <circle cx="21" cy="31" r="2.5" fill="#fff7cc" />
                              <circle cx="36" cy="27" r="3" fill="#fff7cc" />
                              <circle cx="51" cy="31" r="2.5" fill="#fff7cc" />
                            </svg>
                          </div>
                        )}

                        <span
                          className={`flex h-12 w-12 items-center justify-center rounded-2xl border text-lg font-black ${getRankBadgeClasses(
                            rankPosition,
                          )}`}
                        >
                          {rankPosition}
                        </span>
                      </div>
                    )}

                    <Link
                      to={`/games/${game.source}/${game.id}`}
                      className="flex min-w-0 flex-1 items-center gap-4"
                    >
                      <GameCover
                        title={game.title}
                        coverImageUrl={game.coverImageUrl}
                      />
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
                            {language === "en"
                              ? `Added on ${formatDate(game.addedAt)}`
                              : `${formatDate(game.addedAt)} tarixində əlavə edilib`}
                          </span>
                        </div>
                      </div>
                    </Link>

                    {list.isOwner && (
                      <div className="flex shrink-0 items-center gap-2">
                        {list.isRanked && (
                          <div className="flex flex-col gap-1">
                            <button
                              type="button"
                              disabled={
                                index === 0 || positionMutation.isPending
                              }
                              onClick={() => {
                                positionMutation.mutate({
                                  game,
                                  position: index,
                                });
                              }}
                              className="flex h-8 w-9 items-center justify-center rounded-lg border border-white/[0.07] text-zinc-500 transition hover:bg-white/[0.06] hover:text-white disabled:opacity-30"
                              aria-label="Oyunu yuxarı qaldır"
                            >
                              <ArrowUp size={15} />
                            </button>
                            <button
                              type="button"
                              disabled={
                                index === list.games.length - 1 ||
                                positionMutation.isPending
                              }
                              onClick={() => {
                                positionMutation.mutate({
                                  game,
                                  position: index + 2,
                                });
                              }}
                              className="flex h-8 w-9 items-center justify-center rounded-lg border border-white/[0.07] text-zinc-500 transition hover:bg-white/[0.06] hover:text-white disabled:opacity-30"
                              aria-label="Oyunu aşağı endir"
                            >
                              <ArrowDown size={15} />
                            </button>
                          </div>
                        )}

                        <button
                          type="button"
                          onClick={() => {
                            if (
                              window.confirm(
                                `${game.title} oyununu siyahıdan çıxarmaq istəyirsən?`,
                              )
                            ) {
                              removeMutation.mutate(game);
                            }
                          }}
                          disabled={removeMutation.isPending}
                          className="flex h-10 w-10 items-center justify-center rounded-xl border border-red-400/10 text-red-300/65 transition hover:bg-red-500/[0.08] hover:text-red-300 disabled:opacity-50"
                          aria-label="Oyunu siyahıdan çıxar"
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
        </section>
      </main>

      <Footer />

      <InteractionLikesModal
        contentType="game-list"
        contentId={isLikesOpen ? listId : null}
        likeCount={list.likeCount}
        onClose={() => {
          setIsLikesOpen(false);
        }}
      />

      {isEditorOpen && (
        <GameListEditorModal
          open
          title="Siyahını redaktə et"
          submitLabel="Yadda saxla"
          initialValues={{
            name: list.name,
            description: list.description,
            visibility: list.visibility,
            isRanked: list.isRanked,
          }}
          isPending={updateMutation.isPending}
          errorMessage={
            updateMutation.isError
              ? getApiErrorMessage(
                  updateMutation.error,
                  "Siyahını yeniləmək mümkün olmadı.",
                )
              : null
          }
          onClose={() => {
            setIsEditorOpen(false);
          }}
          onSubmit={(values) => {
            updateMutation.mutate(values);
          }}
        />
      )}
    </div>
  );
}
