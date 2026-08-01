import { AlertTriangle, Home, RefreshCw } from "lucide-react";
import {
  Component,
  type ErrorInfo,
  type ReactNode,
} from "react";
import { getCurrentLanguage } from "../../i18n/localization";

interface AppErrorBoundaryProps {
  children: ReactNode;
}

interface AppErrorBoundaryState {
  error: Error | null;
  errorId: string | null;
}

function createErrorId() {
  const timestamp = Date.now().toString(36).toUpperCase();
  const randomPart = Math.random().toString(36).slice(2, 7).toUpperCase();
  return `WP-${timestamp}-${randomPart}`;
}

export class AppErrorBoundary extends Component<
  AppErrorBoundaryProps,
  AppErrorBoundaryState
> {
  state: AppErrorBoundaryState = {
    error: null,
    errorId: null,
  };

  static getDerivedStateFromError(error: Error): AppErrorBoundaryState {
    return {
      error,
      errorId: createErrorId(),
    };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("[WellPlayd] Gözlənilməyən interfeys xətası", {
      error,
      componentStack: info.componentStack,
      errorId: this.state.errorId,
    });
  }

  private reloadPage = () => {
    window.location.reload();
  };

  private goHome = () => {
    window.location.assign("/");
  };

  render() {
    if (!this.state.error) return this.props.children;

    const isEnglish = getCurrentLanguage() === "en";
    const isChunkError = /chunk|dynamically imported|module script/i.test(
      this.state.error.message,
    );

    const title = isEnglish
      ? "Something went wrong"
      : "Gözlənilməyən xəta baş verdi";
    const description = isChunkError
      ? isEnglish
        ? "The application files were updated or could not be loaded. Refresh the page to continue."
        : "Tətbiq faylları yenilənib və ya yüklənə bilməyib. Davam etmək üçün səhifəni yenilə."
      : isEnglish
        ? "Your data is safe. Refresh the page and try the operation again."
        : "Məlumatların təhlükəsizdir. Səhifəni yeniləyib əməliyyatı təkrar yoxla.";

    return (
      <main className="grid min-h-screen place-items-center bg-[#09090d] px-5 py-12 text-zinc-100">
        <section className="w-full max-w-lg rounded-[28px] border border-white/10 bg-white/[0.035] p-7 text-center shadow-2xl shadow-black/40 sm:p-9">
          <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl border border-rose-300/15 bg-rose-400/10 text-rose-300">
            <AlertTriangle size={27} />
          </span>

          <p className="mt-5 text-[10px] font-black uppercase tracking-[0.24em] text-violet-300">
            WellPlayd
          </p>
          <h1 className="mt-2 text-2xl font-bold text-white">{title}</h1>
          <p className="mx-auto mt-3 max-w-md text-sm leading-6 text-zinc-400">
            {description}
          </p>

          {this.state.errorId && (
            <p className="mt-4 text-xs text-zinc-600">
              {isEnglish ? "Error code" : "Xəta kodu"}: {this.state.errorId}
            </p>
          )}

          <div className="mt-7 flex flex-col gap-3 sm:flex-row sm:justify-center">
            <button
              type="button"
              onClick={this.reloadPage}
              className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-bold text-white transition hover:bg-violet-500"
            >
              <RefreshCw size={16} />
              {isEnglish ? "Refresh page" : "Səhifəni yenilə"}
            </button>
            <button
              type="button"
              onClick={this.goHome}
              className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-white/10 bg-white/[0.04] px-5 text-sm font-bold text-zinc-300 transition hover:bg-white/[0.08] hover:text-white"
            >
              <Home size={16} />
              {isEnglish ? "Go home" : "Ana səhifəyə qayıt"}
            </button>
          </div>
        </section>
      </main>
    );
  }
}
