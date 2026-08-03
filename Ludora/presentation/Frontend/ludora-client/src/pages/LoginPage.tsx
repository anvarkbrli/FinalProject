import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  Eye,
  EyeOff,
  ListChecks,
  LoaderCircle,
  LockKeyhole,
  MessageSquareText,
  ShieldCheck,
  UserRound,
} from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import {
  Link,
  Navigate,
  useLocation,
  useNavigate,
} from "react-router-dom";
import { z } from "zod";
import { getApiErrorMessage } from "../lib/api";
import { login } from "../services/authService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../stores/authStore";
import { BrandLogo } from "../components/common/BrandLogo";

const loginSchema = z.object({
  emailOrUserName: z
    .string()
    .trim()
    .min(
      1,
      "Email və ya istifadəçi adını daxil et.",
    ),

  password: z
    .string()
    .min(1, "Şifrəni daxil et."),
});

type LoginFormValues = z.infer<typeof loginSchema>;

const featureItems = [
  {
    icon: ListChecks,
    title: "Oyunlarını izlə",
    description:
      "Oynadığın və planlaşdırdığın oyunları bir yerdə saxla.",
  },
  {
    icon: MessageSquareText,
    title: "İcmaya qoşul",
    description:
      "Forum müzakirələrini izləyib öz fikirlərini paylaş.",
  },
  {
    icon: ShieldCheck,
    title: "Hesabınla davam et",
    description:
      "Şəxsi siyahılarına və profilinə təhlükəsiz daxil ol.",
  },
];

export function LoginPage() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const location = useLocation();

  const [isPasswordVisible, setIsPasswordVisible] =
    useState(false);

  const accessToken = useAuthStore(
    (state) => state.accessToken,
  );

  const expiresAtUtc = useAuthStore(
    (state) => state.expiresAtUtc,
  );

  const setSession = useAuthStore(
    (state) => state.setSession,
  );

  const form = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      emailOrUserName: "",
      password: "",
    },
  });

  const loginMutation = useMutation({
    mutationFn: login,

    onSuccess: (response) => {
      queryClient.clear();
      setSession(response);

      const destination =
        typeof location.state === "object" &&
        location.state !== null &&
        "from" in location.state &&
        typeof location.state.from === "string"
          ? location.state.from
          : "/";

      navigate(destination, {
        replace: true,
      });
    },
  });

  const hasValidSession = isAuthSessionValid(
    accessToken,
    expiresAtUtc,
  );

  if (hasValidSession) {
    return (
      <Navigate
        to="/"
        replace
      />
    );
  }

  function handleLogin(values: LoginFormValues) {
    loginMutation.mutate({
      emailOrUserName:
        values.emailOrUserName.trim(),
      password: values.password,
    });
  }

  const loginError = loginMutation.isError
    ? getApiErrorMessage(
        loginMutation.error,
        "Sistemə daxil olmaq mümkün olmadı.",
      )
    : null;

  const accountDeletionMessage =
    typeof location.state === "object" &&
    location.state !== null &&
    "accountDeletionMessage" in location.state &&
    typeof location.state.accountDeletionMessage === "string"
      ? location.state.accountDeletionMessage
      : null;

  const sessionMessage =
    typeof location.state === "object" &&
    location.state !== null &&
    "sessionMessage" in location.state &&
    typeof location.state.sessionMessage === "string"
      ? location.state.sessionMessage
      : null;

  return (
    <main className="relative min-h-screen overflow-hidden wellplayd-page-bg text-zinc-100">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_20%,rgba(124,58,237,0.20),transparent_30%),radial-gradient(circle_at_90%_80%,rgba(217,70,239,0.10),transparent_28%)]" />

      <div className="pointer-events-none absolute inset-0 opacity-[0.025] [background-image:linear-gradient(to_right,#fff_1px,transparent_1px),linear-gradient(to_bottom,#fff_1px,transparent_1px)] [background-size:50px_50px]" />

      <div className="relative mx-auto grid min-h-screen max-w-7xl lg:grid-cols-[1fr_0.9fr]">
        <section className="hidden flex-col justify-between border-r border-white/[0.06] px-12 py-10 lg:flex">
          <Link
            to="/"
            className="inline-flex w-fit items-center gap-3"
          >
            <BrandLogo className="h-11 w-14" />

            <span>
              <span className="block text-xl font-bold tracking-tight text-white">
                WellPlayd
              </span>

              <span className="block text-[10px] font-semibold uppercase tracking-[0.25em] text-zinc-500">
                Play. Track. Discuss.
              </span>
            </span>
          </Link>

          <div className="max-w-xl">
            <span className="inline-flex items-center gap-2 rounded-full border border-violet-400/20 bg-violet-500/10 px-3 py-1.5 text-xs font-semibold text-violet-200">
              <UserRound size={14} />
              Yenidən xoş gəldin
            </span>

            <h1 className="mt-6 text-5xl font-bold leading-[1.08] tracking-[-0.04em] text-white">
              Oyun dünyana qaldığın yerdən
              davam et.
            </h1>

            <p className="mt-5 max-w-lg text-base leading-8 text-zinc-400">
              Siyahılarına bax, oyun statuslarını
              yenilə və icmadakı son müzakirələri
              izləməyə davam et.
            </p>

            <div className="mt-10 space-y-4">
              {featureItems.map((item) => {
                const Icon = item.icon;

                return (
                  <div
                    key={item.title}
                    className="flex items-start gap-4 rounded-2xl border border-white/[0.06] bg-white/[0.025] p-4"
                  >
                    <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                      <Icon size={19} />
                    </span>

                    <div>
                      <h2 className="text-sm font-semibold text-zinc-200">
                        {item.title}
                      </h2>

                      <p className="mt-1 text-sm leading-6 text-zinc-500">
                        {item.description}
                      </p>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          <p className="text-xs text-zinc-600">
            © {new Date().getFullYear()} WellPlayd
          </p>
        </section>

        <section className="flex min-h-screen items-center justify-center px-5 py-10 sm:px-8 lg:px-14">
          <div className="w-full max-w-md">
            <div className="mb-8 flex items-center justify-between lg:hidden">
              <Link
                to="/"
                className="flex items-center gap-2.5"
              >
                <BrandLogo className="h-10 w-12" />

                <span className="font-bold text-white">
                  WellPlayd
                </span>
              </Link>

              <Link
                to="/"
                className="flex h-10 w-10 items-center justify-center rounded-xl border border-white/10 bg-white/5 text-zinc-400 transition hover:bg-white/10 hover:text-white"
                aria-label="Ana səhifəyə qayıt"
              >
                <ArrowLeft size={19} />
              </Link>
            </div>

            <div>
              <p className="text-xs font-bold uppercase tracking-[0.23em] text-violet-400">
                Hesaba giriş
              </p>

              <h2 className="mt-3 text-3xl font-bold tracking-tight text-white">
                Xoş gəldin
              </h2>

              <p className="mt-3 text-sm leading-6 text-zinc-500">
                Email, istifadəçi adı və şifrənlə
                hesabına daxil ol.
              </p>
            </div>

            {accountDeletionMessage && (
              <div className="mt-6 rounded-2xl border border-emerald-400/15 bg-emerald-500/[0.06] px-4 py-3 text-sm leading-6 text-emerald-200">
                {accountDeletionMessage}
              </div>
            )}

            {sessionMessage && (
              <div className="mt-6 rounded-2xl border border-violet-400/15 bg-violet-500/[0.06] px-4 py-3 text-sm leading-6 text-violet-200">
                {sessionMessage}
              </div>
            )}

            <form
              onSubmit={form.handleSubmit(handleLogin)}
              className="mt-8 space-y-5"
              noValidate
            >
              <div>
                <label
                  htmlFor="emailOrUserName"
                  className="mb-2 block text-sm font-medium text-zinc-300"
                >
                  Email və ya istifadəçi adı
                </label>

                <div className="relative">
                  <UserRound
                    size={18}
                    className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600"
                  />

                  <input
                    id="emailOrUserName"
                    type="text"
                    autoComplete="username"
                    placeholder="Email və ya istifadəçi adınız"
                    {...form.register(
                      "emailOrUserName",
                    )}
                    className="h-12 w-full rounded-xl border border-white/[0.08] bg-white/[0.035] pl-11 pr-4 text-sm text-zinc-100 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/50 focus:bg-white/[0.055] focus:ring-4 focus:ring-violet-500/10"
                  />
                </div>

                {form.formState.errors
                  .emailOrUserName && (
                  <p className="mt-2 text-xs text-red-300">
                    {
                      form.formState.errors
                        .emailOrUserName.message
                    }
                  </p>
                )}
              </div>

              <div>
                <div className="mb-2 flex items-center justify-between">
                  <label
                    htmlFor="password"
                    className="text-sm font-medium text-zinc-300"
                  >
                    Şifrə
                  </label>

                  <Link
                    to="/forgot-password"
                    className="text-xs font-semibold text-violet-300 transition hover:text-violet-200"
                  >
                    Şifrəni unutdum
                  </Link>
                </div>

                <div className="relative">
                  <LockKeyhole
                    size={18}
                    className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600"
                  />

                  <input
                    id="password"
                    type={
                      isPasswordVisible
                        ? "text"
                        : "password"
                    }
                    autoComplete="current-password"
                    placeholder="Şifrəniz"
                    {...form.register("password")}
                    className="h-12 w-full rounded-xl border border-white/[0.08] bg-white/[0.035] pl-11 pr-12 text-sm text-zinc-100 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/50 focus:bg-white/[0.055] focus:ring-4 focus:ring-violet-500/10"
                  />

                  <button
                    type="button"
                    onClick={() => {
                      setIsPasswordVisible(
                        (currentValue) =>
                          !currentValue,
                      );
                    }}
                    className="absolute right-3 top-1/2 flex h-8 w-8 -translate-y-1/2 items-center justify-center rounded-lg text-zinc-600 transition hover:bg-white/5 hover:text-zinc-300"
                    aria-label={
                      isPasswordVisible
                        ? "Şifrəni gizlət"
                        : "Şifrəni göstər"
                    }
                  >
                    {isPasswordVisible ? (
                      <EyeOff size={17} />
                    ) : (
                      <Eye size={17} />
                    )}
                  </button>
                </div>

                {form.formState.errors.password && (
                  <p className="mt-2 text-xs text-red-300">
                    {
                      form.formState.errors
                        .password.message
                    }
                  </p>
                )}
              </div>

              {loginError && (
                <div
                  role="alert"
                  className="rounded-xl border border-red-400/15 bg-red-500/[0.07] px-4 py-3 text-sm leading-6 text-red-200"
                >
                  {loginError}
                </div>
              )}

              <button
                type="submit"
                disabled={loginMutation.isPending}
                className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white shadow-lg shadow-violet-950/40 transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {loginMutation.isPending ? (
                  <>
                    <LoaderCircle
                      size={18}
                      className="animate-spin"
                    />
                    Daxil olunur...
                  </>
                ) : (
                  <>
                    Daxil ol
                    <ArrowLeft
                      size={17}
                      className="rotate-180"
                    />
                  </>
                )}
              </button>
            </form>

            <Link
              to="/resend-email-confirmation"
              className="mx-auto mt-5 flex w-fit items-center gap-2 text-sm text-zinc-500 transition hover:text-violet-300"
            >
              Təsdiq emailini yenidən göndər
            </Link>

            <div className="mt-6 text-center text-sm text-zinc-500">
            Hesabın yoxdur?

            <Link
                to="/register"
                className="ml-2 font-semibold text-violet-300 transition hover:text-violet-200"
            >
                Qeydiyyatdan keç
            </Link>
            </div>

            <Link
              to="/"
              className="mx-auto mt-6 hidden w-fit items-center gap-2 text-sm text-zinc-500 transition hover:text-white lg:flex"
            >
              <ArrowLeft size={16} />
              Ana səhifəyə qayıt
            </Link>
          </div>
        </section>
      </div>
    </main>
  );
}
