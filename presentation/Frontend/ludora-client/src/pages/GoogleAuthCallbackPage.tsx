import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, LoaderCircle, ShieldCheck } from "lucide-react";
import { useEffect, useMemo } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { z } from "zod";
import { BrandLogo } from "../components/common/BrandLogo";
import { GoogleIcon } from "../components/auth/GoogleIcon";
import { useI18n } from "../i18n/useI18n";
import { getApiErrorMessage } from "../lib/api";
import {
  completeGoogleRegistration,
  exchangeGoogleAuthCode,
} from "../services/authService";
import { useAuthStore } from "../stores/authStore";

function createCompletionSchema(language: "az" | "en") {
  return z.object({
    userName: z
      .string()
      .trim()
      .min(
        3,
        language === "en"
          ? "Username must be at least 3 characters."
          : "İstifadəçi adı minimum 3 simvol olmalıdır.",
      )
      .max(
        30,
        language === "en"
          ? "Username can be at most 30 characters."
          : "İstifadəçi adı maksimum 30 simvol ola bilər.",
      )
      .regex(
        /^[a-zA-Z0-9_.]+$/,
        language === "en"
          ? "Only letters, numbers, dots, and underscores are allowed."
          : "Yalnız hərf, rəqəm, nöqtə və alt xətt istifadə edilə bilər.",
      ),
    acceptedTermsAndPrivacy: z.boolean().refine(Boolean, {
      message:
        language === "en"
          ? "You must accept the Terms of Use and Privacy Policy."
          : "İstifadə şərtləri və məxfilik siyasəti ilə razılaşmalısan.",
    }),
    useGoogleProfileImage: z.boolean(),
  });
}

type CompletionValues = z.infer<ReturnType<typeof createCompletionSchema>>;

const errorMessages: Record<string, { az: string; en: string }> = {
  google_cancelled: {
    az: "Google ilə giriş ləğv edildi.",
    en: "Google sign-in was cancelled.",
  },
  google_auth_failed: {
    az: "Google hesabından giriş məlumatı alınmadı.",
    en: "Google account information could not be received.",
  },
  google_identity_missing: {
    az: "Google hesabının identifikasiyası tamamlanmadı.",
    en: "Google account identification could not be completed.",
  },
  google_auth_rejected: {
    az: "Bu Google hesabı ilə girişə icazə verilmədi.",
    en: "Sign-in with this Google account was rejected.",
  },
};

export function GoogleAuthCallbackPage() {
  const { language } = useI18n();
  const completionSchema = useMemo(
    () => createCompletionSchema(language),
    [language],
  );
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const setSession = useAuthStore((state) => state.setSession);
  const code = searchParams.get("code") ?? "";
  const errorCode = searchParams.get("error");

  useEffect(() => {
    if (!code && !errorCode) {
      return;
    }

    window.history.replaceState(
      window.history.state,
      document.title,
      "/auth/google/callback",
    );
  }, [code, errorCode]);

  const exchangeQuery = useQuery({
    queryKey: ["auth", "google", "exchange", code],
    queryFn: () => exchangeGoogleAuthCode({ code }),
    enabled: Boolean(code) && !errorCode,
    retry: false,
    staleTime: Infinity,
  });

  const form = useForm<CompletionValues>({
    resolver: zodResolver(completionSchema),
    defaultValues: {
      userName: "",
      acceptedTermsAndPrivacy: false,
      useGoogleProfileImage: true,
    },
  });

  useEffect(() => {
    if (!exchangeQuery.data?.requiresProfileCompletion) {
      return;
    }

    form.reset({
      userName: exchangeQuery.data.suggestedUserName ?? "",
      acceptedTermsAndPrivacy: false,
      useGoogleProfileImage: true,
    });
  }, [exchangeQuery.data, form]);

  useEffect(() => {
    const response = exchangeQuery.data;
    if (!response?.session) {
      return;
    }

    queryClient.clear();
    setSession(response.session);
    navigate(response.returnPath || "/", { replace: true });
  }, [exchangeQuery.data, navigate, queryClient, setSession]);

  const completionMutation = useMutation({
    mutationFn: completeGoogleRegistration,
    onSuccess: (response) => {
      if (!response.session) {
        return;
      }
      queryClient.clear();
      setSession(response.session);
      navigate(response.returnPath || "/", { replace: true });
    },
  });

  const visibleError = errorCode
    ? (errorMessages[errorCode]?.[language] ??
      (language === "en"
        ? "Google sign-in could not be completed."
        : "Google ilə giriş tamamlanmadı."))
    : exchangeQuery.isError
      ? getApiErrorMessage(
          exchangeQuery.error,
          language === "en"
            ? "Google sign-in code is invalid or expired."
            : "Google giriş kodu etibarsızdır və ya vaxtı bitib.",
        )
      : completionMutation.isError
        ? getApiErrorMessage(
            completionMutation.error,
            language === "en"
              ? "The account could not be created."
              : "Hesab yaradıla bilmədi.",
          )
        : null;

  const exchange = exchangeQuery.data;
  const isCompletingProfile = Boolean(
    exchange?.requiresProfileCompletion && exchange.registrationToken,
  );

  return (
    <main className="relative min-h-screen overflow-hidden wellplayd-page-bg px-5 py-10 text-zinc-100">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_20%_10%,rgba(124,58,237,0.18),transparent_32%),radial-gradient(circle_at_85%_85%,rgba(34,197,94,0.08),transparent_28%)]" />
      <div className="relative mx-auto flex min-h-[calc(100vh-5rem)] max-w-lg items-center justify-center">
        <section className="wp-glass-surface w-full rounded-3xl p-6 backdrop-blur-xl sm:p-8">
          <Link to="/" className="mx-auto flex w-fit items-center gap-3">
            <BrandLogo className="h-10 w-12" />
            <span className="text-lg font-bold text-[var(--wp-text-strong)]">WellPlayd</span>
          </Link>

          <div className="mt-7 flex justify-center">
            <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-white shadow-lg">
              <GoogleIcon className="h-7 w-7" />
            </span>
          </div>

          {!visibleError && !isCompletingProfile && (
            <div className="mt-6 text-center" aria-live="polite">
              <LoaderCircle className="mx-auto animate-spin text-violet-300" size={26} />
              <h1 className="mt-4 text-xl font-bold text-[var(--wp-text-strong)]">
                {language === "en" ? "Completing sign-in" : "Giriş tamamlanır"}
              </h1>
              <p className="mt-2 text-sm leading-6 text-zinc-500">
                {language === "en"
                  ? "Your Google account is being securely connected to WellPlayd."
                  : "Google hesabın təhlükəsiz şəkildə WellPlayd-ə bağlanır."}
              </p>
            </div>
          )}

          {visibleError && (
            <div className="mt-6">
              <div role="alert" className="rounded-2xl border border-red-400/15 bg-red-500/[0.07] p-4 text-sm leading-6 text-red-200">
                {visibleError}
              </div>
              <Link to="/login" className="mt-5 flex h-11 items-center justify-center rounded-xl bg-violet-600 text-sm font-semibold text-white transition hover:bg-violet-500">
                {language === "en" ? "Return to sign in" : "Giriş səhifəsinə qayıt"}
              </Link>
            </div>
          )}

          {isCompletingProfile && !visibleError && (
            <form
              className="mt-6 space-y-5"
              onSubmit={form.handleSubmit((values) => {
                completionMutation.mutate({
                  registrationToken: exchange!.registrationToken!,
                  userName: values.userName.trim(),
                  acceptedTermsAndPrivacy: values.acceptedTermsAndPrivacy,
                  useGoogleProfileImage: values.useGoogleProfileImage,
                });
              })}
              noValidate
            >
              <div className="text-center">
                <h1 className="text-2xl font-bold text-[var(--wp-text-strong)]">
                  {language === "en" ? "Complete your profile" : "Profilini tamamla"}
                </h1>
                <p className="mt-2 text-sm text-zinc-500">
                  {exchange?.email}
                </p>
              </div>

              {exchange?.pictureUrl && (
                <div className="flex justify-center">
                  <img
                    src={exchange.pictureUrl}
                    alt="Google profile"
                    referrerPolicy="no-referrer"
                    className="h-20 w-20 rounded-2xl border border-white/10 object-cover"
                  />
                </div>
              )}

              <label className="block">
                <span className="text-sm font-medium text-zinc-300">
                  {language === "en" ? "Username" : "İstifadəçi adı"}
                </span>
                <input
                  autoComplete="username"
                  {...form.register("userName")}
                  className="mt-2 h-12 w-full rounded-xl border border-white/[0.08] bg-white/[0.035] px-4 text-sm text-zinc-100 outline-none transition focus:border-violet-400/50 focus:ring-4 focus:ring-violet-500/10"
                />
                {form.formState.errors.userName && (
                  <p className="mt-2 text-xs text-red-300">
                    {form.formState.errors.userName.message}
                  </p>
                )}
              </label>

              {exchange?.pictureUrl && (
                <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-white/[0.07] bg-white/[0.025] p-3 text-sm text-zinc-400">
                  <input
                    type="checkbox"
                    {...form.register("useGoogleProfileImage")}
                    className="mt-0.5 h-4 w-4 rounded border-white/20 accent-violet-500"
                  />
                  {language === "en"
                    ? "Use my Google profile picture on WellPlayd"
                    : "Google profil şəklimi WellPlayd-də istifadə et"}
                </label>
              )}

              <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-white/[0.07] bg-white/[0.025] p-3 text-sm leading-6 text-zinc-400">
                <input
                  type="checkbox"
                  {...form.register("acceptedTermsAndPrivacy")}
                  className="mt-1 h-4 w-4 rounded border-white/20 accent-violet-500"
                />
                <span>
                  {language === "en" ? "I agree to the " : ""}
                  <Link to="/terms" target="_blank" className="font-semibold text-violet-300 hover:text-violet-200">
                    {language === "en" ? "Terms of Use" : "İstifadə şərtləri"}
                  </Link>
                  {language === "en" ? " and " : " və "}
                  <Link to="/privacy" target="_blank" className="font-semibold text-violet-300 hover:text-violet-200">
                    {language === "en" ? "Privacy Policy" : "Məxfilik siyasəti"}
                  </Link>
                  {language === "en" ? "." : " ilə razıyam."}
                </span>
              </label>
              {form.formState.errors.acceptedTermsAndPrivacy && (
                <p className="text-xs text-red-300">
                  {form.formState.errors.acceptedTermsAndPrivacy.message}
                </p>
              )}

              <button
                type="submit"
                disabled={completionMutation.isPending}
                className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-violet-600 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:opacity-60"
              >
                {completionMutation.isPending ? (
                  <LoaderCircle size={18} className="animate-spin" />
                ) : (
                  <Check size={18} />
                )}
                {language === "en" ? "Create account" : "Hesabı yarat"}
              </button>

              <p className="flex items-center justify-center gap-2 text-xs text-zinc-600">
                <ShieldCheck size={14} />
                {language === "en"
                  ? "Your Google password is never shared with WellPlayd."
                  : "Google şifrən heç vaxt WellPlayd ilə paylaşılmır."}
              </p>
            </form>
          )}
        </section>
      </div>
    </main>
  );
}
