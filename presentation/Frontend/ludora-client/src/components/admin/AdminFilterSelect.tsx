import { Check, ChevronDown, Filter } from "lucide-react";
import {
  useEffect,
  useId,
  useRef,
  useState,
} from "react";

interface AdminFilterSelectProps {
  label: string;
  value: string;
  options: ReadonlyArray<readonly [string, string]>;
  onChange: (value: string) => void;
  showFilterIcon?: boolean;
  className?: string;
  buttonClassName?: string;
}

export function AdminFilterSelect({
  label,
  value,
  options,
  onChange,
  showFilterIcon = false,
  className = "",
  buttonClassName = "",
}: AdminFilterSelectProps) {
  const [isOpen, setIsOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const menuId = useId();
  const selectedOption =
    options.find(([optionValue]) => optionValue === value) ?? options[0];

  useEffect(() => {
    if (!isOpen) return;

    function handlePointerDown(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setIsOpen(false);
      }
    }

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen]);

  return (
    <div ref={rootRef} className={`admin-filter-select relative ${className}`}>
      <button
        type="button"
        aria-label={label}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-controls={menuId}
        onClick={() => setIsOpen((current) => !current)}
        className={`admin-filter-select__button flex h-11 w-full min-w-0 items-center gap-2 rounded-xl px-3 text-left text-sm font-semibold outline-none transition lg:min-w-[10.5rem] ${buttonClassName}`}
      >
        {showFilterIcon && (
          <Filter
            size={15}
            className="admin-filter-select__leading-icon shrink-0"
          />
        )}
        <span className="min-w-0 flex-1 truncate">
          {selectedOption?.[1] ?? label}
        </span>
        <ChevronDown
          size={16}
          className={`admin-filter-select__chevron shrink-0 transition-transform ${
            isOpen ? "rotate-180" : ""
          }`}
        />
      </button>

      {isOpen && (
        <div
          id={menuId}
          role="listbox"
          aria-label={label}
          className="admin-filter-select__menu absolute right-0 top-[calc(100%+0.45rem)] z-50 min-w-full overflow-hidden rounded-xl p-1 shadow-2xl"
        >
          {options.map(([optionValue, optionLabel]) => {
            const isSelected = optionValue === value;

            return (
              <button
                key={optionValue}
                type="button"
                role="option"
                aria-selected={isSelected}
                onClick={() => {
                  onChange(optionValue);
                  setIsOpen(false);
                }}
                className={`admin-filter-select__option flex w-full items-center gap-2 rounded-lg px-3 py-2.5 text-left text-sm transition ${
                  isSelected ? "is-selected" : ""
                }`}
              >
                <span className="min-w-0 flex-1 truncate">{optionLabel}</span>
                {isSelected && <Check size={15} className="shrink-0" />}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
