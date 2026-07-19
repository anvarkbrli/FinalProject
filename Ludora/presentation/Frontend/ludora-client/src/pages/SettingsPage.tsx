import { zodResolver } from "@hookform/resolvers/zod";
import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ArrowLeft,
  Camera,
  Check,
  Gamepad2,
  KeyRound,
  LoaderCircle,
  Lock,
  Mail,
  Shield,
  User,
  UserCheck,
  UserX,
  Users,
  X,
} from "lucide-react";
import {
  useEffect,
  useRef,
  useState,
} from "react";
import { useForm, useWatch } from "react-hook-form";
import { Link } from "react-router-dom";
import { z } from "zod";
import { getApiErrorMessage } from "../lib/api";
import {
  formatRelativeDate,
  resolveAssetUrl,
} from "../lib/helpers";
import {
  acceptFollowRequest,
  getBlockedUsers,
  getCurrentUser,
  getFollowRequests,
  getUserSettings,
  rejectFollowRequest,
  requestEmailChange,
  unblockUser,
  updateProfile,
  updateProfileImage,
  updateUserSettings,
} from "../services/settingsService";
import { useAuthStore } from "../stores/authStore";
import type { CurrentUserDTO } from "../types/auth";
import type { UserSettingsDTO } from "../types/settings";

const profileSchema = z.object({
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
      /^[a-zA-Z0-9_.]+$/,
      "Yalnız hərf, rəqəm, nöqtə və alt xətt istifadə edilə bilər.",
    ),
  bio: z
    .string()
    .max(
      500,
      "Bio maksimum 500 simvol ola bilər.",
    ),
});

const emailChangeSchema = z.object({
  newEmail: z
    .string()
    .trim()
    .min(1, "Yeni email ünvanını daxil et.")
    .email("Düzgün email ünvanı daxil et.")
    .max(
      128,
      "Email maksimum 128 simvol ola bilər.",
    ),
  currentPassword: z
    .string()
    .min(1, "Cari şifrəni daxil et."),
});

type ProfileFormValues = z.infer<
  typeof profileSchema
>;

type EmailChangeFormValues = z.infer<
  typeof emailChangeSchema
>;

interface NoticeState {
  type: "success" | "error";
  message: string;
}

const allowedImageTypes = [
  "image/jpeg",
  "image/png",
  "image/webp",
];

const maximumImageSize = 5 * 1024 * 1024;

export function SettingsPage() {
  const queryClient = useQueryClient();
  const fileInputRef =
    useRef<HTMLInputElement>(null);

  const storedUser = useAuthStore(
    (state) => state.user,
  );
  const setUser = useAuthStore(
    (state) => state.setUser,
  );

  const [notice, setNotice] =
    useState<NoticeState | null>(null);
  const [selectedImage, setSelectedImage] =
    useState<File | null>(null);
  const [imagePreviewUrl, setImagePreviewUrl] =
    useState<string | null>(null);

  const profileForm = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      userName: storedUser?.userName ?? "",
      bio: storedUser?.bio ?? "",
    },
  });

  const emailChangeForm =
    useForm<EmailChangeFormValues>({
      resolver: zodResolver(emailChangeSchema),
      defaultValues: {
        newEmail: "",
        currentPassword: "",
      },
    });

  const currentUserQuery = useQuery({
    queryKey: ["current-user"],
    queryFn: getCurrentUser,
  });

  const settingsQuery = useQuery({
    queryKey: ["user-settings"],
    queryFn: getUserSettings,
  });

  const blockedUsersQuery = useQuery({
    queryKey: ["blocked-users"],
    queryFn: getBlockedUsers,
  });

  const followRequestsQuery = useQuery({
    queryKey: ["follow-requests"],
    queryFn: getFollowRequests,
  });

  const isProfileFormDirty =
    profileForm.formState.isDirty;
  const resetProfileForm = profileForm.reset;

  useEffect(() => {
    const currentUser = currentUserQuery.data;

    if (!currentUser) {
      return;
    }

    setUser(currentUser);

    if (isProfileFormDirty) {
      return;
    }

    resetProfileForm({
      userName: currentUser.userName,
      bio: currentUser.bio ?? "",
    });
  }, [
    currentUserQuery.data,
    isProfileFormDirty,
    resetProfileForm,
    setUser,
  ]);

  useEffect(() => {
    if (!imagePreviewUrl) {
      return;
    }

    return () => {
      URL.revokeObjectURL(imagePreviewUrl);
    };
  }, [imagePreviewUrl]);

  const profileMutation = useMutation({
    mutationFn: updateProfile,
    onSuccess: (updatedUser) => {
      setUser(updatedUser);
      queryClient.setQueryData(
        ["current-user"],
        updatedUser,
      );
      profileForm.reset({
        userName: updatedUser.userName,
        bio: updatedUser.bio ?? "",
      });
      setNotice({
        type: "success",
        message:
          "Profil məlumatları uğurla yeniləndi.",
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "Profil məlumatları yenilənmədi.",
        ),
      });
    },
  });

  const profileImageMutation = useMutation({
    mutationFn: updateProfileImage,
    onSuccess: (updatedUser) => {
      setUser(updatedUser);
      queryClient.setQueryData(
        ["current-user"],
        updatedUser,
      );
      setSelectedImage(null);
      setImagePreviewUrl(null);

      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }

      setNotice({
        type: "success",
        message:
          "Profil şəkli uğurla yeniləndi.",
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "Profil şəkli yenilənmədi.",
        ),
      });
    },
  });

  const settingsMutation = useMutation({
    mutationFn: updateUserSettings,
    onSuccess: (updatedSettings) => {
      queryClient.setQueryData(
        ["user-settings"],
        updatedSettings,
      );

      queryClient.setQueryData<CurrentUserDTO>(
        ["current-user"],
        (currentUser) => {
          if (!currentUser) {
            return currentUser;
          }

          const updatedUser = {
            ...currentUser,
            isPrivate: updatedSettings.isPrivate,
          };
          setUser(updatedUser);
          return updatedUser;
        },
      );

      setNotice({
        type: "success",
        message:
          "Məxfilik ayarları yeniləndi.",
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "Məxfilik ayarları yenilənmədi.",
        ),
      });
    },
  });

  const emailChangeMutation = useMutation({
    mutationFn: requestEmailChange,
    onSuccess: (result) => {
      emailChangeForm.reset();
      setNotice({
        type: "success",
        message: result.message,
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "Email dəyişiklik sorğusu göndərilmədi.",
        ),
      });
    },
  });

  const unblockMutation = useMutation({
    mutationFn: unblockUser,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ["blocked-users"],
      });
      setNotice({
        type: "success",
        message:
          "İstifadəçi blokdan çıxarıldı.",
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "İstifadəçini blokdan çıxarmaq mümkün olmadı.",
        ),
      });
    },
  });

  const acceptRequestMutation = useMutation({
    mutationFn: acceptFollowRequest,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["follow-requests"],
        }),
        queryClient.invalidateQueries({
          queryKey: ["current-user"],
        }),
      ]);
      setNotice({
        type: "success",
        message: "İzləmə sorğusu qəbul edildi.",
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "İzləmə sorğusunu qəbul etmək mümkün olmadı.",
        ),
      });
    },
  });

  const rejectRequestMutation = useMutation({
    mutationFn: rejectFollowRequest,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ["follow-requests"],
      });
      setNotice({
        type: "success",
        message: "İzləmə sorğusu rədd edildi.",
      });
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          error,
          "İzləmə sorğusunu rədd etmək mümkün olmadı.",
        ),
      });
    },
  });

  const currentUser =
    currentUserQuery.data ?? storedUser;
  const settings =
    settingsQuery.data ??
    ({
      isPrivate:
        currentUser?.isPrivate ?? false,
    } satisfies UserSettingsDTO);

  const profileImageUrl =
    imagePreviewUrl ??
    resolveAssetUrl(
      currentUser?.profileImageUrl,
    );
  const userInitial =
    currentUser?.userName
      .trim()
      .charAt(0)
      .toUpperCase() || "?";
  const bioValue =
    useWatch({
      control: profileForm.control,
      name: "bio",
    }) ?? "";

  function handleProfileSubmit(
    values: ProfileFormValues,
  ) {
    setNotice(null);
    profileMutation.mutate({
      userName: values.userName.trim(),
      bio: values.bio.trim() || null,
    });
  }

  function handleEmailChangeSubmit(
    values: EmailChangeFormValues,
  ) {
    setNotice(null);
    emailChangeMutation.mutate({
      newEmail: values.newEmail.trim(),
      currentPassword: values.currentPassword,
    });
  }

  function handleImageSelection(
    event: React.ChangeEvent<HTMLInputElement>,
  ) {
    const file = event.target.files?.[0] ?? null;
    setNotice(null);

    if (!file) {
      return;
    }

    if (!allowedImageTypes.includes(file.type)) {
      event.target.value = "";
      setNotice({
        type: "error",
        message:
          "Yalnız JPEG, PNG və WEBP şəkilləri qəbul edilir.",
      });
      return;
    }

    if (file.size > maximumImageSize) {
      event.target.value = "";
      setNotice({
        type: "error",
        message:
          "Profil şəkli maksimum 5 MB ola bilər.",
      });
      return;
    }

    setSelectedImage(file);
    setImagePreviewUrl(
      URL.createObjectURL(file),
    );
  }

  function cancelSelectedImage() {
    setSelectedImage(null);
    setImagePreviewUrl(null);

    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  }

  function handlePrivacyChange() {
    setNotice(null);
    settingsMutation.mutate({
      isPrivate: !settings.isPrivate,
    });
  }

  const isInitialLoading =
    currentUserQuery.isLoading && !currentUser;

  if (isInitialLoading) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-[#07070a] text-zinc-100">
        <div className="flex items-center gap-3 text-sm text-zinc-400">
          <LoaderCircle
            size={20}
            className="animate-spin"
          />
          Ayarlar yüklənir...
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-[#07070a] text-zinc-100">
      <header className="border-b border-white/[0.06] bg-[#09090c]/90 backdrop-blur-xl">
        <div className="mx-auto flex h-[72px] max-w-7xl items-center justify-between px-5 lg:px-8">
          <Link
            to="/"
            className="flex items-center gap-3"
          >
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500 to-fuchsia-600">
              <Gamepad2 size={20} />
            </span>
            <span className="font-bold text-white">
              Ludora
            </span>
          </Link>

          <Link
            to={
              currentUser
                ? `/users/${currentUser.userName}`
                : "/"
            }
            className="flex items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.04] px-4 py-2.5 text-sm font-medium text-zinc-300 transition hover:bg-white/[0.08] hover:text-white"
          >
            <ArrowLeft size={17} />
            Profilə qayıt
          </Link>
        </div>
      </header>

      <div className="mx-auto max-w-5xl px-5 py-12 lg:px-8">
        <div className="mb-10">
          <p className="text-xs font-bold uppercase tracking-[0.24em] text-violet-400">
            Hesab idarəetməsi
          </p>
          <h1 className="mt-3 text-3xl font-bold tracking-tight text-white sm:text-4xl">
            Ayarlar
          </h1>
          <p className="mt-3 max-w-2xl text-sm leading-7 text-zinc-500">
            Profil məlumatlarını, email ünvanını,
            məxfilik seçimlərini və istifadəçi
            əlaqələrini idarə et.
          </p>
        </div>

        {notice && (
          <div
            className={`mb-6 flex items-start gap-3 rounded-2xl border px-4 py-3 text-sm ${
              notice.type === "success"
                ? "border-emerald-400/15 bg-emerald-500/[0.07] text-emerald-200"
                : "border-red-400/15 bg-red-500/[0.07] text-red-200"
            }`}
          >
            {notice.type === "success" ? (
              <Check
                size={18}
                className="mt-0.5 shrink-0"
              />
            ) : (
              <X
                size={18}
                className="mt-0.5 shrink-0"
              />
            )}
            <span>{notice.message}</span>
          </div>
        )}

        <div className="space-y-6">
          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-6 sm:p-8">
            <div className="flex items-center gap-3">
              <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                <Camera size={20} />
              </span>
              <div>
                <h2 className="font-semibold text-white">
                  Profil şəkli
                </h2>
                <p className="mt-1 text-sm text-zinc-500">
                  JPEG, PNG və ya WEBP. Maksimum 5 MB.
                </p>
              </div>
            </div>

            <div className="mt-7 flex flex-col gap-6 sm:flex-row sm:items-center">
              <div className="flex h-28 w-28 shrink-0 items-center justify-center overflow-hidden rounded-3xl border border-white/10 bg-violet-500/10 text-3xl font-bold text-violet-300">
                {profileImageUrl ? (
                  <img
                    src={profileImageUrl}
                    alt="Profil şəkli"
                    className="h-full w-full object-cover"
                  />
                ) : (
                  userInitial
                )}
              </div>

              <div className="flex flex-wrap gap-3">
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  onChange={handleImageSelection}
                  className="hidden"
                />
                <button
                  type="button"
                  onClick={() => {
                    fileInputRef.current?.click();
                  }}
                  className="rounded-xl border border-white/[0.09] bg-white/[0.05] px-4 py-2.5 text-sm font-semibold text-zinc-200 transition hover:bg-white/[0.09]"
                >
                  Şəkil seç
                </button>

                {selectedImage && (
                  <>
                    <button
                      type="button"
                      onClick={() => {
                        profileImageMutation.mutate(
                          selectedImage,
                        );
                      }}
                      disabled={
                        profileImageMutation.isPending
                      }
                      className="flex items-center gap-2 rounded-xl bg-violet-600 px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:opacity-60"
                    >
                      {profileImageMutation.isPending && (
                        <LoaderCircle
                          size={16}
                          className="animate-spin"
                        />
                      )}
                      Şəkli yüklə
                    </button>
                    <button
                      type="button"
                      onClick={cancelSelectedImage}
                      disabled={
                        profileImageMutation.isPending
                      }
                      className="rounded-xl px-4 py-2.5 text-sm font-medium text-zinc-500 transition hover:bg-white/5 hover:text-zinc-300"
                    >
                      Ləğv et
                    </button>
                  </>
                )}
              </div>
            </div>
          </section>

          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-6 sm:p-8">
            <div className="flex items-center gap-3">
              <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-fuchsia-500/10 text-fuchsia-300">
                <User size={20} />
              </span>
              <div>
                <h2 className="font-semibold text-white">
                  Profil məlumatları
                </h2>
                <p className="mt-1 text-sm text-zinc-500">
                  İstifadəçi adını və bio hissəsini yenilə.
                </p>
              </div>
            </div>

            <form
              onSubmit={profileForm.handleSubmit(
                handleProfileSubmit,
              )}
              className="mt-7 space-y-5"
              noValidate
            >
              <div>
                <label
                  htmlFor="settings-user-name"
                  className="mb-2 block text-sm font-medium text-zinc-300"
                >
                  İstifadəçi adı
                </label>
                <input
                  id="settings-user-name"
                  type="text"
                  autoComplete="username"
                  {...profileForm.register("userName")}
                  className="h-12 w-full rounded-xl border border-white/[0.08] bg-white/[0.035] px-4 text-sm text-zinc-100 outline-none transition focus:border-violet-400/50 focus:ring-4 focus:ring-violet-500/10"
                />
                {profileForm.formState.errors
                  .userName && (
                  <p className="mt-2 text-xs text-red-300">
                    {
                      profileForm.formState.errors
                        .userName.message
                    }
                  </p>
                )}
              </div>

              <div>
                <div className="mb-2 flex items-center justify-between gap-3">
                  <label
                    htmlFor="settings-bio"
                    className="text-sm font-medium text-zinc-300"
                  >
                    Bio
                  </label>
                  <span className="text-xs text-zinc-600">
                    {bioValue.length}/500
                  </span>
                </div>
                <textarea
                  id="settings-bio"
                  rows={5}
                  placeholder="Özün haqqında qısa məlumat..."
                  {...profileForm.register("bio")}
                  className="w-full resize-none rounded-xl border border-white/[0.08] bg-white/[0.035] px-4 py-3 text-sm leading-6 text-zinc-100 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/50 focus:ring-4 focus:ring-violet-500/10"
                />
                {profileForm.formState.errors.bio && (
                  <p className="mt-2 text-xs text-red-300">
                    {
                      profileForm.formState.errors.bio
                        .message
                    }
                  </p>
                )}
              </div>

              <div className="flex justify-end">
                <button
                  type="submit"
                  disabled={
                    profileMutation.isPending ||
                    !profileForm.formState.isDirty
                  }
                  className="flex items-center gap-2 rounded-xl bg-violet-600 px-5 py-3 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {profileMutation.isPending && (
                    <LoaderCircle
                      size={17}
                      className="animate-spin"
                    />
                  )}
                  Dəyişiklikləri yadda saxla
                </button>
              </div>
            </form>
          </section>

          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-6 sm:p-8">
            <div className="flex items-center gap-3">
              <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-sky-500/10 text-sky-300">
                <Mail size={20} />
              </span>
              <div>
                <h2 className="font-semibold text-white">
                  Email ünvanı
                </h2>
                <p className="mt-1 text-sm text-zinc-500">
                  Yeni email təsdiqlənənədək cari email aktiv qalır.
                </p>
              </div>
            </div>

            <div className="mt-7 rounded-2xl border border-white/[0.06] bg-white/[0.025] p-4">
              <p className="text-xs text-zinc-600">
                Cari email
              </p>
              <div className="mt-2 flex flex-wrap items-center gap-3">
                <p className="break-all text-sm font-semibold text-zinc-200">
                  {currentUser?.email ?? ""}
                </p>
                {currentUser?.emailConfirmed && (
                  <span className="inline-flex items-center gap-1 rounded-full bg-emerald-500/10 px-2.5 py-1 text-[11px] font-semibold text-emerald-300">
                    <Check size={12} />
                    Təsdiqlənib
                  </span>
                )}
              </div>
            </div>

            <form
              onSubmit={emailChangeForm.handleSubmit(
                handleEmailChangeSubmit,
              )}
              className="mt-5 grid gap-5 md:grid-cols-2"
              noValidate
            >
              <div>
                <label
                  htmlFor="new-email"
                  className="mb-2 block text-sm font-medium text-zinc-300"
                >
                  Yeni email
                </label>
                <div className="relative">
                  <Mail
                    size={17}
                    className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600"
                  />
                  <input
                    id="new-email"
                    type="email"
                    autoComplete="email"
                    {...emailChangeForm.register(
                      "newEmail",
                    )}
                    className="h-12 w-full rounded-xl border border-white/[0.08] bg-white/[0.035] pl-11 pr-4 text-sm text-zinc-100 outline-none transition focus:border-violet-400/50 focus:ring-4 focus:ring-violet-500/10"
                  />
                </div>
                {emailChangeForm.formState.errors
                  .newEmail && (
                  <p className="mt-2 text-xs text-red-300">
                    {
                      emailChangeForm.formState.errors
                        .newEmail.message
                    }
                  </p>
                )}
              </div>

              <div>
                <label
                  htmlFor="current-password"
                  className="mb-2 block text-sm font-medium text-zinc-300"
                >
                  Cari şifrə
                </label>
                <div className="relative">
                  <KeyRound
                    size={17}
                    className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-zinc-600"
                  />
                  <input
                    id="current-password"
                    type="password"
                    autoComplete="current-password"
                    {...emailChangeForm.register(
                      "currentPassword",
                    )}
                    className="h-12 w-full rounded-xl border border-white/[0.08] bg-white/[0.035] pl-11 pr-4 text-sm text-zinc-100 outline-none transition focus:border-violet-400/50 focus:ring-4 focus:ring-violet-500/10"
                  />
                </div>
                {emailChangeForm.formState.errors
                  .currentPassword && (
                  <p className="mt-2 text-xs text-red-300">
                    {
                      emailChangeForm.formState.errors
                        .currentPassword.message
                    }
                  </p>
                )}
              </div>

              <div className="flex justify-end md:col-span-2">
                <button
                  type="submit"
                  disabled={emailChangeMutation.isPending}
                  className="flex items-center gap-2 rounded-xl bg-violet-600 px-5 py-3 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:opacity-50"
                >
                  {emailChangeMutation.isPending && (
                    <LoaderCircle
                      size={17}
                      className="animate-spin"
                    />
                  )}
                  Təsdiq linki göndər
                </button>
              </div>
            </form>
          </section>

          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-6 sm:p-8">
            <div className="flex items-center gap-3">
              <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-emerald-500/10 text-emerald-300">
                <Shield size={20} />
              </span>
              <div>
                <h2 className="font-semibold text-white">
                  Məxfilik
                </h2>
                <p className="mt-1 text-sm text-zinc-500">
                  Profilini kimlərin görə biləcəyini idarə et.
                </p>
              </div>
            </div>

            <div className="mt-7 flex items-center justify-between gap-5 rounded-2xl border border-white/[0.06] bg-white/[0.025] p-5">
              <div>
                <p className="text-sm font-semibold text-zinc-200">
                  Private profil
                </p>
                <p className="mt-1 max-w-xl text-sm leading-6 text-zinc-500">
                  Aktiv olduqda digər istifadəçilərin səni
                  izləməsi üçün sorğu göndərməsi tələb olunur.
                </p>
              </div>
              <button
                type="button"
                role="switch"
                aria-checked={settings.isPrivate}
                onClick={handlePrivacyChange}
                disabled={
                  settingsQuery.isLoading ||
                  settingsMutation.isPending
                }
                className={`relative h-7 w-12 shrink-0 rounded-full transition ${
                  settings.isPrivate
                    ? "bg-violet-600"
                    : "bg-zinc-700"
                } disabled:opacity-50`}
              >
                <span
                  className={`absolute top-1 h-5 w-5 rounded-full bg-white shadow transition ${
                    settings.isPrivate
                      ? "left-6"
                      : "left-1"
                  }`}
                />
              </button>
            </div>
          </section>

          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-6 sm:p-8">
            <div className="flex items-center gap-3">
              <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                <Users size={20} />
              </span>
              <div>
                <h2 className="font-semibold text-white">
                  İzləmə sorğuları
                </h2>
                <p className="mt-1 text-sm text-zinc-500">
                  Private profil üçün gələn sorğuları idarə et.
                </p>
              </div>
            </div>

            <div className="mt-7">
              {followRequestsQuery.isLoading ? (
                <div className="flex items-center gap-3 py-6 text-sm text-zinc-500">
                  <LoaderCircle
                    size={18}
                    className="animate-spin"
                  />
                  Sorğular yüklənir...
                </div>
              ) : followRequestsQuery.isError ? (
                <div className="rounded-xl border border-red-400/15 bg-red-500/[0.06] p-4 text-sm text-red-200">
                  İzləmə sorğuları yüklənmədi.
                </div>
              ) : !followRequestsQuery.data?.length ? (
                <div className="rounded-2xl border border-dashed border-white/[0.08] p-8 text-center">
                  <Users
                    size={27}
                    className="mx-auto text-zinc-700"
                  />
                  <p className="mt-3 text-sm text-zinc-500">
                    Gözləyən izləmə sorğusu yoxdur.
                  </p>
                </div>
              ) : (
                <div className="space-y-3">
                  {followRequestsQuery.data.map(
                    (request) => {
                      const requestImageUrl =
                        resolveAssetUrl(
                          request.profileImageUrl,
                        );
                      const requestIsPending =
                        acceptRequestMutation.isPending ||
                        rejectRequestMutation.isPending;

                      return (
                        <div
                          key={request.userId}
                          className="flex flex-col gap-4 rounded-2xl border border-white/[0.06] bg-white/[0.025] p-4 sm:flex-row sm:items-center sm:justify-between"
                        >
                          <Link
                            to={`/users/${request.userName}`}
                            className="flex min-w-0 items-center gap-3"
                          >
                            <div className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-violet-500/10 font-semibold text-violet-300">
                              {requestImageUrl ? (
                                <img
                                  src={requestImageUrl}
                                  alt=""
                                  className="h-full w-full object-cover"
                                />
                              ) : (
                                request.userName
                                  .charAt(0)
                                  .toUpperCase()
                              )}
                            </div>
                            <div className="min-w-0">
                              <p className="truncate text-sm font-semibold text-zinc-200">
                                {request.userName}
                              </p>
                              <p className="mt-1 text-xs text-zinc-600">
                                {formatRelativeDate(
                                  request.requestedAt,
                                )}
                              </p>
                            </div>
                          </Link>

                          <div className="flex gap-2">
                            <button
                              type="button"
                              onClick={() => {
                                acceptRequestMutation.mutate(
                                  request.userId,
                                );
                              }}
                              disabled={requestIsPending}
                              className="flex items-center gap-2 rounded-xl bg-emerald-600/90 px-4 py-2 text-xs font-semibold text-white transition hover:bg-emerald-500 disabled:opacity-50"
                            >
                              <UserCheck size={15} />
                              Qəbul et
                            </button>
                            <button
                              type="button"
                              onClick={() => {
                                rejectRequestMutation.mutate(
                                  request.userId,
                                );
                              }}
                              disabled={requestIsPending}
                              className="flex items-center gap-2 rounded-xl border border-red-400/15 bg-red-500/[0.06] px-4 py-2 text-xs font-semibold text-red-300 transition hover:bg-red-500/15 disabled:opacity-50"
                            >
                              <UserX size={15} />
                              Rədd et
                            </button>
                          </div>
                        </div>
                      );
                    },
                  )}
                </div>
              )}
            </div>
          </section>

          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-6 sm:p-8">
            <div className="flex items-center gap-3">
              <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-red-500/10 text-red-300">
                <Lock size={20} />
              </span>
              <div>
                <h2 className="font-semibold text-white">
                  Bloklanmış istifadəçilər
                </h2>
                <p className="mt-1 text-sm text-zinc-500">
                  Blokladığın hesabları gör və blokdan çıxar.
                </p>
              </div>
            </div>

            <div className="mt-7">
              {blockedUsersQuery.isLoading ? (
                <div className="flex items-center gap-3 py-6 text-sm text-zinc-500">
                  <LoaderCircle
                    size={18}
                    className="animate-spin"
                  />
                  Siyahı yüklənir...
                </div>
              ) : blockedUsersQuery.isError ? (
                <div className="rounded-xl border border-red-400/15 bg-red-500/[0.06] p-4 text-sm text-red-200">
                  Bloklanmış istifadəçilər yüklənmədi.
                </div>
              ) : !blockedUsersQuery.data?.length ? (
                <div className="rounded-2xl border border-dashed border-white/[0.08] p-8 text-center">
                  <Users
                    size={27}
                    className="mx-auto text-zinc-700"
                  />
                  <p className="mt-3 text-sm text-zinc-500">
                    Heç kimi bloklamamısan.
                  </p>
                </div>
              ) : (
                <div className="space-y-3">
                  {blockedUsersQuery.data.map(
                    (blockedUser) => {
                      const blockedImageUrl =
                        resolveAssetUrl(
                          blockedUser.profileImageUrl,
                        );

                      return (
                        <div
                          key={blockedUser.id}
                          className="flex items-center justify-between gap-4 rounded-2xl border border-white/[0.06] bg-white/[0.025] p-4"
                        >
                          <div className="flex min-w-0 items-center gap-3">
                            <div className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-red-500/10 font-semibold text-red-300">
                              {blockedImageUrl ? (
                                <img
                                  src={blockedImageUrl}
                                  alt=""
                                  className="h-full w-full object-cover"
                                />
                              ) : (
                                blockedUser.userName
                                  .charAt(0)
                                  .toUpperCase()
                              )}
                            </div>
                            <div className="min-w-0">
                              <p className="truncate text-sm font-semibold text-zinc-200">
                                {blockedUser.userName}
                              </p>
                              <p className="mt-1 text-xs text-zinc-600">
                                {formatRelativeDate(
                                  blockedUser.blockedAt,
                                )}{" "}
                                bloklanıb
                              </p>
                            </div>
                          </div>

                          <button
                            type="button"
                            onClick={() => {
                              unblockMutation.mutate(
                                blockedUser.id,
                              );
                            }}
                            disabled={
                              unblockMutation.isPending
                            }
                            className="shrink-0 rounded-xl border border-red-400/15 bg-red-500/[0.06] px-4 py-2 text-xs font-semibold text-red-300 transition hover:bg-red-500/15 disabled:opacity-50"
                          >
                            Blokdan çıxar
                          </button>
                        </div>
                      );
                    },
                  )}
                </div>
              )}
            </div>
          </section>
        </div>
      </div>
    </main>
  );
}
