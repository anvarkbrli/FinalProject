import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  Clock3,
  Gamepad2,
  LoaderCircle,
  Search,
  Star,
  X,
} from "lucide-react";
import {
  useCallback,
  useEffect,
  useRef,
  useState,
} from "react";
import { getApiErrorMessage } from "../../lib/api";
import { resolveAssetUrl } from "../../lib/helpers";
import {
  getGameById,
  getGames,
} from "../../services/gamesService";
import {
  createReview,
  getMyGameReview,
  updateReview,
} from "../../services/reviewsService";
import {
  getUserGameState,
  setUserGameRating,
  setUserGameStatus,
} from "../../services/userGamesService";
import type {
  CatalogSource,
  GameListItemDTO,
} from "../../types/home";
import type { GameDetailDTO } from "../../types/games";
import type { UserGameStatus } from "../../types/userGames";

interface GameReference {
  source: CatalogSource;
  id: number;
}

interface LogGameModalProps {
  open: boolean;
  onClose: () => void;
  game?: GameDetailDTO;
  onSaved?: () => void;
}

const statusOptions: Array<{
  value: UserGameStatus;
  label: string;
}> = [
  {
    value: "Playing",
    label: "Oynayıram",
  },
  {
    value: "PlanToPlay",
    label: "Oynayacağam",
  },
  {
    value: "Played",
    label: "Oynamışam",
  },
  {
    value: "Dropped",
    label: "Yarımçıq qoydum",
  },
];

function formatPlatformKey(
  source: CatalogSource,
  id: number,
): string {
  return `${source}:${id}`;
}

function parsePlatformKey(value: string): {
  source: CatalogSource;
  id: number;
} | null {
  const [source, idValue] = value.split(":");
  const id = Number(idValue);

  if (
    (source !== "Local" && source !== "Igdb") ||
    !Number.isSafeInteger(id) ||
    id < 1
  ) {
    return null;
  }

  return {
    source,
    id,
  };
}

function RatingInput({
  value,
  disabled,
  onChange,
}: {
  value: number | null;
  disabled: boolean;
  onChange: (value: number | null) => void;
}) {
  return (
    <div>
      <div className="flex flex-wrap items-center gap-1">
        {[1, 2, 3, 4, 5].map(
          (starNumber) => {
            const halfValue =
              starNumber - 0.5;
            const fillPercent =
              (value ?? 0) >= starNumber
                ? 100
                : (value ?? 0) >= halfValue
                  ? 50
                  : 0;

            return (
              <div
                key={starNumber}
                className="relative h-9 w-9"
              >
                <Star
                  size={29}
                  className="absolute inset-0 m-auto text-zinc-700"
                />

                <span
                  className="pointer-events-none absolute inset-y-0 left-0 overflow-hidden"
                  style={{
                    width: `${fillPercent}%`,
                  }}
                >
                  <Star
                    size={29}
                    className="absolute left-0 top-1/2 -translate-y-1/2 fill-amber-300 text-amber-300"
                  />
                </span>

                <button
                  type="button"
                  disabled={disabled}
                  aria-label={`${halfValue.toFixed(1)} puan`}
                  onClick={() => {
                    onChange(halfValue);
                  }}
                  className="absolute inset-y-0 left-0 z-10 w-1/2 rounded-l-lg outline-none focus-visible:ring-2 focus-visible:ring-amber-300 disabled:cursor-not-allowed"
                />

                <button
                  type="button"
                  disabled={disabled}
                  aria-label={`${starNumber.toFixed(1)} puan`}
                  onClick={() => {
                    onChange(starNumber);
                  }}
                  className="absolute inset-y-0 right-0 z-10 w-1/2 rounded-r-lg outline-none focus-visible:ring-2 focus-visible:ring-amber-300 disabled:cursor-not-allowed"
                />
              </div>
            );
          },
        )}

        {value !== null && (
          <button
            type="button"
            disabled={disabled}
            onClick={() => {
              onChange(null);
            }}
            className="ml-2 text-xs font-semibold text-zinc-500 transition hover:text-zinc-200 disabled:cursor-not-allowed"
          >
            Puanı sil
          </button>
        )}
      </div>

      <p className="mt-2 text-xs text-zinc-600">
        {value === null
          ? "Puan seçilməyib"
          : `${value.toFixed(1)} / 5`}
      </p>
    </div>
  );
}

function SearchGameCard({
  game,
  onSelect,
}: {
  game: GameListItemDTO;
  onSelect: (game: GameListItemDTO) => void;
}) {
  const coverUrl = resolveAssetUrl(
    game.coverImageUrl,
  );

  return (
    <button
      type="button"
      onClick={() => {
        onSelect(game);
      }}
      className="flex w-full items-center gap-3 rounded-2xl border border-white/[0.08] bg-white/[0.025] p-3 text-left transition hover:border-violet-400/30 hover:bg-violet-500/[0.07]"
    >
      <span className="flex h-16 w-12 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-zinc-900 text-zinc-700">
        {coverUrl ? (
          <img
            src={coverUrl}
            alt={`${game.title} örtüyü`}
            className="h-full w-full object-cover"
          />
        ) : (
          <Gamepad2 size={19} />
        )}
      </span>

      <span className="min-w-0">
        <span className="block truncate text-sm font-semibold text-zinc-100">
          {game.title}
        </span>
        <span className="mt-1 block text-xs text-zinc-600">
          {game.platforms
            .slice(0, 2)
            .map((platform) => platform.name)
            .join(", ") ||
            "Platforma göstərilməyib"}
        </span>
      </span>
    </button>
  );
}

export function LogGameModal({
  open,
  onClose,
  game,
  onSaved,
}: LogGameModalProps) {
  const queryClient = useQueryClient();
  const hydratedKeyRef = useRef("");

  const [searchTerm, setSearchTerm] =
    useState("");
  const [selectedReference, setSelectedReference] =
    useState<GameReference | null>(null);
  const [status, setStatus] =
    useState<UserGameStatus | "">("");
  const [rating, setRating] =
    useState<number | null>(null);
  const [platformKey, setPlatformKey] =
    useState("");
  const [hours, setHours] = useState("0");
  const [minutes, setMinutes] = useState("0");
  const [containsSpoilers, setContainsSpoilers] =
    useState(false);
  const [content, setContent] = useState("");
  const [formError, setFormError] =
    useState<string | null>(null);

  const handleClose = useCallback(() => {
    setSearchTerm("");
    setSelectedReference(null);
    setFormError(null);
    hydratedKeyRef.current = "";
    onClose();
  }, [onClose]);

  const selectedSource =
    game?.source ?? selectedReference?.source;
  const selectedId =
    game?.id ?? selectedReference?.id;

  const searchQuery = useQuery({
    queryKey: [
      "log-game-search",
      searchTerm.trim(),
    ],
    queryFn: () =>
      getGames({
        searchName: searchTerm.trim(),
        includeRelatedContent: true,
        pageNumber: 1,
        pageSize: 8,
      }),
    enabled:
      open &&
      !game &&
      !selectedReference &&
      searchTerm.trim().length >= 2,
  });

  const detailQuery = useQuery({
    queryKey: [
      "log-game-detail",
      selectedSource,
      selectedId,
    ],
    queryFn: () =>
      getGameById(
        selectedSource!,
        selectedId!,
      ),
    enabled:
      open &&
      !game &&
      selectedSource !== undefined &&
      selectedId !== undefined,
  });

  const selectedGame =
    game ?? detailQuery.data ?? null;

  const userGameQuery = useQuery({
    queryKey: [
      "user-game-state",
      selectedSource,
      selectedId,
    ],
    queryFn: () =>
      getUserGameState(
        selectedSource!,
        selectedId!,
      ),
    enabled:
      open &&
      selectedSource !== undefined &&
      selectedId !== undefined,
  });

  const myReviewQuery = useQuery({
    queryKey: [
      "my-game-review",
      selectedSource,
      selectedId,
    ],
    queryFn: () =>
      getMyGameReview(
        selectedSource!,
        selectedId!,
      ),
    enabled:
      open &&
      selectedSource !== undefined &&
      selectedId !== undefined,
  });

  useEffect(() => {
    if (!open) {
      return;
    }

    const previousOverflow =
      document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const handleKeyDown = (
      event: KeyboardEvent,
    ) => {
      if (event.key === "Escape") {
        handleClose();
      }
    };

    window.addEventListener(
      "keydown",
      handleKeyDown,
    );

    return () => {
      document.body.style.overflow =
        previousOverflow;
      window.removeEventListener(
        "keydown",
        handleKeyDown,
      );
    };
  }, [open, handleClose]);

  useEffect(() => {
    if (
      !open ||
      !selectedGame ||
      userGameQuery.isLoading ||
      myReviewQuery.isLoading
    ) {
      return;
    }

    const hydrateKey = `${selectedGame.source}:${selectedGame.id}:${myReviewQuery.data?.id ?? "new"}`;

    if (hydratedKeyRef.current === hydrateKey) {
      return;
    }

    const review = myReviewQuery.data;
    const userGame = userGameQuery.data;
    const playtime = review?.playtimeMinutes ?? 0;

    setStatus(userGame?.status ?? "");
    setRating(review?.rating ?? userGame?.rating ?? null);
    setContent(review?.content ?? "");
    setContainsSpoilers(
      review?.containsSpoilers ?? false,
    );
    setHours(
      Math.floor(playtime / 60).toString(),
    );
    setMinutes((playtime % 60).toString());
    setPlatformKey(
      review?.platformSource &&
        review.platformId
        ? formatPlatformKey(
            review.platformSource,
            review.platformId,
          )
        : "",
    );
    setFormError(null);
    hydratedKeyRef.current = hydrateKey;
  }, [
    open,
    selectedGame,
    userGameQuery.data,
    userGameQuery.isLoading,
    myReviewQuery.data,
    myReviewQuery.isLoading,
  ]);

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!selectedGame) {
        throw new Error("Əvvəlcə oyun seçin.");
      }

      const parsedHours = Number(hours || "0");
      const parsedMinutes = Number(minutes || "0");

      if (
        !Number.isInteger(parsedHours) ||
        parsedHours < 0 ||
        parsedHours > 10000
      ) {
        throw new Error(
          "Saat 0-10000 aralığında olmalıdır.",
        );
      }

      if (
        !Number.isInteger(parsedMinutes) ||
        parsedMinutes < 0 ||
        parsedMinutes > 59
      ) {
        throw new Error(
          "Dəqiqə 0-59 aralığında olmalıdır.",
        );
      }

      const trimmedContent = content.trim();
      const selectedPlatform =
        parsePlatformKey(platformKey);
      const totalMinutes =
        parsedHours * 60 + parsedMinutes;
      const existingReview =
        myReviewQuery.data;
      const currentUserGame =
        userGameQuery.data;

      if (
        !trimmedContent &&
        !status &&
        rating === null
      ) {
        throw new Error(
          "Status, puan və ya rəy məlumatlarından ən azı birini əlavə edin.",
        );
      }

      if (
        trimmedContent &&
        selectedGame.platforms.length > 0 &&
        !selectedPlatform
      ) {
        throw new Error(
          "Rəy üçün oynadığınız platformanı seçin.",
        );
      }

      if (
        status &&
        currentUserGame?.status !== status
      ) {
        await setUserGameStatus(
          selectedGame.source,
          selectedGame.id,
          status,
        );
      }

      if (trimmedContent) {
        const request = {
          content: trimmedContent,
          rating,
          containsSpoilers,
          playedOn: null,
          platformSource:
            selectedPlatform?.source ?? null,
          platformId:
            selectedPlatform?.id ?? null,
          playtimeMinutes:
            totalMinutes > 0
              ? totalMinutes
              : null,
        };

        if (existingReview) {
          await updateReview(
            existingReview.id,
            request,
          );
        } else {
          await createReview(
            selectedGame.source,
            selectedGame.id,
            request,
          );
        }
      } else if (
        rating !== currentUserGame?.rating
      ) {
        await setUserGameRating(
          selectedGame.source,
          selectedGame.id,
          rating,
        );
      }
    },
    onSuccess: async () => {
      if (!selectedGame) {
        return;
      }

      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: [
            "game-detail",
            selectedGame.source,
            selectedGame.id,
          ],
        }),
        queryClient.invalidateQueries({
          queryKey: [
            "user-game-state",
            selectedGame.source,
            selectedGame.id,
          ],
        }),
        queryClient.invalidateQueries({
          queryKey: [
            "my-game-review",
            selectedGame.source,
            selectedGame.id,
          ],
        }),
        queryClient.invalidateQueries({
          queryKey: [
            "game-reviews",
            selectedGame.source,
            selectedGame.id,
          ],
        }),
        queryClient.invalidateQueries({
          queryKey: ["user-reviews"],
        }),
        queryClient.invalidateQueries({
          queryKey: ["my-activities"],
        }),
      ]);

      onSaved?.();
      handleClose();
    },
    onError: (error) => {
      setFormError(
        getApiErrorMessage(
          error,
          error instanceof Error
            ? error.message
            : "Oyun jurnalını saxlamaq mümkün olmadı.",
        ),
      );
    },
  });

  if (!open) {
    return null;
  }

  const isLoadingSelectedGame =
    !game &&
    selectedReference !== null &&
    detailQuery.isLoading;
  const isLoadingExistingData =
    selectedGame !== null &&
    (userGameQuery.isLoading ||
      myReviewQuery.isLoading);
  const isPending = saveMutation.isPending;

  return (
    <div className="fixed inset-0 z-[110] flex items-center justify-center p-3 sm:p-6">
      <button
        type="button"
        aria-label="Log pəncərəsini bağla"
        onClick={handleClose}
        className="absolute inset-0 bg-black/80 backdrop-blur-md"
      />

      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="log-game-modal-title"
        className="relative z-10 flex max-h-[92vh] w-full max-w-3xl flex-col overflow-hidden rounded-3xl border border-white/10 bg-[#101116] shadow-2xl shadow-black/70"
      >
        <div className="flex items-start justify-between gap-4 border-b border-white/[0.08] px-5 py-5 sm:px-7">
          <div>
            <h2
              id="log-game-modal-title"
              className="text-xl font-black tracking-tight text-white"
            >
              {myReviewQuery.data
                ? "Oyun qeydini yenilə"
                : "Oyunu log et"}
            </h2>
            <p className="mt-1 text-sm text-zinc-500">
              Status, puan, platforma, oyun müddəti və rəyini bir yerdən əlavə et.
            </p>
          </div>

          <button
            type="button"
            onClick={handleClose}
            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl border border-white/10 bg-white/[0.03] text-zinc-400 transition hover:bg-white/[0.08] hover:text-white"
            aria-label="Pəncərəni bağla"
          >
            <X size={18} />
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5 sm:px-7 sm:py-6">
          {!selectedGame && !isLoadingSelectedGame ? (
            <div>
              <label className="block text-sm font-semibold text-zinc-200">
                Oyun axtar
              </label>

              <div className="relative mt-3">
                <Search
                  size={18}
                  className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600"
                />
                <input
                  type="search"
                  value={searchTerm}
                  onChange={(event) => {
                    setSearchTerm(
                      event.target.value,
                    );
                  }}
                  placeholder="Oyun adını yaz..."
                  autoFocus
                  className="h-12 w-full rounded-2xl border border-white/[0.09] bg-white/[0.035] pl-11 pr-4 text-sm text-white outline-none transition placeholder:text-zinc-700 focus:border-violet-400/45 focus:bg-violet-500/[0.05]"
                />
              </div>

              {searchTerm.trim().length < 2 ? (
                <div className="mt-5 rounded-2xl border border-dashed border-white/[0.08] px-5 py-10 text-center text-sm text-zinc-600">
                  Axtarış üçün ən azı 2 simvol yaz.
                </div>
              ) : searchQuery.isLoading ? (
                <div className="flex items-center justify-center gap-3 py-12 text-sm text-zinc-500">
                  <LoaderCircle
                    size={19}
                    className="animate-spin"
                  />
                  Oyunlar axtarılır...
                </div>
              ) : searchQuery.isError ? (
                <p className="mt-5 rounded-2xl border border-red-400/15 bg-red-500/[0.06] p-4 text-sm text-red-200">
                  {getApiErrorMessage(
                    searchQuery.error,
                    "Oyun axtarışı mümkün olmadı.",
                  )}
                </p>
              ) : (
                <div className="mt-4 grid gap-2 sm:grid-cols-2">
                  {(searchQuery.data?.items ?? []).map(
                    (resultGame) => (
                      <SearchGameCard
                        key={`${resultGame.source}-${resultGame.id}`}
                        game={resultGame}
                        onSelect={(selectedGameItem) => {
                          hydratedKeyRef.current = "";
                          setSelectedReference({
                            source:
                              selectedGameItem.source,
                            id: selectedGameItem.id,
                          });
                        }}
                      />
                    ),
                  )}
                </div>
              )}
            </div>
          ) : isLoadingSelectedGame ||
            isLoadingExistingData ? (
            <div className="flex min-h-80 flex-col items-center justify-center text-center">
              <LoaderCircle
                size={30}
                className="animate-spin text-violet-300"
              />
              <p className="mt-4 text-sm font-semibold text-zinc-300">
                Oyun qeydi hazırlanır...
              </p>
            </div>
          ) : selectedGame ? (
            <div className="space-y-6">
              <div className="flex items-center gap-4 rounded-2xl border border-white/[0.08] bg-white/[0.025] p-3">
                <span className="flex h-20 w-16 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-zinc-900 text-zinc-700">
                  {resolveAssetUrl(
                    selectedGame.coverImageUrl,
                  ) ? (
                    <img
                      src={
                        resolveAssetUrl(
                          selectedGame.coverImageUrl,
                        ) ?? undefined
                      }
                      alt={`${selectedGame.title} örtüyü`}
                      className="h-full w-full object-cover"
                    />
                  ) : (
                    <Gamepad2 size={23} />
                  )}
                </span>

                <div className="min-w-0 flex-1">
                  <p className="truncate font-bold text-white">
                    {selectedGame.title}
                  </p>
                  <p className="mt-1 text-xs text-zinc-600">
                    {myReviewQuery.data
                      ? "Mövcud rəy redaktə olunur"
                      : "Yeni oyun qeydi"}
                  </p>
                </div>

                {!game && (
                  <button
                    type="button"
                    onClick={() => {
                      setSelectedReference(null);
                      hydratedKeyRef.current = "";
                    }}
                    className="text-xs font-semibold text-violet-300 transition hover:text-violet-200"
                  >
                    Oyunu dəyiş
                  </button>
                )}
              </div>

              <div>
                <label className="block text-sm font-semibold text-zinc-200">
                  Status
                </label>
                <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4">
                  {statusOptions.map((option) => (
                    <button
                      key={option.value}
                      type="button"
                      disabled={isPending}
                      onClick={() => {
                        setStatus(option.value);
                      }}
                      className={`rounded-xl border px-3 py-3 text-xs font-bold transition disabled:cursor-not-allowed disabled:opacity-60 ${
                        status === option.value
                          ? "border-violet-400/45 bg-violet-500/15 text-violet-100"
                          : "border-white/[0.08] bg-white/[0.025] text-zinc-500 hover:text-zinc-200"
                      }`}
                    >
                      {option.label}
                    </button>
                  ))}
                </div>
              </div>

              <div>
                <label className="block text-sm font-semibold text-zinc-200">
                  Puan
                </label>
                <div className="mt-3">
                  <RatingInput
                    value={rating}
                    disabled={isPending}
                    onChange={setRating}
                  />
                </div>
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <div>
                  <label
                    htmlFor="log-platform"
                    className="block text-sm font-semibold text-zinc-200"
                  >
                    Hansı platformada oynadın?
                  </label>
                  <select
                    id="log-platform"
                    value={platformKey}
                    disabled={isPending}
                    onChange={(event) => {
                      setPlatformKey(
                        event.target.value,
                      );
                    }}
                    className="mt-3 h-12 w-full rounded-2xl border border-white/[0.09] bg-[#16171d] px-4 text-sm text-zinc-200 outline-none transition focus:border-violet-400/45 disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    <option value="">
                      Platforma seç
                    </option>
                    {selectedGame.platforms.map(
                      (platform) => (
                        <option
                          key={`${platform.source}-${platform.id}`}
                          value={formatPlatformKey(
                            platform.source,
                            platform.id,
                          )}
                        >
                          {platform.name}
                        </option>
                      ),
                    )}
                  </select>
                </div>

                <div>
                  <label className="block text-sm font-semibold text-zinc-200">
                    Oyun müddəti
                  </label>
                  <div className="mt-3 grid grid-cols-2 gap-2">
                    <label className="relative">
                      <input
                        type="number"
                        min="0"
                        max="10000"
                        value={hours}
                        disabled={isPending}
                        onChange={(event) => {
                          setHours(event.target.value);
                        }}
                        className="h-12 w-full rounded-2xl border border-white/[0.09] bg-white/[0.035] px-4 pr-12 text-sm text-white outline-none transition focus:border-violet-400/45 disabled:cursor-not-allowed disabled:opacity-60"
                      />
                      <span className="pointer-events-none absolute right-4 top-1/2 -translate-y-1/2 text-xs text-zinc-600">
                        saat
                      </span>
                    </label>

                    <label className="relative">
                      <input
                        type="number"
                        min="0"
                        max="59"
                        value={minutes}
                        disabled={isPending}
                        onChange={(event) => {
                          setMinutes(
                            event.target.value,
                          );
                        }}
                        className="h-12 w-full rounded-2xl border border-white/[0.09] bg-white/[0.035] px-4 pr-14 text-sm text-white outline-none transition focus:border-violet-400/45 disabled:cursor-not-allowed disabled:opacity-60"
                      />
                      <span className="pointer-events-none absolute right-4 top-1/2 -translate-y-1/2 text-xs text-zinc-600">
                        dəq.
                      </span>
                    </label>
                  </div>
                </div>
              </div>

              <div>
                <label
                  htmlFor="log-review-content"
                  className="block text-sm font-semibold text-zinc-200"
                >
                  Rəy
                </label>
                <textarea
                  id="log-review-content"
                  value={content}
                  disabled={isPending}
                  onChange={(event) => {
                    setContent(event.target.value);
                  }}
                  rows={6}
                  maxLength={10000}
                  placeholder="Oyun haqqında fikirlərini yaz... Rəy yazmaq məcburi deyil."
                  className="mt-3 w-full resize-y rounded-2xl border border-white/[0.09] bg-white/[0.035] px-4 py-3 text-sm leading-7 text-white outline-none transition placeholder:text-zinc-700 focus:border-violet-400/45 disabled:cursor-not-allowed disabled:opacity-60"
                />
                <p className="mt-2 text-right text-xs text-zinc-700">
                  {content.length} / 10000
                </p>
              </div>

              <fieldset className="rounded-2xl border border-white/[0.08] bg-white/[0.025] p-4">
                <legend className="px-1 text-sm font-semibold text-zinc-200">
                  Spoiler vəziyyəti
                </legend>

                <div className="mt-2 grid gap-2 sm:grid-cols-2">
                  <label className={`flex cursor-pointer items-start gap-3 rounded-xl border p-3 transition ${
                    !containsSpoilers
                      ? "border-violet-400/35 bg-violet-500/[0.09]"
                      : "border-white/[0.07] bg-white/[0.02]"
                  }`}>
                    <input
                      type="radio"
                      name="review-spoiler"
                      checked={!containsSpoilers}
                      disabled={isPending}
                      onChange={() => {
                        setContainsSpoilers(false);
                      }}
                      className="mt-1 h-4 w-4 accent-violet-500"
                    />
                    <span>
                      <span className="block text-sm font-semibold text-zinc-200">
                        Spoiler yoxdur
                      </span>
                      <span className="mt-1 block text-xs text-zinc-600">
                        Rəy birbaşa görünəcək.
                      </span>
                    </span>
                  </label>

                  <label className={`flex cursor-pointer items-start gap-3 rounded-xl border p-3 transition ${
                    containsSpoilers
                      ? "border-amber-300/30 bg-amber-500/[0.07]"
                      : "border-white/[0.07] bg-white/[0.02]"
                  }`}>
                    <input
                      type="radio"
                      name="review-spoiler"
                      checked={containsSpoilers}
                      disabled={isPending}
                      onChange={() => {
                        setContainsSpoilers(true);
                      }}
                      className="mt-1 h-4 w-4 accent-amber-400"
                    />
                    <span>
                      <span className="block text-sm font-semibold text-zinc-200">
                        Spoiler var
                      </span>
                      <span className="mt-1 block text-xs leading-5 text-zinc-600">
                        Rəy klik edilənədək bağlı görünəcək.
                      </span>
                    </span>
                  </label>
                </div>
              </fieldset>

              {formError && (
                <p className="rounded-2xl border border-red-400/15 bg-red-500/[0.06] px-4 py-3 text-sm leading-6 text-red-200">
                  {formError}
                </p>
              )}
            </div>
          ) : detailQuery.isError ? (
            <p className="rounded-2xl border border-red-400/15 bg-red-500/[0.06] p-4 text-sm text-red-200">
              {getApiErrorMessage(
                detailQuery.error,
                "Oyun məlumatını yükləmək mümkün olmadı.",
              )}
            </p>
          ) : null}
        </div>

        {selectedGame &&
          !isLoadingExistingData && (
            <div className="flex flex-col-reverse gap-3 border-t border-white/[0.08] px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-7">
              <span className="inline-flex items-center gap-2 text-xs text-zinc-600">
                <Clock3 size={15} />
                Dəyişikliklər jurnalında saxlanacaq
              </span>

              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={isPending}
                  onClick={handleClose}
                  className="rounded-xl border border-white/[0.09] px-4 py-2.5 text-sm font-semibold text-zinc-400 transition hover:bg-white/[0.05] hover:text-white disabled:cursor-not-allowed disabled:opacity-60"
                >
                  Ləğv et
                </button>
                <button
                  type="button"
                  disabled={isPending}
                  onClick={() => {
                    setFormError(null);
                    saveMutation.mutate();
                  }}
                  className="inline-flex items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 py-2.5 text-sm font-bold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {isPending && (
                    <LoaderCircle
                      size={16}
                      className="animate-spin"
                    />
                  )}
                  {myReviewQuery.data
                    ? "Yenilə"
                    : "Log et"}
                </button>
              </div>
            </div>
          )}
      </div>
    </div>
  );
}
