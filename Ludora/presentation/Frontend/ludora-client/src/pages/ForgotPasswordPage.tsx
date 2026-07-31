import { useMutation } from "@tanstack/react-query";
import { ArrowLeft, CheckCircle2, LoaderCircle, Mail, KeyRound } from "lucide-react";
import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { BrandLogo } from "../components/common/BrandLogo";
import { getApiErrorMessage } from "../lib/api";
import { forgotPassword } from "../services/authService";

export function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const mutation = useMutation({ mutationFn: forgotPassword });

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const normalized = email.trim();
    if (!normalized) return;
    mutation.mutate({ email: normalized });
  }

  return (
    <main className="relative flex min-h-screen items-center justify-center overflow-hidden wellplayd-page-bg px-5 py-10 text-zinc-100">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_20%,rgba(124,58,237,0.20),transparent_30%),radial-gradient(circle_at_90%_80%,rgba(217,70,239,0.10),transparent_28%)]" />
      <section className="relative w-full max-w-md rounded-3xl border border-white/[0.08] bg-[#101015]/95 p-7 shadow-2xl shadow-black/40 sm:p-9">
        <Link to="/" className="inline-flex items-center gap-3">
          <BrandLogo className="h-10 w-12" />
          <span className="font-bold text-white">WellPlayd</span>
        </Link>

        <span className="mt-8 flex h-14 w-14 items-center justify-center rounded-2xl bg-violet-500/10 text-violet-300">
          {mutation.isSuccess ? <CheckCircle2 size={27} /> : <KeyRound size={27} />}
        </span>

        <p className="mt-6 text-xs font-bold uppercase tracking-[0.23em] text-violet-400">Hesabın bərpası</p>
        <h1 className="mt-3 text-3xl font-bold tracking-tight text-white">Şifrəni unutmusansa</h1>
        <p className="mt-4 text-sm leading-7 text-zinc-400">
          Hesabına bağlı email ünvanını daxil et. Uyğun hesab varsa, təhlükəsiz şifrə yeniləmə linki göndəriləcək.
        </p>

        {mutation.isSuccess ? (
          <div className="mt-7">
            <div className="rounded-2xl border border-emerald-400/15 bg-emerald-500/[0.06] px-4 py-4 text-sm leading-6 text-emerald-200">
              {mutation.data.message}
            </div>
            <Link to="/login" className="mt-4 flex h-12 items-center justify-center rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white transition hover:bg-violet-500">
              Giriş səhifəsinə qayıt
            </Link>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="mt-7 space-y-4">
            <label className="block">
              <span className="mb-2 block text-sm font-medium text-zinc-300">Email</span>
              <span className="relative block">
                <Mail size={18} className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600" />
                <input
                  type="email"
                  autoComplete="email"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="email@example.com"
                  className="h-12 w-full rounded-xl border border-white/[0.08] bg-white/[0.035] pl-11 pr-4 text-sm text-zinc-100 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/50 focus:ring-4 focus:ring-violet-500/10"
                />
              </span>
            </label>

            {mutation.isError && (
              <div className="rounded-xl border border-red-400/15 bg-red-500/[0.06] px-4 py-3 text-sm text-red-200">
                {getApiErrorMessage(mutation.error, "Sorğunu göndərmək mümkün olmadı.")}
              </div>
            )}

            <button
              type="submit"
              disabled={mutation.isPending || !email.trim()}
              className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {mutation.isPending && <LoaderCircle size={18} className="animate-spin" />}
              Yeniləmə linki göndər
            </button>
          </form>
        )}

        <Link to="/login" className="mt-4 flex items-center justify-center gap-2 text-sm font-semibold text-zinc-500 transition hover:text-white">
          <ArrowLeft size={16} /> Giriş səhifəsinə qayıt
        </Link>
      </section>
    </main>
  );
}
