import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import {
  ArrowLeft,
  Check,
  Eye,
  EyeOff,
  Gamepad2,
  LoaderCircle,
  LockKeyhole,
  Mail,
  MailCheck,
  ShieldCheck,
  UserPlus,
  UserRound,
} from "lucide-react";
import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import {
  Link,
  Navigate,
} from "react-router-dom";
import { z } from "zod";
import { getApiErrorMessage } from "../lib/api";
import { register } from "../services/authService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../stores/authStore";

const userNamePattern = /^[a-zA-Z0-9_.]+$/;

const registerSchema = z
  .object({
    userName: z
      .string()
      .trim()
      .min(
        3,
        "İstifadəçi adı minimum 3 simvol olmalıdır.",
      )
      .max(
        30,
        "İstifadəçi adı maksimum 30 simvol ola bilər.",
      )
      .regex(
        userNamePattern,
        "Yalnız hərf, rəqəm, nöqtə və alt xətt istifadə edilə bilər.",
      ),

    email: z
      .string()
      .trim()
      .min(1, "Email ünvanını daxil et.")
      .email("Düzgün email ünvanı daxil et.")
      .max(
        128,
        "Email maksimum 128 simvol ola bilər.",
      ),

    password: z
      .string()
      .min(
        8,
        "Şifrə minimum 8 simvol olmalıdır.",
      )
      .max(
        100,
        "Şifrə maksimum 100 simvol ola bilər.",
      )
      .regex(
        /[A-Z]/,
        "Şifrədə ən azı bir böyük hərf olmalıdır.",
      )
      .regex(
        /[a-z]/,
        "Şifrədə ən azı bir kiçik hərf olmalıdır.",
      )
      .regex(
        /[0-9]/,
        "Şifrədə ən azı bir rəqəm olmalıdır.",
      ),

    confirmPassword: z
      .string()
      .min(1, "Şifrəni təkrar daxil et."),
  })
  .refine(
    (values) => {
      return (
        values.password === values.confirmPassword
      );
    },
    {
      path: ["confirmPassword"],
      message: "Şifrələr eyni deyil.",
    },
  );

type RegisterFormValues = z.infer<
  typeof registerSchema
>;

interface PasswordRuleProps {
  isValid: boolean;
  label: string;
}

function PasswordRule({
  isValid,
  label,
}: PasswordRuleProps) {
  return (
    <div
      className={`flex items-center gap-2 text-xs ${
        isValid
          ? "text-emerald-400"
          : "text-zinc-600"
      }`}
    >
      <span
        className={`flex h-4 w-4 items-center justify-center rounded-full border ${
          isValid
            ? "border-emerald-400/30 bg-emerald-500/10"
            : "border-white/10"
        }`}
      >
        {isValid && <Check size={10} />}
      </span>

      {label}
    </div>
  );
}

export function RegisterPage() {
  const [isPasswordVisible, setIsPasswordVisible] =
    useState(false);

  const [
    isConfirmPasswordVisible,
    setIsConfirmPasswordVisible,
  ] = useState(false);

  const accessToken = useAuthStore(
    (state) => state.accessToken,
  );

  const expiresAtUtc = useAuthStore(
    (state) => state.expiresAtUtc,
  );

  const form = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),

    defaultValues: {
      userName: "",
      email: "",
      password: "",
      confirmPassword: "",
    },

    mode: "onTouched",
  });

  const passwordValue =
    useWatch({
      control: form.control,
      name: "password",
    }) ?? "";

  const registerMutation = useMutation({
    mutationFn: register,
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

  if (registerMutation.isSuccess) {
    const registeredEmail =
      registerMutation.variables.email.trim();

    return (
      <main className="relative flex min-h-screen items-center justify-center overflow-hidden wellplayd-page-bg px-5 py-10 text-zinc-100">
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_20%_20%,rgba(124,58,237,0.20),transparent_30%),radial-gradient(circle_at_85%_75%,rgba(217,70,239,0.10),transparent_28%)]" />

        <div className="relative w-full max-w-lg rounded-3xl border border-white/[0.08] bg-[#101015]/95 p-7 shadow-2xl shadow-black/40 sm:p-9">
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

          <span className="mt-8 flex h-14 w-14 items-center justify-center rounded-2xl bg-emerald-500/10 text-emerald-300">
            <MailCheck size={27} />
          </span>

          <p className="mt-6 text-xs font-bold uppercase tracking-[0.23em] text-emerald-400">
            Qeydiyyat tamamlandı
          </p>

          <h1 className="mt-3 text-3xl font-bold tracking-tight text-white">
            Email ünvanını təsdiqlə
          </h1>

          <p className="mt-4 text-sm leading-7 text-zinc-400">
            {registerMutation.data.message}
          </p>

          <div className="mt-6 rounded-2xl border border-white/[0.07] bg-white/[0.03] px-4 py-3">
            <p className="text-xs text-zinc-600">
              Təsdiq emaili göndərilən ünvan
            </p>

            <p className="mt-1 break-all text-sm font-semibold text-zinc-200">
              {registeredEmail}
            </p>
          </div>

          <div className="mt-7 grid gap-3 sm:grid-cols-2">
            <Link
              to="/login"
              className="flex h-12 items-center justify-center rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white transition hover:bg-violet-500"
            >
              Daxil ol
            </Link>

            <Link
              to={`/resend-email-confirmation?email=${encodeURIComponent(registeredEmail)}`}
              className="flex h-12 items-center justify-center rounded-xl border border-white/[0.09] bg-white/[0.04] px-5 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.08] hover:text-white"
            >
              Emaili yenidən göndər
            </Link>
          </div>
        </div>
      </main>
    );
  }

  const passwordRequirements = {
    hasMinimumLength: passwordValue.length >= 8,
    hasUppercase: /[A-Z]/.test(passwordValue),
    hasLowercase: /[a-z]/.test(passwordValue),
    hasDigit: /[0-9]/.test(passwordValue),
  };

  const registerError = registerMutation.isError
    ? getApiErrorMessage(
        registerMutation.error,
        "Qeydiyyatı tamamlamaq mümkün olmadı.",
      )
    : null;

  function handleRegister(
    values: RegisterFormValues,
  ) {
    registerMutation.mutate({
      userName: values.userName.trim(),
      email: values.email.trim(),
      password: values.password,
      confirmPassword: values.confirmPassword,
    });
  }

  return (
    <main className="relative min-h-screen overflow-hidden wellplayd-page-bg text-zinc-100">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_20%,rgba(124,58,237,0.20),transparent_30%),radial-gradient(circle_at_90%_80%,rgba(217,70,239,0.11),transparent_28%)]" />

      <div className="pointer-events-none absolute inset-0 opacity-[0.025] [background-image:linear-gradient(to_right,#fff_1px,transparent_1px),linear-gradient(to_bottom,#fff_1px,transparent_1px)] [background-size:50px_50px]" />

      <div className="relative mx-auto grid min-h-screen max-w-7xl lg:grid-cols-[0.9fr_1.1fr]">
        <section className="hidden flex-col justify-between border-r border-white/[0.06] px-12 py-10 lg:flex">
          <Link
            to="/"
            className="inline-flex w-fit items-center gap-3"
          >
            <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500 to-fuchsia-600 shadow-lg shadow-violet-950/50">
              <Gamepad2 size={22} />
            </span>

            <span>
              <span className="block text-xl font-bold tracking-tight text-white">
                Ludora
              </span>

              <span className="block text-[10px] font-semibold uppercase tracking-[0.25em] text-zinc-500">
                Play. Track. Discuss.
              </span>
            </span>
          </Link>

          <div className="max-w-lg">
            <span className="inline-flex items-center gap-2 rounded-full border border-violet-400/20 bg-violet-500/10 px-3 py-1.5 text-xs font-semibold text-violet-200">
              <UserPlus size={14} />
              Ludora icmasına qoşul
            </span>

            <h1 className="mt-6 text-5xl font-bold leading-[1.08] tracking-[-0.04em] text-white">
              Oyun tarixçəni yaratmağa bu gün başla.
            </h1>

            <p className="mt-5 text-base leading-8 text-zinc-400">
              Oynadığın oyunları qeyd et, fərdi
              kolleksiyalar yarat və icmadakı oyunçularla
              müzakirələr apar.
            </p>

            <div className="mt-10 space-y-4">
              <div className="flex items-start gap-4 rounded-2xl border border-white/[0.06] bg-white/[0.025] p-4">
                <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                  <Gamepad2 size={19} />
                </span>

                <div>
                  <h2 className="text-sm font-semibold text-zinc-200">
                    Oyunlarını izləmə siyahısına əlavə et
                  </h2>

                  <p className="mt-1 text-sm leading-6 text-zinc-500">
                    Oynayıram, oynayacağam, bitirdim və
                    yarımçıq qoydum statuslarını istifadə et.
                  </p>
                </div>
              </div>

              <div className="flex items-start gap-4 rounded-2xl border border-white/[0.06] bg-white/[0.025] p-4">
                <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-fuchsia-500/10 text-fuchsia-300">
                  <UserRound size={19} />
                </span>

                <div>
                  <h2 className="text-sm font-semibold text-zinc-200">
                    Şəxsi profilini qur
                  </h2>

                  <p className="mt-1 text-sm leading-6 text-zinc-500">
                    Profil şəkli və bio əlavə et, digər
                    oyunçuları izlə.
                  </p>
                </div>
              </div>

              <div className="flex items-start gap-4 rounded-2xl border border-white/[0.06] bg-white/[0.025] p-4">
                <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-emerald-500/10 text-emerald-300">
                  <ShieldCheck size={19} />
                </span>

                <div>
                  <h2 className="text-sm font-semibold text-zinc-200">
                    Məxfilik sənə bağlıdır
                  </h2>

                  <p className="mt-1 text-sm leading-6 text-zinc-500">
                    Profilini public və ya private olaraq
                    idarə et.
                  </p>
                </div>
              </div>
            </div>
          </div>

          <p className="text-xs text-zinc-600">
            © {new Date().getFullYear()} Ludora
          </p>
        </section>

        <section className="flex min-h-screen items-center justify-center px-5 py-10 sm:px-8 lg:px-14">
          <div className="w-full max-w-lg">
            <div className="mb-8 flex items-center justify-between lg:hidden">
              <Link
                to="/"
                className="flex items-center gap-2.5"
              >
                <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500 to-fuchsia-600">
                  <Gamepad2 size={20} />
                </span>

                <span className="font-bold text-white">
                  Ludora
                </span>
              </Link>

              <Link
                to="/"
                className="flex h-10 w-10 items-center justify-center rounded-xl border border-white/10 bg-white/5 text-zinc-400"
                aria-label="Ana səhifəyə qayıt"
              >
                <ArrowLeft size={19} />
              </Link>
            </div>

            <div>
              <p className="text-xs font-bold uppercase tracking-[0.23em] text-violet-400">
                Hesab yarat
              </p>

              <h2 className="mt-3 text-3xl font-bold tracking-tight text-white">
                Ludora-ya qoşul
              </h2>

              <p className="mt-3 text-sm leading-6 text-zinc-500">
                Yeni hesabını yaratmaq üçün məlumatlarını
                daxil et.
              </p>
            </div>

            <form
              onSubmit={form.handleSubmit(handleRegister)}
              className="mt-8 space-y-5"
              noValidate
            >
              <div>
                <label
                  htmlFor="register-user-name"
                  className="mb-2 block text-sm font-medium text-zinc-300"
                >
                  İstifadəçi adı
                </label>

                <div className="relative">
                  <UserRound
                    size={18}
                    className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600"
                  />

                  <input
                    id="register-user-name"
                    type="text"
                    autoComplete="username"
                    placeholder="İstifadəçi adın"
                    {...form.register("userName")}
                    className="h-12 w-full rounded-xl border border-white/[0.08] bg-white/[0.035] pl-11 pr-4 text-sm text-zinc-100 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/50 focus:bg-white/[0.055] focus:ring-4 focus:ring-violet-500/10"
                  />
                </div>

                {form.formState.errors.userName && (
                  <p className="mt-2 text-xs text-red-300">
                    {
                      form.formState.errors.userName
                        .message
                    }
                  </p>
                )}
              </div>

              <div>
                <label
                  htmlFor="register-email"
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
                    id="register-email"
                    type="email"
                    autoComplete="email"
                    placeholder="email@example.com"
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

              <div>
                <label
                  htmlFor="register-password"
                  className="mb-2 block text-sm font-medium text-zinc-300"
                >
                  Şifrə
                </label>

                <div className="relative">
                  <LockKeyhole
                    size={18}
                    className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600"
                  />

                  <input
                    id="register-password"
                    type={
                      isPasswordVisible
                        ? "text"
                        : "password"
                    }
                    autoComplete="new-password"
                    placeholder="Şifrən"
                    {...form.register("password")}
                    className="h-12 w-full rounded-xl border border-white/[0.08] bg-white/[0.035] pl-11 pr-12 text-sm text-zinc-100 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/50 focus:bg-white/[0.055] focus:ring-4 focus:ring-violet-500/10"
                  />

                  <button
                    type="button"
                    onClick={() => {
                      setIsPasswordVisible(
                        (current) => !current,
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

                <div className="mt-3 grid gap-2 sm:grid-cols-2">
                  <PasswordRule
                    isValid={
                      passwordRequirements
                        .hasMinimumLength
                    }
                    label="Minimum 8 simvol"
                  />

                  <PasswordRule
                    isValid={
                      passwordRequirements.hasUppercase
                    }
                    label="Bir böyük hərf"
                  />

                  <PasswordRule
                    isValid={
                      passwordRequirements.hasLowercase
                    }
                    label="Bir kiçik hərf"
                  />

                  <PasswordRule
                    isValid={
                      passwordRequirements.hasDigit
                    }
                    label="Bir rəqəm"
                  />
                </div>

                {form.formState.errors.password && (
                  <p className="mt-2 text-xs text-red-300">
                    {
                      form.formState.errors.password
                        .message
                    }
                  </p>
                )}
              </div>

              <div>
                <label
                  htmlFor="register-confirm-password"
                  className="mb-2 block text-sm font-medium text-zinc-300"
                >
                  Şifrəni təkrarla
                </label>

                <div className="relative">
                  <LockKeyhole
                    size={18}
                    className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600"
                  />

                  <input
                    id="register-confirm-password"
                    type={
                      isConfirmPasswordVisible
                        ? "text"
                        : "password"
                    }
                    autoComplete="new-password"
                    placeholder="Şifrəni yenidən daxil et"
                    {...form.register(
                      "confirmPassword",
                    )}
                    className="h-12 w-full rounded-xl border border-white/[0.08] bg-white/[0.035] pl-11 pr-12 text-sm text-zinc-100 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/50 focus:bg-white/[0.055] focus:ring-4 focus:ring-violet-500/10"
                  />

                  <button
                    type="button"
                    onClick={() => {
                      setIsConfirmPasswordVisible(
                        (current) => !current,
                      );
                    }}
                    className="absolute right-3 top-1/2 flex h-8 w-8 -translate-y-1/2 items-center justify-center rounded-lg text-zinc-600 transition hover:bg-white/5 hover:text-zinc-300"
                    aria-label={
                      isConfirmPasswordVisible
                        ? "Şifrəni gizlət"
                        : "Şifrəni göstər"
                    }
                  >
                    {isConfirmPasswordVisible ? (
                      <EyeOff size={17} />
                    ) : (
                      <Eye size={17} />
                    )}
                  </button>
                </div>

                {form.formState.errors
                  .confirmPassword && (
                  <p className="mt-2 text-xs text-red-300">
                    {
                      form.formState.errors
                        .confirmPassword.message
                    }
                  </p>
                )}
              </div>

              {registerError && (
                <div
                  role="alert"
                  className="rounded-xl border border-red-400/15 bg-red-500/[0.07] px-4 py-3 text-sm leading-6 text-red-200"
                >
                  {registerError}
                </div>
              )}

              <button
                type="submit"
                disabled={registerMutation.isPending}
                className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white shadow-lg shadow-violet-950/40 transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {registerMutation.isPending ? (
                  <>
                    <LoaderCircle
                      size={18}
                      className="animate-spin"
                    />

                    Hesab yaradılır...
                  </>
                ) : (
                  <>
                    <UserPlus size={18} />
                    Qeydiyyatı tamamla
                  </>
                )}
              </button>
            </form>

            <div className="mt-6 text-center text-sm text-zinc-500">
              Artıq hesabın var?

              <Link
                to="/login"
                className="ml-2 font-semibold text-violet-300 transition hover:text-violet-200"
              >
                Daxil ol
              </Link>
            </div>

            <Link
              to="/"
              className="mx-auto mt-6 flex w-fit items-center gap-2 text-sm text-zinc-500 transition hover:text-white"
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