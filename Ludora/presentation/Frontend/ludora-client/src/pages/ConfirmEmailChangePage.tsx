import { AlertTriangle, CheckCircle2, LoaderCircle, Mail } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { BrandLogo } from "../components/common/BrandLogo";
import { endServerSession, getApiErrorMessage } from "../lib/api";
import { confirmEmailChange } from "../services/settingsService";
import { useAuthStore } from "../stores/authStore";

type Status = "loading" | "success" | "error";

export function ConfirmEmailChangePage() {
  const [searchParams] = useSearchParams();
  const startedRef = useRef(false);
  const clearSession = useAuthStore((state) => state.clearSession);
  const userId = searchParams.get("userId")?.trim() ?? "";
  const newEmail = searchParams.get("newEmail")?.trim() ?? "";
  const token = searchParams.get("token")?.trim() ?? "";
  const hasCompleteRequest = Boolean(userId && newEmail && token);
  const [status, setStatus] = useState<Status>(() =>
    hasCompleteRequest ? "loading" : "error",
  );
  const [message, setMessage] = useState(() =>
    hasCompleteRequest
      ? "Email dəyişikliyi təsdiqlənir..."
      : "Email dəyişiklik linki natamamdır.",
  );

  useEffect(() => {
    if (startedRef.current) return;
    startedRef.current = true;

    if (!hasCompleteRequest) return;

    void confirmEmailChange({ userId, newEmail, token })
      .then(async (result) => {
        try {
          await endServerSession();
        } catch {
          clearSession();
        }
        setStatus("success");
        setMessage(result.message);
      })
      .catch((error: unknown) => {
        setStatus("error");
        setMessage(getApiErrorMessage(error, "Email ünvanı dəyişdirilə bilmədi. Linkin vaxtı bitmiş ola bilər."));
      });
  }, [clearSession, hasCompleteRequest, newEmail, token, userId]);

  const loading = status === "loading";
  const success = status === "success";

  return (
    <main className="relative flex min-h-screen items-center justify-center overflow-hidden wellplayd-page-bg px-5 py-10 text-zinc-100">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_20%,rgba(124,58,237,0.20),transparent_30%),radial-gradient(circle_at_90%_80%,rgba(217,70,239,0.10),transparent_28%)]" />
      <section className="relative w-full max-w-md rounded-3xl border border-white/[0.08] bg-[#101015]/95 p-7 shadow-2xl shadow-black/40 sm:p-9">
        <Link to="/" className="inline-flex items-center gap-3">
          <BrandLogo className="h-10 w-12" />
          <span className="font-bold text-white">WellPlayd</span>
        </Link>
        <span className={`mt-8 flex h-14 w-14 items-center justify-center rounded-2xl ${loading ? "bg-violet-500/10 text-violet-300" : success ? "bg-emerald-500/10 text-emerald-300" : "bg-red-500/10 text-red-300"}`}>
          {loading ? <LoaderCircle size={27} className="animate-spin" /> : success ? <CheckCircle2 size={27} /> : <AlertTriangle size={27} />}
        </span>
        <p className="mt-6 text-xs font-bold uppercase tracking-[0.23em] text-violet-400">Email dəyişikliyi</p>
        <h1 className="mt-3 text-3xl font-bold tracking-tight text-white">{loading ? "Təsdiqlənir" : success ? "Email dəyişdirildi" : "Təsdiq alınmadı"}</h1>
        <p className="mt-4 text-sm leading-7 text-zinc-400">{message}</p>
        {!loading && (
          <Link to={success ? "/login" : "/settings"} className="mt-7 flex h-12 items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white transition hover:bg-violet-500">
            <Mail size={17} /> {success ? "Yeni email ilə daxil ol" : "Ayarlara qayıt"}
          </Link>
        )}
      </section>
    </main>
  );
}
