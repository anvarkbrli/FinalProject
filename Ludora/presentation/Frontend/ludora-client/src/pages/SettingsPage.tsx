import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  Ban,
  BellRing,
  Check,
  ChevronRight,
  ExternalLink,
  Gamepad2,
  KeyRound,
  LoaderCircle,
  LockKeyhole,
  Mail,
  Moon,
  RefreshCw,
  ShieldCheck,
  Sun,
  Unplug,
  UserRoundPen,
  UserX,
  X,
} from "lucide-react";
import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { NotificationSettingsPanel } from "../components/notifications/NotificationSettingsPanel";
import { EditProfilePage } from "./EditProfilePage";
import { getApiErrorMessage } from "../lib/api";
import { applyTheme, getActiveTheme, type WellPlaydTheme } from "../lib/theme";
import {
  changePassword,
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
import type { SteamConnectionMessage } from "../types/steam";

type SettingsSection =
  | "profile"
  | "account"
  | "notifications"
  | "integrations"
  | "blocked";

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
    id: "account",
    label: "Hesab",
    description: "Email, şifrə, məxfilik və görünüş",
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

function getInitialSteamNotice(): NoticeState | null {
  const query = new URLSearchParams(window.location.search);

  if (!query.has("steamSuccess")) return null;

  const success = query.get("steamSuccess") === "true";
  return {
    type: success ? "success" : "error",
    message: success
      ? `${query.get("steamName") ?? "Steam hesabı"} uğurla bağlandı.`
      : query.get("steamError") ?? "Steam hesabı bağlanmadı.",
  };
}

export function SettingsPage() {
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const activeSection = parseSettingsSection(
    searchParams.get("section"),
    searchParams.has("steamSuccess"),
  );
  const storedUser = useAuthStore((state) => state.user);
  const setUser = useAuthStore((state) => state.setUser);
  const [newEmail, setNewEmail] = useState("");
  const [emailPassword, setEmailPassword] = useState("");
  const [passwordCurrent, setPasswordCurrent] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [notice, setNotice] = useState<NoticeState | null>(getInitialSteamNotice);
  const [theme, setTheme] = useState<WellPlaydTheme>(() => getActiveTheme());

  const selectTheme = (nextTheme: WellPlaydTheme) => {
    applyTheme(nextTheme);
    setTheme(nextTheme);
    setNotice({
      type: "success",
      message: nextTheme === "dark"
        ? "Tünd görünüş aktiv edildi."
        : "Açıq görünüş aktiv edildi.",
    });
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
    onSuccess: async (settings) => {
      if (userQuery.data) {
        setUser({ ...userQuery.data, isPrivate: settings.isPrivate });
      }
      setNotice({ type: "success", message: "Məxfilik seçimi yeniləndi." });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["user-settings"] }),
        queryClient.invalidateQueries({ queryKey: ["current-user"] }),
        queryClient.invalidateQueries({ queryKey: ["user-profile"] }),
      ]);
    },
    onError: (error) => setNotice({
      type: "error",
      message: getApiErrorMessage(error, "Məxfilik seçimi yenilənmədi."),
    }),
  });

  const emailMutation = useMutation({
    mutationFn: requestEmailChange,
    onSuccess: (result) => {
      setNewEmail("");
      setEmailPassword("");
      setNotice({ type: "success", message: result.message });
    },
    onError: (error) => setNotice({
      type: "error",
      message: getApiErrorMessage(error, "Email dəyişiklik sorğusu göndərilmədi."),
    }),
  });


  const passwordMutation = useMutation({
    mutationFn: changePassword,
    onSuccess: (result) => {
      setPasswordCurrent("");
      setNewPassword("");
      setConfirmNewPassword("");
      setNotice({ type: "success", message: result.message });
    },
    onError: (error) => setNotice({
      type: "error",
      message: getApiErrorMessage(error, "Şifrə dəyişdirilmədi."),
    }),
  });

  const unblockMutation = useMutation({
    mutationFn: unblockUser,
    onSuccess: async () => {
      setNotice({ type: "success", message: "İstifadəçinin bloku açıldı." });
      await queryClient.invalidateQueries({ queryKey: ["blocked-users"] });
    },
    onError: (error) => setNotice({
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
      setNotice({
        type: "error",
        message: getApiErrorMessage(error, "Steam bağlantısı başladılmadı."),
      });
    },
  });

  const steamDisconnectMutation = useMutation({
    mutationFn: disconnectSteam,
    onSuccess: async () => {
      setNotice({ type: "success", message: "Steam hesabı ayrıldı." });
      await queryClient.invalidateQueries({ queryKey: ["steam-connection"] });
    },
    onError: (error) => setNotice({
      type: "error",
      message: getApiErrorMessage(error, "Steam hesabı ayrıla bilmədi."),
    }),
  });

  const steamSyncMutation = useMutation({
    mutationFn: syncSteamOwnedGames,
    onSuccess: async (result) => {
      setNotice({
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
    onError: (error) => setNotice({
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
        setNotice({
          type: "success",
          message: `${event.data.steamName ?? "Steam hesabı"} uğurla bağlandı.`,
        });
        await queryClient.invalidateQueries({ queryKey: ["steam-connection"] });
        setSearchParams({ section: "integrations" }, { replace: true });
        return;
      }

      setNotice({
        type: "error",
        message: event.data.error ?? "Steam hesabı bağlanmadı.",
      });
    };

    window.addEventListener("message", handleSteamMessage);
    return () => window.removeEventListener("message", handleSteamMessage);
  }, [queryClient, setSearchParams]);

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
  const settings = settingsQuery.data ?? { isPrivate: user.isPrivate };
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
              <ShieldCheck size={22} />
            </span>
            <div>
              <h1 className="text-3xl font-bold tracking-tight text-white">Parametrlər</h1>
              <p className="mt-2 text-sm leading-6 text-zinc-500">
                Profilini, hesab təhlükəsizliyini, bildirişləri və inteqrasiyaları bir yerdən idarə et.
              </p>
            </div>
          </div>
        </div>

        {notice && (
          <div className={`mt-6 flex items-start gap-3 rounded-2xl border px-4 py-3 text-sm ${
            notice.type === "success"
              ? "border-emerald-400/15 bg-emerald-500/[0.06] text-emerald-200"
              : "border-red-400/15 bg-red-500/[0.06] text-red-200"
          }`}>
            {notice.type === "success" ? <Check size={18} /> : <X size={18} />}
            {notice.message}
          </div>
        )}

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
                      setNotice(null);
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
          </section>
          )}

          {activeSection === "account" && (
          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
            <div className="flex items-start gap-3">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                {theme === "dark" ? <Moon size={20} /> : <Sun size={20} />}
              </span>
              <div>
                <h2 className="font-semibold text-white">Görünüş</h2>
                <p className="mt-1 text-xs leading-5 text-zinc-600">
                  Ludora interfeysinin rəng rejimini seç. Seçimin bu brauzerdə yadda qalacaq.
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
                        <ExternalLink size={15} /> Profil
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
                      onClick={() => steamDisconnectMutation.mutate()}
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
                        setNotice({
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
                  onChange={(event) => setNewEmail(event.target.value)}
                  className="mt-2 h-11 w-full rounded-xl border border-white/[0.08] bg-white/[0.03] px-3 text-sm text-zinc-100 outline-none focus:border-violet-400/50"
                />
              </label>
              <label className="block">
                <span className="text-xs font-semibold text-zinc-400">Cari şifrə</span>
                <div className="relative mt-2">
                  <KeyRound size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-600" />
                  <input
                    type="password"
                    value={emailPassword}
                    onChange={(event) => setEmailPassword(event.target.value)}
                    className="h-11 w-full rounded-xl border border-white/[0.08] bg-white/[0.03] pl-10 pr-3 text-sm text-zinc-100 outline-none focus:border-violet-400/50"
                  />
                </div>
              </label>
            </div>
            <button
              type="button"
              onClick={() => {
                setNotice(null);
                emailMutation.mutate({ newEmail: newEmail.trim(), currentPassword: emailPassword });
              }}
              disabled={emailMutation.isPending || !newEmail.trim() || !emailPassword}
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
              ) : (blockedUsersQuery.data?.length ?? 0) === 0 ? (
                <p className="rounded-2xl border border-dashed border-white/[0.07] p-5 text-sm text-zinc-600">Bloklanmış hesab yoxdur.</p>
              ) : blockedUsersQuery.data?.map((blockedUser) => (
                <div key={blockedUser.id} className="flex items-center justify-between gap-4 rounded-2xl border border-white/[0.06] bg-white/[0.02] p-4">
                  <Link to={`/users/${blockedUser.userName}`} className="font-semibold text-zinc-200 hover:text-violet-300">{blockedUser.userName}</Link>
                  <button type="button" onClick={() => unblockMutation.mutate(blockedUser.id)} className="flex h-9 items-center gap-2 rounded-lg border border-red-400/10 bg-red-500/[0.04] px-3 text-xs font-semibold text-red-300"><UserX size={15} /> Bloku aç</button>
                </div>
              ))}
            </div>
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
                  onChange={(event) => setPasswordCurrent(event.target.value)}
                  className="mt-2 h-11 w-full rounded-xl border border-white/[0.08] bg-white/[0.03] px-3 text-sm text-zinc-100 outline-none focus:border-violet-400/50"
                />
              </label>
              <label className="block">
                <span className="text-xs font-semibold text-zinc-400">Yeni şifrə</span>
                <input
                  type="password"
                  autoComplete="new-password"
                  value={newPassword}
                  onChange={(event) => setNewPassword(event.target.value)}
                  className="mt-2 h-11 w-full rounded-xl border border-white/[0.08] bg-white/[0.03] px-3 text-sm text-zinc-100 outline-none focus:border-violet-400/50"
                />
              </label>
              <label className="block">
                <span className="text-xs font-semibold text-zinc-400">Yeni şifrəni təkrarla</span>
                <input
                  type="password"
                  autoComplete="new-password"
                  value={confirmNewPassword}
                  onChange={(event) => setConfirmNewPassword(event.target.value)}
                  className="mt-2 h-11 w-full rounded-xl border border-white/[0.08] bg-white/[0.03] px-3 text-sm text-zinc-100 outline-none focus:border-violet-400/50"
                />
              </label>
            </div>

            <button
              type="button"
              onClick={() => {
                setNotice(null);

                if (newPassword.length < 8) {
                  setNotice({ type: "error", message: "Yeni şifrə minimum 8 simvol olmalıdır." });
                  return;
                }

                if (newPassword !== confirmNewPassword) {
                  setNotice({ type: "error", message: "Yeni şifrə və təkrar şifrə eyni deyil." });
                  return;
                }

                passwordMutation.mutate({
                  currentPassword: passwordCurrent,
                  newPassword,
                  confirmNewPassword,
                });
              }}
              disabled={
                passwordMutation.isPending ||
                !passwordCurrent ||
                !newPassword ||
                !confirmNewPassword
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
          </div>
        </div>
      </main>
      <Footer />
    </div>
  );
}
