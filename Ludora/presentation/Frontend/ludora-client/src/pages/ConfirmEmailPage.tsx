import {
  AlertTriangle,
  CheckCircle2,
  Gamepad2,
  LoaderCircle,
  Mail,
} from "lucide-react";
import {
  useEffect,
  useRef,
  useState,
} from "react";
import {
  Link,
  useSearchParams,
} from "react-router-dom";
import { getApiErrorMessage } from "../lib/api";
import { confirmEmail } from "../services/authService";

interface ConfirmationState {
  status: "loading" | "success" | "error";
  message: string;
}

export function ConfirmEmailPage() {
  const [searchParams] = useSearchParams();
  const hasStartedRef = useRef(false);
  const [state, setState] =
    useState<ConfirmationState>({
      status: "loading",
      message: "Email təsdiqlənir...",
    });

  const userId = searchParams.get("userId")?.trim() ?? "";
  const token = searchParams.get("token")?.trim() ?? "";

  useEffect(() => {
    if (hasStartedRef.current) return;
    hasStartedRef.current = true;

    if (!userId || !token) {
      setState({
        status: "error",
        message:
          "Email təsdiq linki natamamdır. Yeni təsdiq emaili istə.",
      });
      return;
    }

    void confirmEmail({ userId, token })
      .then((result) => {
        setState({
          status: "success",
          message: result.message,
        });
      })
      .catch((error: unknown) => {
        setState({
          status: "error",
          message: getApiErrorMessage(
            error,
            "Email təsdiqlənə bilmədi. Linkin vaxtı bitmiş ola bilər.",
          ),
        });
      });
  }, [token, userId]);

  const isLoading = state.status === "loading";
  const isSuccess = state.status === "success";

  return (
    <main className="relative flex min-h-screen items-center justify-center overflow-hidden wellplayd-page-bg px-5 py-10 text-zinc-100">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_20%,rgba(124,58,237,0.20),transparent_30%),radial-gradient(circle_at_90%_80%,rgba(217,70,239,0.10),transparent_28%)]" />

      <section className="relative w-full max-w-md rounded-3xl border border-white/[0.08] bg-[#101015]/95 p-7 shadow-2xl shadow-black/40 sm:p-9">
        <Link
          to="/"
          className="inline-flex items-center gap-3"
        >
          <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500 to-fuchsia-600">
            <Gamepad2 size={20} />
          </span>

          <span className="font-bold text-white">
            Ludora
          </span>
        </Link>

        <span
          className={`mt-8 flex h-14 w-14 items-center justify-center rounded-2xl ${
            isLoading
              ? "bg-violet-500/10 text-violet-300"
              : isSuccess
                ? "bg-emerald-500/10 text-emerald-300"
                : "bg-red-500/10 text-red-300"
          }`}
        >
          {isLoading ? (
            <LoaderCircle
              size={27}
              className="animate-spin"
            />
          ) : isSuccess ? (
            <CheckCircle2 size={27} />
          ) : (
            <AlertTriangle size={27} />
          )}
        </span>

        <p className="mt-6 text-xs font-bold uppercase tracking-[0.23em] text-violet-400">
          Email təsdiqi
        </p>

        <h1 className="mt-3 text-3xl font-bold tracking-tight text-white">
          {isLoading
            ? "Təsdiqlənir"
            : isSuccess
              ? "Email təsdiqləndi"
              : "Təsdiq alınmadı"}
        </h1>

        <p className="mt-4 text-sm leading-7 text-zinc-400">
          {state.message}
        </p>

        {!isLoading && (
          <div className="mt-7 grid gap-3">
            {isSuccess ? (
              <Link
                to="/login"
                className="flex h-12 items-center justify-center rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white transition hover:bg-violet-500"
              >
                Hesaba daxil ol
              </Link>
            ) : (
              <Link
                to="/resend-email-confirmation"
                className="flex h-12 items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white transition hover:bg-violet-500"
              >
                <Mail size={17} />
                Yeni təsdiq emaili istə
              </Link>
            )}

            <Link
              to="/"
              className="flex h-12 items-center justify-center rounded-xl border border-white/[0.09] bg-white/[0.04] px-5 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.08] hover:text-white"
            >
              Ana səhifəyə qayıt
            </Link>
          </div>
        )}
      </section>
    </main>
  );
}
