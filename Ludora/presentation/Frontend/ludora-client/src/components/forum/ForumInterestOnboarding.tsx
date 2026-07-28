import { useQuery } from "@tanstack/react-query";
import {
  ArrowLeft,
  ArrowRight,
  BookOpenText,
  Check,
  ChevronDown,
  ChevronUp,
  Coffee,
  Gamepad2,
  Handshake,
  LoaderCircle,
  Search,
  Sparkles,
  Trophy,
  UserRound,
  UsersRound,
  X,
} from "lucide-react";
import {
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { resolveAssetUrl } from "../../lib/helpers";
import {
  getGames,
  getIgdbGameGenres,
} from "../../services/gamesService";
import type { GameCatalogItemDTO } from "../../types/games";

interface ForumInterestOnboardingProps {
  open: boolean;
  userId: string;
  onComplete: () => void;
  onDismiss: () => void;
}

interface SuggestedGame {
  key: string;
  title: string;
  subtitle: string;
  colors: string;
  coverImageUrl?: string;
  result?: GameCatalogItemDTO;
}

const playStyles = [
  {
    id: "singleplayer",
    title: "Tək oyunçu",
    description: "Öz tempimdə, təkbaşına",
    icon: UserRound,
  },
  {
    id: "multiplayer",
    title: "Çox oyunçu",
    description: "Dostlarla və ya komanda ilə",
    icon: UsersRound,
  },
  {
    id: "competitive",
    title: "Rəqabətli",
    description: "Reytinq, turnir və inkişaf",
    icon: Trophy,
  },
  {
    id: "casual",
    title: "Rahat",
    description: "Stresssiz və gündəlik oyun",
    icon: Coffee,
  },
  {
    id: "story",
    title: "Hekayə yönümlü",
    description: "Yadda qalan dünya və personajlar",
    icon: BookOpenText,
  },
  {
    id: "coop",
    title: "Birgə oyun",
    description: "Birlikdə məqsədə çatmaq",
    icon: Handshake,
  },
];

const popularGames: SuggestedGame[] = [
  {
    key: "rdr2",
    title: "Red Dead Redemption 2",
    subtitle: "Açıq dünya · Macəra",
    colors: "from-red-950 via-red-700 to-amber-600",
    coverImageUrl:
      "https://images.igdb.com/igdb/image/upload/t_cover_big/co1q1f.jpg",
  },
  {
    key: "gta5",
    title: "Grand Theft Auto V",
    subtitle: "Açıq dünya · Aksiyon",
    colors: "from-emerald-950 via-emerald-700 to-lime-500",
    coverImageUrl:
      "https://images.igdb.com/igdb/image/upload/t_cover_big/co2lbd.jpg",
  },
  {
    key: "fortnite",
    title: "Fortnite",
    subtitle: "Battle Royale · Onlayn",
    colors: "from-violet-950 via-indigo-600 to-sky-500",
    coverImageUrl:
      "https://images.igdb.com/igdb/image/upload/t_cover_big/cocj20.jpg",
  },
  {
    key: "minecraft",
    title: "Minecraft",
    subtitle: "Sandbox · Yaradıcılıq",
    colors: "from-stone-900 via-green-700 to-lime-500",
    coverImageUrl:
      "https://images.igdb.com/igdb/image/upload/t_cover_big/co8fu7.jpg",
  },
  {
    key: "elden-ring",
    title: "Elden Ring",
    subtitle: "RPG · Açıq dünya",
    colors: "from-zinc-950 via-amber-900 to-yellow-600",
    coverImageUrl:
      "https://images.igdb.com/igdb/image/upload/t_cover_big/co4jni.jpg",
  },
  {
    key: "valorant",
    title: "VALORANT",
    subtitle: "Taktiki FPS · Rəqabətli",
    colors: "from-slate-950 via-rose-700 to-red-500",
    coverImageUrl:
      "https://images.igdb.com/igdb/image/upload/t_cover_big/cocbci.jpg",
  },
  {
    key: "witcher-3",
    title: "The Witcher 3",
    subtitle: "RPG · Hekayə",
    colors: "from-neutral-950 via-slate-700 to-red-800",
    coverImageUrl:
      "https://images.igdb.com/igdb/image/upload/t_cover_big/coaarl.jpg",
  },
  {
    key: "fc",
    title: "EA SPORTS FC",
    subtitle: "İdman · Çox oyunçu",
    colors: "from-blue-950 via-blue-600 to-cyan-400",
    coverImageUrl:
      "https://images.igdb.com/igdb/image/upload/t_cover_big/coa5wx.jpg",
  },
];

const initialVisibleGenreCount = 16;

const genreLabels: Record<string, string> = {
  Action: "Aksiyon",
  Adventure: "Macəra",
  Arcade: "Arkada",
  "Card & Board Game": "Kart və stolüstü",
  Fighting: "Döyüş",
  "Hack and slash/Beat 'em up": "Hack and slash",
  Indie: "Indie",
  MOBA: "MOBA",
  Music: "Musiqi",
  Pinball: "Pinbol",
  Platform: "Platformer",
  "Point-and-click": "Point-and-click",
  Puzzle: "Tapmaca",
  "Quiz/Trivia": "Viktorina",
  Racing: "Yarış",
  "Real Time Strategy (RTS)": "Real vaxt strategiyası",
  "Role-playing (RPG)": "RPG",
  Shooter: "Atıcı",
  Simulator: "Simulyasiya",
  Sport: "İdman",
  Strategy: "Strategiya",
  Tactical: "Taktiki",
  "Turn-based strategy (TBS)": "Növbəli strategiya",
  "Visual Novel": "Vizual roman",
};

const platforms = [
  {
    id: "pc",
    title: "PC",
    subtitle: "Steam, Epic Games və digərləri",
    mark: "PC",
  },
  {
    id: "playstation",
    title: "PlayStation",
    subtitle: "PS5 və PS4",
    mark: "PS",
  },
  {
    id: "xbox",
    title: "Xbox",
    subtitle: "Series X|S və Xbox One",
    mark: "XB",
  },
  {
    id: "nintendo",
    title: "Nintendo",
    subtitle: "Switch ailəsi",
    mark: "NS",
  },
  {
    id: "mobile",
    title: "Mobil",
    subtitle: "iOS və Android",
    mark: "MB",
  },
];

const stepMeta = [
  {
    eyebrow: "Oyun tərzin",
    title: "Necə oynamağı sevirsən?",
    description:
      "Sənə uyğun müzakirələri tanımaq üçün bir və ya bir neçə oyun tərzi seç.",
  },
  {
    eyebrow: "Sevimli oyunların",
    title: "Səni ən yaxşı hansı oyunlar tanıdır?",
    description:
      "Ən az 3 oyun seç. Siyahıda yoxdursa axtarışdan əlavə edə bilərsən.",
  },
  {
    eyebrow: "Sevdiyin janrlar",
    title: "Hansı dünyalara daha çox qayıdırsan?",
    description:
      "Bir neçə janr seçə bilərsən — lentini bunun əsasında formalaşdıracağıq.",
  },
  {
    eyebrow: "Əsas platforman",
    title: "Ən çox harada oynayırsan?",
    description:
      "Əsas platformanı seç ki, uyğun icmalar və platforma mövzuları önə çıxsın.",
  },
];

function toggleValue(values: string[], value: string) {
  return values.includes(value)
    ? values.filter((item) => item !== value)
    : [...values, value];
}

export function ForumInterestOnboarding({
  open,
  userId,
  onComplete,
  onDismiss,
}: ForumInterestOnboardingProps) {
  const [step, setStep] = useState(0);
  const [styles, setStyles] = useState<string[]>([]);
  const [selectedGames, setSelectedGames] = useState<
    SuggestedGame[]
  >([]);
  const [selectedGenres, setSelectedGenres] = useState<
    string[]
  >([]);
  const [platform, setPlatform] = useState("");
  const [searchValue, setSearchValue] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [genresExpanded, setGenresExpanded] = useState(false);
  const firstAdditionalGenreRef = useRef<HTMLButtonElement | null>(null);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setDebouncedSearch(searchValue.trim());
    }, 350);

    return () => window.clearTimeout(timeoutId);
  }, [searchValue]);

  useEffect(() => {
    if (!open) {
      return;
    }

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    return () => {
      document.body.style.overflow = previousOverflow;
    };
  }, [open]);

  useEffect(() => {
    if (!genresExpanded) return;

    const frameId = window.requestAnimationFrame(() => {
      firstAdditionalGenreRef.current?.scrollIntoView({
        behavior: "smooth",
        block: "nearest",
      });
    });

    return () => window.cancelAnimationFrame(frameId);
  }, [genresExpanded]);

  const searchQuery = useQuery({
    queryKey: ["forum-onboarding-game-search", debouncedSearch],
    queryFn: ({ signal }) =>
      getGames(
        {
          searchName: debouncedSearch,
          includeRelatedContent: false,
          pageNumber: 1,
          pageSize: 12,
        },
        signal,
      ),
    enabled: open && step === 1 && debouncedSearch.length >= 2,
  });

  const genresQuery = useQuery({
    queryKey: ["forum-onboarding-genres", "Igdb"],
    queryFn: ({ signal }) => getIgdbGameGenres(signal),
    enabled: open && step === 2,
    staleTime: 30 * 60 * 1000,
  });

  const searchedGames = useMemo<SuggestedGame[]>(
    () =>
      (searchQuery.data?.items ?? []).map((game) => ({
        key: `${game.source}-${game.id}`,
        title: game.title,
        subtitle: game.releaseDate
          ? new Date(game.releaseDate).getFullYear().toString()
          : "Oyun",
        colors: "from-slate-950 via-violet-900 to-teal-700",
        result: game,
      })),
    [searchQuery.data],
  );

  const genreOptions = useMemo(
    () =>
      (genresQuery.data ?? [])
        .filter(
          (genre, index, items) =>
            items.findIndex(
              (item) =>
                item.name.toLocaleLowerCase() ===
                genre.name.toLocaleLowerCase(),
            ) === index,
        )
        .map((genre) => ({
          key: `${genre.source}-${genre.id}`,
          value: genre.name,
          label: genreLabels[genre.name] ?? genre.name,
        })),
    [genresQuery.data],
  );

  if (!open) {
    return null;
  }

  const currentMeta = stepMeta[step];
  const currentIsValid =
    (step === 0 && styles.length > 0) ||
    (step === 1 && selectedGames.length >= 3) ||
    (step === 2 && selectedGenres.length > 0) ||
    (step === 3 && platform.length > 0);

  function toggleGame(game: SuggestedGame) {
    setSelectedGames((current) => {
      const isSelected = current.some(
        (item) => item.key === game.key,
      );
      return isSelected
        ? current.filter((item) => item.key !== game.key)
        : [...current, game];
    });
  }

  function finishOnboarding() {
    localStorage.setItem(
      `ludora-forum-interests:${userId}`,
      JSON.stringify({
        styles,
        games: selectedGames.map((game) => ({
          key: game.key,
          title: game.title,
        })),
        genres: selectedGenres,
        platform,
        completedAt: new Date().toISOString(),
      }),
    );
    onComplete();
  }

  function goForward() {
    if (!currentIsValid) {
      return;
    }
    if (step === stepMeta.length - 1) {
      finishOnboarding();
      return;
    }
    setStep((current) => current + 1);
  }

  const isSearchingGames = debouncedSearch.length >= 2;
  const gamesToShow = isSearchingGames
    ? searchedGames
    : popularGames;
  const gamesAreLoading = isSearchingGames && searchQuery.isLoading;
  const visibleGenres = genresExpanded
    ? genreOptions
    : genreOptions.slice(0, initialVisibleGenreCount);

  return (
    <div
      className="fixed inset-0 z-[90] flex items-end justify-center bg-black/75 p-0 backdrop-blur-md sm:items-center sm:p-5"
      role="dialog"
      aria-modal="true"
      aria-labelledby="forum-onboarding-title"
    >
      <div className="relative flex h-[94vh] w-full max-w-5xl flex-col overflow-hidden rounded-t-[28px] border border-teal-200/15 bg-[#0a1212] shadow-[0_30px_100px_rgba(0,0,0,0.65)] sm:h-[min(780px,92vh)] sm:rounded-[30px]">
        <div className="pointer-events-none absolute inset-0 overflow-hidden">
          <div className="absolute -right-40 -top-40 h-96 w-96 rounded-full bg-teal-300/[0.08] blur-3xl" />
          <div className="absolute -bottom-48 -left-32 h-96 w-96 rounded-full bg-violet-500/[0.08] blur-3xl" />
        </div>

        <header className="relative flex items-center justify-between border-b border-white/[0.07] px-5 py-4 sm:px-7">
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-2xl bg-gradient-to-br from-teal-300 to-cyan-600 text-[#07100f] shadow-lg shadow-teal-950/40">
              <Sparkles size={19} strokeWidth={2.3} />
            </span>
            <div>
              <p className="text-sm font-bold text-white">
                Lentini sənə uyğunlaşdıraq
              </p>
              <p className="mt-0.5 text-xs text-zinc-500">
                Təxminən 1 dəqiqə
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={onDismiss}
            className="flex h-9 w-9 items-center justify-center rounded-xl border border-white/[0.08] bg-white/[0.03] text-zinc-500 transition hover:bg-white/[0.07] hover:text-white"
            aria-label="Maraq seçimini bağla"
            title="Sonra tamamla"
          >
            <X size={18} />
          </button>
        </header>

        <div className="relative flex min-h-0 flex-1 flex-col lg:grid lg:grid-cols-[230px_1fr]">
          <aside className="hidden border-r border-white/[0.07] p-6 lg:block">
            <p className="text-[10px] font-bold uppercase tracking-[0.22em] text-zinc-600">
              Addımlar
            </p>
            <ol className="mt-5 space-y-5">
              {stepMeta.map((item, index) => {
                const isDone = index < step;
                const isActive = index === step;
                return (
                  <li
                    key={item.eyebrow}
                    className="flex items-start gap-3"
                  >
                    <span
                      className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full border text-[11px] font-bold ${
                        isDone
                          ? "border-teal-300 bg-teal-300 text-[#07100f]"
                          : isActive
                            ? "border-teal-300/50 bg-teal-300/10 text-teal-200"
                            : "border-white/10 bg-white/[0.025] text-zinc-600"
                      }`}
                    >
                      {isDone ? <Check size={14} /> : index + 1}
                    </span>
                    <div>
                      <p
                        className={`text-sm font-semibold ${
                          isActive || isDone
                            ? "text-zinc-200"
                            : "text-zinc-600"
                        }`}
                      >
                        {item.eyebrow}
                      </p>
                      <p className="mt-1 text-[11px] text-zinc-600">
                        {index === 0
                          ? `${styles.length} seçim`
                          : index === 1
                            ? `${selectedGames.length} oyun`
                            : index === 2
                              ? `${selectedGenres.length} janr`
                              : platform
                                ? "Seçildi"
                                : "Gözləyir"}
                      </p>
                    </div>
                  </li>
                );
              })}
            </ol>
          </aside>

          <section className="flex min-h-0 flex-col">
            <div className="min-h-0 flex-1 overflow-y-auto px-5 py-6 sm:px-8 sm:py-8">
              <div className="mx-auto max-w-2xl">
                <div className="flex items-center gap-2 lg:hidden">
                  {stepMeta.map((item, index) => (
                    <span
                      key={item.eyebrow}
                      className={`h-1.5 flex-1 rounded-full ${
                        index <= step
                          ? "bg-teal-300"
                          : "bg-white/[0.08]"
                      }`}
                    />
                  ))}
                </div>

                <p className="mt-5 text-[11px] font-bold uppercase tracking-[0.22em] text-teal-300 lg:mt-0">
                  {currentMeta.eyebrow} · {step + 1}/4
                </p>
                <h2
                  id="forum-onboarding-title"
                  className="mt-2 text-2xl font-bold tracking-tight text-white sm:text-3xl"
                >
                  {currentMeta.title}
                </h2>
                <p className="mt-2 max-w-xl text-sm leading-6 text-zinc-500">
                  {currentMeta.description}
                </p>

                {step === 0 && (
                  <div className="mt-7 grid gap-3 sm:grid-cols-2">
                    {playStyles.map((item) => {
                      const isSelected = styles.includes(item.id);
                      const Icon = item.icon;
                      return (
                        <button
                          key={item.id}
                          type="button"
                          onClick={() =>
                            setStyles((current) =>
                              toggleValue(current, item.id),
                            )
                          }
                          className={`group relative flex items-center gap-4 rounded-2xl border p-4 text-left transition ${
                            isSelected
                              ? "border-teal-300/45 bg-teal-300/[0.09]"
                              : "border-white/[0.08] bg-white/[0.025] hover:border-white/[0.15] hover:bg-white/[0.045]"
                          }`}
                        >
                          <span
                            className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl ${
                              isSelected
                                ? "bg-teal-300 text-[#07100f]"
                                : "bg-white/[0.05] text-zinc-400"
                            }`}
                          >
                            <Icon size={21} strokeWidth={1.9} />
                          </span>
                          <span>
                            <span className="block text-sm font-bold text-zinc-100">
                              {item.title}
                            </span>
                            <span className="mt-1 block text-xs text-zinc-500">
                              {item.description}
                            </span>
                          </span>
                          {isSelected && (
                            <span className="absolute right-3 top-3 flex h-5 w-5 items-center justify-center rounded-full bg-teal-300 text-[#07100f]">
                              <Check size={12} strokeWidth={3} />
                            </span>
                          )}
                        </button>
                      );
                    })}
                  </div>
                )}

                {step === 1 && (
                  <div className="mt-7">
                    <div className="relative">
                      <Search
                        size={18}
                        className="absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600"
                      />
                      <input
                        type="search"
                        value={searchValue}
                        onChange={(event) =>
                          setSearchValue(event.target.value)
                        }
                        placeholder="Sevdiyin oyunu axtar..."
                        className="h-12 w-full rounded-2xl border border-white/[0.09] bg-white/[0.035] pl-11 pr-4 text-sm text-white outline-none transition placeholder:text-zinc-600 focus:border-teal-300/35 focus:bg-white/[0.05]"
                        autoFocus
                      />
                    </div>

                    <div className="mt-4 flex items-center justify-between">
                      <p className="text-xs font-semibold text-zinc-400">
                        {debouncedSearch.length >= 2
                          ? "Axtarış nəticələri"
                          : "Oyunçular arasında populyar"}
                      </p>
                      <span
                        className={`rounded-full px-2.5 py-1 text-[11px] font-bold ${
                          selectedGames.length >= 3
                            ? "bg-teal-300/10 text-teal-200"
                            : "bg-white/[0.05] text-zinc-500"
                        }`}
                      >
                        {selectedGames.length}/3 minimum
                      </span>
                    </div>

                    {gamesAreLoading ? (
                      <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
                        {Array.from({ length: 4 }).map((_, index) => (
                          <div
                            key={index}
                            className="h-40 animate-pulse rounded-2xl bg-white/[0.04]"
                          />
                        ))}
                      </div>
                    ) : searchQuery.isError &&
                      debouncedSearch.length >= 2 ? (
                      <div className="mt-4 rounded-2xl border border-rose-400/15 bg-rose-400/[0.05] p-6 text-center">
                        <p className="text-sm font-semibold text-rose-200">
                          Oyun axtarışı tamamlanmadı.
                        </p>
                        <button
                          type="button"
                          onClick={() => searchQuery.refetch()}
                          className="mt-3 rounded-xl border border-rose-300/15 px-3 py-2 text-xs font-bold text-rose-200 transition hover:bg-rose-300/[0.07]"
                        >
                          Yenidən yoxla
                        </button>
                      </div>
                    ) : (
                      <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
                        {gamesToShow.map((game) => {
                          const isSelected = selectedGames.some(
                            (item) => item.key === game.key,
                          );
                          const coverImageUrl =
                            game.result?.coverImageUrl ??
                            game.coverImageUrl;
                          return (
                            <button
                              key={game.key}
                              type="button"
                              onClick={() => toggleGame(game)}
                              className={`group relative min-h-40 overflow-hidden rounded-2xl border text-left transition ${
                                isSelected
                                  ? "border-teal-300 ring-2 ring-teal-300/15"
                                  : "border-white/[0.08] hover:-translate-y-0.5 hover:border-white/20"
                              }`}
                            >
                              <span
                                className={`absolute inset-0 bg-gradient-to-br ${game.colors}`}
                              />
                              {coverImageUrl && (
                                <img
                                  src={
                                    resolveAssetUrl(
                                      coverImageUrl,
                                    ) ?? undefined
                                  }
                                  alt={`${game.title} örtük şəkli`}
                                  onError={(event) => {
                                    event.currentTarget.style.display = "none";
                                  }}
                                  className="absolute inset-0 h-full w-full object-cover"
                                />
                              )}
                              <span className="absolute inset-0 bg-gradient-to-t from-black via-black/35 to-transparent" />
                              <span className="absolute inset-x-0 bottom-0 p-3">
                                <span className="block text-sm font-bold leading-5 text-white">
                                  {game.title}
                                </span>
                                <span className="mt-1 block text-[10px] text-zinc-300/75">
                                  {game.subtitle}
                                </span>
                              </span>
                              <span
                                className={`absolute right-2.5 top-2.5 flex h-6 w-6 items-center justify-center rounded-full border backdrop-blur ${
                                  isSelected
                                    ? "border-teal-200 bg-teal-300 text-[#07100f]"
                                    : "border-white/20 bg-black/25 text-transparent"
                                }`}
                              >
                                <Check size={13} strokeWidth={3} />
                              </span>
                            </button>
                          );
                        })}
                      </div>
                    )}

                    {debouncedSearch.length >= 2 &&
                      !searchQuery.isLoading &&
                      !searchQuery.isError &&
                      gamesToShow.length === 0 && (
                        <div className="mt-4 rounded-2xl border border-dashed border-white/10 p-8 text-center">
                          <Gamepad2
                            size={24}
                            className="mx-auto text-zinc-700"
                          />
                          <p className="mt-3 text-sm text-zinc-500">
                            Bu adda oyun tapılmadı.
                          </p>
                        </div>
                      )}
                  </div>
                )}

                {step === 2 && (
                  <div className="mt-7">
                    {genresQuery.isLoading ? (
                      <div className="flex min-h-40 items-center justify-center gap-3 rounded-2xl border border-white/[0.07] bg-white/[0.02] text-sm text-zinc-500">
                        <LoaderCircle size={18} className="animate-spin" />
                        IGDB janrları yüklənir...
                      </div>
                    ) : genresQuery.isError ? (
                      <div className="rounded-2xl border border-rose-400/15 bg-rose-400/[0.05] p-6 text-center">
                        <p className="text-sm font-semibold text-rose-200">
                          IGDB janrlarını yükləmək mümkün olmadı.
                        </p>
                        <button
                          type="button"
                          onClick={() => void genresQuery.refetch()}
                          className="mt-3 rounded-xl border border-rose-300/15 px-3 py-2 text-xs font-bold text-rose-200 transition hover:bg-rose-300/[0.07]"
                        >
                          Yenidən yoxla
                        </button>
                      </div>
                    ) : genreOptions.length === 0 ? (
                      <div className="rounded-2xl border border-dashed border-white/10 p-8 text-center text-sm text-zinc-500">
                        IGDB-dən janr tapılmadı.
                      </div>
                    ) : (
                      <>
                        <div className="flex flex-wrap gap-3">
                          {visibleGenres.map((genre, index) => {
                            const isSelected =
                              selectedGenres.includes(genre.value);
                            return (
                              <button
                                key={genre.key}
                                ref={
                                  genresExpanded &&
                                  index === initialVisibleGenreCount
                                    ? firstAdditionalGenreRef
                                    : undefined
                                }
                                type="button"
                                onClick={() =>
                                  setSelectedGenres((current) =>
                                    toggleValue(current, genre.value),
                                  )
                                }
                                className={`flex items-center gap-2 rounded-2xl border px-4 py-3 text-sm font-semibold transition ${
                                  isSelected
                                    ? "border-teal-300/45 bg-teal-300/[0.11] text-teal-100"
                                    : "border-white/[0.08] bg-white/[0.025] text-zinc-400 hover:border-white/[0.16] hover:text-white"
                                }`}
                              >
                                {isSelected && (
                                  <Check size={14} strokeWidth={3} />
                                )}
                                {genre.label}
                              </button>
                            );
                          })}
                        </div>

                        {genreOptions.length > initialVisibleGenreCount && (
                          <button
                            type="button"
                            onClick={() => {
                              if (genresExpanded) {
                                setGenresExpanded(false);
                                return;
                              }

                              setGenresExpanded(true);
                            }}
                            className="mt-5 inline-flex h-10 items-center gap-2 rounded-xl border border-teal-300/20 bg-teal-300/[0.06] px-4 text-xs font-bold text-teal-200 transition hover:bg-teal-300/[0.11]"
                          >
                            {genresExpanded ? (
                              <>
                                Daha az göstər
                                <ChevronUp size={15} />
                              </>
                            ) : (
                              <>
                                Daha çox ({genreOptions.length - initialVisibleGenreCount})
                                <ChevronDown size={15} />
                              </>
                            )}
                          </button>
                        )}
                      </>
                    )}
                  </div>
                )}

                {step === 3 && (
                  <div className="mt-7 grid gap-3 sm:grid-cols-2">
                    {platforms.map((item) => {
                      const isSelected = platform === item.id;
                      return (
                        <button
                          key={item.id}
                          type="button"
                          onClick={() => setPlatform(item.id)}
                          className={`relative flex items-center gap-4 rounded-2xl border p-4 text-left transition ${
                            isSelected
                              ? "border-teal-300/45 bg-teal-300/[0.09]"
                              : "border-white/[0.08] bg-white/[0.025] hover:border-white/[0.16] hover:bg-white/[0.045]"
                          }`}
                        >
                          <span
                            className={`flex h-11 w-11 items-center justify-center rounded-2xl text-xs font-black tracking-tight ${
                              isSelected
                                ? "bg-teal-300 text-[#07100f]"
                                : "bg-white/[0.055] text-zinc-400"
                            }`}
                          >
                            {item.mark}
                          </span>
                          <span>
                            <span className="block text-sm font-bold text-zinc-100">
                              {item.title}
                            </span>
                            <span className="mt-1 block text-xs text-zinc-500">
                              {item.subtitle}
                            </span>
                          </span>
                          {isSelected && (
                            <span className="absolute right-4 top-1/2 flex h-6 w-6 -translate-y-1/2 items-center justify-center rounded-full bg-teal-300 text-[#07100f]">
                              <Check size={13} strokeWidth={3} />
                            </span>
                          )}
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>
            </div>

            <footer className="relative border-t border-white/[0.07] bg-[#0a1212]/95 px-5 py-4 sm:px-8">
              <div className="mx-auto flex max-w-2xl items-center justify-between gap-3">
                <button
                  type="button"
                  onClick={() =>
                    step === 0
                      ? onDismiss()
                      : setStep((current) => current - 1)
                  }
                  className="flex h-11 items-center gap-2 rounded-xl px-3 text-sm font-semibold text-zinc-500 transition hover:bg-white/[0.04] hover:text-zinc-200"
                >
                  <ArrowLeft size={17} />
                  {step === 0 ? "Sonra" : "Geri"}
                </button>

                <button
                  type="button"
                  onClick={goForward}
                  disabled={!currentIsValid}
                  className="flex h-11 items-center gap-2 rounded-xl bg-teal-300 px-5 text-sm font-black text-[#07100f] shadow-lg shadow-teal-950/30 transition hover:bg-teal-200 disabled:cursor-not-allowed disabled:bg-white/[0.07] disabled:text-zinc-600 disabled:shadow-none"
                >
                  {step === stepMeta.length - 1
                    ? "Lentimi hazırla"
                    : "Davam et"}
                  {step === stepMeta.length - 1 ? (
                    <Sparkles size={16} />
                  ) : (
                    <ArrowRight size={17} />
                  )}
                </button>
              </div>
            </footer>
          </section>
        </div>
      </div>
    </div>
  );
}
