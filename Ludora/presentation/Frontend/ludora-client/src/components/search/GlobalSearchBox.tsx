import { useQuery } from "@tanstack/react-query";
import {
  Gamepad2,
  ListChecks,
  LoaderCircle,
  MessageSquareText,
  Search,
  Star,
  UserRound,
} from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { resolveAssetUrl } from "../../lib/helpers";
import { globalSearch } from "../../services/searchService";

interface Suggestion {
  key: string;
  label: string;
  detail: string;
  score: number;
  imageUrl: string | null;
  icon: typeof Gamepad2;
  target: string;
}

export function GlobalSearchBox({ mobile = false }: { mobile?: boolean }) {
  const navigate = useNavigate();
  const rootRef = useRef<HTMLDivElement>(null);
  const [value, setValue] = useState("");
  const [debouncedValue, setDebouncedValue] = useState("");
  const [isOpen, setIsOpen] = useState(false);

  useEffect(() => {
    const timeoutId = window.setTimeout(
      () => setDebouncedValue(value.trim()),
      250,
    );
    return () => window.clearTimeout(timeoutId);
  }, [value]);

  useEffect(() => {
    function closeOnOutsideClick(event: MouseEvent) {
      if (!rootRef.current?.contains(event.target as Node)) setIsOpen(false);
    }
    document.addEventListener("mousedown", closeOnOutsideClick);
    return () => document.removeEventListener("mousedown", closeOnOutsideClick);
  }, []);

  const searchQuery = useQuery({
    queryKey: ["global-search-preview", debouncedValue],
    queryFn: () => globalSearch(debouncedValue, 5),
    enabled: debouncedValue.length >= 2,
    staleTime: 30_000,
  });

  const suggestions = useMemo<Suggestion[]>(() => {
    const data = searchQuery.data;
    if (!data) return [];

    return [
      ...data.games.map((item) => ({
        key: `game-${item.source}-${item.id}`,
        label: item.title,
        detail: "Oyun",
        score: item.score,
        imageUrl: resolveAssetUrl(item.coverImageUrl),
        icon: Gamepad2,
        target: `/games/${item.source}/${item.id}`,
      })),
      ...data.users.map((item) => ({
        key: `user-${item.id}`,
        label: item.userName,
        detail: "İstifadəçi",
        score: item.score,
        imageUrl: resolveAssetUrl(item.profileImageUrl),
        icon: UserRound,
        target: `/users/${item.userName}`,
      })),
      ...data.discussions.map((item) => ({
        key: `discussion-${item.id}`,
        label: item.title,
        detail: `Müzakirə · ${item.categoryName}`,
        score: item.score,
        imageUrl: null,
        icon: MessageSquareText,
        target: `/#forum`,
      })),
      ...data.reviews.map((item) => ({
        key: `review-${item.id}`,
        label: item.gameName,
        detail: `Rəy · ${item.userName}`,
        score: item.score,
        imageUrl: resolveAssetUrl(item.gameCoverImageUrl),
        icon: Star,
        target: `/games/${item.source}/${item.gameId}#reviews`,
      })),
      ...data.lists.map((item) => ({
        key: `list-${item.id}`,
        label: item.name,
        detail: `Siyahı · ${item.ownerUserName}`,
        score: item.score,
        imageUrl: null,
        icon: ListChecks,
        target: `/lists/${item.id}`,
      })),
    ]
      .sort((a, b) => b.score - a.score)
      .slice(0, 8);
  }, [searchQuery.data]);

  function submitSearch() {
    const query = value.trim();
    if (query.length < 2) return;
    setIsOpen(false);
    navigate(`/search?q=${encodeURIComponent(query)}`);
  }

  return (
    <div ref={rootRef} className={`relative ${mobile ? "w-full" : "group/search w-40 focus-within:w-72"} transition-[width] duration-300 ease-out`}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          submitSearch();
        }}
        className="relative"
      >
        <Search size={16} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-zinc-500" />
        <input
          type="search"
          value={value}
          onChange={(event) => {
            setValue(event.target.value);
            setIsOpen(true);
          }}
          onFocus={() => setIsOpen(true)}
          placeholder="Kəşf et"
          autoComplete="off"
          aria-label="Saytda axtar"
          aria-autocomplete="list"
          className="h-11 w-full rounded-xl border border-violet-400/20 bg-violet-500/10 pl-10 pr-10 text-sm font-semibold text-violet-100 outline-none transition placeholder:text-violet-200/75 focus:border-violet-400/45 focus:bg-[#111018]"
        />
        {searchQuery.isFetching && (
          <LoaderCircle size={16} className="absolute right-3.5 top-1/2 -translate-y-1/2 animate-spin text-violet-300" />
        )}
      </form>

      {isOpen && debouncedValue.length >= 2 && (
        <div className={`absolute z-[70] mt-2 overflow-hidden rounded-2xl border border-white/[0.09] bg-[#111116] p-2 shadow-2xl shadow-black/60 ${mobile ? "left-0 right-0" : "right-0 w-[390px]"}`}>
          {searchQuery.isError ? (
            <p className="px-3 py-5 text-center text-sm text-red-300">Axtarış zamanı xəta baş verdi.</p>
          ) : suggestions.length === 0 && !searchQuery.isFetching ? (
            <p className="px-3 py-5 text-center text-sm text-zinc-500">Uyğun nəticə tapılmadı.</p>
          ) : (
            suggestions.map((item) => {
              const Icon = item.icon;
              return (
                <button
                  key={item.key}
                  type="button"
                  onClick={() => {
                    setIsOpen(false);
                    navigate(item.target);
                  }}
                  className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left transition hover:bg-white/[0.06]"
                >
                  <span className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-violet-500/10 text-violet-300">
                    {item.imageUrl ? <img src={item.imageUrl} alt="" className="h-full w-full object-cover" /> : <Icon size={18} />}
                  </span>
                  <span className="min-w-0">
                    <span className="block truncate text-sm font-semibold text-zinc-100">{item.label}</span>
                    <span className="mt-0.5 block truncate text-xs text-zinc-500">{item.detail}</span>
                  </span>
                </button>
              );
            })
          )}
          <button type="button" onClick={submitSearch} className="mt-1 w-full rounded-xl border-t border-white/[0.06] px-3 py-3 text-sm font-semibold text-violet-300 transition hover:bg-violet-500/10">
            Bütün nəticələrə bax
          </button>
        </div>
      )}
    </div>
  );
}
