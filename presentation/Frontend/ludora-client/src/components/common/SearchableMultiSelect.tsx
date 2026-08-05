import {
  Check,
  ChevronDown,
  LoaderCircle,
  Search,
  X,
} from "lucide-react";
import {
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
} from "react";
import type { SearchableSelectOption } from "./SearchableSelect";

interface SearchableMultiSelectProps {
  values: string[];
  options: SearchableSelectOption[];
  onChange: (values: string[]) => void;

  placeholder: string;
  clearLabel: string;
  selectedCountLabel?: (count: number) => string;
  searchPlaceholder?: string;
  emptyMessage?: string;

  searchable?: boolean;
  disabled?: boolean;
  isSearching?: boolean;
  onSearchChange?: (value: string) => void;
}

function normalizeSearchValue(value: string): string {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLocaleLowerCase()
    .trim();
}

export function SearchableMultiSelect({
  values,
  options,
  onChange,
  placeholder,
  clearLabel,
  selectedCountLabel = (count) => `${count} seçim`,
  searchPlaceholder = "Axtar...",
  emptyMessage = "Nəticə tapılmadı.",
  searchable = true,
  disabled = false,
  isSearching = false,
  onSearchChange,
}: SearchableMultiSelectProps) {
  const searchInputId = useId();
  const containerRef = useRef<HTMLDivElement>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [searchValue, setSearchValue] = useState("");

  const selectedValueSet = useMemo(
    () => new Set(values),
    [values],
  );

  const selectedOptions = useMemo(
    () => options.filter((option) => selectedValueSet.has(option.value)),
    [options, selectedValueSet],
  );

  useEffect(() => {
    function handleOutsideClick(event: MouseEvent) {
      if (
        containerRef.current &&
        !containerRef.current.contains(event.target as Node)
      ) {
        setIsOpen(false);
        setSearchValue("");
      }
    }

    function handleEscape(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setIsOpen(false);
        setSearchValue("");
      }
    }

    document.addEventListener("mousedown", handleOutsideClick);
    document.addEventListener("keydown", handleEscape);

    return () => {
      document.removeEventListener("mousedown", handleOutsideClick);
      document.removeEventListener("keydown", handleEscape);
    };
  }, []);

  useEffect(() => {
    onSearchChange?.(searchValue);
  }, [onSearchChange, searchValue]);

  const filteredOptions = useMemo(() => {
    const normalizedSearch = normalizeSearchValue(searchValue);

    if (!normalizedSearch) {
      return options;
    }

    return options.filter((option) => {
      const searchableText = [
        option.label,
        option.description ?? "",
      ].join(" ");

      return normalizeSearchValue(searchableText).includes(normalizedSearch);
    });
  }, [options, searchValue]);

  const groupedOptions = useMemo(() => {
    const groups = new Map<string, SearchableSelectOption[]>();

    for (const option of filteredOptions) {
      const groupName = option.group ?? "";
      const groupItems = groups.get(groupName) ?? [];
      groupItems.push(option);
      groups.set(groupName, groupItems);
    }

    return Array.from(groups.entries());
  }, [filteredOptions]);

  function toggleValue(value: string) {
    if (selectedValueSet.has(value)) {
      onChange(values.filter((item) => item !== value));
    } else {
      onChange([...values, value]);
    }
  }

  const primarySelectedOption = selectedOptions[0];
  const missingSelectedCount = Math.max(0, values.length - selectedOptions.length);
  const visibleSelectedCount = selectedOptions.length + missingSelectedCount;

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        disabled={disabled}
        onClick={() => {
          if (isOpen) {
            setSearchValue("");
          }
          setIsOpen((current) => !current);
        }}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        className={`flex min-h-11 w-full items-center justify-between gap-3 rounded-xl border px-3 py-2 text-left text-sm outline-none transition ${
          isOpen
            ? "border-violet-400/50 bg-violet-500/[0.07] ring-4 ring-violet-500/10"
            : "border-[color:var(--wp-border)] bg-[color:var(--wp-surface-strong)] hover:border-[color:var(--wp-border-strong)]"
        } disabled:cursor-not-allowed disabled:opacity-50`}
      >
        <span className="min-w-0 flex-1">
          {visibleSelectedCount === 0 ? (
            <span className="block truncate text-[color:var(--wp-text-muted)]">
              {placeholder}
            </span>
          ) : (
            <span className="flex min-w-0 items-center gap-2">
              <span className="truncate font-semibold text-[var(--wp-text-strong)]">
                {primarySelectedOption?.label ?? selectedCountLabel(visibleSelectedCount)}
              </span>
              {visibleSelectedCount > 1 && (
                <span className="shrink-0 rounded-full bg-violet-500/12 px-2 py-0.5 text-[10px] font-black text-violet-300">
                  +{visibleSelectedCount - 1}
                </span>
              )}
            </span>
          )}
        </span>

        <ChevronDown
          size={16}
          className={`shrink-0 text-[color:var(--wp-text-muted)] transition-transform ${
            isOpen ? "rotate-180" : ""
          }`}
        />
      </button>

      {isOpen && (
        <div className="absolute left-0 right-0 top-[calc(100%+8px)] z-[70] overflow-hidden rounded-2xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-strong)] shadow-[var(--wp-shadow-floating)]">
          {searchable && (
            <div className="border-b border-[color:var(--wp-border)] p-3">
              <div className="relative">
                <Search
                  size={16}
                  className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[color:var(--wp-text-muted)]"
                />
                <input
                  id={searchInputId}
                  type="search"
                  autoComplete="off"
                  value={searchValue}
                  onChange={(event) => setSearchValue(event.target.value)}
                  autoFocus
                  placeholder={searchPlaceholder}
                  className="h-10 w-full rounded-xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-soft)] pl-10 pr-9 text-sm text-[var(--wp-text)] outline-none transition placeholder:text-[color:var(--wp-text-muted)] focus:border-violet-400/40 focus:ring-4 focus:ring-violet-500/10"
                />
                {searchValue && (
                  <button
                    type="button"
                    onClick={() => setSearchValue("")}
                    className="absolute right-2 top-1/2 flex h-7 w-7 -translate-y-1/2 items-center justify-center rounded-lg text-[color:var(--wp-text-muted)] transition hover:bg-violet-500/10 hover:text-[var(--wp-text-strong)]"
                    aria-label="Axtarışı təmizlə"
                  >
                    <X size={14} />
                  </button>
                )}
              </div>
            </div>
          )}

          <div className="border-b border-[color:var(--wp-border)] p-2">
            <button
              type="button"
              onClick={() => onChange([])}
              className={`flex w-full items-center justify-between rounded-xl px-3 py-2.5 text-left text-sm transition ${
                values.length === 0
                  ? "bg-violet-500/10 text-violet-300"
                  : "text-[color:var(--wp-text-muted)] hover:bg-violet-500/[0.06] hover:text-[var(--wp-text-strong)]"
              }`}
            >
              <span>{clearLabel}</span>
              {values.length === 0 && <Check size={15} />}
            </button>
          </div>

          <div
            role="listbox"
            aria-multiselectable="true"
            className="max-h-80 overflow-y-auto p-2 [scrollbar-color:#6366f1_transparent] [scrollbar-width:thin]"
          >
            {isSearching ? (
              <div className="flex items-center justify-center gap-2 px-4 py-10 text-sm text-[color:var(--wp-text-muted)]">
                <LoaderCircle size={16} className="animate-spin" />
                Axtarılır...
              </div>
            ) : filteredOptions.length === 0 ? (
              <div className="px-4 py-10 text-center text-sm text-[color:var(--wp-text-muted)]">
                {emptyMessage}
              </div>
            ) : (
              groupedOptions.map(([groupName, groupItems]) => (
                <div key={groupName || "default-group"} className="mt-2">
                  {groupName && (
                    <p className="px-3 pb-1.5 pt-2 text-[10px] font-bold uppercase tracking-[0.18em] text-[color:var(--wp-text-muted)]">
                      {groupName}
                    </p>
                  )}

                  {groupItems.map((option) => {
                    const isSelected = selectedValueSet.has(option.value);

                    return (
                      <button
                        key={option.value}
                        type="button"
                        role="option"
                        aria-selected={isSelected}
                        onClick={() => toggleValue(option.value)}
                        className={`flex w-full items-center justify-between gap-3 rounded-xl px-3 py-2.5 text-left transition ${
                          isSelected
                            ? "bg-violet-500/10 text-violet-300"
                            : "text-[var(--wp-text)] hover:bg-violet-500/[0.06] hover:text-[var(--wp-text-strong)]"
                        }`}
                      >
                        <span className="min-w-0">
                          <span className="block truncate text-sm">
                            {option.label}
                          </span>
                          {option.description && (
                            <span className="mt-0.5 block truncate text-[10px] text-[color:var(--wp-text-muted)]">
                              {option.description}
                            </span>
                          )}
                        </span>

                        <span
                          className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-md border transition ${
                            isSelected
                              ? "border-violet-400/50 bg-violet-500 text-white"
                              : "border-[color:var(--wp-border-strong)]"
                          }`}
                        >
                          {isSelected && <Check size={13} />}
                        </span>
                      </button>
                    );
                  })}
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}
