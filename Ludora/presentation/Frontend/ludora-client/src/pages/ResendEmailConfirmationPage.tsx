import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import {
  ArrowLeft,
  Check,
  Gamepad2,
  LoaderCircle,
  Mail,
  Send,
} from "lucide-react";
import { useForm } from "react-hook-form";
import {
  Link,
  useSearchParams,
} from "react-router-dom";
import { z } from "zod";
import { getApiErrorMessage } from "../lib/api";
import { resendEmailConfirmation } from "../services/authService";

const resendSchema = z.object({
  email: z
    .string()
    .trim()
    .min(1, "Email ünvanını daxil et.")
    .email("Düzgün email ünvanı daxil et.")
    .max(128, "Email maksimum 128 simvol ola bilər."),
});

type ResendFormValues = z.infer<
  typeof resendSchema
>;

export function ResendEmailConfirmationPage() {
  const [searchParams] = useSearchParams();

  const form = useForm<ResendFormValues>({
    resolver: zodResolver(resendSchema),
    defaultValues: {
      email: searchParams.get("email") ?? "",
    },
  });

  const resendMutation = useMutation({
    mutationFn: resendEmailConfirmation,
  });

  function handleSubmit(values: ResendFormValues) {
    resendMutation.mutate({
      email: values.email.trim(),
    });
  }

  const errorMessage = resendMutation.isError
    ? getApiErrorMessage(
        resendMutation.error,
        "Təsdiq emailini göndərmək mümkün olmadı.",
      )
    : null;

  return (
    <main className="relative flex min-h-screen items-center justify-center overflow-hidden wellplayd-page-bg px-5 py-10 text-zinc-100">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_20%,rgba(124,58,237,0.20),transparent_30%),radial-gradient(circle_at_90%_80%,rgba(217,70,239,0.10),transparent_28%)]" />

      <div className="relative w-full max-w-md rounded-3xl border border-white/[0.08] bg-[#101015]/95 p-7 shadow-2xl shadow-black/40 sm:p-9">
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

        <span className="mt-8 flex h-12 w-12 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
          <Send size={22} />
        </span>

        <p className="mt-6 text-xs font-bold uppercase tracking-[0.23em] text-violet-400">
          Email təsdiqi
        </p>

        <h1 className="mt-3 text-3xl font-bold tracking-tight text-white">
          Təsdiq linkini yenidən göndər
        </h1>

        <p className="mt-3 text-sm leading-6 text-zinc-500">
          Qeydiyyatda istifadə etdiyin email ünvanını
          daxil et.
        </p>

        <form
          onSubmit={form.handleSubmit(handleSubmit)}
          className="mt-8 space-y-5"
          noValidate
        >
          <div>
            <label
              htmlFor="resend-email"
              className="mb-2 block text-sm font-medium text-zinc-300"
            >
              Email
            </label>

            <div className="relative">
              <Mail
                size={18}
                className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600"
              />

              <input
                id="resend-email"
                type="email"
                autoComplete="email"
                placeholder="omar@email.com"
                {...form.register("email")}
                className="h-12 w-full rounded-xl border border-white/[0.08] bg-white/[0.035] pl-11 pr-4 text-sm text-zinc-100 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/50 focus:bg-white/[0.055] focus:ring-4 focus:ring-violet-500/10"
              />
            </div>

            {form.formState.errors.email && (
              <p className="mt-2 text-xs text-red-300">
                {form.formState.errors.email.message}
              </p>
            )}
          </div>

          {resendMutation.isSuccess && (
            <div className="flex items-start gap-3 rounded-xl border border-emerald-400/15 bg-emerald-500/[0.07] px-4 py-3 text-sm leading-6 text-emerald-200">
              <Check
                size={18}
                className="mt-0.5 shrink-0"
              />

              <span>
                {resendMutation.data.message}
              </span>
            </div>
          )}

          {errorMessage && (
            <div className="rounded-xl border border-red-400/15 bg-red-500/[0.07] px-4 py-3 text-sm leading-6 text-red-200">
              {errorMessage}
            </div>
          )}

          <button
            type="submit"
            disabled={resendMutation.isPending}
            className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {resendMutation.isPending && (
              <LoaderCircle
                size={18}
                className="animate-spin"
              />
            )}

            Təsdiq emailini göndər
          </button>
        </form>

        <Link
          to="/login"
          className="mx-auto mt-6 flex w-fit items-center gap-2 text-sm text-zinc-500 transition hover:text-white"
        >
          <ArrowLeft size={16} />
          Giriş səhifəsinə qayıt
        </Link>
      </div>
    </main>
  );
}
