import {
  AlertTriangle,
  LogOut,
  RotateCcw,
  ShieldAlert,
  Trash2,
  Unplug,
  UserMinus,
  X,
} from "lucide-react";
import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { createPortal } from "react-dom";
import {
  ConfirmationContext,
  type ConfirmationKind,
  type ConfirmationOptions,
  type ConfirmationTone,
} from "../../lib/confirmation";

interface ConfirmationDialogProviderProps {
  children: ReactNode;
}

const toneStyles: Record<
  ConfirmationTone,
  {
    icon: string;
    glow: string;
    detail: string;
    confirm: string;
  }
> = {
  danger: {
    icon: "border-red-400/20 bg-red-500/10 text-red-200",
    glow: "bg-red-500/10",
    detail: "border-red-400/15 bg-red-500/[0.055] text-red-100/80",
    confirm:
      "bg-red-500 text-white shadow-lg shadow-red-950/25 hover:bg-red-400",
  },
  warning: {
    icon: "border-amber-300/20 bg-amber-400/10 text-amber-200",
    glow: "bg-amber-400/10",
    detail:
      "border-amber-300/15 bg-amber-400/[0.055] text-amber-100/80",
    confirm:
      "bg-amber-300 text-[#171006] shadow-lg shadow-amber-950/20 hover:bg-amber-200",
  },
  neutral: {
    icon: "border-violet-300/20 bg-violet-400/10 text-violet-200",
    glow: "bg-violet-400/10",
    detail:
      "border-violet-300/15 bg-violet-400/[0.055] text-violet-100/80",
    confirm:
      "bg-violet-500 text-white shadow-lg shadow-violet-950/25 hover:bg-violet-400",
  },
};

function ConfirmationIcon({
  kind,
}: {
  kind: ConfirmationKind;
}) {
  switch (kind) {
    case "delete":
      return <Trash2 size={22} />;
    case "leave":
      return <LogOut size={22} />;
    case "remove-user":
      return <UserMinus size={22} />;
    case "disconnect":
      return <Unplug size={22} />;
    case "reset":
      return <RotateCcw size={22} />;
    case "moderation":
      return <ShieldAlert size={22} />;
    default:
      return <AlertTriangle size={22} />;
  }
}

export function ConfirmationDialogProvider({
  children,
}: ConfirmationDialogProviderProps) {
  const [options, setOptions] =
    useState<ConfirmationOptions | null>(null);
  const resolverRef = useRef<
    ((confirmed: boolean) => void) | null
  >(null);
  const cancelButtonRef = useRef<HTMLButtonElement>(null);

  const closeDialog = useCallback((confirmed: boolean) => {
    const resolve = resolverRef.current;
    resolverRef.current = null;
    setOptions(null);
    resolve?.(confirmed);
  }, []);

  const requestConfirmation = useCallback(
    (nextOptions: ConfirmationOptions) =>
      new Promise<boolean>((resolve) => {
        resolverRef.current?.(false);
        resolverRef.current = resolve;
        setOptions(nextOptions);
      }),
    [],
  );

  useEffect(() => {
    if (!options) return;

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const focusTimer = window.setTimeout(() => {
      cancelButtonRef.current?.focus();
    }, 0);

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        closeDialog(false);
      }
    };

    document.addEventListener("keydown", handleKeyDown);

    return () => {
      window.clearTimeout(focusTimer);
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [closeDialog, options]);

  useEffect(
    () => () => {
      resolverRef.current?.(false);
      resolverRef.current = null;
    },
    [],
  );

  const tone = options?.tone ?? "danger";
  const kind = options?.kind ?? "delete";
  const styles = toneStyles[tone];

  return (
    <ConfirmationContext.Provider
      value={requestConfirmation}
    >
      {children}
      {options &&
        createPortal(
          <div
            className="fixed inset-0 z-[1500] grid place-items-center overflow-y-auto bg-[#050507]/80 p-4 backdrop-blur-md sm:p-6"
            role="presentation"
            onMouseDown={(event) => {
              if (event.target === event.currentTarget) {
                closeDialog(false);
              }
            }}
          >
            <section
              role="alertdialog"
              aria-modal="true"
              aria-labelledby="confirmation-dialog-title"
              aria-describedby="confirmation-dialog-description"
              className="relative w-full max-w-md overflow-hidden rounded-[28px] border border-white/[0.09] bg-[#111116] p-5 text-white shadow-[0_30px_100px_rgba(0,0,0,0.72)] sm:p-6"
            >
              <div
                className={`pointer-events-none absolute -right-16 -top-20 h-48 w-48 rounded-full blur-3xl ${styles.glow}`}
              />

              <button
                type="button"
                onClick={() => closeDialog(false)}
                className="absolute right-4 top-4 z-10 flex h-9 w-9 items-center justify-center rounded-xl border border-white/[0.07] text-zinc-600 transition hover:bg-white/[0.06] hover:text-zinc-200"
                aria-label="Təsdiq pəncərəsini bağla"
              >
                <X size={17} />
              </button>

              <div
                className={`relative flex h-12 w-12 items-center justify-center rounded-2xl border ${styles.icon}`}
              >
                <ConfirmationIcon kind={kind} />
              </div>

              <div className="relative mt-5 pr-8">
                <p className="text-[10px] font-black uppercase tracking-[0.2em] text-zinc-600">
                  Əməliyyatı təsdiqlə
                </p>
                <h2
                  id="confirmation-dialog-title"
                  className="mt-2 text-xl font-black tracking-tight text-white"
                >
                  {options.title}
                </h2>
                <p
                  id="confirmation-dialog-description"
                  className="mt-3 text-sm leading-6 text-zinc-400"
                >
                  {options.description}
                </p>
              </div>

              {options.detail && (
                <div
                  className={`relative mt-5 flex items-start gap-3 rounded-2xl border px-4 py-3 text-xs leading-5 ${styles.detail}`}
                >
                  <AlertTriangle
                    size={16}
                    className="mt-0.5 shrink-0"
                  />
                  <span>{options.detail}</span>
                </div>
              )}

              <div className="relative mt-6 grid grid-cols-2 gap-3">
                <button
                  ref={cancelButtonRef}
                  type="button"
                  onClick={() => closeDialog(false)}
                  className="h-11 rounded-xl border border-white/[0.09] bg-white/[0.025] px-4 text-sm font-bold text-zinc-300 transition hover:bg-white/[0.07] hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/30"
                >
                  {options.cancelLabel ?? "Ləğv et"}
                </button>
                <button
                  type="button"
                  onClick={() => closeDialog(true)}
                  className={`h-11 rounded-xl px-4 text-sm font-black transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/40 ${styles.confirm}`}
                >
                  {options.confirmLabel}
                </button>
              </div>
            </section>
          </div>,
          document.body,
        )}
    </ConfirmationContext.Provider>
  );
}
