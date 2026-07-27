import { Gauge, Sparkles } from "lucide-react";
import {
  useRef,
  useState,
} from "react";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../../stores/authStore";
import { FpsEstimatorModal } from "./FpsEstimatorModal";

interface FpsEstimatorActionProps {
  gameId: number;
  gameName: string;
}

export function FpsEstimatorAction({
  gameId,
  gameName,
}: FpsEstimatorActionProps) {
  const [isOpen, setIsOpen] = useState(false);
  const buttonRef =
    useRef<HTMLButtonElement>(null);
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

  function handleClose() {
    setIsOpen(false);
    window.requestAnimationFrame(() => {
      buttonRef.current?.focus();
    });
  }

  return (
    <>
      <div className="overflow-hidden rounded-2xl border border-violet-300/15 bg-violet-400/[0.07] p-5">
        <div className="flex items-start gap-3">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-violet-300/20 bg-violet-400/10 text-violet-300">
            <Sparkles size={19} />
          </span>
          <div>
            <p className="text-sm font-black text-zinc-100">
              Sistemində necə işləyər?
            </p>
            <p className="mt-1 text-xs leading-5 text-zinc-500">
              AI ilə oyun ayarlarına uyğun təxmini
              performansı öyrən.
            </p>
          </div>
        </div>

        <button
          ref={buttonRef}
          type="button"
          onClick={() => setIsOpen(true)}
          className="mt-4 inline-flex h-11 w-full items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-black text-white transition hover:bg-violet-500 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400/60"
        >
          <Gauge size={18} />
          FPS hesabla
        </button>
      </div>

      <FpsEstimatorModal
        key={`${gameId}:${gameName}`}
        isOpen={isOpen}
        isAuthenticated={isAuthenticated}
        gameId={gameId}
        gameName={gameName}
        onClose={handleClose}
      />
    </>
  );
}
