import { Search, X } from "lucide-react";
import {
  useCallback,
  useEffect,
  useRef,
  useState,
} from "react";
import {
  useLocation,
  useNavigate,
  useSearchParams,
} from "react-router-dom";

interface AdminLiveSearchProps {
  placeholder: string;
  ariaLabel?: string;
  debounceMs?: number;
}

export function AdminLiveSearch({
  placeholder,
  ariaLabel = "Admin paneldə axtarış",
  debounceMs = 350,
}: AdminLiveSearchProps) {
  const location = useLocation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const urlValue = searchParams.get("search") ?? "";
  const [value, setValue] = useState(urlValue);
  const previousUrlValue = useRef(urlValue);

  const updateUrl = useCallback(
    (nextValue: string) => {
      const next = new URLSearchParams(location.search);
      const normalizedValue = nextValue.trim();

      if (normalizedValue) next.set("search", normalizedValue);
      else next.delete("search");

      next.delete("page");
      const query = next.toString();

      navigate(
        {
          pathname: location.pathname,
          search: query ? `?${query}` : "",
        },
        { replace: true },
      );
    },
    [location.pathname, location.search, navigate],
  );

  useEffect(() => {
    if (previousUrlValue.current === urlValue) return;

    previousUrlValue.current = urlValue;
    if (value === urlValue) return;

    const timeoutId = window.setTimeout(() => {
      setValue(urlValue);
    }, 0);

    return () => window.clearTimeout(timeoutId);
  }, [urlValue, value]);

  useEffect(() => {
    const normalizedValue = value.trim();
    if (normalizedValue === urlValue) return;

    const timeoutId = window.setTimeout(() => {
      updateUrl(normalizedValue);
    }, debounceMs);

    return () => window.clearTimeout(timeoutId);
  }, [debounceMs, updateUrl, urlValue, value]);

  return (
    <div className="relative">
      <Search
        size={17}
        className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-zinc-600"
      />
      <input
        type="text"
        role="searchbox"
        autoComplete="off"
        value={value}
        aria-label={ariaLabel}
        onChange={(event) => setValue(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            event.preventDefault();
            updateUrl(value);
          }
        }}
        placeholder={placeholder}
        className="h-11 w-full rounded-xl border border-white/[0.08] bg-[#0a0a0e] pl-10 pr-11 text-sm text-white outline-none transition placeholder:text-zinc-700 focus:border-violet-500/50"
      />
      {value.length > 0 && (
        <button
          type="button"
          aria-label="Axtarışı təmizlə"
          title="Axtarışı təmizlə"
          onClick={() => {
            setValue("");
            updateUrl("");
          }}
          className="absolute right-1.5 top-1/2 flex h-8 w-8 -translate-y-1/2 items-center justify-center rounded-lg text-zinc-500 transition hover:bg-white/[0.07] hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400/60"
        >
          <X size={16} />
        </button>
      )}
    </div>
  );
}
