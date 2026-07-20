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

export interface SearchableSelectOption {
  value: string;
  label: string;
  description?: string;
  group?: string;
}

interface SearchableSelectProps {
  value: string;
  options: SearchableSelectOption[];
  onChange: (value: string) => void;

  placeholder: string;
  clearLabel: string;
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

export function SearchableSelect({
  value,
  options,
  onChange,
  placeholder,
  clearLabel,
  searchPlaceholder = "Axtar...",
  emptyMessage = "Nəticə tapılmadı.",
  searchable = true,
  disabled = false,
  isSearching = false,
  onSearchChange,
}: SearchableSelectProps) {
  const searchInputId = useId();

  const containerRef =
    useRef<HTMLDivElement>(null);

  const [isOpen, setIsOpen] =
    useState(false);

  const [searchValue, setSearchValue] =
    useState("");

  const selectedOption = options.find(
    (option) => option.value === value,
  );

  useEffect(() => {
    function handleOutsideClick(
      event: MouseEvent,
    ) {
      if (
        containerRef.current &&
        !containerRef.current.contains(
          event.target as Node,
        )
      ) {
        setIsOpen(false);
        setSearchValue("");
      }
    }

    function handleEscape(
      event: KeyboardEvent,
    ) {
      if (event.key === "Escape") {
        setIsOpen(false);
        setSearchValue("");
      }
    }

    document.addEventListener(
      "mousedown",
      handleOutsideClick,
    );

    document.addEventListener(
      "keydown",
      handleEscape,
    );

    return () => {
      document.removeEventListener(
        "mousedown",
        handleOutsideClick,
      );

      document.removeEventListener(
        "keydown",
        handleEscape,
      );
    };
  }, []);

  useEffect(() => {
    onSearchChange?.(searchValue);
  }, [onSearchChange, searchValue]);

  const filteredOptions = useMemo(() => {
    const normalizedSearch =
      normalizeSearchValue(searchValue);

    if (!normalizedSearch) {
      return options;
    }

    return options.filter((option) => {
      const searchableText = [
        option.label,
        option.description ?? "",
      ].join(" ");

      return normalizeSearchValue(
        searchableText,
      ).includes(normalizedSearch);
    });
  }, [options, searchValue]);

  const groupedOptions = useMemo(() => {
    const groups = new Map<
      string,
      SearchableSelectOption[]
    >();

    for (const option of filteredOptions) {
      const groupName =
        option.group ?? "";

      const groupItems =
        groups.get(groupName) ?? [];

      groupItems.push(option);
      groups.set(groupName, groupItems);
    }

    return Array.from(groups.entries());
  }, [filteredOptions]);

  function selectValue(
    selectedValue: string,
  ) {
    onChange(selectedValue);
    setIsOpen(false);
    setSearchValue("");
  }

  return (
    <div
      ref={containerRef}
      className="relative"
    >
      <button
        type="button"
        disabled={disabled}
        onClick={() => {
          if (isOpen) {
            setSearchValue("");
          }

          setIsOpen(!isOpen);
        }}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        className={`flex h-11 w-full items-center justify-between gap-3 rounded-xl border px-3 text-left text-sm outline-none transition ${
          isOpen
            ? "border-violet-400/50 bg-violet-500/[0.07] ring-4 ring-violet-500/10"
            : "border-white/[0.08] bg-[#111116] hover:border-white/[0.15] hover:bg-white/[0.045]"
        } disabled:cursor-not-allowed disabled:opacity-50`}
      >
        <span className="min-w-0">
          <span
            className={`block truncate ${
              selectedOption
                ? "text-zinc-200"
                : "text-zinc-600"
            }`}
          >
            {selectedOption?.label ??
              placeholder}
          </span>

          {selectedOption?.description && (
            <span className="mt-0.5 block truncate text-[10px] text-zinc-600">
              {selectedOption.description}
            </span>
          )}
        </span>

        <ChevronDown
          size={16}
          className={`shrink-0 text-zinc-600 transition-transform ${
            isOpen ? "rotate-180" : ""
          }`}
        />
      </button>

      {isOpen && (
        <div className="absolute left-0 right-0 top-[calc(100%+8px)] z-[70] overflow-hidden rounded-2xl border border-white/[0.1] bg-[#121218] shadow-2xl shadow-black/60">
          {searchable && (
            <div className="border-b border-white/[0.06] p-3">
              <div className="relative">
                <Search
                  size={16}
                  className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-600"
                />

                <input
                  id={searchInputId}
                  type="search"
                  autoComplete="off"
                  value={searchValue}
                  onChange={(event) => {
                    setSearchValue(
                      event.target.value,
                    );
                  }}
                  autoFocus
                  placeholder={
                    searchPlaceholder
                  }
                  className="h-10 w-full rounded-xl border border-white/[0.08] bg-black/25 pl-10 pr-9 text-sm text-zinc-200 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/40 focus:ring-4 focus:ring-violet-500/10"
                />

                {searchValue && (
                  <button
                    type="button"
                    onClick={() => {
                      setSearchValue("");
                    }}
                    className="absolute right-2 top-1/2 flex h-7 w-7 -translate-y-1/2 items-center justify-center rounded-lg text-zinc-600 transition hover:bg-white/5 hover:text-zinc-300"
                    aria-label="Axtarışı təmizlə"
                  >
                    <X size={14} />
                  </button>
                )}
              </div>
            </div>
          )}

          <div
            role="listbox"
            className="max-h-80 overflow-y-auto p-2 [scrollbar-color:#3f3f46_transparent] [scrollbar-width:thin]"
          >
            <button
              type="button"
              role="option"
              aria-selected={value === ""}
              onClick={() => {
                selectValue("");
              }}
              className={`flex w-full items-center justify-between rounded-xl px-3 py-2.5 text-left text-sm transition ${
                value === ""
                  ? "bg-violet-500/10 text-violet-200"
                  : "text-zinc-400 hover:bg-white/[0.05] hover:text-white"
              }`}
            >
              <span>{clearLabel}</span>

              {value === "" && (
                <Check size={15} />
              )}
            </button>

            {isSearching ? (
              <div className="flex items-center justify-center gap-2 px-4 py-10 text-sm text-zinc-600">
                <LoaderCircle
                  size={16}
                  className="animate-spin"
                />
                Axtarılır...
              </div>
            ) : filteredOptions.length === 0 ? (
              <div className="px-4 py-10 text-center text-sm text-zinc-600">
                {emptyMessage}
              </div>
            ) : (
              groupedOptions.map(
                ([groupName, groupItems]) => (
                  <div
                    key={
                      groupName ||
                      "default-group"
                    }
                    className="mt-2"
                  >
                    {groupName && (
                      <p className="px-3 pb-1.5 pt-2 text-[10px] font-bold uppercase tracking-[0.18em] text-zinc-600">
                        {groupName}
                      </p>
                    )}

                    {groupItems.map(
                      (option) => {
                        const isSelected =
                          option.value ===
                          value;

                        return (
                          <button
                            key={option.value}
                            type="button"
                            role="option"
                            aria-selected={
                              isSelected
                            }
                            onClick={() => {
                              selectValue(
                                option.value,
                              );
                            }}
                            className={`flex w-full items-center justify-between gap-3 rounded-xl px-3 py-2.5 text-left transition ${
                              isSelected
                                ? "bg-violet-500/10 text-violet-200"
                                : "text-zinc-300 hover:bg-white/[0.05] hover:text-white"
                            }`}
                          >
                            <span className="min-w-0">
                              <span className="block truncate text-sm">
                                {option.label}
                              </span>

                              {option.description && (
                                <span className="mt-0.5 block truncate text-[10px] text-zinc-600">
                                  {
                                    option.description
                                  }
                                </span>
                              )}
                            </span>

                            {isSelected && (
                              <Check
                                size={15}
                                className="shrink-0"
                              />
                            )}
                          </button>
                        );
                      },
                    )}
                  </div>
                ),
              )
            )}
          </div>
        </div>
      )}
    </div>
  );
}