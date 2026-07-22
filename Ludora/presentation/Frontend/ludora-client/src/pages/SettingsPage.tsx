import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  Ban,
  Check,
  ExternalLink,
  Gamepad2,
  KeyRound,
  LoaderCircle,
  LockKeyhole,
  Mail,
  RefreshCw,
  ShieldCheck,
  Unplug,
  UserCheck,
  UserRoundPen,
  UserX,
  X,
} from "lucide-react";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { NotificationSettingsPanel } from "../components/notifications/NotificationSettingsPanel";
import { getApiErrorMessage } from "../lib/api";
import { resolveAssetUrl } from "../lib/helpers";
import {
  acceptFollowRequest,
  changePassword,
  getBlockedUsers,
  getCurrentUser,
  getFollowRequests,
  getUserSettings,
  rejectFollowRequest,
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
  const storedUser = useAuthStore((state) => state.user);
  const setUser = useAuthStore((state) => state.setUser);
  const [newEmail, setNewEmail] = useState("");
  const [emailPassword, setEmailPassword] = useState("");
  const [passwordCurrent, setPasswordCurrent] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [notice, setNotice] = useState<NoticeState | null>(getInitialSteamNotice);

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
    queryFn: getBlockedUsers,
  });

  const followRequestsQuery = useQuery({
    queryKey: ["follow-requests"],
    queryFn: getFollowRequests,
  });

  const steamConnectionQuery = useQuery({
    queryKey: ["steam-connection"],
    queryFn: getSteamConnectionStatus,
  });

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

  const acceptMutation = useMutation({
    mutationFn: acceptFollowRequest,
    onSuccess: async () => {
      setNotice({ type: "success", message: "İzləmə sorğusu qəbul edildi." });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["follow-requests"] }),
        queryClient.invalidateQueries({ queryKey: ["current-user"] }),
        queryClient.invalidateQueries({ queryKey: ["user-profile"] }),
      ]);
    },
    onError: (error) => setNotice({
      type: "error",
      message: getApiErrorMessage(error, "İzləmə sorğusu qəbul edilmədi."),
    }),
  });

  const rejectMutation = useMutation({
    mutationFn: rejectFollowRequest,
    onSuccess: async () => {
      setNotice({ type: "success", message: "İzləmə sorğusu rədd edildi." });
      await queryClient.invalidateQueries({ queryKey: ["follow-requests"] });
    },
    onError: (error) => setNotice({
      type: "error",
      message: getApiErrorMessage(error, "İzləmə sorğusu rədd edilmədi."),
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
        document.getElementById("export")?.scrollIntoView({ behavior: "smooth" });
        return;
      }

      setNotice({
        type: "error",
        message: event.data.error ?? "Steam hesabı bağlanmadı.",
      });
    };

    window.addEventListener("message", handleSteamMessage);
    return () => window.removeEventListener("message", handleSteamMessage);
  }, [queryClient]);

  useEffect(() => {
    const query = new URLSearchParams(window.location.search);
    if (query.get("section") === "export" || query.has("steamSuccess")) {
      requestAnimationFrame(() => {
        document.getElementById("export")?.scrollIntoView({ behavior: "smooth" });
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
          <LoaderCircle size={20} className="animate-spin" /> Ayarlar yüklənir...
        </main>
      </div>
    );
  }

  const user = userQuery.data;
  const settings = settingsQuery.data ?? { isPrivate: user.isPrivate };
  const profileImageUrl = resolveAssetUrl(user.profileImageUrl);

  return (
    <div className="min-h-screen wellplayd-page-bg text-zinc-100">
      <Navbar />
      <main className="mx-auto max-w-5xl px-5 py-10 lg:px-8 lg:py-14">
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
              <h1 className="text-3xl font-bold tracking-tight text-white">Ayarlar</h1>
              <p className="mt-2 text-sm leading-6 text-zinc-500">
                Hesab təhlükəsizliyi, məxfilik və sistem sorğularını idarə et.
              </p>
            </div>
          </div>
          <Link
            to="/profile/edit"
            className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-white/[0.09] bg-white/[0.04] px-4 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.08] hover:text-white"
          >
            <UserRoundPen size={17} /> Profil redaktəsi
          </Link>
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

        <div className="mt-7 space-y-6">
          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
            <div className="flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-center gap-4">
                <span className="flex h-12 w-12 items-center justify-center overflow-hidden rounded-xl bg-violet-500/10 font-bold text-violet-300">
                  {profileImageUrl ? (
                    <img src={profileImageUrl} alt="Profil şəkli" className="h-full w-full object-cover" />
                  ) : (
                    user.userName.charAt(0).toUpperCase()
                  )}
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

          <NotificationSettingsPanel />

          <section id="export" className="scroll-mt-24 rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
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

          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
            <div className="flex items-center gap-3">
              <UserCheck size={19} className="text-emerald-300" />
              <div>
                <h2 className="font-semibold text-white">İzləmə sorğuları</h2>
                <p className="mt-1 text-xs text-zinc-600">Gizli profilə gələn sorğuları idarə et.</p>
              </div>
            </div>
            <div className="mt-5 space-y-3">
              {followRequestsQuery.isLoading ? (
                <div className="flex items-center gap-2 text-sm text-zinc-600"><LoaderCircle size={16} className="animate-spin" /> Yüklənir...</div>
              ) : (followRequestsQuery.data?.length ?? 0) === 0 ? (
                <p className="rounded-2xl border border-dashed border-white/[0.07] p-5 text-sm text-zinc-600">Gözləyən izləmə sorğusu yoxdur.</p>
              ) : followRequestsQuery.data?.map((request) => (
                <div key={request.userId} className="flex flex-col gap-3 rounded-2xl border border-white/[0.06] bg-white/[0.02] p-4 sm:flex-row sm:items-center sm:justify-between">
                  <Link to={`/users/${request.userName}`} className="font-semibold text-zinc-200 hover:text-violet-300">{request.userName}</Link>
                  <div className="flex gap-2">
                    <button type="button" onClick={() => acceptMutation.mutate(request.userId)} className="flex h-9 items-center gap-2 rounded-lg bg-emerald-500/10 px-3 text-xs font-semibold text-emerald-300"><Check size={15} /> Qəbul et</button>
                    <button type="button" onClick={() => rejectMutation.mutate(request.userId)} className="flex h-9 items-center gap-2 rounded-lg bg-red-500/10 px-3 text-xs font-semibold text-red-300"><X size={15} /> Rədd et</button>
                  </div>
                </div>
              ))}
            </div>
          </section>

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
        </div>
      </main>
      <Footer />
    </div>
  );
}
