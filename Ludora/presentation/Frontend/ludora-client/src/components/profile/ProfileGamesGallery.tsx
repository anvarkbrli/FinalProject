import {
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Filter,
  Gamepad2,
  MessageSquareText,
  Search,
  SlidersHorizontal,
  Star,
  X,
} from "lucide-react";
import { Link } from "react-router-dom";
import { RatingStarIcon } from "../common/RatingStarIcon";
import type {
  ProfileGameEntryDTO,
  UserProfileGameSortBy,
} from "../../types/profile";
import { resolveAssetUrl } from "../../lib/helpers";

const statusLabels: Record<
  NonNullable<ProfileGameEntryDTO["status"]>,
  string
> = {
  Playing: "Oynayır",
  PlanToPlay: "Oynayacaq",
  Played: "Oynayıb",
  Dropped: "Yarıda buraxıb",
};

const sortOptions: {
  value: UserProfileGameSortBy;
  label: string;
}[] = [
  {
    value: "RecentlyUpdated",
    label: "Son aktivliyə görə",
  },
  {
    value: "RecentlyReleased",
    label: "Ən yeni çıxış tarixi",
  },
  {
    value: "OldestRelease",
    label: "Ən köhnə çıxış tarixi",
  },
  {
    value: "RatingHighToLow",
    label: "Reytinq yüksəkdən aşağı",
  },
  {
    value: "RatingLowToHigh",
    label: "Reytinq aşağıdan yuxarı",
  },
  {
    value: "NameAsc",
    label: "Ada görə A-Z",
  },
  {
    value: "NameDesc",
    label: "Ada görə Z-A",
  },
];

interface ProfileGamesGalleryProps {
  items: ProfileGameEntryDTO[];
  totalCount: number;
  pageNumber: number;
  totalPages: number;
  pageSize: number;
  isLoading: boolean;
  errorMessage: string | null;
  isFilterOpen: boolean;
  onToggleFilter: () => void;
  searchValue: string;
  onSearchChange: (value: string) => void;
  selectedGenre: string | null;
  selectedCompany: string | null;
  selectedPlatform: string | null;
  selectedPlayedPlatform: string | null;
  selectedStatus: ProfileGameEntryDTO["status"];
  selectedReleaseYear: number | null;
  selectedRating: number | null;
  sortBy: UserProfileGameSortBy;
  genreOptions: string[];
  companyOptions: string[];
  platformOptions: string[];
  playedPlatformOptions: string[];
  releaseYearOptions: number[];
  onGenreChange: (value: string | null) => void;
  onCompanyChange: (value: string | null) => void;
  onPlatformChange: (value: string | null) => void;
  onPlayedPlatformChange: (
    value: string | null,
  ) => void;
  onStatusChange: (
    value: ProfileGameEntryDTO["status"],
  ) => void;
  onReleaseYearChange: (value: number | null) => void;
  onRatingChange: (value: number | null) => void;
  onSortChange: (value: UserProfileGameSortBy) => void;
  onClearFilters: () => void;
  onPageChange: (page: number) => void;
  onOpenReview: (reviewId: number) => void;
}

interface FilterSelectProps {
  label: string;
  value: string;
  options: Array<{
    value: string;
    label: string;
  }>;
  onChange: (value: string) => void;
}

function GameRatingStars({ rating }: { rating: number }) {
  return (
    <div
      className="inline-flex items-center gap-1"
      aria-label={`${rating.toFixed(1)} ulduz`}
      title={`${rating.toFixed(1)} ulduz`}
    >
      {[1, 2, 3, 4, 5].map((position) => {
        const fillPercent = Math.max(
          0,
          Math.min(100, (rating - (position - 1)) * 100),
        );

        return (
          <RatingStarIcon
            key={position}
            fillPercent={fillPercent}
            size={14}
          />
        );
      })}
    </div>
  );
}

function FilterSelect({
  label,
  value,
  options,
  onChange,
}: FilterSelectProps) {
  return (
    <label className="block">
      <span className="mb-2 block text-[11px] font-bold uppercase tracking-[0.16em] text-zinc-600">
        {label}
      </span>
      <select
        value={value}
        onChange={(event) => {
          onChange(event.target.value);
        }}
        className="w-full rounded-2xl border border-white/[0.08] bg-white/[0.035] px-4 py-3 text-sm text-zinc-200 outline-none transition focus:border-violet-400/25"
      >
        {options.map((option) => (
          <option
            key={option.value}
            value={option.value}
            className="bg-[#12121a]"
          >
            {option.label}
          </option>
        ))}
      </select>
    </label>
  );
}

export function ProfileGamesGallery({
  items,
  totalCount,
  pageNumber,
  totalPages,
  pageSize,
  isLoading,
  errorMessage,
  isFilterOpen,
  onToggleFilter,
  searchValue,
  onSearchChange,
  selectedGenre,
  selectedCompany,
  selectedPlatform,
  selectedPlayedPlatform,
  selectedStatus,
  selectedReleaseYear,
  selectedRating,
  sortBy,
  genreOptions,
  companyOptions,
  platformOptions,
  playedPlatformOptions,
  releaseYearOptions,
  onGenreChange,
  onCompanyChange,
  onPlatformChange,
  onPlayedPlatformChange,
  onStatusChange,
  onReleaseYearChange,
  onRatingChange,
  onSortChange,
  onClearFilters,
  onPageChange,
  onOpenReview,
}: ProfileGamesGalleryProps) {
  const hasFilters =
    Boolean(searchValue.trim()) ||
    Boolean(selectedGenre) ||
    Boolean(selectedCompany) ||
    Boolean(selectedPlatform) ||
    Boolean(selectedPlayedPlatform) ||
    Boolean(selectedStatus) ||
    Boolean(selectedReleaseYear) ||
    selectedRating !== null;

  return (
    <div className="mx-auto max-w-6xl space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={onToggleFilter}
            className={`inline-flex h-11 items-center gap-2 rounded-xl border px-4 text-sm font-bold transition ${
              isFilterOpen
                ? "border-violet-400/30 bg-violet-500/15 text-violet-200"
                : "border-white/[0.08] bg-[#101015] text-zinc-300 hover:border-white/[0.14] hover:text-white"
            }`}
          >
            <SlidersHorizontal size={17} />
            Filtr
            <ChevronDown
              size={16}
              className={`transition-transform ${
                isFilterOpen ? "rotate-180" : ""
              }`}
            />
          </button>

          {selectedRating !== null && (
            <div className="inline-flex h-11 items-center gap-2 rounded-xl border border-amber-300/15 bg-amber-500/[0.08] px-3 text-sm font-bold text-amber-300">
              <Star size={14} fill="currentColor" />
              {selectedRating.toFixed(1)} ulduz
              <button
                type="button"
                onClick={() => {
                  onRatingChange(null);
                }}
                className="ml-1 flex h-6 w-6 items-center justify-center rounded-md text-amber-200/65 transition hover:bg-amber-400/10 hover:text-amber-100"
              >
                <X size={13} />
              </button>
            </div>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <label className="flex h-11 min-w-[13rem] items-center gap-2 rounded-xl border border-white/[0.08] bg-[#101015] px-3 text-zinc-400 focus-within:border-violet-400/25">
            <Search size={16} />
            <input
              value={searchValue}
              onChange={(event) => {
                onSearchChange(event.target.value);
              }}
              placeholder="Oyun axtar..."
              className="w-full bg-transparent text-sm text-zinc-200 outline-none placeholder:text-zinc-600"
            />
          </label>

          <select
            value={sortBy}
            onChange={(event) => {
              onSortChange(
                event.target.value as UserProfileGameSortBy,
              );
            }}
            className="h-11 rounded-xl border border-white/[0.08] bg-[#101015] px-4 text-sm text-zinc-200 outline-none transition focus:border-violet-400/25"
          >
            {sortOptions.map((option) => (
              <option
                key={option.value}
                value={option.value}
                className="bg-[#12121a]"
              >
                {option.label}
              </option>
            ))}
          </select>

          <span className="rounded-full border border-white/[0.07] bg-white/[0.035] px-3 py-1.5 text-xs font-semibold text-zinc-400">
            {totalCount} oyun
          </span>
        </div>
      </div>

      <div
        className={`grid items-start gap-5 transition-[grid-template-columns] duration-300 ease-out ${
          isFilterOpen
            ? "xl:grid-cols-[20rem_minmax(0,1fr)]"
            : "xl:grid-cols-[0px_minmax(0,1fr)]"
        }`}
      >
        <aside
          className={`overflow-hidden transition-[max-height,opacity,transform] duration-300 ease-out ${
            isFilterOpen
              ? "max-h-[70rem] translate-y-0 opacity-100 xl:translate-x-0"
              : "max-h-0 -translate-y-2 opacity-0 xl:max-h-[70rem] xl:-translate-x-3"
          }`}
        >
          <div className="rounded-3xl border border-white/[0.07] bg-[#101015] p-4 sm:p-5">
            <div className="flex items-start justify-between gap-3">
              <div>
                <p className="text-sm font-black text-white">
                  Oyun filtrləri
                </p>
                <p className="mt-1 text-xs leading-5 text-zinc-600">
                  Status, buraxılış ili, platforma və reytinqə görə kolleksiyanı daralt.
                </p>
              </div>
              {hasFilters && (
                <button
                  type="button"
                  onClick={onClearFilters}
                  className="inline-flex items-center gap-1 rounded-xl border border-white/[0.08] px-3 py-2 text-xs font-bold text-zinc-300 transition hover:border-violet-400/20 hover:text-white"
                >
                  <Filter size={14} />
                  Təmizlə
                </button>
              )}
            </div>

            <div className="mt-5 space-y-4">
              <FilterSelect
                label="Status"
                value={selectedStatus ?? ""}
                options={[
                  { value: "", label: "Hamısı" },
                  { value: "Playing", label: "Oynayır" },
                  { value: "PlanToPlay", label: "Oynayacaq" },
                  { value: "Played", label: "Oynayıb" },
                  { value: "Dropped", label: "Yarıda buraxıb" },
                ]}
                onChange={(value) => {
                  onStatusChange(
                    value
                      ? (value as ProfileGameEntryDTO["status"])
                      : null,
                  );
                }}
              />

              <FilterSelect
                label="Janr"
                value={selectedGenre ?? ""}
                options={[
                  { value: "", label: "Hamısı" },
                  ...genreOptions.map((item) => ({
                    value: item,
                    label: item,
                  })),
                ]}
                onChange={(value) => {
                  onGenreChange(value || null);
                }}
              />

              <FilterSelect
                label="Şirkət"
                value={selectedCompany ?? ""}
                options={[
                  { value: "", label: "Hamısı" },
                  ...companyOptions.map((item) => ({
                    value: item,
                    label: item,
                  })),
                ]}
                onChange={(value) => {
                  onCompanyChange(value || null);
                }}
              />

              <FilterSelect
                label="Platforma"
                value={selectedPlatform ?? ""}
                options={[
                  { value: "", label: "Hamısı" },
                  ...platformOptions.map((item) => ({
                    value: item,
                    label: item,
                  })),
                ]}
                onChange={(value) => {
                  onPlatformChange(value || null);
                }}
              />

              <FilterSelect
                label="Oynanılan platforma"
                value={selectedPlayedPlatform ?? ""}
                options={[
                  { value: "", label: "Hamısı" },
                  ...playedPlatformOptions.map((item) => ({
                    value: item,
                    label: item,
                  })),
                ]}
                onChange={(value) => {
                  onPlayedPlatformChange(value || null);
                }}
              />

              <FilterSelect
                label="Çıxış ili"
                value={selectedReleaseYear?.toString() ?? ""}
                options={[
                  { value: "", label: "Hamısı" },
                  ...releaseYearOptions.map((item) => ({
                    value: item.toString(),
                    label: item.toString(),
                  })),
                ]}
                onChange={(value) => {
                  onReleaseYearChange(
                    value ? Number(value) : null,
                  );
                }}
              />
            </div>

            <div className="mt-5 border-t border-white/[0.06] pt-5">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <p className="text-xs font-bold uppercase tracking-[0.12em] text-zinc-600">
                    Ulduz seçimi
                  </p>
                  <p className="mt-1 text-[11px] leading-5 text-zinc-600">
                    Yalnız seçdiyin reytinqdə oyunları göstər.
                  </p>
                </div>
                <span className="inline-flex shrink-0 items-center rounded-xl border border-amber-300/15 bg-amber-500/[0.08] px-3 py-2 text-sm font-black text-amber-300">
                  {(selectedRating ?? 2.5).toFixed(1)}
                </span>
              </div>

              <div className="mt-5">
                <input
                  type="range"
                  min="0.5"
                  max="5"
                  step="0.5"
                  value={selectedRating ?? 2.5}
                  onChange={(event) => {
                    onRatingChange(Number(event.target.value));
                  }}
                  className="block h-5 w-full cursor-pointer accent-violet-500"
                />
                <div className="mx-2 mt-3 flex h-4 items-start justify-between text-[10px] font-semibold text-zinc-700">
                  <span>0.5</span>
                  <span>5</span>
                </div>
                {selectedRating !== null && (
                  <button
                    type="button"
                    onClick={() => {
                      onRatingChange(null);
                    }}
                    className="mt-4 text-xs font-semibold text-violet-300 transition hover:text-violet-200"
                  >
                    Ulduz filtrini sıfırla
                  </button>
                )}
              </div>
            </div>
          </div>
        </aside>

        <div className="min-w-0">
          {isLoading ? (
            <div className="rounded-3xl border border-white/[0.07] bg-[#101015] px-6 py-16 text-center text-sm text-zinc-500">
              Oyunlar yüklənir...
            </div>
          ) : errorMessage ? (
            <div className="rounded-3xl border border-amber-400/15 bg-amber-500/[0.05] p-8 text-center text-sm text-amber-100/70">
              {errorMessage}
            </div>
          ) : items.length === 0 ? (
            <div className="rounded-3xl border border-dashed border-white/[0.08] p-12 text-center">
              <Gamepad2 size={31} className="mx-auto text-zinc-700" />
              <p className="mt-4 text-sm text-zinc-500">
                Bu filtrə uyğun oyun tapılmadı.
              </p>
            </div>
          ) : (
            <>
              <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5">
                {items.map((game) => {
                  const coverUrl = resolveAssetUrl(
                    game.coverUrl,
                  );

                  return (
                    <div key={`${game.source}-${game.gameId}`}>
                      <div className="group relative overflow-hidden rounded-[1.75rem] border border-white/[0.08] bg-[#111118] shadow-[0_18px_45px_rgba(6,6,18,0.34)]">
                        <Link
                          to={`/games/${game.source}/${game.gameId}`}
                          className="relative block aspect-[0.72] overflow-hidden"
                        >
                          {coverUrl ? (
                            <img
                              src={coverUrl}
                              alt={`${game.name} örtüyü`}
                              className="h-full w-full object-cover transition duration-300 group-hover:scale-[1.04] group-hover:blur-[2px]"
                            />
                          ) : (
                            <div className="flex h-full items-center justify-center bg-gradient-to-br from-zinc-900 to-zinc-950 text-zinc-700">
                              <Gamepad2 size={28} />
                            </div>
                          )}

                          <div className="absolute inset-0 bg-gradient-to-t from-[#090910]/90 via-[#090910]/10 to-transparent opacity-0 transition group-hover:opacity-100" />
                          <div className="absolute inset-x-0 bottom-0 translate-y-6 p-4 opacity-0 transition duration-300 group-hover:translate-y-0 group-hover:opacity-100">
                            <p className="line-clamp-2 text-base font-bold text-white drop-shadow-[0_2px_8px_rgba(0,0,0,0.6)]">
                              {game.name}
                            </p>
                          </div>
                        </Link>

                        <div className="space-y-3 px-3.5 py-3.5">
                          <div className="flex items-center justify-between gap-2">
                            <div className="flex min-w-0 flex-1 items-center gap-1.5 text-[11px] text-zinc-500">
                              {game.status && (
                                <span className="truncate rounded-full border border-violet-400/15 bg-violet-500/[0.08] px-2.5 py-1 font-semibold text-violet-200">
                                  {statusLabels[game.status]}
                                </span>
                              )}
                            </div>

                            {game.reviewId !== null && (
                              <button
                                type="button"
                                onClick={() => {
                                  onOpenReview(game.reviewId!);
                                }}
                                className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border border-white/[0.08] bg-white/[0.03] text-zinc-300 transition hover:border-violet-400/20 hover:bg-violet-500/[0.08] hover:text-violet-200"
                                title="Rəyə keç"
                              >
                                <MessageSquareText size={15} />
                              </button>
                            )}
                          </div>

                          <div className="flex min-h-5 items-center text-sm">
                            {game.rating !== null ? (
                              <GameRatingStars rating={game.rating} />
                            ) : (
                              <span className="text-xs text-zinc-600">
                                Ulduz verilməyib
                              </span>
                            )}
                          </div>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>

              {totalPages > 1 && (
                <div className="mt-6 flex flex-wrap items-center justify-between gap-3 rounded-3xl border border-white/[0.07] bg-[#101015] px-4 py-4">
                  <p className="text-sm text-zinc-500">
                    {(pageNumber - 1) * pageSize + 1} -{" "}
                    {Math.min(pageNumber * pageSize, totalCount)}
                    / {totalCount}
                  </p>

                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      disabled={pageNumber <= 1}
                      onClick={() => {
                        onPageChange(pageNumber - 1);
                      }}
                      className="inline-flex h-10 w-10 items-center justify-center rounded-xl border border-white/[0.08] bg-white/[0.03] text-zinc-300 transition hover:border-white/[0.14] hover:text-white disabled:cursor-not-allowed disabled:opacity-40"
                    >
                      <ChevronLeft size={17} />
                    </button>
                    <span className="rounded-xl border border-white/[0.08] bg-white/[0.03] px-4 py-2 text-sm font-semibold text-white">
                      {pageNumber} / {totalPages}
                    </span>
                    <button
                      type="button"
                      disabled={pageNumber >= totalPages}
                      onClick={() => {
                        onPageChange(pageNumber + 1);
                      }}
                      className="inline-flex h-10 w-10 items-center justify-center rounded-xl border border-white/[0.08] bg-white/[0.03] text-zinc-300 transition hover:border-white/[0.14] hover:text-white disabled:cursor-not-allowed disabled:opacity-40"
                    >
                      <ChevronRight size={17} />
                    </button>
                  </div>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
