import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ArrowLeft,
  Award,
  Ban,
  BellRing,
  CalendarDays,
  ChevronRight,
  CircleAlert,
  FileText,
  History,
  LoaderCircle,
  LockKeyhole,
  Mail,
  MessageSquareText,
  RotateCcw,
  Save,
  ShieldCheck,
  TriangleAlert,
  Trash2,
  UserRound,
  UsersRound,
} from "lucide-react";
import type { FormEvent, ReactNode } from "react";
import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getApiErrorMessage } from "../../lib/api";
import { hasAuthRole } from "../../lib/authRoles";
import { useConfirmation } from "../../lib/confirmation";
import {
  formatDate,
  formatDateTime,
  resolveAssetUrl,
} from "../../lib/helpers";
import { getModerationActionTitle } from "../../lib/moderationAudit";
import { useI18n } from "../../i18n/useI18n";
import {
  assignAdminBadgeToUser,
  banAdminUser,
  changeAdminUserRole,
  getAdminBadges,
  getAdminUser,
  getAdminUserBanHistory,
  getAdminUserBadges,
  getAdminUserWarnings,
  getModerationAuditByUser,
  revokeAdminBadgeFromUser,
  unbanAdminUser,
  warnAdminUser,
} from "../../services/adminService";
import { useAuthStore } from "../../stores/authStore";
import type { BanType } from "../../types/admin";

interface Notice {
  type: "success" | "error";
  message: string;
}

export function AdminUserDetailPage() {
  const { language } = useI18n();
  const { userId } = useParams<{ userId: string }>();
  const queryClient = useQueryClient();
  const requestConfirmation = useConfirmation();
  const [notice, setNotice] = useState<Notice | null>(null);
  const [warningMessage, setWarningMessage] = useState("");
  const [banType, setBanType] = useState<BanType>(1);
  const [banDuration, setBanDuration] = useState("1440");
  const [banReason, setBanReason] = useState("");
  const [selectedRole, setSelectedRole] = useState("");
  const [selectedBadgeId, setSelectedBadgeId] = useState("");
  const accessToken = useAuthStore((state) => state.accessToken);
  const currentUser = useAuthStore((state) => state.user);
  const isCurrentAdmin = hasAuthRole(
    accessToken,
    "Admin",
    currentUser?.roles ?? [],
  );

  const detailQuery = useQuery({
    queryKey: ["admin", "user", userId],
    queryFn: ({ signal }) => getAdminUser(userId!, signal),
    enabled: Boolean(userId),
    refetchInterval: 10_000,
  });

  const warningsQuery = useQuery({
    queryKey: ["admin", "user", userId, "warnings"],
    queryFn: ({ signal }) => getAdminUserWarnings(userId!, signal),
    enabled: Boolean(userId),
  });

  const banHistoryQuery = useQuery({
    queryKey: ["admin", "user", userId, "ban-history"],
    queryFn: ({ signal }) => getAdminUserBanHistory(userId!, signal),
    enabled: Boolean(userId),
  });

  const auditQuery = useQuery({
    queryKey: ["admin", "user", userId, "audit"],
    queryFn: ({ signal }) => getModerationAuditByUser(userId!, signal),
    enabled: Boolean(userId),
  });

  const userBadgesQuery = useQuery({
    queryKey: ["admin", "user", userId, "badges"],
    queryFn: ({ signal }) => getAdminUserBadges(userId!, signal),
    enabled: Boolean(userId) && isCurrentAdmin,
  });

  const badgeDefinitionsQuery = useQuery({
    queryKey: ["admin", "badges"],
    queryFn: ({ signal }) => getAdminBadges(signal),
    enabled: isCurrentAdmin,
  });

  async function refreshUserData() {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ["admin", "user", userId],
      }),
      queryClient.invalidateQueries({
        queryKey: ["admin", "users"],
      }),
      queryClient.invalidateQueries({
        queryKey: ["admin", "dashboard"],
      }),
    ]);
  }

  const warningMutation = useMutation({
    mutationFn: (message: string) => warnAdminUser(userId!, message),
    onSuccess: async () => {
      setWarningMessage("");
      setNotice({
        type: "success",
        message: "Xəbərdarlıq istifadəçiyə göndərildi.",
      });
      await Promise.all([
        refreshUserData(),
        queryClient.invalidateQueries({
          queryKey: ["admin", "user", userId, "warnings"],
        }),
        queryClient.invalidateQueries({
          queryKey: ["admin", "user", userId, "audit"],
        }),
      ]);
    },
    onError: (error) => setNotice({
      type: "error",
      message: getApiErrorMessage(
        error,
        "Xəbərdarlıq göndərilmədi.",
      ),
    }),
  });

  const banMutation = useMutation({
    mutationFn: () =>
      banAdminUser(userId!, {
        type: banType,
        durationMinutes: banType === 1
          ? Number(banDuration)
          : undefined,
        reason: banReason.trim(),
      }),
    onSuccess: async () => {
      setBanReason("");
      setNotice({
        type: "success",
        message: "İstifadəçi banlandı.",
      });
      await Promise.all([
        refreshUserData(),
        queryClient.invalidateQueries({
          queryKey: ["admin", "user", userId, "ban-history"],
        }),
        queryClient.invalidateQueries({
          queryKey: ["admin", "user", userId, "audit"],
        }),
      ]);
    },
    onError: (error) => setNotice({
      type: "error",
      message: getApiErrorMessage(error, "Ban əməliyyatı uğursuz oldu."),
    }),
  });

  const unbanMutation = useMutation({
    mutationFn: () => unbanAdminUser(userId!),
    onSuccess: async () => {
      setNotice({
        type: "success",
        message: "İstifadəçinin banı açıldı.",
      });
      await Promise.all([
        refreshUserData(),
        queryClient.invalidateQueries({
          queryKey: ["admin", "user", userId, "ban-history"],
        }),
        queryClient.invalidateQueries({
          queryKey: ["admin", "user", userId, "audit"],
        }),
      ]);
    },
    onError: (error) => setNotice({
      type: "error",
      message: getApiErrorMessage(error, "Ban açılmadı."),
    }),
  });

  const roleMutation = useMutation({
    mutationFn: (role: string) =>
      changeAdminUserRole(userId!, role),
    onSuccess: async (updatedUser) => {
      setSelectedRole(updatedUser.role);
      setNotice({
        type: "success",
        message: `İstifadəçi rolu ${updatedUser.role} olaraq yeniləndi.`,
      });
      await refreshUserData();
    },
    onError: (error) => setNotice({
      type: "error",
      message: getApiErrorMessage(error, "Rol dəyişdirilmədi."),
    }),
  });

  const assignBadgeMutation = useMutation({
    mutationFn: () =>
      assignAdminBadgeToUser(userId!, Number(selectedBadgeId)),
    onSuccess: async (badge) => {
      setSelectedBadgeId("");
      setNotice({
        type: "success",
        message: `“${badge.name}” nişanı istifadəçiyə verildi.`,
      });
      await queryClient.invalidateQueries({
        queryKey: ["admin", "user", userId, "badges"],
      });
    },
    onError: (mutationError) =>
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          mutationError,
          "Nişan istifadəçiyə verilə bilmədi.",
        ),
      }),
  });

  const revokeBadgeMutation = useMutation({
    mutationFn: (badgeId: number) =>
      revokeAdminBadgeFromUser(userId!, badgeId),
    onSuccess: async () => {
      setNotice({
        type: "success",
        message: "Nişan istifadəçidən geri alındı.",
      });
      await queryClient.invalidateQueries({
        queryKey: ["admin", "user", userId, "badges"],
      });
    },
    onError: (mutationError) =>
      setNotice({
        type: "error",
        message: getApiErrorMessage(
          mutationError,
          "Nişan geri alına bilmədi.",
        ),
      }),
  });

  if (!userId) {
    return <ErrorState message="İstifadəçi ID-si düzgün deyil." />;
  }

  if (detailQuery.isPending) {
    return <DetailSkeleton />;
  }

  if (detailQuery.isError || !detailQuery.data) {
    return (
      <ErrorState
        message={getApiErrorMessage(
          detailQuery.error,
          "İstifadəçi məlumatı tapılmadı.",
        )}
      />
    );
  }

  const user = detailQuery.data;
  const avatarUrl = resolveAssetUrl(user.profileImageUrl);
  const roleValue = selectedRole || user.role;
  const userBadges = userBadgesQuery.data ?? [];
  const assignedBadgeIds = new Set(
    userBadges.map((badge) => badge.id),
  );
  const availableBadges = (badgeDefinitionsQuery.data ?? []).filter(
    (badge) => badge.isActive && !assignedBadgeIds.has(badge.id),
  );
  const isBusy =
    warningMutation.isPending ||
    banMutation.isPending ||
    unbanMutation.isPending ||
    roleMutation.isPending ||
    assignBadgeMutation.isPending ||
    revokeBadgeMutation.isPending;

  function submitWarning(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const message = warningMessage.trim();
    if (!message) {
      setNotice({
        type: "error",
        message: "Xəbərdarlıq mətni boş ola bilməz.",
      });
      return;
    }
    setNotice(null);
    warningMutation.mutate(message);
  }

  async function submitBan(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!banReason.trim()) {
      setNotice({
        type: "error",
        message: "Ban səbəbini daxil et.",
      });
      return;
    }
    if (
      banType === 1 &&
      (!Number.isFinite(Number(banDuration)) || Number(banDuration) < 1)
    ) {
      setNotice({
        type: "error",
        message: "Ban müddəti ən az 1 dəqiqə olmalıdır.",
      });
      return;
    }

    const confirmed = await requestConfirmation({
      title: `${user.userName} istifadəçisi banlansın?`,
      description:
        banType === 1
          ? `İstifadəçinin hesaba girişi ${banDuration} dəqiqə müddətinə məhdudlaşdırılacaq.`
          : "İstifadəçinin hesaba girişi müddətsiz məhdudlaşdırılacaq.",
      detail: `Səbəb: ${banReason.trim()}`,
      confirmLabel:
        banType === 1 ? "Müddətli ban ver" : "Müddətsiz ban ver",
      kind: "moderation",
      tone: "danger",
    });

    if (!confirmed) return;

    setNotice(null);
    banMutation.mutate();
  }

  function handleUnban() {
    const confirmed = window.confirm(
      `${user.userName} istifadəçisinin banını açmaq istəyirsən?`,
    );
    if (confirmed) {
      setNotice(null);
      unbanMutation.mutate();
    }
  }

  return (
    <div className="space-y-6">
      <Link
        to="/admin/users"
        className="inline-flex items-center gap-2 text-sm font-medium text-zinc-500 transition hover:text-violet-300"
      >
        <ArrowLeft size={16} />
        İstifadəçilərə qayıt
      </Link>

      {notice && (
        <div
          role="status"
          className={`flex items-start justify-between gap-4 rounded-xl border px-4 py-3 text-sm ${
            notice.type === "success"
              ? "border-emerald-400/15 bg-emerald-500/[0.07] text-emerald-200"
              : "border-red-400/15 bg-red-500/[0.07] text-red-200"
          }`}
        >
          <span className="flex items-start gap-2">
            {notice.type === "success" ? (
              <ShieldCheck size={17} className="mt-0.5 shrink-0" />
            ) : (
              <CircleAlert size={17} className="mt-0.5 shrink-0" />
            )}
            {notice.message}
          </span>
          <button
            type="button"
            className="text-xs opacity-70 hover:opacity-100"
            onClick={() => setNotice(null)}
          >
            Bağla
          </button>
        </div>
      )}

      <section className="overflow-hidden rounded-2xl border border-white/[0.07] bg-white/[0.025]">
        <div className="relative h-28 bg-gradient-to-r from-violet-950/70 via-fuchsia-950/30 to-[#0b0b0f]">
          <div className="absolute inset-0 opacity-60 [background-image:radial-gradient(circle_at_30%_20%,rgba(139,92,246,0.28),transparent_35%)]" />
        </div>
        <div className="px-5 pb-6 sm:px-7">
          <div className="-mt-10 flex flex-col justify-between gap-5 sm:flex-row sm:items-end">
            <div className="flex items-end gap-4">
              <span className="relative flex h-20 w-20 shrink-0 items-center justify-center overflow-hidden rounded-2xl border-4 border-[#0b0b0f] bg-violet-500/15 text-2xl font-bold text-violet-200">
                {avatarUrl ? (
                  <img
                    src={avatarUrl}
                    alt={`${user.userName} profil şəkli`}
                    className="h-full w-full object-cover"
                  />
                ) : (
                  user.userName.charAt(0).toUpperCase()
                )}
              </span>
              <div className="pb-1">
                <div className="flex flex-wrap items-center gap-2">
                  <h2 className="text-2xl font-bold text-white">
                    {user.userName}
                  </h2>
                  <span className="rounded-full bg-violet-500/10 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-violet-200">
                    {user.role}
                  </span>
                  <span className={`rounded-full px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider ${
                    user.isOnline
                      ? "bg-emerald-500/10 text-emerald-300"
                      : "bg-zinc-500/10 text-zinc-500"
                  }`}>
                    {user.isOnline ? "Onlayn" : "Oflayn"}
                  </span>
                  {user.isBanned && (
                    <span className="rounded-full bg-red-500/10 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-red-300">
                      Banlı
                    </span>
                  )}
                </div>
                <p className="mt-2 flex items-center gap-2 text-sm text-zinc-500">
                  <Mail size={14} />
                  {user.email}
                </p>
              </div>
            </div>
            <p className="flex items-center gap-2 text-xs text-zinc-600">
              <CalendarDays size={14} />
              Qeydiyyat: {formatDate(user.createdAt)}
            </p>
          </div>

          {user.bio && (
            <p className="mt-5 max-w-3xl text-sm leading-6 text-zinc-400">
              {user.bio}
            </p>
          )}
        </div>
      </section>

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
        <StatCard
          icon={<UsersRound size={17} />}
          label="İzləyici"
          value={user.followerCount}
          to={`/users/${encodeURIComponent(user.userName)}?connections=followers`}
        />
        <StatCard
          icon={<UserRound size={17} />}
          label="İzlənən"
          value={user.followingCount}
          to={`/users/${encodeURIComponent(user.userName)}?connections=following`}
        />
        <StatCard
          icon={<FileText size={17} />}
          label="Post"
          value={user.postCount}
          detail={`${user.deletedPostCount} silinib`}
          to={`/admin/posts?userId=${encodeURIComponent(user.id)}`}
        />
        <StatCard
          icon={<MessageSquareText size={17} />}
          label="Müzakirə"
          value={user.discussionCount}
          to={`/admin/forum?tab=discussions&search=${encodeURIComponent(user.userName)}`}
        />
        <StatCard
          icon={<BellRing size={17} />}
          label="Warning"
          value={user.warningCount}
          detail={`${user.unreadWarningCount} oxunmayıb`}
          href="#warning-history"
        />
        <StatCard
          icon={<TriangleAlert size={17} />}
          label="Şikayət"
          value={user.reportCount}
          detail={`${user.pendingReportCount} açıq`}
          to={
            user.reportCount > 0
              ? `/admin/reports/users/${encodeURIComponent(user.id)}`
              : `/admin/reports?search=${encodeURIComponent(user.userName)}`
          }
        />
      </section>

      <div className="grid gap-6 xl:grid-cols-[0.9fr_1.1fr]">
        <div className="space-y-6">
          {isCurrentAdmin && (
            <Panel
            title="Rol idarəetməsi"
            description="İstifadəçinin sistem rolunu dəyiş."
            icon={<ShieldCheck size={18} />}
          >
            <form
              onSubmit={async (event) => {
                event.preventDefault();
                const confirmed =
                  await requestConfirmation({
                    title: `${user.userName} istifadəçisinin rolu dəyişsin?`,
                    description: `İstifadəçinin sistem rolu “${user.role}” rolundan “${roleValue}” roluna dəyişdiriləcək.`,
                    detail:
                      roleValue === "Admin"
                        ? "Admin rolu idarə panelində bütün inzibati əməliyyatlara giriş verir."
                        : roleValue === "Moderator"
                          ? "Moderator admin CRUD əməliyyatlarına daxil ola bilər, lakin silmə əməliyyatlarını yerinə yetirə bilməz."
                          : "İstifadəçinin admin və moderator səlahiyyətləri geri alınacaq.",
                    confirmLabel: "Rolu dəyiş",
                    kind: "moderation",
                    tone:
                      roleValue === "Admin"
                        ? "danger"
                        : "warning",
                  });

                if (!confirmed) return;

                setNotice(null);
                roleMutation.mutate(roleValue);
              }}
              className="flex gap-3"
            >
              <select
                value={roleValue}
                onChange={(event) => setSelectedRole(event.target.value)}
                disabled={isBusy}
                className="h-11 min-w-0 flex-1 rounded-xl border border-white/[0.08] bg-[#09090d] px-3 text-sm text-zinc-200 outline-none focus:border-violet-500/50"
              >
                <option value="User">User</option>
                <option value="Moderator">Moderator</option>
                <option value="Admin">Admin</option>
              </select>
              <button
                type="submit"
                disabled={isBusy || roleValue === user.role}
                className="inline-flex h-11 items-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-40"
              >
                {roleMutation.isPending ? (
                  <LoaderCircle size={16} className="animate-spin" />
                ) : (
                  <Save size={16} />
                )}
                Yadda saxla
              </button>
            </form>
            </Panel>
          )}

          {isCurrentAdmin && (
            <Panel
              title="Nişanlar"
              description="İstifadəçiyə nişan ver və ya mövcud nişanı geri al."
              icon={<Award size={18} />}
            >
              <div className="space-y-4">
                <div className="flex gap-3">
                  <select
                    value={selectedBadgeId}
                    disabled={
                      isBusy ||
                      badgeDefinitionsQuery.isPending
                    }
                    onChange={(event) =>
                      setSelectedBadgeId(event.target.value)
                    }
                    className="h-11 min-w-0 flex-1 rounded-xl border border-white/[0.08] bg-[#09090d] px-3 text-sm text-zinc-200 outline-none focus:border-violet-500/50"
                  >
                    <option value="">
                      Veriləcək nişanı seç...
                    </option>
                    {availableBadges.map((badge) => (
                      <option key={badge.id} value={badge.id}>
                        {badge.name}
                        {badge.metric === "Manual"
                          ? " — Xüsusi"
                          : " — Avtomatik"}
                      </option>
                    ))}
                  </select>
                  <button
                    type="button"
                    disabled={!selectedBadgeId || isBusy}
                    onClick={() => assignBadgeMutation.mutate()}
                    className="inline-flex h-11 items-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white transition hover:bg-violet-500 disabled:opacity-40"
                  >
                    {assignBadgeMutation.isPending ? (
                      <LoaderCircle
                        size={16}
                        className="animate-spin"
                      />
                    ) : (
                      <Award size={16} />
                    )}
                    Nişan ver
                  </button>
                </div>

                {userBadgesQuery.isPending ? (
                  <InlineLoader />
                ) : userBadges.length > 0 ? (
                  <div className="max-h-[24rem] space-y-2 overflow-y-auto pr-1">
                    {userBadges.map((badge) => {
                      const iconUrl = resolveAssetUrl(badge.iconUrl);
                      return (
                        <article
                          key={badge.id}
                          className="flex items-center justify-between gap-3 rounded-xl border border-white/[0.06] bg-black/15 p-3"
                        >
                          <div className="flex min-w-0 items-center gap-3">
                            <span
                              className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-xl"
                              style={{
                                color: badge.color,
                                backgroundColor: `${badge.color}18`,
                              }}
                            >
                              {iconUrl ? (
                                <img
                                  src={iconUrl}
                                  alt=""
                                  className="h-full w-full object-cover"
                                />
                              ) : (
                                <Award size={18} />
                              )}
                            </span>
                            <div className="min-w-0">
                              <p className="truncate text-sm font-semibold text-zinc-200">
                                {badge.name}
                              </p>
                              <p className="truncate text-xs text-zinc-600">
                                {badge.description}
                              </p>
                            </div>
                          </div>
                          <button
                            type="button"
                            disabled={isBusy}
                            title="Nişanı geri al"
                            aria-label={`${badge.name} nişanını geri al`}
                            onClick={() => {
                              if (
                                window.confirm(
                                  `“${badge.name}” nişanı istifadəçidən geri alınsın?`,
                                )
                              ) {
                                revokeBadgeMutation.mutate(
                                  badge.id,
                                );
                              }
                            }}
                            className="rounded-lg p-2 text-zinc-600 transition hover:bg-red-500/10 hover:text-red-300 disabled:opacity-40"
                          >
                            <Trash2 size={15} />
                          </button>
                        </article>
                      );
                    })}
                  </div>
                ) : (
                  <EmptyText text="İstifadəçiyə hələ nişan verilməyib." />
                )}
              </div>
            </Panel>
          )}

          <Panel
            title="Xəbərdarlıq göndər"
            description="Xəbərdarlıq istifadəçiyə bildiriş kimi çatacaq."
            icon={<BellRing size={18} />}
          >
            <form onSubmit={submitWarning}>
              <textarea
                value={warningMessage}
                onChange={(event) => setWarningMessage(event.target.value)}
                disabled={isBusy}
                maxLength={1000}
                rows={4}
                placeholder="Qayda pozuntusunu aydın şəkildə izah et..."
                className="w-full resize-none rounded-xl border border-white/[0.08] bg-[#09090d] p-3 text-sm leading-6 text-zinc-200 outline-none placeholder:text-zinc-700 focus:border-amber-500/40"
              />
              <div className="mt-3 flex items-center justify-between">
                <span className="text-xs text-zinc-700">
                  {warningMessage.length}/1000
                </span>
                <button
                  type="submit"
                  disabled={isBusy}
                  className="inline-flex h-10 items-center gap-2 rounded-xl bg-amber-500/15 px-4 text-sm font-bold text-amber-200 transition hover:bg-amber-500/25 disabled:opacity-40"
                >
                  {warningMutation.isPending ? (
                    <LoaderCircle size={16} className="animate-spin" />
                  ) : (
                    <BellRing size={16} />
                  )}
                  Göndər
                </button>
              </div>
            </form>
          </Panel>

          <Panel
            title="Ban idarəetməsi"
            description={
              user.isBanned
                ? "Bu istifadəçinin hazırda aktiv banı var."
                : "Müddətli və ya müddətsiz ban tətbiq et."
            }
            icon={<Ban size={18} />}
            danger
          >
            {user.isBanned ? (
              <div>
                <div className="rounded-xl border border-red-400/10 bg-red-500/[0.05] p-4">
                  <p className="text-sm font-semibold text-red-200">
                    {user.isPermanentlyBanned
                      ? "Müddətsiz ban"
                      : `Bitiş: ${formatDateTime(user.bannedUntilUtc)}`}
                  </p>
                  {user.banReason && (
                    <p className="mt-2 text-sm leading-6 text-zinc-500">
                      {user.banReason}
                    </p>
                  )}
                </div>
                <button
                  type="button"
                  disabled={isBusy}
                  onClick={handleUnban}
                  className="mt-3 inline-flex h-10 items-center gap-2 rounded-xl bg-emerald-500/12 px-4 text-sm font-bold text-emerald-200 transition hover:bg-emerald-500/20 disabled:opacity-40"
                >
                  {unbanMutation.isPending ? (
                    <LoaderCircle size={16} className="animate-spin" />
                  ) : (
                    <RotateCcw size={16} />
                  )}
                  Banı aç
                </button>
              </div>
            ) : (
              <form onSubmit={submitBan} className="space-y-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <label>
                    <span className="mb-1.5 block text-xs font-medium text-zinc-500">
                      Ban növü
                    </span>
                    <select
                      value={banType}
                      onChange={(event) =>
                        setBanType(Number(event.target.value) as BanType)
                      }
                      disabled={isBusy}
                      className="h-11 w-full rounded-xl border border-white/[0.08] bg-[#09090d] px-3 text-sm text-zinc-200 outline-none focus:border-red-500/40"
                    >
                      <option value={1}>
                        {language === "en" ? "Temporary" : "Müddətli"}
                      </option>
                      <option value={2}>
                        {language === "en" ? "Permanent" : "Müddətsiz"}
                      </option>
                    </select>
                  </label>
                  {banType === 1 && (
                    <label>
                      <span className="mb-1.5 block text-xs font-medium text-zinc-500">
                        Müddət (dəqiqə)
                      </span>
                      <input
                        type="number"
                        min={1}
                        max={525600}
                        value={banDuration}
                        onChange={(event) => setBanDuration(event.target.value)}
                        disabled={isBusy}
                        className="h-11 w-full rounded-xl border border-white/[0.08] bg-[#09090d] px-3 text-sm text-zinc-200 outline-none focus:border-red-500/40"
                      />
                    </label>
                  )}
                </div>
                <textarea
                  value={banReason}
                  onChange={(event) => setBanReason(event.target.value)}
                  disabled={isBusy}
                  maxLength={500}
                  rows={3}
                  placeholder="Ban səbəbi..."
                  className="w-full resize-none rounded-xl border border-white/[0.08] bg-[#09090d] p-3 text-sm leading-6 text-zinc-200 outline-none placeholder:text-zinc-700 focus:border-red-500/40"
                />
                <button
                  type="submit"
                  disabled={isBusy}
                  className="inline-flex h-10 items-center gap-2 rounded-xl bg-red-500/15 px-4 text-sm font-bold text-red-200 transition hover:bg-red-500/25 disabled:opacity-40"
                >
                  {banMutation.isPending ? (
                    <LoaderCircle size={16} className="animate-spin" />
                  ) : (
                    <LockKeyhole size={16} />
                  )}
                  Ban tətbiq et
                </button>
              </form>
            )}
          </Panel>
        </div>

        <div className="space-y-6">
          <div id="warning-history" className="scroll-mt-24">
            <Panel
              title="Xəbərdarlıq tarixçəsi"
              description={`${warningsQuery.data?.totalWarningCount ?? user.warningCount} qeyd`}
              icon={<BellRing size={18} />}
            >
            {warningsQuery.isPending ? (
              <InlineLoader />
            ) : warningsQuery.data && warningsQuery.data.items.length > 0 ? (
              <div className="max-h-[25rem] space-y-3 overflow-y-auto pr-1">
                {warningsQuery.data.items.map((warning) => (
                  <article
                    key={warning.id}
                    className="rounded-xl border border-white/[0.06] bg-white/[0.02] p-4"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <p className="text-sm leading-6 text-zinc-300">
                        {warning.message}
                      </p>
                      <span className={`shrink-0 rounded-full px-2 py-1 text-[9px] font-bold uppercase ${
                        warning.isRead
                          ? "bg-white/[0.05] text-zinc-500"
                          : "bg-amber-500/10 text-amber-300"
                      }`}>
                        {warning.isRead ? "Oxunub" : "Yeni"}
                      </span>
                    </div>
                    <p className="mt-3 text-xs text-zinc-600">
                      {warning.warnedByUserName} ·{" "}
                      {formatDateTime(warning.createdAtUtc)}
                    </p>
                  </article>
                ))}
              </div>
              ) : (
                <EmptyText text="Xəbərdarlıq tarixçəsi boşdur." />
              )}
            </Panel>
          </div>

          <Panel
            title="Ban tarixçəsi"
            description={`${banHistoryQuery.data?.totalBanCount ?? 0} qeyd`}
            icon={<History size={18} />}
          >
            {banHistoryQuery.isPending ? (
              <InlineLoader />
            ) : banHistoryQuery.data &&
              banHistoryQuery.data.items.length > 0 ? (
              <div className="max-h-[24rem] space-y-3 overflow-y-auto pr-1">
                {banHistoryQuery.data.items.map((banItem) => (
                  <article
                    key={banItem.id}
                    className="rounded-xl border border-white/[0.06] bg-white/[0.02] p-4"
                  >
                    <div className="flex items-center justify-between gap-3">
                      <p className="text-sm font-semibold text-zinc-200">
                        {banItem.type === 2
                          ? "Müddətsiz ban"
                          : "Müddətli ban"}
                      </p>
                      <span className={`rounded-full px-2 py-1 text-[9px] font-bold uppercase ${
                        banItem.isActive
                          ? "bg-red-500/10 text-red-300"
                          : "bg-white/[0.05] text-zinc-500"
                      }`}>
                        {banItem.isActive
                          ? "Aktiv"
                          : banItem.wasManuallyUnbanned
                            ? "Açılıb"
                            : "Bitib"}
                      </span>
                    </div>
                    <p className="mt-2 text-sm leading-6 text-zinc-500">
                      {banItem.reason}
                    </p>
                    <p className="mt-3 text-xs text-zinc-700">
                      {banItem.bannedByUserName} ·{" "}
                      {formatDateTime(banItem.bannedAtUtc)}
                    </p>
                  </article>
                ))}
              </div>
            ) : (
              <EmptyText text="Ban tarixçəsi boşdur." />
            )}
          </Panel>

          <Panel
            title="Moderasiya auditı"
            description="Bu istifadəçi ilə bağlı admin əməliyyatları"
            icon={<ShieldCheck size={18} />}
          >
            {auditQuery.isPending ? (
              <InlineLoader />
            ) : auditQuery.data && auditQuery.data.length > 0 ? (
              <div className="max-h-[24rem] space-y-4 overflow-y-auto pr-1">
                {auditQuery.data.map((entry) => (
                  <article
                    key={entry.id}
                    className="relative border-l border-violet-400/20 pl-4"
                  >
                    <span className="absolute -left-1 top-1.5 h-2 w-2 rounded-full bg-violet-400" />
                    <p className="text-sm font-semibold text-zinc-300">
                      {getModerationActionTitle(entry, language)}
                    </p>
                    {entry.reason && (
                      <p className="mt-1 text-sm leading-6 text-zinc-600">
                        Qeyd: {entry.reason}
                      </p>
                    )}
                    <p className="mt-2 text-xs text-zinc-700">
                      Əməliyyatı edən: {entry.adminUserName} ·{" "}
                      {formatDateTime(entry.createdAtUtc)}
                    </p>
                  </article>
                ))}
              </div>
            ) : (
              <EmptyText text="Audit qeydi yoxdur." />
            )}
          </Panel>
        </div>
      </div>
    </div>
  );
}

interface StatCardProps {
  icon: ReactNode;
  label: string;
  value: number;
  detail?: string;
  to?: string;
  href?: string;
}

function StatCard({
  icon,
  label,
  value,
  detail,
  to,
  href,
}: StatCardProps) {
  const content = (
    <>
      <div className="flex items-start justify-between gap-3">
        <span className="text-violet-300">{icon}</span>
        {(to || href) && (
          <ChevronRight
            size={15}
            className="text-zinc-700 transition group-hover:translate-x-0.5 group-hover:text-violet-300"
          />
        )}
      </div>
      <p className="mt-4 text-xl font-bold text-white">{value}</p>
      <p className="mt-1 text-xs text-zinc-500">{label}</p>
      {detail && (
        <p className="mt-2 text-[10px] text-zinc-700">{detail}</p>
      )}
    </>
  );
  const className =
    "group block rounded-xl border border-white/[0.07] bg-white/[0.025] p-4 transition hover:-translate-y-0.5 hover:border-violet-400/25 hover:bg-violet-500/[0.06] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400/60";

  if (to) {
    return (
      <Link to={to} className={className}>
        {content}
      </Link>
    );
  }

  if (href) {
    return (
      <a href={href} className={className}>
        {content}
      </a>
    );
  }

  return <article className={className}>{content}</article>;
}

interface PanelProps {
  title: string;
  description: string;
  icon: ReactNode;
  danger?: boolean;
  children: ReactNode;
}

function Panel({
  title,
  description,
  icon,
  danger = false,
  children,
}: PanelProps) {
  return (
    <section className={`rounded-2xl border bg-white/[0.025] ${
      danger ? "border-red-400/10" : "border-white/[0.07]"
    }`}>
      <div className="flex items-center gap-3 border-b border-white/[0.06] p-5">
        <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${
          danger
            ? "bg-red-500/10 text-red-300"
            : "bg-violet-500/10 text-violet-300"
        }`}>
          {icon}
        </span>
        <div>
          <h3 className="font-bold text-white">{title}</h3>
          <p className="mt-0.5 text-xs text-zinc-600">{description}</p>
        </div>
      </div>
      <div className="p-5">{children}</div>
    </section>
  );
}

function InlineLoader() {
  return (
    <div className="flex h-24 items-center justify-center text-zinc-600">
      <LoaderCircle size={20} className="animate-spin" />
    </div>
  );
}

function EmptyText({ text }: { text: string }) {
  return (
    <p className="rounded-xl border border-dashed border-white/[0.07] px-4 py-8 text-center text-sm text-zinc-600">
      {text}
    </p>
  );
}

function ErrorState({ message }: { message: string }) {
  return (
    <div className="rounded-2xl border border-red-400/15 bg-red-500/[0.06] p-6">
      <CircleAlert className="text-red-300" size={22} />
      <h2 className="mt-3 font-semibold text-white">
        Məlumat yüklənmədi
      </h2>
      <p className="mt-2 text-sm text-zinc-500">{message}</p>
      <Link
        to="/admin/users"
        className="mt-5 inline-flex items-center gap-2 rounded-xl bg-white/[0.06] px-4 py-2 text-sm font-semibold text-zinc-200"
      >
        <ArrowLeft size={15} />
        Siyahıya qayıt
      </Link>
    </div>
  );
}

function DetailSkeleton() {
  return (
    <div className="animate-pulse space-y-6" aria-label="İstifadəçi yüklənir">
      <div className="h-6 w-40 rounded bg-white/[0.05]" />
      <div className="h-64 rounded-2xl border border-white/[0.05] bg-white/[0.025]" />
      <div className="grid gap-3 sm:grid-cols-3 xl:grid-cols-6">
        {Array.from({ length: 6 }).map((_, index) => (
          <div
            key={index}
            className="h-32 rounded-xl border border-white/[0.05] bg-white/[0.025]"
          />
        ))}
      </div>
    </div>
  );
}
