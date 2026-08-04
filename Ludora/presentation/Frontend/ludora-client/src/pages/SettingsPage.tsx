import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  Ban,
  BellRing,
  Check,
  ChevronRight,
  Gamepad2,
  KeyRound,
  LoaderCircle,
  LockKeyhole,
  Languages,
  Mail,
  MonitorSmartphone,
  Moon,
  RefreshCw,
  Settings,
  ShieldCheck,
  Sun,
  Trash2,
  TriangleAlert,
  Unplug,
  UserRoundPen,
  UserX,
  X,
} from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Footer } from "../components/layout/Footer";
import { LanguageSwitcher } from "../components/layout/LanguageSwitcher";
import { Navbar } from "../components/layout/Navbar";
import { useI18n } from "../i18n/useI18n";
import { NotificationSettingsPanel } from "../components/notifications/NotificationSettingsPanel";
import { EditProfilePage } from "./EditProfilePage";
import { getApiErrorMessage, getApiFieldErrors } from "../lib/api";
import { useConfirmation } from "../lib/confirmation";
import {
  getAuthDeviceSessions,
  logout,
  revokeAuthDeviceSession,
} from "../services/authService";
import { applyTheme, getActiveTheme, type WellPlaydTheme } from "../lib/theme";
import {
  changePassword,
  deleteAccount,
  getBlockedUsers,
  getCurrentUser,
  getUserSettings,
  requestEmailChange,
  unblockUser,
  updateUserSettings,
} from "../services/settingsService";
import {
  disconnectSteam,
  getSteamConnectionStatus,
  startSteamConnection,
  syncSteamOwnedGames,
} from "../services/steamService";
import { useAuthStore } from "../stores/authStore";
import type { UserSettingsDTO } from "../types/settings";
import type { SteamConnectionMessage } from "../types/steam";

type SettingsSection =
  | "profile"
  | "appearance"
  | "account"
  | "notifications"
  | "integrations"
  | "sessions"
  | "blocked";


const HOME_WELCOME_VISIBILITY_KEY = "wellplayd:hide-home-welcome";

function getHomeWelcomeHiddenPreference(): boolean {
  if (typeof window === "undefined") {
    return false;
  }

  return window.localStorage.getItem(HOME_WELCOME_VISIBILITY_KEY) === "true";
}

function persistHomeWelcomeHiddenPreference(value: boolean): void {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(
    HOME_WELCOME_VISIBILITY_KEY,
    value ? "true" : "false",
  );
  window.dispatchEvent(
    new CustomEvent("wellplayd:home-welcome-visibility-changed"),
  );
}

const settingsSections: Array<{
  id: SettingsSection;
  label: string;
  description: string;
  icon: typeof UserRoundPen;
}> = [
  {
    id: "profile",
    label: "Profil",
    description: "Şəkil, istifadəçi adı, bio və sosial linklər",
    icon: UserRoundPen,
  },
  {
    id: "appearance",
    label: "Görünüş və dil",
    description: "Dil və rəng rejimi seçimləri",
    icon: Languages,
  },
  {
    id: "account",
    label: "Hesab",
    description: "Email, şifrə, məxfilik və təhlükəsizlik",
    icon: ShieldCheck,
  },
  {
    id: "notifications",
    label: "Bildirişlər",
    description: "Fəaliyyət bildirişlərinin idarəsi",
    icon: BellRing,
  },
  {
    id: "integrations",
    label: "İnteqrasiyalar",
    description: "Steam və xarici hesab bağlantıları",
    icon: Gamepad2,
  },
  {
    id: "blocked",
    label: "Bloklanmış istifadəçilər",
    description: "Blok siyahısına nəzarət",
    icon: Ban,
  },
];

function formatSessionDateTime(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "—";
  }

  const pad = (number: number) => String(number).padStart(2, "0");

  return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()}, ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function parseSettingsSection(
  value: string | null,
  hasSteamResult: boolean,
): SettingsSection {
  if (hasSteamResult) {
    return "integrations";
  }

  return settingsSections.some((section) => section.id === value)
    ? (value as SettingsSection)
    : "profile";
}

interface NoticeState {
  type: "success" | "error";
  message: string;
}

type NoticeArea =
  | "privacy"
  | "appearance"
  | "email"
  | "password"
  | "delete"
  | "integrations"
  | "sessions"
  | "blocked";

type SettingsField =
  | "newEmail"
  | "emailPassword"
  | "passwordCurrent"
  | "newPassword"
  | "confirmNewPassword"
  | "deletePassword"
  | "deleteConfirmation";

type SettingsFieldErrors = Partial<Record<SettingsField, string>>;

function normalizeApiFieldName(value: string): string {
  return value
    .split(".")
    .at(-1)!
    .replace(/[^a-z0-9]/gi, "")
    .toLocaleLowerCase();
}

function getValidationMessage(
  error: unknown,
  ...fieldNames: string[]
): string | null {
  const expectedNames = new Set(
    fieldNames.map((fieldName) => normalizeApiFieldName(fieldName)),
  );

  for (const [fieldName, messages] of Object.entries(getApiFieldErrors(error))) {
    if (!expectedNames.has(normalizeApiFieldName(fieldName))) continue;

    const message = messages.find(Boolean);
    if (message) return message;
  }

  return null;
}

function containsAny(value: string, fragments: string[]): boolean {
  const normalizedValue = value.toLocaleLowerCase("az-AZ");
  return fragments.some((fragment) => normalizedValue.includes(fragment));
}

function InlineFieldError({
  id,
  message,
}: {
  id: string;
  message?: string;
}) {
  if (!message) return null;

  return (
    <p id={id} role="alert" className="mt-2 text-xs leading-5 text-red-300">
      {message}
    </p>
  );
}

function LocalNotice({ notice }: { notice?: NoticeState }) {
  if (!notice) return null;

  return (
    <div
      role={notice.type === "error" ? "alert" : "status"}
      className={`mt-4 flex items-start gap-3 rounded-xl border px-4 py-3 text-sm ${
        notice.type === "success"
          ? "border-emerald-400/15 bg-emerald-500/[0.06] text-emerald-200"
          : "border-red-400/15 bg-red-500/[0.06] text-red-200"
      }`}
    >
      {notice.type === "success" ? (
        <Check size={17} className="mt-0.5 shrink-0" />
      ) : (
        <X size={17} className="mt-0.5 shrink-0" />
      )}
      <span>{notice.message}</span>
    </div>
  );
}

function getInitialSteamNotice(language: "az" | "en"): NoticeState | null {
  const query = new URLSearchParams(window.location.search);

  if (!query.has("steamSuccess")) return null;

  const success = query.get("steamSuccess") === "true";
  return {
    type: success ? "success" : "error",
    message: success
      ? language === "en"
        ? `${query.get("steamName") ?? "Steam account"} connected successfully.`
        : `${query.get("steamName") ?? "Steam hesabı"} uğurla bağlandı.`
      : query.get("steamError") ??
        (language === "en"
          ? "The Steam account could not be connected."
          : "Steam hesabı bağlanmadı."),
  };
}

export function SettingsPage() {
  const { t, language } = useI18n();
  const requestConfirmation = useConfirmation();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const activeSection = parseSettingsSection(
    searchParams.get("section"),
    searchParams.has("steamSuccess"),
  );
  const storedUser = useAuthStore((state) => state.user);
  const setUser = useAuthStore((state) => state.setUser);
  const clearSession = useAuthStore((state) => state.clearSession);
  const [newEmail, setNewEmail] = useState("");
  const [emailPassword, setEmailPassword] = useState("");
  const [passwordCurrent, setPasswordCurrent] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [notices, setNotices] = useState<Partial<Record<NoticeArea, NoticeState>>>(
    () => {
      const steamNotice = getInitialSteamNotice(language);
      return steamNotice ? { integrations: steamNotice } : {};
    },
  );
  const [fieldErrors, setFieldErrors] = useState<SettingsFieldErrors>({});
  const [theme, setTheme] = useState<WellPlaydTheme>(() => getActiveTheme());
  const [hideHomeWelcome, setHideHomeWelcome] = useState(() =>
    getHomeWelcomeHiddenPreference(),
  );
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [deletePassword, setDeletePassword] = useState("");
  const [deleteConfirmation, setDeleteConfirmation] = useState("");

  const setAreaNotice = useCallback((area: NoticeArea, notice: NoticeState | null) => {
    setNotices((current) => {
      const next = { ...current };
      if (notice) next[area] = notice;
      else delete next[area];
      return next;
    });
  }, []);



  const handleHomeWelcomeVisibilityToggle = useCallback(() => {
    try {
      const nextValue = !hideHomeWelcome;
      persistHomeWelcomeHiddenPreference(nextValue);
      setHideHomeWelcome(nextValue);
      setAreaNotice(
        "appearance",
        {
          type: "success",
          message: nextValue
            ? language === "en"
              ? "The home welcome panel is now hidden."
              : "Ana səhifədəki xoş gəldin paneli gizlədildi."
            : language === "en"
              ? "The home welcome panel is visible again."
              : "Ana səhifədəki xoş gəldin paneli yenidən göstərilir.",
        },
      );
    } catch {
      setAreaNotice(
        "appearance",
        {
          type: "error",
          message:
            language === "en"
              ? "The home welcome panel preference could not be updated."
              : "Ana səhifədəki xoş gəldin paneli seçimi yenilənmədi.",
        },
      );
    }
  }, [hideHomeWelcome, language, setAreaNotice]);

  const clearFieldError = (field: SettingsField) => {
    setFieldErrors((current) => {
      if (!current[field]) return current;
      const next = { ...current };
      delete next[field];
      return next;
    });
  };

  const applyEmailError = (error: unknown) => {
    const message = getApiErrorMessage(
      error,
      "Email dəyişiklik sorğusu göndərilmədi.",
    );
    const nextErrors: SettingsFieldErrors = {};
    const newEmailError = getValidationMessage(error, "NewEmail");
    const currentPasswordError = getValidationMessage(error, "CurrentPassword");

    if (newEmailError) nextErrors.newEmail = newEmailError;
    if (currentPasswordError) nextErrors.emailPassword = currentPasswordError;

    if (!newEmailError && !currentPasswordError) {
      if (
        containsAny(message, [
          "cari şifrə",
          "current password",
          "password is incorrect",
          "incorrect password",
        ])
      ) {
        nextErrors.emailPassword = message;
      } else if (containsAny(message, ["email", "e-mail"])) {
        nextErrors.newEmail = message;
      }
    }

    if (Object.keys(nextErrors).length > 0) {
      setFieldErrors((current) => ({ ...current, ...nextErrors }));
      setAreaNotice("email", null);
      return;
    }

    setAreaNotice("email", { type: "error", message });
  };

  const applyPasswordError = (error: unknown) => {
    const message = getApiErrorMessage(error, "Şifrə dəyişdirilmədi.");
    const nextErrors: SettingsFieldErrors = {};
    const currentPasswordError = getValidationMessage(error, "CurrentPassword");
    const newPasswordError = getValidationMessage(error, "NewPassword");
    const confirmationError = getValidationMessage(error, "ConfirmNewPassword");

    if (currentPasswordError) nextErrors.passwordCurrent = currentPasswordError;
    if (newPasswordError) nextErrors.newPassword = newPasswordError;
    if (confirmationError) {
      nextErrors.confirmNewPassword = confirmationError;
    }

    if (!currentPasswordError && !newPasswordError && !confirmationError) {
      if (
        containsAny(message, [
          "cari şifrə",
          "current password",
          "password is incorrect",
          "incorrect password",
        ])
      ) {
        nextErrors.passwordCurrent = message;
      } else if (
        containsAny(message, [
          "təkrar",
          "təsdiq",
          "confirm",
          "passwords do not match",
        ])
      ) {
        nextErrors.confirmNewPassword = message;
      } else if (
        containsAny(message, [
          "yeni şifrə",
          "password must",
          "password requires",
          "passwords must",
          "too short",
        ])
      ) {
        nextErrors.newPassword = message;
      }
    }

    if (Object.keys(nextErrors).length > 0) {
      setFieldErrors((current) => ({ ...current, ...nextErrors }));
      setAreaNotice("password", null);
      return;
    }

    setAreaNotice("password", { type: "error", message });
  };

  const applyDeleteAccountError = (error: unknown) => {
    const message = getApiErrorMessage(
      error,
      "Hesab silinməsi başladılmadı.",
    );
    const nextErrors: SettingsFieldErrors = {};
    const currentPasswordError = getValidationMessage(error, "CurrentPassword");
    const confirmationError = getValidationMessage(error, "ConfirmationText");

    if (currentPasswordError) nextErrors.deletePassword = currentPasswordError;
    if (confirmationError) {
      nextErrors.deleteConfirmation = confirmationError;
    }

    if (!currentPasswordError && !confirmationError) {
      if (
        containsAny(message, [
          "cari şifrə",
          "current password",
          "password is incorrect",
          "incorrect password",
        ])
      ) {
        nextErrors.deletePassword = message;
      } else if (
        containsAny(message, [
          "istifadəçi ad",
          "confirmation",
          "təsdiq",
        ])
      ) {
        nextErrors.deleteConfirmation = message;
      }
    }

    if (Object.keys(nextErrors).length > 0) {
      setFieldErrors((current) => ({ ...current, ...nextErrors }));
      setAreaNotice("delete", null);
      return;
    }

    setAreaNotice("delete", { type: "error", message });
  };

  const userQuery = useQuery({
    queryKey: ["current-user"],
    queryFn: getCurrentUser,
    initialData: storedUser ?? undefined,
  });

  const settingsQuery = useQuery({
    queryKey: ["user-settings"],
    queryFn: getUserSettings,
  });

  const blockedUsersQuery = useQuery({
    queryKey: ["blocked-users"],
    queryFn: ({ signal }) => getBlockedUsers(signal),
    enabled: activeSection === "blocked",
  });

  const deviceSessionsQuery = useQuery({
    queryKey: ["auth-device-sessions"],
    queryFn: ({ signal }) => getAuthDeviceSessions(signal),
    enabled: activeSection === "account",
  });

  const steamConnectionQuery = useQuery({
    queryKey: ["steam-connection"],
    queryFn: getSteamConnectionStatus,
    enabled: activeSection === "integrations",
    refetchInterval: (query) => {
      const status = query.state.data;
      return status?.isConnected && !status.lastSyncedAtUtc
        ? 2_000
        : false;
    },
  });

  const lastSteamSyncAt =
    steamConnectionQuery.data?.lastSyncedAtUtc;

  const syncAuthenticatedUserSettings = (
    updatedSettings: UserSettingsDTO,
    expectedUserId: string | undefined,
  ) => {
    const activeUser = useAuthStore.getState().user;

    if (!activeUser || activeUser.id !== expectedUserId) return;

    const updatedUser = {
      ...activeUser,
      isPrivate: updatedSettings.isPrivate,
      preferredLanguage: updatedSettings.preferredLanguage,
      preferredTheme: updatedSettings.preferredTheme,
    };

    setUser(updatedUser);
    queryClient.setQueryData(["current-user"], updatedUser);
  };

  useEffect(() => {
    if (!lastSteamSyncAt) return;

    void Promise.all([
      queryClient.invalidateQueries({ queryKey: ["steam-owned-games"] }),
      queryClient.invalidateQueries({ queryKey: ["user-profile"] }),
      queryClient.invalidateQueries({ queryKey: ["user-profile-games"] }),
      queryClient.invalidateQueries({
        queryKey: ["user-profile-recent-played-games"],
      }),
      queryClient.invalidateQueries({
        queryKey: ["user-playtime-statistics"],
      }),
      queryClient.invalidateQueries({ queryKey: ["user-reviews"] }),
    ]);
  }, [lastSteamSyncAt, queryClient]);

  const privacyMutation = useMutation({
    mutationFn: updateUserSettings,
    onMutate: () => ({ userId: useAuthStore.getState().user?.id }),
    onSuccess: async (settings, _variables, context) => {
      syncAuthenticatedUserSettings(settings, context.userId);
      setAreaNotice("privacy", {
        type: "success",
        message: "Məxfilik seçimi yeniləndi.",
      });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["user-settings"] }),
        queryClient.invalidateQueries({ queryKey: ["current-user"] }),
        queryClient.invalidateQueries({ queryKey: ["user-profile"] }),
      ]);
    },
    onError: (error) => setAreaNotice("privacy", {
      type: "error",
      message: getApiErrorMessage(error, "Məxfilik seçimi yenilənmədi."),
    }),
  });

  const themeMutation = useMutation({
    mutationFn: (nextTheme: WellPlaydTheme) =>
      updateUserSettings({ preferredTheme: nextTheme }),
    onMutate: (nextTheme) => {
      const previousTheme = theme;
      applyTheme(nextTheme);
      setTheme(nextTheme);
      setAreaNotice("appearance", null);
      return {
        previousTheme,
        userId: useAuthStore.getState().user?.id,
      };
    },
    onSuccess: (updatedSettings, _nextTheme, context) => {
      syncAuthenticatedUserSettings(updatedSettings, context.userId);

      queryClient.setQueryData(["user-settings"], updatedSettings);
      setAreaNotice("appearance", {
        type: "success",
        message: updatedSettings.preferredTheme === "dark"
          ? "Tünd görünüş hesabında yadda saxlanıldı."
          : "Açıq görünüş hesabında yadda saxlanıldı.",
      });
    },
    onError: (error, _nextTheme, context) => {
      const activeUserId = useAuthStore.getState().user?.id;

      if (context?.userId && activeUserId !== context.userId) return;

      const previousTheme = context?.previousTheme ?? "dark";
      applyTheme(previousTheme);
      setTheme(previousTheme);
      setAreaNotice("appearance", {
        type: "error",
        message: getApiErrorMessage(
          error,
          "Görünüş seçimi hesabında yadda saxlanılmadı.",
        ),
      });
    },
  });

  const selectTheme = (nextTheme: WellPlaydTheme) => {
    if (themeMutation.isPending || nextTheme === theme) return;
    themeMutation.mutate(nextTheme);
  };

  const emailMutation = useMutation({
    mutationFn: requestEmailChange,
    onSuccess: (result) => {
      setNewEmail("");
      setEmailPassword("");
      setFieldErrors((current) => ({
        ...current,
        newEmail: undefined,
        emailPassword: undefined,
      }));
      setAreaNotice("email", { type: "success", message: result.message });
    },
    onError: applyEmailError,
  });


  const passwordMutation = useMutation({
    mutationFn: changePassword,
    onSuccess: (result) => {
      setPasswordCurrent("");
      setNewPassword("");
      setConfirmNewPassword("");
      setFieldErrors((current) => ({
        ...current,
        passwordCurrent: undefined,
        newPassword: undefined,
        confirmNewPassword: undefined,
      }));
      setAreaNotice("password", { type: "success", message: result.message });
    },
    onError: applyPasswordError,
  });

  const deleteAccountMutation = useMutation({
    mutationFn: deleteAccount,
    onSuccess: async (result) => {
      setIsDeleteModalOpen(false);
      setDeletePassword("");
      setDeleteConfirmation("");
      await logout();
      clearSession();
      queryClient.clear();
      navigate("/login", {
        replace: true,
        state: {
          accountDeletionMessage: result.message,
        },
      });
    },
    onError: applyDeleteAccountError,
  });

  const revokeDeviceSessionMutation = useMutation({
    mutationFn: revokeAuthDeviceSession,
    onSuccess: async (result) => {
      if (result.currentSessionRevoked) {
        clearSession();
        queryClient.clear();
        navigate("/login", {
          replace: true,
          state: { sessionMessage: result.message },
        });
        return;
      }

      setAreaNotice("sessions", {
        type: "success",
        message: result.message,
      });
      await queryClient.invalidateQueries({
        queryKey: ["auth-device-sessions"],
      });
    },
    onError: (error) => setAreaNotice("sessions", {
      type: "error",
      message: getApiErrorMessage(error, "Cihaz hesabdan çıxarıla bilmədi."),
    }),
  });

  const unblockMutation = useMutation({
    mutationFn: unblockUser,
    onSuccess: async () => {
      setAreaNotice("blocked", {
        type: "success",
        message: "İstifadəçinin bloku açıldı.",
      });
      await queryClient.invalidateQueries({ queryKey: ["blocked-users"] });
    },
    onError: (error) => setAreaNotice("blocked", {
      type: "error",
      message: getApiErrorMessage(error, "İstifadəçinin bloku açılmadı."),
    }),
  });

  const steamConnectMutation = useMutation({
    mutationFn: async (popup: Window) => ({
      popup,
      connection: await startSteamConnection(),
    }),
    onSuccess: ({ popup, connection }) => {
      popup.location.href = connection.authorizationUrl;
    },
    onError: (error, popup) => {
      popup.close();
      setAreaNotice("integrations", {
        type: "error",
        message: getApiErrorMessage(error, "Steam bağlantısı başladılmadı."),
      });
    },
  });

  const steamDisconnectMutation = useMutation({
    mutationFn: disconnectSteam,
    onSuccess: async () => {
      setAreaNotice("integrations", {
        type: "success",
        message: "Steam hesabı ayrıldı.",
      });
      await queryClient.invalidateQueries({ queryKey: ["steam-connection"] });
    },
    onError: (error) => setAreaNotice("integrations", {
      type: "error",
      message: getApiErrorMessage(error, "Steam hesabı ayrıla bilmədi."),
    }),
  });

  const steamSyncMutation = useMutation({
    mutationFn: syncSteamOwnedGames,
    onSuccess: async (result) => {
      setAreaNotice("integrations", {
        type: "success",
        message: `Steam kitabxanası sinxronlaşdırıldı: ${result.matchedGameCount} oyun uyğunlaşdırıldı, ${result.updatedReviewPlaytimeCount} oyun saatı Steam məlumatı ilə eyniləşdirildi.`,
      });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["steam-connection"] }),
        queryClient.invalidateQueries({ queryKey: ["steam-owned-games"] }),
        queryClient.invalidateQueries({ queryKey: ["user-profile"] }),
        queryClient.invalidateQueries({ queryKey: ["user-profile-games"] }),
        queryClient.invalidateQueries({ queryKey: ["user-profile-recent-played-games"] }),
        queryClient.invalidateQueries({ queryKey: ["user-playtime-statistics"] }),
        queryClient.invalidateQueries({ queryKey: ["user-reviews"] }),
      ]);
    },
    onError: (error) => setAreaNotice("integrations", {
      type: "error",
      message: getApiErrorMessage(error, "Steam kitabxanası sinxronlaşdırılmadı."),
    }),
  });

  useEffect(() => {
    const configuredOrigin = import.meta.env.VITE_API_ORIGIN as string | undefined;
    const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined;
    const apiOrigin = configuredOrigin
      ? new URL(configuredOrigin).origin
      : configuredBaseUrl
        ? new URL(configuredBaseUrl).origin
        : null;

    const handleSteamMessage = async (event: MessageEvent<SteamConnectionMessage>) => {
      if (!apiOrigin || event.origin !== apiOrigin) return;
      if (event.data?.type !== "wellplayd:steam-link-result") return;

      if (event.data.success) {
        setAreaNotice("integrations", {
          type: "success",
          message: language === "en"
            ? `${event.data.steamName ?? "Steam account"} connected successfully.`
            : `${event.data.steamName ?? "Steam hesabı"} uğurla bağlandı.`,
        });
        await queryClient.invalidateQueries({ queryKey: ["steam-connection"] });
        setSearchParams({ section: "integrations" }, { replace: true });
        return;
      }

      setAreaNotice("integrations", {
        type: "error",
        message:
          event.data.error ??
          (language === "en"
            ? "The Steam account could not be connected."
            : "Steam hesabı bağlanmadı."),
      });
    };

    window.addEventListener("message", handleSteamMessage);
    return () => window.removeEventListener("message", handleSteamMessage);
  }, [language, queryClient, setAreaNotice, setSearchParams]);

  useEffect(() => {
    const query = new URLSearchParams(window.location.search);
    if (query.get("section") === "integrations" || query.has("steamSuccess")) {
      requestAnimationFrame(() => {
        document.getElementById("integrations")?.scrollIntoView({ behavior: "smooth" });
      });
    }

    if (query.has("steamSuccess")) {
      void queryClient.invalidateQueries({ queryKey: ["steam-connection"] });
    }
  }, [queryClient]);

  if (!userQuery.data || settingsQuery.isLoading) {
    return (
      <div className="min-h-screen wellplayd-page-bg text-zinc-100">
        <Navbar />
        <main className="flex min-h-[70vh] items-center justify-center gap-3 text-sm text-zinc-500">
          <LoaderCircle size={20} className="animate-spin" /> Parametrlər yüklənir...
        </main>
      </div>
    );
  }

  const user = userQuery.data;
  const settings = settingsQuery.data ?? {
    isPrivate: user.isPrivate,
    preferredLanguage: user.preferredLanguage,
    preferredTheme: user.preferredTheme,
  };
  return (
    <div className="min-h-screen wellplayd-page-bg text-zinc-100">
      <Navbar />
      <main className="mx-auto max-w-7xl px-5 py-10 lg:px-8 lg:py-14">
        <Link
          to={`/users/${user.userName}`}
          className="inline-flex items-center gap-2 text-sm font-semibold text-zinc-500 transition hover:text-violet-300"
        >
          <ArrowLeft size={16} /> Profilə qayıt
        </Link>

        <div className="mt-6 flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-start gap-4">
            <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-violet-500/12 text-violet-300">
              <Settings size={22} />
            </span>
            <div>
              <h1 className="text-3xl font-bold tracking-tight text-white">Parametrlər</h1>
              <p className="mt-2 text-sm leading-6 text-zinc-500">
                Profilini, hesab təhlükəsizliyini, bildirişləri və inteqrasiyaları bir yerdən idarə et.
              </p>
            </div>
          </div>
        </div>

        <div className="mt-7 grid items-start gap-6 lg:grid-cols-[17rem_minmax(0,1fr)]">
          <aside className="overflow-hidden rounded-3xl border border-white/[0.07] bg-[#101015] p-2 lg:sticky lg:top-24">
            <nav aria-label="Parametr bölmələri" className="space-y-1">
              {settingsSections.map((section) => {
                const Icon = section.icon;
                const isActive = activeSection === section.id;

                return (
                  <button
                    key={section.id}
                    type="button"
                    onClick={() => {
                      setSearchParams(
                        { section: section.id },
                        { replace: true },
                      );
                    }}
                    className={`group flex w-full items-center gap-3 rounded-2xl px-3 py-3 text-left transition ${
                      isActive
                        ? "bg-violet-500/12 text-violet-200"
                        : "text-zinc-400 hover:bg-white/[0.05] hover:text-zinc-200"
                    }`}
                  >
                    <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${
                      isActive
                        ? "bg-violet-500/15 text-violet-300"
                        : "bg-white/[0.035] text-zinc-500 group-hover:text-zinc-300"
                    }`}>
                      <Icon size={18} />
                    </span>
                    <span className="min-w-0 flex-1">
                      <strong className="block text-sm font-semibold">
                        {section.label}
                      </strong>
                      <span className="mt-0.5 hidden text-[11px] leading-4 text-zinc-600 xl:block">
                        {section.description}
                      </span>
                    </span>
                    <ChevronRight
                      size={16}
                      className={isActive ? "text-violet-300" : "text-zinc-700"}
                    />
                  </button>
                );
              })}
            </nav>
          </aside>

          <div className="min-w-0 space-y-6">
          {activeSection === "profile" && (
            <EditProfilePage embedded />
          )}

          {activeSection === "appearance" && (
          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
            <div className="flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h2 className="font-semibold text-white">
                  {t("profile:settings.language")}
                </h2>
                <p className="mt-1 text-xs leading-5 text-zinc-600">
                  {t("profile:settings.languageDescription")}
                </p>
              </div>
              <LanguageSwitcher />
            </div>
          </section>
          )}

          {activeSection === "account" && (
          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
            <div className="flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-center gap-4">
                <span className="flex h-12 w-12 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                  <LockKeyhole size={21} />
                </span>
                <div>
                  <h2 className="font-semibold text-white">Hesab məxfiliyi</h2>
                  <p className="mt-1 text-xs leading-5 text-zinc-600">
                    Gizli profildə post, siyahı və oyun kolleksiyalarını yalnız qəbul edilmiş izləyicilər görə bilər.
                  </p>
                </div>
              </div>
              <button
                type="button"
                role="switch"
                aria-checked={settings.isPrivate}
                disabled={privacyMutation.isPending}
                onClick={() => privacyMutation.mutate({ isPrivate: !settings.isPrivate })}
                className={`relative h-7 w-13 rounded-full transition ${settings.isPrivate ? "bg-violet-600" : "bg-zinc-700"}`}
              >
                <span className={`absolute top-1 h-5 w-5 rounded-full bg-white transition ${settings.isPrivate ? "left-7" : "left-1"}`} />
              </button>
            </div>
            {settingsQuery.isError && (
              <div role="alert" className="mt-4 rounded-xl border border-red-400/15 bg-red-500/[0.06] px-4 py-3 text-sm text-red-200">
                {getApiErrorMessage(
                  settingsQuery.error,
                  "Məxfilik seçimi yüklənmədi.",
                )}
              </div>
            )}
            <LocalNotice notice={notices.privacy} />
          </section>
          )}

          {activeSection === "appearance" && (
          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
            <div className="flex items-start gap-3">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                {theme === "dark" ? <Moon size={20} /> : <Sun size={20} />}
              </span>
              <div>
                <h2 className="font-semibold text-white">Görünüş</h2>
                <p className="mt-1 text-xs leading-5 text-zinc-600">
                  WellPlayd interfeysinin rəng rejimini seç. Seçim hesabında yadda qalacaq və daxil olduqda avtomatik tətbiq ediləcək.
                </p>
              </div>
            </div>

            <div
              className="mt-5 grid gap-3 sm:grid-cols-2"
              role="radiogroup"
              aria-label="Rəng rejimi"
            >
              <button
                type="button"
                role="radio"
                aria-checked={theme === "light"}
                onClick={() => selectTheme("light")}
                disabled={themeMutation.isPending}
                className={`group rounded-2xl border p-3 text-left transition ${
                  theme === "light"
                    ? "border-violet-400/50 bg-violet-500/[0.08] ring-2 ring-violet-500/15"
                    : "border-white/[0.08] bg-white/[0.02] hover:border-violet-400/30"
                }`}
              >
                <span
                  className="block h-24 overflow-hidden rounded-xl border border-slate-200 bg-[#f5f7fb] p-3 shadow-sm"
                  aria-hidden="true"
                >
                  <span className="flex items-center justify-between">
                    <span className="h-2 w-14 rounded-full bg-slate-300" />
                    <span className="h-5 w-5 rounded-full bg-violet-500" />
                  </span>
                  <span className="mt-3 grid grid-cols-[0.65fr_1fr] gap-2">
                    <span className="h-11 rounded-lg bg-white shadow-sm" />
                    <span className="space-y-2 rounded-lg bg-white p-2 shadow-sm">
                      <span className="block h-1.5 w-3/4 rounded-full bg-slate-300" />
                      <span className="block h-1.5 w-1/2 rounded-full bg-slate-200" />
                    </span>
                  </span>
                </span>
                <span className="mt-3 flex items-center justify-between gap-3 px-1">
                  <span>
                    <span className="flex items-center gap-2 text-sm font-semibold text-zinc-200">
                      <Sun size={16} className="text-amber-400" /> Açıq rejim
                    </span>
                    <span className="mt-1 block text-xs text-zinc-600">
                      Parlaq və təmiz interfeys
                    </span>
                  </span>
                  <span className={`flex h-5 w-5 items-center justify-center rounded-full border ${
                    theme === "light"
                      ? "border-violet-500 bg-violet-600 text-white"
                      : "border-white/[0.14]"
                  }`}>
                    {theme === "light" && <Check size={13} />}
                  </span>
                </span>
              </button>

              <button
                type="button"
                role="radio"
                aria-checked={theme === "dark"}
                onClick={() => selectTheme("dark")}
                disabled={themeMutation.isPending}
                className={`group rounded-2xl border p-3 text-left transition ${
                  theme === "dark"
                    ? "border-violet-400/50 bg-violet-500/[0.08] ring-2 ring-violet-500/15"
                    : "border-white/[0.08] bg-white/[0.02] hover:border-violet-400/30"
                }`}
              >
                <span
                  className="block h-24 overflow-hidden rounded-xl border border-white/10 bg-[#0d1018] p-3 shadow-sm"
                  aria-hidden="true"
                >
                  <span className="flex items-center justify-between">
                    <span className="h-2 w-14 rounded-full bg-zinc-700" />
                    <span className="h-5 w-5 rounded-full bg-violet-500" />
                  </span>
                  <span className="mt-3 grid grid-cols-[0.65fr_1fr] gap-2">
                    <span className="h-11 rounded-lg bg-[#191c26] shadow-sm" />
                    <span className="space-y-2 rounded-lg bg-[#191c26] p-2 shadow-sm">
                      <span className="block h-1.5 w-3/4 rounded-full bg-zinc-600" />
                      <span className="block h-1.5 w-1/2 rounded-full bg-zinc-700" />
                    </span>
                  </span>
                </span>
                <span className="mt-3 flex items-center justify-between gap-3 px-1">
                  <span>
                    <span className="flex items-center gap-2 text-sm font-semibold text-zinc-200">
                      <Moon size={16} className="text-violet-300" /> Tünd rejim
                    </span>
                    <span className="mt-1 block text-xs text-zinc-600">
                      Gözlər üçün rahat qaranlıq interfeys
                    </span>
                  </span>
                  <span className={`flex h-5 w-5 items-center justify-center rounded-full border ${
                    theme === "dark"
                      ? "border-violet-500 bg-violet-600 text-white"
                      : "border-white/[0.14]"
                  }`}>
                    {theme === "dark" && <Check size={13} />}
                  </span>
                </span>
              </button>
            </div>

            <div className="mt-5 rounded-2xl border border-white/[0.08] bg-white/[0.02] p-4">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <h3 className="text-sm font-semibold text-zinc-200">
                    {language === "en" ? "Home welcome panel" : "Ana səhifə xoş gəldin paneli"}
                  </h3>
                  <p className="mt-1 text-xs leading-5 text-zinc-600">
                    {language === "en"
                      ? "Hide the welcome dashboard shown at the top of the home page. You can show it again at any time."
                      : "Ana səhifənin yuxarısında görünən xoş gəldin panelini gizlət. İstədiyin vaxt yenidən göstərə bilərsən."}
                  </p>
                </div>

                <button
                  type="button"
                  role="switch"
                  aria-checked={hideHomeWelcome}
                  onClick={handleHomeWelcomeVisibilityToggle}
                  className={`relative h-7 w-13 shrink-0 rounded-full transition ${hideHomeWelcome ? "bg-violet-600" : "bg-zinc-700"}`}
                >
                  <span
                    className={`absolute top-1 h-5 w-5 rounded-full bg-white transition ${hideHomeWelcome ? "left-7" : "left-1"}`}
                  />
                </button>
              </div>
            </div>
            <LocalNotice notice={notices.appearance} />
          </section>
          )}

          {activeSection === "account" && (
          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
            <div className="flex items-start gap-3">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                <MonitorSmartphone size={20} />
              </span>
              <div>
                <h2 className="font-semibold text-white">Aktiv cihazlar</h2>
                <p className="mt-1 text-xs leading-5 text-zinc-600">
                  Hesabının açıq olduğu cihazları və son aktivlik vaxtını gör. Tanımadığın cihazı dərhal hesabdan çıxara bilərsən.
                </p>
              </div>
            </div>

            <div className="mt-5 space-y-3">
              {deviceSessionsQuery.isLoading ? (
                <div className="flex items-center gap-2 rounded-2xl border border-white/[0.06] bg-white/[0.02] px-4 py-5 text-sm text-zinc-500">
                  <LoaderCircle size={17} className="animate-spin" /> Aktiv cihazlar yüklənir...
                </div>
              ) : deviceSessionsQuery.isError ? (
                <div role="alert" className="rounded-xl border border-red-400/15 bg-red-500/[0.06] px-4 py-3 text-sm text-red-200">
                  {getApiErrorMessage(
                    deviceSessionsQuery.error,
                    "Aktiv cihazlar yüklənmədi.",
                  )}
                </div>
              ) : (deviceSessionsQuery.data?.length ?? 0) === 0 ? (
                <p className="rounded-2xl border border-dashed border-white/[0.07] p-5 text-sm text-zinc-600">
                  Aktiv cihaz tapılmadı.
                </p>
              ) : deviceSessionsQuery.data?.map((session) => {
                const isRevoking =
                  revokeDeviceSessionMutation.isPending &&
                  revokeDeviceSessionMutation.variables === session.id;
                const signedInAt = formatSessionDateTime(session.signedInAtUtc);
                const lastActive = formatSessionDateTime(session.lastActiveAtUtc);

                return (
                  <div
                    key={session.id}
                    className={`rounded-2xl border p-4 ${
                      session.isCurrent
                        ? "border-violet-400/20 bg-violet-500/[0.045]"
                        : "border-white/[0.06] bg-white/[0.02]"
                    }`}
                  >
                    <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                      <div className="flex min-w-0 items-start gap-3">
                        <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-white/[0.04] text-zinc-400">
                          <MonitorSmartphone size={20} />
                        </span>
                        <div className="min-w-0">
                          <div className="flex flex-wrap items-center gap-2">
                            <p className="font-semibold text-zinc-200">
                              {session.deviceName}
                            </p>
                            {session.isCurrent && (
                              <span className="rounded-full border border-emerald-400/15 bg-emerald-500/[0.07] px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-emerald-300">
                                Bu cihaz
                              </span>
                            )}
                          </div>
                          <p className="mt-1 text-xs text-zinc-500">
                            {session.browser} · {session.operatingSystem}
                          </p>
                          <p className="mt-1 text-xs leading-5 text-zinc-600">
                            Son giriş: {signedInAt} · Son aktivlik: {lastActive}
                          </p>
                        </div>
                      </div>

                      <button
                        type="button"
                        disabled={isRevoking}
                        onClick={async () => {
                          setAreaNotice("sessions", null);
                          const confirmed = await requestConfirmation({
                            title: session.isCurrent
                              ? "Cari cihazdan çıxış edilsin?"
                              : "Bu cihaz hesabdan çıxarılsın?",
                            description: session.isCurrent
                              ? "Bu brauzerdə sessiyan bağlanacaq və login səhifəsinə yönləndiriləcəksən."
                              : `${session.deviceName} cihazındakı aktiv sessiya ləğv ediləcək.`,
                            confirmLabel: session.isCurrent
                              ? "Çıxış et"
                              : "Cihazı çıxar",
                            kind: "disconnect",
                            tone: "warning",
                          });

                          if (confirmed) {
                            revokeDeviceSessionMutation.mutate(session.id);
                          }
                        }}
                        aria-label={session.isCurrent ? "Cari cihazdan çıxış et" : "Cihazı hesabdan çıxar"}
                        title={session.isCurrent ? "Cari cihazdan çıxış et" : "Cihazı hesabdan çıxar"}
                        className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border border-red-400/10 bg-red-500/[0.04] text-red-300 transition hover:bg-red-500/[0.08] disabled:opacity-50"
                      >
                        {isRevoking ? (
                          <LoaderCircle size={15} className="animate-spin" />
                        ) : (
                          <X size={16} />
                        )}
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
            <LocalNotice notice={notices.sessions} />
          </section>
          )}

          {activeSection === "notifications" && (
            <NotificationSettingsPanel />
          )}

          {activeSection === "integrations" && (
          <section id="integrations" className="scroll-mt-24 rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
            <div className="flex flex-col gap-5 sm:flex-row sm:items-start sm:justify-between">
              <div className="flex items-start gap-3">
                <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-sky-500/10 text-sky-300">
                  <Gamepad2 size={20} />
                </span>
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-zinc-600">Export</p>
                  <h2 className="mt-1 font-semibold text-white">Steam hesabı</h2>
                  <p className="mt-1 max-w-2xl text-xs leading-5 text-zinc-600">
                    Steam kitabxananı, oynanma saatlarını, nailiyyətləri və dost uyğunluqlarını WellPlayd profilinə köçür.
                  </p>
                </div>
              </div>

              {steamConnectionQuery.data?.isConnected && (
                <span className="inline-flex items-center gap-2 self-start rounded-full border border-emerald-400/15 bg-emerald-500/[0.07] px-3 py-1.5 text-xs font-semibold text-emerald-300">
                  <Check size={15} /> Bağlıdır
                </span>
              )}
            </div>

            <div className="mt-5 rounded-2xl border border-white/[0.06] bg-white/[0.02] p-4">
              {steamConnectionQuery.isLoading ? (
                <div className="flex items-center gap-2 text-sm text-zinc-500">
                  <LoaderCircle size={17} className="animate-spin" /> Steam statusu yoxlanılır...
                </div>
              ) : steamConnectionQuery.isError ? (
                <div role="alert" className="rounded-xl border border-red-400/15 bg-red-500/[0.06] px-4 py-3 text-sm text-red-200">
                  {getApiErrorMessage(
                    steamConnectionQuery.error,
                    "Steam bağlantısının vəziyyəti yoxlanılmadı.",
                  )}
                </div>
              ) : steamConnectionQuery.data?.isConnected ? (
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex min-w-0 items-center gap-3">
                    <span className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-sky-500/10 text-sky-300">
                      {steamConnectionQuery.data.avatarUrl ? (
                        <img
                          src={steamConnectionQuery.data.avatarUrl}
                          alt="Steam avatar"
                          className="h-full w-full object-cover"
                        />
                      ) : (
                        <Gamepad2 size={20} />
                      )}
                    </span>
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <Check size={17} className="shrink-0 text-emerald-400" />
                        <p className="truncate font-semibold text-white">
                          {steamConnectionQuery.data.steamName ?? "Steam istifadəçisi"}
                        </p>
                      </div>
                      <p className="mt-1 text-xs text-zinc-600">
                        {steamConnectionQuery.data.lastSyncedAtUtc
                          ? `Son sync: ${new Date(steamConnectionQuery.data.lastSyncedAtUtc).toLocaleString()}`
                          : "Kitabxana hələ sinxronlaşdırılmayıb."}
                      </p>
                    </div>
                  </div>

                  <div className="flex flex-wrap gap-2">
                    {steamConnectionQuery.data.profileUrl && (
                      <a
                        href={steamConnectionQuery.data.profileUrl}
                        target="_blank"
                        rel="noreferrer"
                        className="inline-flex h-10 items-center gap-2 rounded-xl border border-white/[0.08] px-3 text-xs font-semibold text-zinc-300 transition hover:bg-white/[0.05] hover:text-white"
                      >
                        <ChevronRight size={15} /> Profil
                      </a>
                    )}
                    <button
                      type="button"
                      onClick={() => steamSyncMutation.mutate()}
                      disabled={steamSyncMutation.isPending}
                      className="inline-flex h-10 items-center gap-2 rounded-xl bg-sky-500/10 px-3 text-xs font-semibold text-sky-300 transition hover:bg-sky-500/15 disabled:opacity-50"
                    >
                      {steamSyncMutation.isPending ? (
                        <LoaderCircle size={15} className="animate-spin" />
                      ) : (
                        <RefreshCw size={15} />
                      )}
                      Kitabxananı sync et
                    </button>
                    <button
                      type="button"
                      onClick={async () => {
                        const confirmed =
                          await requestConfirmation({
                            title: "Steam hesabını ayırmaq istəyirsən?",
                            description:
                              "Steam bağlantın WellPlayd hesabından silinəcək və avtomatik kitabxana sinxronizasiyası dayanacaq.",
                            detail:
                              "Sonradan Steam hesabını yenidən qoşa bilərsən. Mövcud oyun qeydlərin silinməyəcək.",
                            confirmLabel: "Steam-i ayır",
                            kind: "disconnect",
                            tone: "warning",
                          });

                        if (confirmed) {
                          steamDisconnectMutation.mutate();
                        }
                      }}
                      disabled={steamDisconnectMutation.isPending}
                      className="inline-flex h-10 items-center gap-2 rounded-xl border border-red-400/10 bg-red-500/[0.04] px-3 text-xs font-semibold text-red-300 transition hover:bg-red-500/[0.08] disabled:opacity-50"
                    >
                      {steamDisconnectMutation.isPending ? (
                        <LoaderCircle size={15} className="animate-spin" />
                      ) : (
                        <Unplug size={15} />
                      )}
                      Ayır
                    </button>
                  </div>
                </div>
              ) : (
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <p className="text-sm font-semibold text-zinc-200">Steam hesabı bağlı deyil</p>
                    <p className="mt-1 text-xs leading-5 text-zinc-600">
                      Bağlantı Steam-in rəsmi OpenID səhifəsində təsdiqlənəcək. Steam şifrən WellPlayd-ə göndərilmir.
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => {
                      const popup = window.open(
                        "about:blank",
                        "wellplayd-steam-connect",
                        "popup=yes,width=720,height=760",
                      );

                      if (!popup) {
                        setAreaNotice("integrations", {
                          type: "error",
                          message: "Brauzer Steam bağlantı pəncərəsini blokladı. Popup icazəsini aktiv edin.",
                        });
                        return;
                      }

                      popup.document.title = "Steam bağlantısı";
                      popup.document.body.textContent = "Steam bağlantısı hazırlanır...";
                      steamConnectMutation.mutate(popup);
                    }}
                    disabled={steamConnectMutation.isPending}
                    className="inline-flex h-11 shrink-0 items-center justify-center gap-2 rounded-xl bg-sky-600 px-4 text-sm font-semibold text-white transition hover:bg-sky-500 disabled:opacity-50"
                  >
                    {steamConnectMutation.isPending ? (
                      <LoaderCircle size={17} className="animate-spin" />
                    ) : (
                      <Gamepad2 size={17} />
                    )}
                    Steam ilə bağlan
                  </button>
                </div>
              )}
            </div>
            <LocalNotice notice={notices.integrations} />
          </section>
          )}

          {activeSection === "account" && (
          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
            <div className="flex items-center gap-3">
              <Mail size={19} className="text-violet-300" />
              <div>
                <h2 className="font-semibold text-white">Email ünvanını dəyiş</h2>
                <p className="mt-1 text-xs text-zinc-600">Cari email: {user.email}</p>
              </div>
            </div>
            <div className="mt-5 grid gap-4 sm:grid-cols-2">
              <label className="block">
                <span className="text-xs font-semibold text-zinc-400">Yeni email</span>
                <input
                  type="email"
                  value={newEmail}
                  aria-invalid={Boolean(fieldErrors.newEmail)}
                  aria-describedby={fieldErrors.newEmail ? "new-email-error" : undefined}
                  onChange={(event) => {
                    setNewEmail(event.target.value);
                    clearFieldError("newEmail");
                    setAreaNotice("email", null);
                  }}
                  className={`mt-2 h-11 w-full rounded-xl border bg-white/[0.03] px-3 text-sm text-zinc-100 outline-none transition ${
                    fieldErrors.newEmail
                      ? "border-red-400/50 focus:border-red-400 focus:ring-4 focus:ring-red-500/10"
                      : "border-white/[0.08] focus:border-violet-400/50"
                  }`}
                />
                <InlineFieldError id="new-email-error" message={fieldErrors.newEmail} />
              </label>
              <label className="block">
                <span className="text-xs font-semibold text-zinc-400">Cari şifrə</span>
                <div className="relative mt-2">
                  <KeyRound size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-600" />
                  <input
                    type="password"
                    autoComplete="current-password"
                    value={emailPassword}
                    aria-invalid={Boolean(fieldErrors.emailPassword)}
                    aria-describedby={fieldErrors.emailPassword ? "email-password-error" : undefined}
                    onChange={(event) => {
                      setEmailPassword(event.target.value);
                      clearFieldError("emailPassword");
                      setAreaNotice("email", null);
                    }}
                    className={`h-11 w-full rounded-xl border bg-white/[0.03] pl-10 pr-3 text-sm text-zinc-100 outline-none transition ${
                      fieldErrors.emailPassword
                        ? "border-red-400/50 focus:border-red-400 focus:ring-4 focus:ring-red-500/10"
                        : "border-white/[0.08] focus:border-violet-400/50"
                    }`}
                  />
                </div>
                <InlineFieldError id="email-password-error" message={fieldErrors.emailPassword} />
              </label>
            </div>
            <LocalNotice notice={notices.email} />
            <button
              type="button"
              onClick={() => {
                const nextErrors: SettingsFieldErrors = {};
                const normalizedEmail = newEmail.trim();

                if (!normalizedEmail) {
                  nextErrors.newEmail = "Yeni email ünvanını daxil edin.";
                } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalizedEmail)) {
                  nextErrors.newEmail = "Düzgün email ünvanı daxil edin.";
                }

                if (!emailPassword) {
                  nextErrors.emailPassword = "Cari şifrəni daxil edin.";
                }

                setFieldErrors((current) => ({ ...current, ...nextErrors }));
                setAreaNotice("email", null);
                if (Object.keys(nextErrors).length > 0) return;

                emailMutation.mutate({ newEmail: newEmail.trim(), currentPassword: emailPassword });
              }}
              disabled={emailMutation.isPending}
              className="mt-4 flex h-11 items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:opacity-50"
            >
              {emailMutation.isPending ? <LoaderCircle size={17} className="animate-spin" /> : <Mail size={17} />}
              Təsdiq linki göndər
            </button>
          </section>
          )}

          {activeSection === "blocked" && (
          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
            <div className="flex items-center gap-3">
              <Ban size={19} className="text-red-300" />
              <div>
                <h2 className="font-semibold text-white">Bloklanmış hesablar</h2>
                <p className="mt-1 text-xs text-zinc-600">Blokladığın istifadəçiləri nəzərdən keçir.</p>
              </div>
            </div>
            <div className="mt-5 space-y-3">
              {blockedUsersQuery.isLoading ? (
                <div className="flex items-center gap-2 text-sm text-zinc-600"><LoaderCircle size={16} className="animate-spin" /> Yüklənir...</div>
              ) : blockedUsersQuery.isError ? (
                <div role="alert" className="rounded-xl border border-red-400/15 bg-red-500/[0.06] px-4 py-3 text-sm text-red-200">
                  {getApiErrorMessage(
                    blockedUsersQuery.error,
                    "Bloklanmış istifadəçilər yüklənmədi.",
                  )}
                </div>
              ) : (blockedUsersQuery.data?.length ?? 0) === 0 ? (
                <p className="rounded-2xl border border-dashed border-white/[0.07] p-5 text-sm text-zinc-600">Bloklanmış hesab yoxdur.</p>
              ) : blockedUsersQuery.data?.map((blockedUser) => (
                <div key={blockedUser.id} className="rounded-2xl border border-white/[0.06] bg-white/[0.02] p-4">
                  <div className="flex items-center justify-between gap-4">
                    <Link to={`/users/${blockedUser.userName}`} className="font-semibold text-zinc-200 hover:text-violet-300">{blockedUser.userName}</Link>
                    <button
                      type="button"
                      disabled={
                        unblockMutation.isPending &&
                        unblockMutation.variables === blockedUser.id
                      }
                      onClick={() => {
                        setAreaNotice("blocked", null);
                        unblockMutation.mutate(blockedUser.id);
                      }}
                      className="flex h-9 items-center gap-2 rounded-lg border border-red-400/10 bg-red-500/[0.04] px-3 text-xs font-semibold text-red-300 disabled:opacity-50"
                    >
                      {unblockMutation.isPending &&
                      unblockMutation.variables === blockedUser.id ? (
                        <LoaderCircle size={15} className="animate-spin" />
                      ) : (
                        <UserX size={15} />
                      )}
                      Bloku aç
                    </button>
                  </div>
                  {notices.blocked?.type === "error" &&
                    unblockMutation.variables === blockedUser.id && (
                      <LocalNotice notice={notices.blocked} />
                    )}
                </div>
              ))}
            </div>
            {notices.blocked?.type === "success" && (
              <LocalNotice notice={notices.blocked} />
            )}
          </section>
          )}

          {activeSection === "account" && (
          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
            <div className="flex items-center gap-3">
              <LockKeyhole size={19} className="text-violet-300" />
              <div>
                <h2 className="font-semibold text-white">Şifrəni dəyiş</h2>
                <p className="mt-1 text-xs leading-5 text-zinc-600">
                  Cari şifrəni təsdiqlə və minimum 8 simvoldan ibarət yeni şifrə seç.
                </p>
              </div>
            </div>

            <div className="mt-5 grid gap-4 lg:grid-cols-3">
              <label className="block">
                <span className="text-xs font-semibold text-zinc-400">Cari şifrə</span>
                <input
                  type="password"
                  autoComplete="current-password"
                  value={passwordCurrent}
                  aria-invalid={Boolean(fieldErrors.passwordCurrent)}
                  aria-describedby={fieldErrors.passwordCurrent ? "password-current-error" : undefined}
                  onChange={(event) => {
                    setPasswordCurrent(event.target.value);
                    clearFieldError("passwordCurrent");
                    setAreaNotice("password", null);
                  }}
                  className={`mt-2 h-11 w-full rounded-xl border bg-white/[0.03] px-3 text-sm text-zinc-100 outline-none transition ${
                    fieldErrors.passwordCurrent
                      ? "border-red-400/50 focus:border-red-400 focus:ring-4 focus:ring-red-500/10"
                      : "border-white/[0.08] focus:border-violet-400/50"
                  }`}
                />
                <InlineFieldError id="password-current-error" message={fieldErrors.passwordCurrent} />
              </label>
              <label className="block">
                <span className="text-xs font-semibold text-zinc-400">Yeni şifrə</span>
                <input
                  type="password"
                  autoComplete="new-password"
                  value={newPassword}
                  aria-invalid={Boolean(fieldErrors.newPassword)}
                  aria-describedby={fieldErrors.newPassword ? "new-password-error" : undefined}
                  onChange={(event) => {
                    setNewPassword(event.target.value);
                    clearFieldError("newPassword");
                    setAreaNotice("password", null);
                  }}
                  className={`mt-2 h-11 w-full rounded-xl border bg-white/[0.03] px-3 text-sm text-zinc-100 outline-none transition ${
                    fieldErrors.newPassword
                      ? "border-red-400/50 focus:border-red-400 focus:ring-4 focus:ring-red-500/10"
                      : "border-white/[0.08] focus:border-violet-400/50"
                  }`}
                />
                <InlineFieldError id="new-password-error" message={fieldErrors.newPassword} />
              </label>
              <label className="block">
                <span className="text-xs font-semibold text-zinc-400">Yeni şifrəni təkrarla</span>
                <input
                  type="password"
                  autoComplete="new-password"
                  value={confirmNewPassword}
                  aria-invalid={Boolean(fieldErrors.confirmNewPassword)}
                  aria-describedby={fieldErrors.confirmNewPassword ? "confirm-new-password-error" : undefined}
                  onChange={(event) => {
                    setConfirmNewPassword(event.target.value);
                    clearFieldError("confirmNewPassword");
                    setAreaNotice("password", null);
                  }}
                  className={`mt-2 h-11 w-full rounded-xl border bg-white/[0.03] px-3 text-sm text-zinc-100 outline-none transition ${
                    fieldErrors.confirmNewPassword
                      ? "border-red-400/50 focus:border-red-400 focus:ring-4 focus:ring-red-500/10"
                      : "border-white/[0.08] focus:border-violet-400/50"
                  }`}
                />
                <InlineFieldError id="confirm-new-password-error" message={fieldErrors.confirmNewPassword} />
              </label>
            </div>

            <LocalNotice notice={notices.password} />
            <button
              type="button"
              onClick={() => {
                const nextErrors: SettingsFieldErrors = {};

                if (!passwordCurrent) {
                  nextErrors.passwordCurrent = "Cari şifrəni daxil edin.";
                }

                if (!newPassword) {
                  nextErrors.newPassword = "Yeni şifrəni daxil edin.";
                } else if (newPassword.length < 8) {
                  nextErrors.newPassword = "Yeni şifrə minimum 8 simvol olmalıdır.";
                } else if (newPassword === passwordCurrent) {
                  nextErrors.newPassword = "Yeni şifrə cari şifrədən fərqli olmalıdır.";
                }

                if (!confirmNewPassword) {
                  nextErrors.confirmNewPassword = "Yeni şifrəni təkrar daxil edin.";
                } else if (newPassword !== confirmNewPassword) {
                  nextErrors.confirmNewPassword = "Yeni şifrə və təkrar şifrə eyni deyil.";
                }

                setFieldErrors((current) => ({ ...current, ...nextErrors }));
                setAreaNotice("password", null);
                if (Object.keys(nextErrors).length > 0) return;

                passwordMutation.mutate({
                  currentPassword: passwordCurrent,
                  newPassword,
                  confirmNewPassword,
                });
              }}
              disabled={
                passwordMutation.isPending
              }
              className="mt-4 flex h-11 items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:opacity-50"
            >
              {passwordMutation.isPending ? (
                <LoaderCircle size={17} className="animate-spin" />
              ) : (
                <KeyRound size={17} />
              )}
              Şifrəni yenilə
            </button>
          </section>
          )}

          {activeSection === "account" && (
          <section className="rounded-3xl border border-red-400/15 bg-red-500/[0.035] p-5 sm:p-6">
            <div className="flex flex-col gap-5 sm:flex-row sm:items-start sm:justify-between">
              <div className="flex items-start gap-3">
                <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-red-500/10 text-red-300">
                  <TriangleAlert size={20} />
                </span>
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-red-300/80">
                    Təhlükəli zona
                  </p>
                  <h2 className="mt-1 font-semibold text-white">Hesabı həmişəlik sil</h2>
                  <p className="mt-2 max-w-2xl text-xs leading-5 text-zinc-500">
                    Postlar, rəylər, siyahılar, oyun statusları, forum müzakirələri, şərhlər,
                    bəyənmələr, mesajlar və Steam məlumatları hard-delete ediləcək. Bu əməliyyat geri qaytarılmır.
                  </p>
                  <p className="mt-2 max-w-2xl text-xs leading-5 text-zinc-600">
                    Sahib olduğun icmada üzvlər varsa, sahiblik əvvəl moderatorlara, sonra ən köhnə üzvlərə təklif ediləcək.
                    Hamı rədd etsə icma da silinəcək.
                  </p>
                </div>
              </div>

              <button
                type="button"
                onClick={() => {
                  setFieldErrors((current) => ({
                    ...current,
                    deletePassword: undefined,
                    deleteConfirmation: undefined,
                  }));
                  setAreaNotice("delete", null);
                  setIsDeleteModalOpen(true);
                }}
                className="inline-flex h-11 shrink-0 items-center justify-center gap-2 rounded-xl border border-red-400/20 bg-red-500/10 px-4 text-sm font-semibold text-red-200 transition hover:bg-red-500/15"
              >
                <Trash2 size={17} /> Hesabı sil
              </button>
            </div>
          </section>
          )}
          </div>
        </div>
      </main>
      {isDeleteModalOpen && (
        <div
          className="fixed inset-0 z-[120] flex items-center justify-center bg-black/75 px-4 py-6 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          aria-labelledby="delete-account-title"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget && !deleteAccountMutation.isPending) {
              setIsDeleteModalOpen(false);
            }
          }}
        >
          <div className="w-full max-w-lg overflow-hidden rounded-3xl border border-red-400/15 bg-[#15161d] shadow-2xl shadow-black/60">
            <div className="flex items-start justify-between gap-4 border-b border-white/[0.07] p-5 sm:p-6">
              <div className="flex items-start gap-3">
                <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-red-500/10 text-red-300">
                  <TriangleAlert size={21} />
                </span>
                <div>
                  <h2 id="delete-account-title" className="text-lg font-bold text-white">
                    Hesabı həmişəlik sil
                  </h2>
                  <p className="mt-1 text-xs leading-5 text-zinc-500">
                    Bu əməliyyat geri qaytarılmır və məlumatlar soft-delete edilməyəcək.
                  </p>
                </div>
              </div>
              <button
                type="button"
                aria-label="Pəncərəni bağla"
                disabled={deleteAccountMutation.isPending}
                onClick={() => setIsDeleteModalOpen(false)}
                className="flex h-9 w-9 items-center justify-center rounded-xl border border-white/[0.08] text-zinc-500 transition hover:bg-white/[0.05] hover:text-white disabled:opacity-50"
              >
                <X size={18} />
              </button>
            </div>

            <div className="space-y-4 p-5 sm:p-6">
              <div className="rounded-2xl border border-red-400/10 bg-red-500/[0.04] p-4 text-xs leading-5 text-red-100/75">
                Hesab silinməsi başladıqdan sonra sistemdən çıxarılacaqsan. Sahiblik təklifi gözləyən icmalar varsa,
                minimal hesab qeydi yalnız proses tamamlanana qədər saxlanacaq, sonra o da hard-delete ediləcək.
              </div>

              <label className="block">
                <span className="text-xs font-semibold text-zinc-300">Cari şifrə</span>
                <input
                  type="password"
                  autoComplete="current-password"
                  value={deletePassword}
                  aria-invalid={Boolean(fieldErrors.deletePassword)}
                  aria-describedby={fieldErrors.deletePassword ? "delete-password-error" : undefined}
                  onChange={(event) => {
                    setDeletePassword(event.target.value);
                    clearFieldError("deletePassword");
                    setAreaNotice("delete", null);
                  }}
                  className={`mt-2 h-11 w-full rounded-xl border bg-white/[0.035] px-3 text-sm text-zinc-100 outline-none transition focus:ring-4 focus:ring-red-500/10 ${
                    fieldErrors.deletePassword
                      ? "border-red-400/50 focus:border-red-400"
                      : "border-white/[0.08] focus:border-red-400/50"
                  }`}
                />
                <InlineFieldError id="delete-password-error" message={fieldErrors.deletePassword} />
              </label>

              <label className="block">
                <span className="text-xs font-semibold text-zinc-300">
                  Təsdiq üçün <strong className="text-red-300">{user.userName}</strong> yaz
                </span>
                <input
                  type="text"
                  value={deleteConfirmation}
                  aria-invalid={Boolean(fieldErrors.deleteConfirmation)}
                  aria-describedby={fieldErrors.deleteConfirmation ? "delete-confirmation-error" : undefined}
                  onChange={(event) => {
                    setDeleteConfirmation(event.target.value);
                    clearFieldError("deleteConfirmation");
                    setAreaNotice("delete", null);
                  }}
                  className={`mt-2 h-11 w-full rounded-xl border bg-white/[0.035] px-3 text-sm text-zinc-100 outline-none transition focus:ring-4 focus:ring-red-500/10 ${
                    fieldErrors.deleteConfirmation
                      ? "border-red-400/50 focus:border-red-400"
                      : "border-white/[0.08] focus:border-red-400/50"
                  }`}
                />
                <InlineFieldError id="delete-confirmation-error" message={fieldErrors.deleteConfirmation} />
              </label>

              <LocalNotice notice={notices.delete} />
              <div className="flex flex-col-reverse gap-2 pt-2 sm:flex-row sm:justify-end">
                <button
                  type="button"
                  disabled={deleteAccountMutation.isPending}
                  onClick={() => setIsDeleteModalOpen(false)}
                  className="h-11 rounded-xl border border-white/[0.08] px-4 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.05] disabled:opacity-50"
                >
                  Ləğv et
                </button>
                <button
                  type="button"
                  disabled={
                    deleteAccountMutation.isPending
                  }
                  onClick={() => {
                    const nextErrors: SettingsFieldErrors = {};
                    const normalizedConfirmation = deleteConfirmation.trim();

                    if (!deletePassword) {
                      nextErrors.deletePassword = "Cari şifrəni daxil edin.";
                    }

                    if (!normalizedConfirmation) {
                      nextErrors.deleteConfirmation = "Təsdiq üçün istifadəçi adını daxil edin.";
                    } else if (
                      normalizedConfirmation.toLocaleLowerCase("az-AZ") !==
                      user.userName.toLocaleLowerCase("az-AZ")
                    ) {
                      nextErrors.deleteConfirmation =
                        "İstifadəçi adını yuxarıda göstərildiyi kimi düzgün yazın.";
                    }

                    setFieldErrors((current) => ({ ...current, ...nextErrors }));
                    setAreaNotice("delete", null);
                    if (Object.keys(nextErrors).length > 0) return;

                    deleteAccountMutation.mutate({
                      currentPassword: deletePassword,
                      confirmationText: deleteConfirmation.trim(),
                    });
                  }}
                  className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-red-600 px-4 text-sm font-semibold text-white transition hover:bg-red-500 disabled:cursor-not-allowed disabled:opacity-45"
                >
                  {deleteAccountMutation.isPending ? (
                    <LoaderCircle size={17} className="animate-spin" />
                  ) : (
                    <Trash2 size={17} />
                  )}
                  Həmişəlik sil
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
      <Footer />
    </div>
  );
}
