import { useMutation } from "@tanstack/react-query";
import { AlertTriangle, CheckCircle2, Eye, EyeOff, LoaderCircle, LockKeyhole } from "lucide-react";
import { useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { BrandLogo } from "../components/common/BrandLogo";
import { getApiErrorMessage } from "../lib/api";
import { resetPassword } from "../services/authService";

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const email = searchParams.get("email")?.trim() ?? "";
  const token = searchParams.get("token")?.trim() ?? "";
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [visible, setVisible] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);
  const mutation = useMutation({ mutationFn: resetPassword });
  const linkIsValid = Boolean(email && token);

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setLocalError(null);
    if (newPassword.length < 8) {
      setLocalError("Yeni şifrə ən azı 8 simvol olmalıdır.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setLocalError("Şifrələr eyni deyil.");
      return;
    }
    mutation.mutate({ email, token, newPassword, confirmNewPassword: confirmPassword });
  }

  return (
    <main className="relative flex min-h-screen items-center justify-center overflow-hidden wellplayd-page-bg px-5 py-10 text-zinc-100">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_20%,rgba(124,58,237,0.20),transparent_30%),radial-gradient(circle_at_90%_80%,rgba(217,70,239,0.10),transparent_28%)]" />
      <section className="relative w-full max-w-md rounded-3xl border border-white/[0.08] bg-[#101015]/95 p-7 shadow-2xl shadow-black/40 sm:p-9">
        <Link to="/" className="inline-flex items-center gap-3">
          <BrandLogo className="h-10 w-12" />
          <span className="font-bold text-white">WellPlayd</span>
        </Link>

        <span className={`mt-8 flex h-14 w-14 items-center justify-center rounded-2xl ${mutation.isSuccess ? "bg-emerald-500/10 text-emerald-300" : linkIsValid ? "bg-violet-500/10 text-violet-300" : "bg-red-500/10 text-red-300"}`}>
          {mutation.isSuccess ? <CheckCircle2 size={27} /> : linkIsValid ? <LockKeyhole size={27} /> : <AlertTriangle size={27} />}
        </span>

        <p className="mt-6 text-xs font-bold uppercase tracking-[0.23em] text-violet-400">Şifrə yeniləmə</p>
        <h1 className="mt-3 text-3xl font-bold tracking-tight text-white">
          {mutation.isSuccess ? "Şifrə yeniləndi" : "Yeni şifrə yarat"}
        </h1>

        {!linkIsValid ? (
          <div className="mt-6">
            <p className="text-sm leading-7 text-zinc-400">Şifrə yeniləmə linki natamamdır. Yeni link tələb et.</p>
            <Link to="/forgot-password" className="mt-5 flex h-12 items-center justify-center rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white">Yeni link istə</Link>
          </div>
        ) : mutation.isSuccess ? (
          <div className="mt-6">
            <p className="text-sm leading-7 text-emerald-200">{mutation.data.message}</p>
            <Link to="/login" className="mt-5 flex h-12 items-center justify-center rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white">Yeni şifrə ilə daxil ol</Link>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="mt-7 space-y-4">
            {[{ label: "Yeni şifrə", value: newPassword, setValue: setNewPassword }, { label: "Yeni şifrəni təkrarla", value: confirmPassword, setValue: setConfirmPassword }].map((field) => (
              <label key={field.label} className="block">
                <span className="mb-2 block text-sm font-medium text-zinc-300">{field.label}</span>
                <span className="relative block">
                  <LockKeyhole size={18} className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600" />
                  <input
                    type={visible ? "text" : "password"}
                    autoComplete="new-password"
                    value={field.value}
                    onChange={(event) => field.setValue(event.target.value)}
                    className="h-12 w-full rounded-xl border border-white/[0.08] bg-white/[0.035] pl-11 pr-12 text-sm text-zinc-100 outline-none transition focus:border-violet-400/50 focus:ring-4 focus:ring-violet-500/10"
                  />
                  <button type="button" onClick={() => setVisible((current) => !current)} className="absolute right-3 top-1/2 -translate-y-1/2 text-zinc-600 hover:text-white" aria-label="Şifrə görünüşünü dəyiş">
                    {visible ? <EyeOff size={18} /> : <Eye size={18} />}
                  </button>
                </span>
              </label>
            ))}

            {(localError || mutation.isError) && (
              <div className="rounded-xl border border-red-400/15 bg-red-500/[0.06] px-4 py-3 text-sm text-red-200">
                {localError ?? getApiErrorMessage(mutation.error, "Şifrə yenilənmədi.")}
              </div>
            )}

            <button type="submit" disabled={mutation.isPending} className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:opacity-50">
              {mutation.isPending && <LoaderCircle size={18} className="animate-spin" />}
              Şifrəni yenilə
            </button>
          </form>
        )}
      </section>
    </main>
  );
}
