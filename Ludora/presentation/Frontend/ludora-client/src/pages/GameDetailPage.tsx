import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ArrowLeft,
  Bookmark,
  Building2,
  CheckCircle2,
  Clock3,
  Database,
  Gamepad2,
  Glasses,
  Globe2,
  Heart,
  Joystick,
  MousePointer2,
  Smartphone,
  Layers3,
  LoaderCircle,
  PenLine,
  PlayCircle,
  TabletSmartphone,
  Tv,
  XCircle,
} from "lucide-react";
import { useLayoutEffect, useMemo, useState } from "react";
import {
  Link,
  useLocation,
  useParams,
} from "react-router-dom";
import { RatingStarIcon } from "../components/common/RatingStarIcon";
import { useI18n } from "../i18n/useI18n";
import { FpsEstimatorAction } from "../components/fps/FpsEstimatorAction";
import { GameCommunityStats } from "../components/game/GameCommunityStats";
import { FriendWishlistNotice } from "../components/game/FriendWishlistNotice";
import { GameReviewsSection } from "../components/reviews/GameReviewsSection";
import { LogGameModal } from "../components/reviews/LogGameModal";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { ApiErrorStatusPage } from "../components/status/ApiErrorStatusPage";
import { getApiErrorMessage } from "../lib/api";
import { canViewCatalogSources } from "../lib/authRoles";
import {
  isFutureReleaseDate,
  resolveAssetUrl,
} from "../lib/helpers";
import { getGameById } from "../services/gamesService";
import { getMyGameReview } from "../services/reviewsService";
import {
  getUserGameState,
  removeUserGameStatus,
  setUserGameFavorite,
  setUserGameRating,
  setUserGameStatus,
  setUserGameWishlist,
} from "../services/userGamesService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../stores/authStore";
import type {
  CatalogSource,
  GameListItemDTO,
  GameType,
} from "../types/home";
import type {
  GameDetailDTO,
  RelatedGamesDTO,
} from "../types/games";
import type {
  UserGameDTO,
  UserGameStatus,
} from "../types/userGames";

interface RelatedSection {
  key: keyof Omit<
    RelatedGamesDTO,
    "parentGame"
  >;
  title: string;
  shortTitle: string;
}

interface RelatedTab {
  key: string;
  title: string;
  games: GameListItemDTO[];
}

function getRelatedSections(language: "az" | "en"): RelatedSection[] {
  if (language === "en") {
    return [
      { key: "seriesGames", title: "Series titles", shortTitle: "Series" },
      { key: "franchiseGames", title: "Franchise titles", shortTitle: "Franchise" },
      { key: "dlcs", title: "DLCs", shortTitle: "DLC" },
      { key: "expansions", title: "Expansions", shortTitle: "Expansion" },
      { key: "standaloneExpansions", title: "Standalone expansions", shortTitle: "Standalone" },
      { key: "bundles", title: "Bundles", shortTitle: "Bundle" },
      { key: "remakes", title: "Remakes", shortTitle: "Remake" },
      { key: "remasters", title: "Remasters", shortTitle: "Remaster" },
    ];
  }

  return [
    { key: "seriesGames", title: "Seriya oyunları", shortTitle: "Seriya" },
    { key: "franchiseGames", title: "Franchise oyunları", shortTitle: "Franchise" },
    { key: "dlcs", title: "DLC-lər", shortTitle: "DLC" },
    { key: "expansions", title: "Expansion-lar", shortTitle: "Expansion" },
    { key: "standaloneExpansions", title: "Müstəqil expansion-lar", shortTitle: "Standalone" },
    { key: "bundles", title: "Bundle-lar", shortTitle: "Bundle" },
    { key: "remakes", title: "Remake-lər", shortTitle: "Remake" },
    { key: "remasters", title: "Remaster-lər", shortTitle: "Remaster" },
  ];
}

const gameTypeLabels: Record<
  GameType,
  string
> = {
  MainGame: "Əsas oyun",
  DlcAddon: "DLC",
  Expansion: "Expansion",
  Bundle: "Bundle",
  StandaloneExpansion:
    "Müstəqil expansion",
  Mod: "Mod",
  Episode: "Epizod",
  Season: "Sezon",
  Remake: "Remake",
  Remaster: "Remaster",
  ExpandedGame: "Genişləndirilmiş oyun",
  Port: "Port",
  Fork: "Fork",
  Pack: "Paket",
  Update: "Yeniləmə",
};

function parseRouteSource(
  value: string | undefined,
): CatalogSource | null {
  switch (value?.trim().toLowerCase()) {
    case "local":
      return "Local";
    case "igdb":
      return "Igdb";
    default:
      return null;
  }
}

function formatReleaseDate(
  value: string | null,
): string {
  if (!value) {
    return "Tarix qeyd edilməyib";
  }

  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(
    value,
  );

  if (!match) {
    return value;
  }

  const [, year, month, day] = match;

  return `${day}.${month}.${year}`;
}

type CatalogFilterKind =
  | "company"
  | "genre"
  | "platform";

function buildCatalogFilterUrl(
  kind: CatalogFilterKind,
  source: CatalogSource,
  id: number,
): string {
  const searchParams = new URLSearchParams({
    [`${kind}Source`]: source,
    [`${kind}Id`]: String(id),
  });

  return `/games?${searchParams.toString()}`;
}

function buildCatalogFilterState(
  kind: CatalogFilterKind,
  source: CatalogSource,
  id: number,
  name: string,
) {
  return {
    catalogFilter: {
      kind,
      source,
      id,
      name,
    },
  };
}

function getPlatformIcon(
  platformName: string,
): typeof Gamepad2 {
  const normalizedName = platformName
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLocaleLowerCase();

  if (
    normalizedName.includes("windows") ||
    normalizedName.includes("pc") ||
    normalizedName.includes("linux") ||
    normalizedName.includes("mac") ||
    normalizedName.includes("computer")
  ) {
    return MousePointer2;
  }

  if (
    normalizedName.includes("android") ||
    normalizedName.includes("ios") ||
    normalizedName.includes("mobile") ||
    normalizedName.includes("phone")
  ) {
    return Smartphone;
  }

  if (
    normalizedName.includes("tablet") ||
    normalizedName.includes("ipad")
  ) {
    return TabletSmartphone;
  }

  if (
    normalizedName.includes("web") ||
    normalizedName.includes("browser")
  ) {
    return Globe2;
  }

  if (normalizedName.includes("arcade")) {
    return Joystick;
  }

  if (
    normalizedName.includes("vr") ||
    normalizedName.includes(
      "virtual reality",
    )
  ) {
    return Glasses;
  }

  if (
    normalizedName.includes("tv") ||
    normalizedName.includes("television")
  ) {
    return Tv;
  }

  return Gamepad2;
}

function InteractiveRatingStar({
  starNumber,
  selectedRating,
  disabled,
  onPreview,
  onSelect,
}: {
  starNumber: number;
  selectedRating: number;
  disabled: boolean;
  onPreview: (
    value: number | null,
  ) => void;
  onSelect: (value: number) => void;
}) {
  const { language } = useI18n();
  const halfValue = starNumber - 0.5;

  const fillPercent =
    selectedRating >= starNumber
      ? 100
      : selectedRating >= halfValue
        ? 50
        : 0;

  return (
    <div className="relative h-8 w-8 shrink-0">
      <RatingStarIcon
        fillPercent={fillPercent}
        size={28}
        className="absolute inset-0 m-auto"
      />

      <button
        type="button"
        disabled={disabled}
        aria-label={`${halfValue.toFixed(
          1,
        )} ${language === "en" ? "star rating" : "ulduz reytinq ver"}`}
        onMouseEnter={() =>
          onPreview(halfValue)
        }
        onFocus={() =>
          onPreview(halfValue)
        }
        onBlur={() => onPreview(null)}
        onClick={() =>
          onSelect(halfValue)
        }
        className="absolute inset-y-0 left-0 z-10 w-1/2 rounded-l-md outline-none focus-visible:ring-2 focus-visible:ring-amber-300 disabled:cursor-not-allowed"
      />

      <button
        type="button"
        disabled={disabled}
        aria-label={`${starNumber.toFixed(
          1,
        )} ${language === "en" ? "star rating" : "ulduz reytinq ver"}`}
        onMouseEnter={() =>
          onPreview(starNumber)
        }
        onFocus={() =>
          onPreview(starNumber)
        }
        onBlur={() => onPreview(null)}
        onClick={() =>
          onSelect(starNumber)
        }
        className="absolute inset-y-0 right-0 z-10 w-1/2 rounded-r-md outline-none focus-visible:ring-2 focus-visible:ring-amber-300 disabled:cursor-not-allowed"
      />
    </div>
  );
}

type TrackingAction =
  | {
      type: "status";
      status: UserGameStatus;
    }
  | {
      type: "remove-status";
    }
  | {
      type: "favorite";
      value: boolean;
    }
  | {
      type: "wishlist";
      value: boolean;
    }
  | {
      type: "rating";
      value: number | null;
    };

function getTrackingStatusOptions(language: "az" | "en"): Array<{
  value: UserGameStatus;
  label: string;
  icon: typeof PlayCircle;
}> {
  if (language === "en") {
    return [
      { value: "Playing", label: "Playing", icon: PlayCircle },
      { value: "PlanToPlay", label: "Going to play", icon: Clock3 },
      { value: "Played", label: "Played", icon: CheckCircle2 },
      { value: "Dropped", label: "Abandoned", icon: XCircle },
    ];
  }

  return [
    { value: "Playing", label: "Oynayıram", icon: PlayCircle },
    { value: "PlanToPlay", label: "Oynayacağam", icon: Clock3 },
    { value: "Played", label: "Oynamışam", icon: CheckCircle2 },
    { value: "Dropped", label: "Yarımçıq qoydum", icon: XCircle },
  ];
}

function GameTrackingPanel({
  game,
}: {
  game: GameDetailDTO;
}) {
  const { language } = useI18n();
  const trackingStatusOptions = getTrackingStatusOptions(language);
  const queryClient = useQueryClient();

  const accessToken = useAuthStore(
    (state) => state.accessToken,
  );

  const expiresAtUtc = useAuthStore(
    (state) => state.expiresAtUtc,
  );

  const isAuthenticated =
    isAuthSessionValid(
      accessToken,
      expiresAtUtc,
    );

  const [actionError, setActionError] =
    useState<string | null>(null);

  const [
    hoveredRating,
    setHoveredRating,
  ] = useState<number | null>(null);

  const [
    isLogModalOpen,
    setIsLogModalOpen,
  ] = useState(false);

  const userStateKey = [
    "user-game-state",
    game.source,
    game.id,
  ] as const;

  const stateQuery = useQuery({
    queryKey: userStateKey,
    queryFn: () =>
      getUserGameState(
        game.source,
        game.id,
      ),
    enabled: isAuthenticated,
  });

  const myReviewQuery = useQuery({
    queryKey: [
      "my-game-review",
      game.source,
      game.id,
    ],
    queryFn: () =>
      getMyGameReview(
        game.source,
        game.id,
      ),
    enabled: isAuthenticated,
  });

  const actionMutation = useMutation<
    UserGameDTO | null,
    unknown,
    TrackingAction
  >({
    mutationFn: (action) => {
      switch (action.type) {
        case "status":
          return setUserGameStatus(
            game.source,
            game.id,
            action.status,
          );

        case "remove-status":
          return removeUserGameStatus(
            game.source,
            game.id,
          );

        case "favorite":
          return setUserGameFavorite(
            game.source,
            game.id,
            action.value,
          );

        case "wishlist":
          return setUserGameWishlist(
            game.source,
            game.id,
            action.value,
          );

        case "rating":
          return setUserGameRating(
            game.source,
            game.id,
            action.value,
          );
      }
    },

    onSuccess: (state) => {
      queryClient.setQueryData(
        userStateKey,
        state,
      );

      void queryClient.invalidateQueries({
        queryKey: [
          "game-detail",
          game.source,
          game.id,
        ],
      });

      void queryClient.invalidateQueries({
        queryKey: [
          "my-game-review",
          game.source,
          game.id,
        ],
      });

      void queryClient.invalidateQueries({
        queryKey: ["user-profile"],
      });

      void queryClient.invalidateQueries({
        queryKey: ["user-profile-games"],
      });

      void queryClient.invalidateQueries({
        queryKey: ["user-profile-recent-played-games"],
      });

      void queryClient.invalidateQueries({
        queryKey: ["user-playtime-statistics"],
      });

      void queryClient.invalidateQueries({
        queryKey: ["my-activities"],
      });
    },

    onError: (error) => {
      setActionError(
        getApiErrorMessage(
          error,
          "Oyun məlumatını yeniləmək mümkün olmadı.",
        ),
      );
    },
  });

  const runAction = (
    action: TrackingAction,
  ) => {
    setActionError(null);
    actionMutation.mutate(action);
  };

  if (!isAuthenticated) {
    return (
      <div className="rounded-2xl border border-violet-400/15 bg-violet-500/[0.07] p-5">
        <p className="text-sm font-bold text-zinc-100">
          {language === "en" ? "Track this game on your profile" : "Oyunu öz profilində izlə"}
        </p>

        <p className="mt-2 text-sm leading-6 text-zinc-500">
          {language === "en"
            ? "Sign in to add a status, wishlist, favorite, and personal rating."
            : "Status, wishlist, favori və şəxsi reytinq əlavə etmək üçün hesabına daxil ol."}
        </p>

        <Link
          to="/login"
          className="mt-4 inline-flex rounded-lg bg-violet-500 px-4 py-2 text-sm font-bold text-white transition hover:bg-violet-400"
        >
          {language === "en" ? "Sign in" : "Daxil ol"}
        </Link>
      </div>
    );
  }

  const userState = stateQuery.data;

  const selectedRating =
    hoveredRating ??
    userState?.rating ??
    0;

  const isPending =
    actionMutation.isPending;

  return (
    <>
      <div className="rounded-2xl border border-white/[0.08] bg-[#15161c] p-5">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-sm font-bold text-zinc-100">
            {language === "en" ? "My game state" : "Mənim oyun vəziyyətim"}
          </p>

          <p className="mt-1 text-xs text-zinc-600">
            {language === "en" ? "Manage your status and rating" : "Statusunu və reytinqini idarə et"}
          </p>
        </div>

        {isPending && (
          <LoaderCircle
            size={18}
            className="animate-spin text-violet-300"
          />
        )}
      </div>

      {stateQuery.isLoading ? (
        <div className="mt-5 space-y-3 animate-pulse">
          <div className="h-10 rounded-lg bg-white/[0.05]" />
          <div className="h-10 rounded-lg bg-white/[0.05]" />
          <div className="h-12 rounded-lg bg-white/[0.05]" />
        </div>
      ) : (
        <>
          <div className="mt-5 grid gap-2">
            {trackingStatusOptions.map(
              (option) => {
                const Icon = option.icon;

                const isActive =
                  userState?.status ===
                  option.value;

                return (
                  <button
                    key={option.value}
                    type="button"
                    disabled={isPending}
                    onClick={() => {
                      runAction(
                        isActive
                          ? {
                              type:
                                "remove-status",
                            }
                          : {
                              type: "status",
                              status:
                                option.value,
                            },
                      );
                    }}
                    className={`flex items-center gap-3 rounded-lg border px-3 py-2.5 text-left text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-60 ${
                      isActive
                        ? "border-violet-400/45 bg-violet-500/15 text-violet-100"
                        : "border-white/[0.08] bg-white/[0.025] text-zinc-400 hover:border-white/15 hover:text-zinc-100"
                    }`}
                  >
                    <Icon size={17} />
                    {option.label}
                  </button>
                );
              },
            )}
          </div>

          <div className="mt-3 grid grid-cols-2 gap-2">
            <button
              type="button"
              disabled={isPending}
              onClick={() => {
                runAction({
                  type: "wishlist",
                  value:
                    !userState?.isWishlisted,
                });
              }}
              className={`inline-flex items-center justify-center gap-2 rounded-lg border px-3 py-2.5 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-60 ${
                userState?.isWishlisted
                  ? "border-fuchsia-400/40 bg-fuchsia-500/15 text-fuchsia-100"
                  : "border-white/[0.08] bg-white/[0.025] text-zinc-400 hover:text-zinc-100"
              }`}
            >
              <Bookmark
                size={17}
                className={
                  userState?.isWishlisted
                    ? "fill-current"
                    : ""
                }
              />

              Wishlist
            </button>

            <button
              type="button"
              disabled={isPending}
              onClick={() => {
                runAction({
                  type: "favorite",
                  value:
                    !userState?.isFavorite,
                });
              }}
              className={`inline-flex items-center justify-center gap-2 rounded-lg border px-3 py-2.5 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-60 ${
                userState?.isFavorite
                  ? "border-rose-400/40 bg-rose-500/15 text-rose-100"
                  : "border-white/[0.08] bg-white/[0.025] text-zinc-400 hover:text-zinc-100"
              }`}
            >
              <Heart
                size={17}
                className={
                  userState?.isFavorite
                    ? "fill-current"
                    : ""
                }
              />

              {language === "en" ? "Favorite" : "Favori"}
            </button>
          </div>

          <div className="mt-5 border-t border-white/[0.08] pt-5">
            <div className="flex items-center justify-between gap-3">
              <span className="text-sm font-semibold text-zinc-300">
                {language === "en" ? "My rating" : "Mənim reytinqim"}
              </span>

              {userState?.rating !== null &&
                userState?.rating !==
                  undefined && (
                  <button
                    type="button"
                    disabled={isPending}
                    onClick={() => {
                      runAction({
                        type: "rating",
                        value: null,
                      });
                    }}
                    className="text-xs font-semibold text-zinc-600 transition hover:text-red-300 disabled:opacity-50"
                  >
                    {language === "en" ? "Remove" : "Sil"}
                  </button>
                )}
            </div>

            <div
              className="mt-3 flex items-center gap-1"
              onMouseLeave={() => {
                setHoveredRating(null);
              }}
            >
              {[1, 2, 3, 4, 5].map(
                (starNumber) => (
                  <InteractiveRatingStar
                    key={starNumber}
                    starNumber={
                      starNumber
                    }
                    selectedRating={
                      selectedRating
                    }
                    disabled={isPending}
                    onPreview={
                      setHoveredRating
                    }
                    onSelect={(rating) => {
                      runAction({
                        type: "rating",
                        value: rating,
                      });
                    }}
                  />
                ),
              )}
            </div>

            <p className="mt-2 text-xs text-zinc-600">
              {userState?.rating
                ? `${userState.rating.toFixed(
                    1,
                  )} / 5`
                : language === "en" ? "You have not rated this game yet" : "Hələ reytinq verməmisən"}
            </p>

            <button
              type="button"
              disabled={
                isPending ||
                myReviewQuery.isLoading
              }
              onClick={() => {
                setIsLogModalOpen(true);
              }}
              className="mt-4 inline-flex w-full items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 py-3 text-sm font-bold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {myReviewQuery.isLoading ? (
                <LoaderCircle
                  size={17}
                  className="animate-spin"
                />
              ) : (
                <PenLine size={17} />
              )}
              {myReviewQuery.isLoading
                ? language === "en" ? "Checking review..." : "Rəy yoxlanılır..."
                : myReviewQuery.data
                  ? language === "en" ? "Edit review" : "Rəyi redaktə et"
                  : language === "en" ? "Add review" : "Rəy əlavə et"}
            </button>
          </div>
        </>
      )}

      {(actionError ||
        stateQuery.isError) && (
        <p className="mt-4 rounded-lg border border-red-400/20 bg-red-500/10 px-3 py-2 text-xs leading-5 text-red-200">
          {actionError ??
            getApiErrorMessage(
              stateQuery.error,
              "İstifadəçi vəziyyəti yüklənmədi.",
            )}
        </p>
      )}
      </div>

      <LogGameModal
        open={isLogModalOpen}
        game={game}
        onClose={() => {
          setIsLogModalOpen(false);
        }}
      />
    </>
  );
}

function RelatedGameCard({
  game,
}: {
  game: GameListItemDTO;
}) {
  const accessToken = useAuthStore(
    (state) => state.accessToken,
  );

  const canViewSources =
    canViewCatalogSources(accessToken);

  const coverUrl = resolveAssetUrl(
    game.coverImageUrl,
  );

  const [
    failedCoverUrl,
    setFailedCoverUrl,
  ] = useState<string | null>(null);

  const shouldShowCover =
    coverUrl !== null &&
    failedCoverUrl !== coverUrl;

  return (
    <Link
      to={`/games/${game.source}/${game.id}`}
      className="group block min-w-0"
    >
      <article className="overflow-hidden rounded-xl border border-white/[0.09] bg-[#111218] transition duration-300 hover:-translate-y-1 hover:border-violet-400/40 hover:shadow-2xl hover:shadow-violet-950/20">
        <div className="relative aspect-[3/4] overflow-hidden bg-gradient-to-br from-zinc-800 to-zinc-950">
          {shouldShowCover ? (
            <img
              src={coverUrl}
              alt={`${game.title} oyun örtüyü`}
              loading="lazy"
              className="h-full w-full object-cover transition duration-500 group-hover:scale-105"
              onError={() => {
                setFailedCoverUrl(
                  coverUrl,
                );
              }}
            />
          ) : (
            <div className="flex h-full flex-col items-center justify-center gap-3 px-5 text-center text-zinc-600">
              <Gamepad2 size={32} />

              <span className="line-clamp-3 text-sm font-medium">
                {game.title}
              </span>
            </div>
          )}

          <div className="pointer-events-none absolute inset-0 bg-gradient-to-t from-[#09090d] via-transparent to-transparent" />
        </div>

        <div className="p-3.5">
          <h3
            className="line-clamp-2 min-h-11 text-sm font-semibold leading-5 text-zinc-100 transition group-hover:text-violet-200"
            title={game.title}
          >
            {game.title}
          </h3>

          {canViewSources && (
            <p className="mt-2 text-xs text-zinc-600">
              {game.source === "Local"
                ? "WellPlayd"
                : "IGDB"}
            </p>
          )}
        </div>
      </article>
    </Link>
  );
}

function GameDetailContent({
  game,
}: {
  game: GameDetailDTO;
}) {
  const { language } = useI18n();
  const relatedSections = useMemo(() => getRelatedSections(language), [language]);
  const accessToken = useAuthStore(
    (state) => state.accessToken,
  );

  const canViewSources =
    canViewCatalogSources(accessToken);

  const coverUrl = resolveAssetUrl(
    game.coverImageUrl,
  );

  const [
    failedCoverUrl,
    setFailedCoverUrl,
  ] = useState<string | null>(null);

  const shouldShowCover =
    coverUrl !== null &&
    failedCoverUrl !== coverUrl;

  const isComingSoon =
    isFutureReleaseDate(
      game.releaseDate,
    );

  const developers = game.companies.filter(
    (company) =>
      company.role === "Developer",
  );

  const publishers = game.companies.filter(
    (company) =>
      company.role === "Publisher",
  );

  const relatedTabs =
    useMemo<RelatedTab[]>(() => {
      const tabs: RelatedTab[] = [];

      if (game.relatedGames.parentGame) {
        tabs.push({
          key: "parentGame",
          title: language === "en" ? "Main game" : "Əsas oyun",
          games: [
            game.relatedGames.parentGame,
          ],
        });
      }

      for (
        const section of relatedSections
      ) {
        const games =
          game.relatedGames[section.key] ?? [];

        if (games.length > 0) {
          tabs.push({
            key: section.key,
            title: section.shortTitle,
            games,
          });
        }
      }

      return tabs;
    }, [game.relatedGames, language, relatedSections]);

  const [
    activeRelatedTab,
    setActiveRelatedTab,
  ] = useState("");

  const effectiveRelatedTab =
    relatedTabs.some(
      (tab) =>
        tab.key === activeRelatedTab,
    )
      ? activeRelatedTab
      : (relatedTabs[0]?.key ?? "");

  const selectedRelatedTab =
    relatedTabs.find(
      (tab) =>
        tab.key === effectiveRelatedTab,
    ) ?? relatedTabs[0];

  const relatedGameCount =
    relatedTabs.reduce(
      (total, tab) =>
        total + tab.games.length,
      0,
    );

  const companyByline =
    developers.length > 0
      ? developers
      : publishers;

  return (
    <>
      <section className="relative isolate border-b border-white/[0.08] bg-[#0a0a0e]">
        <div className="pointer-events-none absolute inset-0 overflow-hidden">
          {coverUrl && (
            <div
              className="absolute -inset-12 scale-110 bg-cover bg-center opacity-35 blur-xl"
              style={{
                backgroundImage: `url(${coverUrl})`,
              }}
            />
          )}

          <div className="absolute inset-0 bg-[linear-gradient(to_bottom,rgba(7,7,10,0.3),rgba(7,7,10,0.72)_45%,#0a0a0e_100%)]" />

          <div className="absolute inset-0 bg-[linear-gradient(to_right,#07070a_0%,rgba(7,7,10,0.45)_45%,rgba(7,7,10,0.82)_100%)]" />
        </div>

        <div className="relative mx-auto max-w-7xl px-5 pb-12 pt-8 lg:px-8 lg:pb-14 lg:pt-10">
          <Link
            to="/games"
            className="inline-flex items-center gap-2 rounded-full border border-white/10 bg-black/25 px-4 py-2 text-sm font-semibold text-zinc-300 backdrop-blur-md transition hover:border-violet-400/30 hover:text-white"
          >
            <ArrowLeft size={16} />
            Oyun kataloqu
          </Link>

          <div className="mt-12 grid items-center gap-8 lg:grid-cols-[280px_minmax(0,1fr)] lg:gap-12">
            <div className="mx-auto w-full max-w-[280px] lg:mx-0">
              <div className="relative aspect-[3/4] overflow-hidden rounded-2xl border border-white/15 bg-gradient-to-br from-zinc-800 to-zinc-950 shadow-2xl shadow-black/50">
                {isComingSoon && (
                  <span className="coming-soon-badge">
                    {language === "en" ? "Coming Soon" : "Tezliklə"}
                  </span>
                )}

                {shouldShowCover ? (
                  <img
                    src={coverUrl}
                    alt={`${game.title} oyun örtüyü`}
                    className="h-full w-full object-cover"
                    onError={() => {
                      setFailedCoverUrl(
                        coverUrl,
                      );
                    }}
                  />
                ) : (
                  <div className="flex h-full flex-col items-center justify-center gap-4 px-6 text-center text-zinc-600">
                    <Gamepad2 size={46} />

                    <span className="font-semibold">
                      {game.title}
                    </span>
                  </div>
                )}
              </div>
            </div>

            <div className="min-w-0 py-2 lg:py-6">
              <div className="flex flex-wrap items-center gap-2">
                <span className="rounded-full border border-violet-400/25 bg-violet-500/15 px-3 py-1.5 text-xs font-bold text-violet-100 backdrop-blur-sm">
                  {
                    gameTypeLabels[
                      game.gameType
                    ]
                  }
                </span>

                {canViewSources && (
                  <span className="rounded-full border border-white/10 bg-black/25 px-3 py-1.5 text-xs font-semibold text-zinc-300 backdrop-blur-sm">
                    {game.source === "Local"
                      ? "WellPlayd"
                      : "IGDB"}
                  </span>
                )}

                {canViewSources &&
                  game.isOverridden && (
                    <span className="rounded-full border border-fuchsia-400/20 bg-fuchsia-500/10 px-3 py-1.5 text-xs font-semibold text-fuchsia-200">
                      Redaktə edilib
                    </span>
                  )}
              </div>

              <div className="mt-5 flex flex-col gap-5 xl:flex-row xl:items-start xl:justify-between">
                <div className="min-w-0">
                  <h1 className="max-w-4xl text-4xl font-black tracking-[-0.045em] text-white sm:text-5xl lg:text-6xl">
                    {game.title}
                  </h1>

                  {companyByline.length >
                    0 && (
                    <p className="mt-4 text-base text-zinc-300 sm:text-lg">
                      {language === "en" ? (
                        <>
                          <span className="text-zinc-500">by </span>
                          {companyByline.map(
                            (
                              company,
                              index,
                            ) => (
                              <span
                                key={`${company.source}-${company.id}-${company.role}`}
                              >
                                {index > 0 &&
                                  ", "}

                                <Link
                                  to={buildCatalogFilterUrl(
                                    "company",
                                    company.source,
                                    company.id,
                                  )}
                                  state={buildCatalogFilterState(
                                    "company",
                                    company.source,
                                    company.id,
                                    company.name,
                                  )}
                                  className="font-semibold text-zinc-200 underline decoration-white/20 underline-offset-4 transition hover:text-violet-200 hover:decoration-violet-300"
                                >
                                  {
                                    company.name
                                  }
                                </Link>
                              </span>
                            ),
                          )}
                        </>
                      ) : (
                        <>
                          {companyByline.map(
                            (
                              company,
                              index,
                            ) => (
                              <span
                                key={`${company.source}-${company.id}-${company.role}`}
                              >
                                {index > 0 &&
                                  ", "}

                                <Link
                                  to={buildCatalogFilterUrl(
                                    "company",
                                    company.source,
                                    company.id,
                                  )}
                                  state={buildCatalogFilterState(
                                    "company",
                                    company.source,
                                    company.id,
                                    company.name,
                                  )}
                                  className="font-semibold text-zinc-200 underline decoration-white/20 underline-offset-4 transition hover:text-violet-200 hover:decoration-violet-300"
                                >
                                  {
                                    company.name
                                  }
                                </Link>
                              </span>
                            ),
                          )}{" "}
                          <span className="text-zinc-500">
                            tərəfindən
                          </span>
                        </>
                      )}
                    </p>
                  )}
                </div>

                <div className="inline-flex w-fit shrink-0 items-center gap-3 rounded-xl border border-white/10 bg-[#1a1b22]/85 px-4 py-3 text-sm backdrop-blur-md">
                  <span className="font-bold text-zinc-100">
                    {isComingSoon
                      ? language === "en" ? "Coming Soon" : "Tezliklə"
                      : language === "en" ? "Release date" : "Çıxış tarixi"}
                  </span>

                  <span className="text-zinc-400">
                    {formatReleaseDate(
                      game.releaseDate,
                    )}
                  </span>
                </div>
              </div>

              <p className="mt-6 max-w-4xl whitespace-pre-line text-base leading-8 text-zinc-300 sm:text-lg sm:leading-8">
                {game.description ??
                  "Bu oyun üçün açıqlama əlavə edilməyib."}
              </p>
            </div>
          </div>
        </div>
      </section>

      <section className="border-b border-white/[0.07] bg-[#0d0e12]">
        <div className="mx-auto grid max-w-7xl gap-8 px-5 pb-10 pt-10 lg:grid-cols-[280px_minmax(0,1fr)] lg:px-8 lg:pb-12 lg:pt-12">
          <aside className="space-y-4">
            <div className="rounded-2xl border border-white/[0.08] bg-[#15161c] p-5">
              <div className="flex items-center gap-2 text-sm font-semibold text-zinc-200">
                <Database
                  size={17}
                  className="text-violet-300"
                />
                Kataloq məlumatı
              </div>

              <dl className="mt-5 space-y-4 text-sm">
                {canViewSources && (
                  <div className="flex items-center justify-between gap-4">
                    <dt className="text-zinc-500">
                      Mənbə
                    </dt>

                    <dd className="font-semibold text-zinc-200">
                      {game.source ===
                      "Local"
                        ? "WellPlayd"
                        : "IGDB"}
                    </dd>
                  </div>
                )}

                <div className="flex items-center justify-between gap-4">
                  <dt className="text-zinc-500">
                    Növ
                  </dt>

                  <dd className="text-right font-semibold text-zinc-200">
                    {
                      gameTypeLabels[
                        game.gameType
                      ]
                    }
                  </dd>
                </div>

                <div className="flex items-center justify-between gap-4">
                  <dt className="text-zinc-500">
                    {language === "en" ? "Related" : "Əlaqəli kontent"}
                  </dt>

                  <dd className="font-semibold text-zinc-200">
                    {relatedGameCount}
                  </dd>
                </div>
              </dl>
            </div>

            <GameTrackingPanel
              game={game}
            />

            <FpsEstimatorAction
              gameId={game.id}
              gameName={game.title}
              gameReleaseYear={
                game.releaseDate
                  ? new Date(
                      game.releaseDate,
                    ).getUTCFullYear()
                  : undefined
              }
            />

            <FriendWishlistNotice
              source={game.source}
              gameId={game.id}
            />
          </aside>

          <div className="min-w-0">
            <div className="space-y-6 border-b border-white/[0.08] pb-8">
              <div className="grid gap-3 sm:grid-cols-[150px_minmax(0,1fr)] sm:items-start">
                <h2 className="text-sm font-bold uppercase tracking-wider text-zinc-500">
                  Janrlar
                </h2>

                <div className="flex flex-wrap gap-x-3 gap-y-2">
                  {game.genres.length > 0 ? (
                    game.genres.map(
                      (genre) => (
                        <Link
                          key={`${genre.source}-${genre.id}`}
                          to={buildCatalogFilterUrl(
                            "genre",
                            genre.source,
                            genre.id,
                          )}
                          className="border-b border-zinc-500 pb-1 text-sm font-medium text-zinc-300 transition hover:border-violet-300 hover:text-violet-200"
                        >
                          {genre.name}
                        </Link>
                      ),
                    )
                  ) : (
                    <span className="text-sm text-zinc-600">
                      Janr qeyd edilməyib.
                    </span>
                  )}
                </div>
              </div>

              <div className="grid gap-3 sm:grid-cols-[150px_minmax(0,1fr)] sm:items-start">
                <h2 className="text-sm font-bold uppercase tracking-wider text-zinc-500">
                  Platformalar
                </h2>

                <div className="flex flex-wrap gap-2">
                  {game.platforms.length >
                  0 ? (
                    game.platforms.map(
                      (platform) => {
                        const PlatformIcon =
                          getPlatformIcon(
                            platform.name,
                          );

                        return (
                          <Link
                            key={`${platform.source}-${platform.id}`}
                            to={buildCatalogFilterUrl(
                              "platform",
                              platform.source,
                              platform.id,
                            )}
                            className="inline-flex items-center gap-2 rounded-lg border border-white/15 bg-white/[0.04] px-3 py-2 text-sm font-semibold text-zinc-300 transition hover:border-violet-400/40 hover:bg-violet-500/10 hover:text-violet-100"
                          >
                            <PlatformIcon
                              size={15}
                            />

                            {
                              platform.name
                            }
                          </Link>
                        );
                      },
                    )
                  ) : (
                    <span className="text-sm text-zinc-600">
                      Platforma qeyd
                      edilməyib.
                    </span>
                  )}
                </div>
              </div>

              <div className="grid gap-3 sm:grid-cols-[150px_minmax(0,1fr)] sm:items-start">
                <h2 className="text-sm font-bold uppercase tracking-wider text-zinc-500">
                  Şirkətlər
                </h2>

                <div className="flex flex-wrap gap-2">
                  {game.companies.length >
                  0 ? (
                    game.companies.map(
                      (company) => (
                        <Link
                          key={`${company.source}-${company.id}-${company.role}`}
                          to={buildCatalogFilterUrl(
                            "company",
                            company.source,
                            company.id,
                          )}
                          state={buildCatalogFilterState(
                            "company",
                            company.source,
                            company.id,
                            company.name,
                          )}
                          className="inline-flex items-center gap-2 rounded-lg bg-white/[0.04] px-3 py-2 text-sm text-zinc-300 transition hover:bg-white/[0.08] hover:text-white"
                        >
                          <Building2
                            size={15}
                          />

                          <span>
                            {company.name}
                          </span>

                          <span className="text-xs text-zinc-600">
                            {company.role ===
                            "Developer"
                              ? "Developer"
                              : "Publisher"}
                          </span>
                        </Link>
                      ),
                    )
                  ) : (
                    <span className="text-sm text-zinc-600">
                      Şirkət qeyd
                      edilməyib.
                    </span>
                  )}
                </div>
              </div>
            </div>

            <GameCommunityStats
              source={game.source}
              gameId={game.id}
              stats={game.communityStats}
            />
          </div>
        </div>
      </section>

      <section className="bg-[#0a0a0e]">
        <div className="mx-auto max-w-7xl px-5 py-14 lg:px-8 lg:py-18">
          <div className="flex flex-col gap-4 border-b border-white/[0.09] md:flex-row md:items-end md:justify-between">
            <div>
              <div className="flex items-center gap-2">
                <Layers3
                  size={20}
                  className="text-violet-300"
                />

                <h2 className="text-2xl font-black tracking-tight text-white">
                  {language === "en" ? "Related" : "Əlaqəli kontent"}
                </h2>
              </div>

              <p className="mt-2 text-sm text-zinc-600">
                {language === "en"
                  ? "Sequels, DLC, expansions, bundles, remakes, and other related releases."
                  : "Davam oyunları, DLC, expansion, bundle, remake və digər əlaqəli buraxılışlar."}
              </p>
            </div>

            {relatedTabs.length > 0 && (
              <span className="pb-4 text-sm font-semibold text-zinc-500">
                {relatedGameCount} {language === "en" ? "results" : "nəticə"}
              </span>
            )}
          </div>

          {relatedTabs.length > 0 ? (
            <>
              <div className="flex gap-7 overflow-x-auto border-b border-white/[0.08] pt-6">
                {relatedTabs.map((tab) => {
                  const isActive =
                    tab.key ===
                    effectiveRelatedTab;

                  return (
                    <button
                      key={tab.key}
                      type="button"
                      onClick={() => {
                        setActiveRelatedTab(
                          tab.key,
                        );
                      }}
                      className={`relative shrink-0 pb-4 text-sm font-bold transition ${
                        isActive
                          ? "text-white"
                          : "text-zinc-500 hover:text-zinc-200"
                      }`}
                    >
                      {tab.title}

                      <span className="ml-2 rounded-full bg-white/[0.06] px-2 py-0.5 text-[11px] text-zinc-400">
                        {tab.games.length}
                      </span>

                      {isActive && (
                        <span className="absolute inset-x-0 bottom-0 h-0.5 rounded-full bg-violet-400" />
                      )}
                    </button>
                  );
                })}
              </div>

              {selectedRelatedTab && (
                <div className="mt-7 grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6">
                  {selectedRelatedTab.games.map(
                    (relatedGame) => (
                      <RelatedGameCard
                        key={`${relatedGame.source}-${relatedGame.id}`}
                        game={
                          relatedGame
                        }
                      />
                    ),
                  )}
                </div>
              )}
            </>
          ) : (
            <div className="mt-8 rounded-2xl border border-dashed border-white/10 bg-white/[0.02] p-10 text-center">
              <Layers3
                size={32}
                className="mx-auto text-zinc-700"
              />

              <p className="mt-4 font-bold text-zinc-300">
                {language === "en" ? "No related content" : "Əlaqəli məzmun yoxdur"}
              </p>

              <p className="mt-2 text-sm text-zinc-600">
                {language === "en"
                  ? "No sequel, DLC, expansion, bundle, or alternate version was found for this game."
                  : "Bu oyun üçün davam oyunu, DLC, expansion, bundle və ya başqa versiya tapılmadı."}
              </p>
            </div>
          )}
        </div>
      </section>

      <GameReviewsSection game={game} />
    </>
  );
}

export function GameDetailPage() {
  const location = useLocation();

  const {
    source: sourceValue,
    id: idValue,
  } = useParams();

  const source =
    parseRouteSource(sourceValue);

  const id = Number(idValue);

  const isValidRoute =
    source !== null &&
    Number.isSafeInteger(id) &&
    id > 0;

  useLayoutEffect(() => {
    if (window.location.hash) {
      window.history.replaceState(
        window.history.state,
        "",
        `${window.location.pathname}${window.location.search}`,
      );
    }

    window.scrollTo({
      top: 0,
      left: 0,
      behavior: "auto",
    });

    document.documentElement.scrollTop = 0;
    document.body.scrollTop = 0;
  }, [location.key, sourceValue, idValue]);

  const gameQuery = useQuery({
    queryKey: [
      "game-detail",
      source,
      id,
    ],
    queryFn: ({ signal }) =>
      getGameById(source!, id, signal),
    enabled: isValidRoute,
  });

  const errorMessage = gameQuery.isError
    ? getApiErrorMessage(
        gameQuery.error,
        "Oyun məlumatlarını yükləmək mümkün olmadı.",
      )
    : null;

  return (
    <div className="min-h-screen wellplayd-page-bg text-zinc-100">
      <Navbar />

      <main>
        {!isValidRoute ? (
          <div className="mx-auto max-w-3xl px-5 py-24 text-center">
            <Gamepad2
              size={36}
              className="mx-auto text-zinc-600"
            />

            <h1 className="mt-5 text-2xl font-bold text-white">
              Oyun ünvanı yanlışdır
            </h1>

            <Link
              to="/games"
              className="mt-6 inline-flex items-center gap-2 text-sm font-semibold text-violet-300 transition hover:text-violet-200"
            >
              <ArrowLeft size={17} />
              Kataloqa qayıt
            </Link>
          </div>
        ) : gameQuery.isLoading ? (
          <div className="animate-pulse">
            <div className="min-h-[540px] border-b border-white/[0.06] bg-white/[0.025]">
              <div className="mx-auto max-w-7xl px-5 pb-10 pt-10 lg:px-8">
                <div className="h-10 w-40 rounded-full bg-white/[0.05]" />

                <div className="mt-16 grid items-end gap-8 lg:grid-cols-[260px_minmax(0,1fr)]">
                  <div className="aspect-[3/4] rounded-2xl bg-white/[0.06]" />

                  <div className="space-y-5 pb-8">
                    <div className="h-7 w-36 rounded-full bg-white/[0.05]" />

                    <div className="h-14 max-w-3xl rounded bg-white/[0.07]" />

                    <div className="h-6 max-w-lg rounded bg-white/[0.05]" />

                    <div className="h-28 max-w-4xl rounded bg-white/[0.04]" />
                  </div>
                </div>
              </div>
            </div>

            <div className="mx-auto grid max-w-7xl gap-8 px-5 py-12 lg:grid-cols-[260px_minmax(0,1fr)] lg:px-8 lg:pt-24">
              <div className="h-48 rounded-2xl bg-white/[0.04]" />

              <div>
                <div className="h-32 rounded-2xl bg-white/[0.04]" />

                <div className="mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                  {Array.from({
                    length: 4,
                  }).map((_, index) => (
                    <div
                      key={index}
                      className="h-40 rounded-2xl bg-white/[0.04]"
                    />
                  ))}
                </div>
              </div>
            </div>
          </div>
        ) : errorMessage ? (
          <ApiErrorStatusPage
            error={gameQuery.error}
            entityName="Oyun"
          />
        ) : gameQuery.data ? (
          <GameDetailContent
            game={gameQuery.data}
          />
        ) : null}
      </main>

      <Footer />
    </div>
  );
}
