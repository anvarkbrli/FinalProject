import { useQuery } from "@tanstack/react-query";
import {
  AlertTriangle,
  Crown,
  LoaderCircle,
  LogOut,
  Search,
  Send,
  Shield,
  ShieldOff,
  Trash2,
  UserMinus,
  UsersRound,
  X,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { createPortal } from "react-dom";
import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/useI18n";
import { getApiErrorMessage } from "../../lib/api";
import { useConfirmation } from "../../lib/confirmation";
import { resolveAssetUrl } from "../../lib/helpers";
import {
  deleteForumCommunity,
  getCommunityMembers,
  inviteToCommunity,
  leaveForumCommunity,
  removeCommunityMember,
  searchCommunityInviteCandidates,
  updateCommunityMemberRole,
} from "../../services/forumCommunityService";
import type {
  ForumCommunityInviteCandidateDTO,
  ForumCommunityMemberDTO,
  ForumCommunityMemberRole,
} from "../../types/community";

export type CommunityOwnerModalMode =
  | "invite"
  | "members"
  | "delete"
  | "leave"
  | null;

interface CommunityOwnerManagementModalProps {
  mode: CommunityOwnerModalMode;
  communityId: number;
  communityName: string;
  viewerRole: "Owner" | "Moderator";
  onClose: () => void;
  onCommunityChanged: () => Promise<void>;
  onExited: () => void;
}

function getCandidateActionLabel(
  candidate: ForumCommunityInviteCandidateDTO,
): string {
  switch (candidate.membershipStatus) {
    case "Active":
      return "Artıq üzvdür";
    case "Invited":
      return "Dəvət göndərilib";
    case "Pending":
      return "Sorğu gözləyir";
    default:
      return "Dəvət et";
  }
}

function UserAvatar({
  userName,
  profileImageUrl,
}: {
  userName: string;
  profileImageUrl: string | null;
}) {
  const imageUrl = resolveAssetUrl(profileImageUrl);

  return (
    <div className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] text-sm font-black text-teal-300">
      {imageUrl ? (
        <img src={imageUrl} alt="" className="h-full w-full object-cover" />
      ) : (
        userName.charAt(0).toUpperCase()
      )}
    </div>
  );
}

function RoleBadge({ role }: { role: ForumCommunityMemberRole }) {
  if (role === "Moderator") {
    return (
      <span className="inline-flex items-center gap-1 rounded-full border border-violet-400/20 bg-violet-400/[0.08] px-2 py-0.5 text-[10px] font-bold text-violet-300">
        <Shield size={10} />
        Moderator
      </span>
    );
  }

  if (role === "Owner") {
    return (
      <span className="inline-flex items-center gap-1 rounded-full border border-amber-400/20 bg-amber-400/[0.08] px-2 py-0.5 text-[10px] font-bold text-amber-300">
        <Crown size={10} />
        Sahib
      </span>
    );
  }

  return (
    <span className="rounded-full border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] px-2 py-0.5 text-[10px] font-semibold text-[var(--wp-text-muted)]">
      Üzv
    </span>
  );
}

export function CommunityOwnerManagementModal({
  mode,
  communityId,
  communityName,
  viewerRole,
  onClose,
  onCommunityChanged,
  onExited,
}: CommunityOwnerManagementModalProps) {
  const { language } = useI18n();
  const requestConfirmation = useConfirmation();
  const [searchValue, setSearchValue] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [memberSearch, setMemberSearch] = useState("");
  const [isBusy, setIsBusy] = useState(false);
  const [activeUserId, setActiveUserId] = useState<string | null>(null);
  const [memberToRemove, setMemberToRemove] =
    useState<ForumCommunityMemberDTO | null>(null);
  const [error, setError] = useState("");
  const [successMessage, setSuccessMessage] = useState("");

  const isOwner = viewerRole === "Owner";

  const membersQuery = useQuery({
    queryKey: ["forum", "community", communityId, "members"],
    queryFn: ({ signal }) => getCommunityMembers(communityId, signal),
    enabled: mode === "members",
  });

  const candidatesQuery = useQuery({
    queryKey: [
      "forum",
      "community",
      communityId,
      "invite-candidates",
      debouncedSearch,
    ],
    queryFn: ({ signal }) =>
      searchCommunityInviteCandidates(
        communityId,
        debouncedSearch,
        signal,
      ),
    enabled: mode === "invite" && debouncedSearch.length >= 2,
  });

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(searchValue.trim());
    }, 350);
    return () => window.clearTimeout(timer);
  }, [searchValue]);

  useEffect(() => {
    if (mode === null) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape" || isBusy) return;
      if (memberToRemove) setMemberToRemove(null);
      else onClose();
    };

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isBusy, memberToRemove, mode, onClose]);

  const filteredMembers = useMemo(() => {
    const query = memberSearch.trim().toLocaleLowerCase("az");
    const members = membersQuery.data ?? [];
    if (!query) return members;
    return members.filter((member) =>
      member.userName.toLocaleLowerCase("az").includes(query),
    );
  }, [memberSearch, membersQuery.data]);

  if (mode === null) return null;

  async function runModalAction(
    action: () => Promise<void>,
    options?: { userId?: string; success?: string },
  ) {
    setError("");
    setSuccessMessage("");
    setIsBusy(true);
    setActiveUserId(options?.userId ?? null);

    try {
      await action();
      if (options?.success) setSuccessMessage(options.success);
    } catch (actionError) {
      setError(
        getApiErrorMessage(
          actionError,
          "Əməliyyatı tamamlamaq mümkün olmadı.",
        ),
      );
    } finally {
      setIsBusy(false);
      setActiveUserId(null);
    }
  }

  async function handleInvite(candidate: ForumCommunityInviteCandidateDTO) {
    await runModalAction(
      async () => {
        await inviteToCommunity(communityId, candidate.userName);
        await candidatesQuery.refetch();
      },
      {
        userId: candidate.userId,
        success: `${candidate.userName} istifadəçisinə dəvət göndərildi.`,
      },
    );
  }

  async function handleRemoveMember() {
    if (!memberToRemove) return;
    const selectedMember = memberToRemove;

    await runModalAction(
      async () => {
        await removeCommunityMember(
          communityId,
          selectedMember.membershipId,
        );
        setMemberToRemove(null);
        await Promise.all([
          membersQuery.refetch(),
          onCommunityChanged(),
        ]);
      },
      {
        userId: selectedMember.userId,
        success: `${selectedMember.userName} icmadan çıxarıldı.`,
      },
    );
  }

  async function handleRoleChange(member: ForumCommunityMemberDTO) {
    if (!isOwner) return;
    const nextRole: ForumCommunityMemberRole =
      member.role === "Moderator" ? "Member" : "Moderator";
    const isPromoting = nextRole === "Moderator";
    const confirmed = await requestConfirmation({
      title: isPromoting
        ? `${member.userName} moderator təyin edilsin?`
        : `${member.userName} moderatorluqdan çıxarılsın?`,
      description: isPromoting
        ? "İstifadəçiyə icmanın məzmun və üzvlərini idarə etmək səlahiyyəti veriləcək."
        : "İstifadəçinin icma üzrə moderasiya səlahiyyətləri geri alınacaq.",
      detail: isPromoting
        ? "Moderator icmanı silə bilməz, lakin qaydalara uyğun olaraq üzvləri və məzmunu idarə edə bilər."
        : "İstifadəçi icmada adi üzv kimi qalacaq.",
      confirmLabel: isPromoting
        ? "Moderator təyin et"
        : "Səlahiyyəti geri al",
      kind: "moderation",
      tone: isPromoting ? "warning" : "neutral",
    });

    if (!confirmed) return;

    await runModalAction(
      async () => {
        await updateCommunityMemberRole(
          communityId,
          member.membershipId,
          nextRole,
        );
        await Promise.all([
          membersQuery.refetch(),
          onCommunityChanged(),
        ]);
      },
      {
        userId: member.userId,
        success:
          nextRole === "Moderator"
            ? `${member.userName} moderator təyin edildi.`
            : `${member.userName} artıq moderator deyil.`,
      },
    );
  }

  async function handleDeleteCommunity() {
    await runModalAction(async () => {
      await deleteForumCommunity(communityId);
      onExited();
    });
  }

  async function handleLeaveCommunity() {
    await runModalAction(async () => {
      const result = await leaveForumCommunity(communityId);

      if (result.leftCommunity) {
        onExited();
        return;
      }

      await onCommunityChanged();
      setSuccessMessage(
        result.candidateUserName
          ? `${result.candidateUserName} istifadəçisinə sahiblik təklifi göndərildi. Təklif qəbul edilənədək siz icma sahibi olaraq qalırsınız.`
          : "Sahiblik təklifi göndərildi. Təklif qəbul edilənədək siz icma sahibi olaraq qalırsınız.",
      );
    });
  }

  const title =
    mode === "invite"
      ? "İstifadəçi dəvət et"
      : mode === "members"
        ? "Üzvləri idarə et"
        : mode === "delete"
          ? "İcmanı sil"
          : "İcmadan ayrıl";

  return createPortal(
    <>
      <div
        className="fixed inset-0 z-[1050] grid place-items-center overflow-y-auto bg-black/70 p-4 backdrop-blur-sm sm:p-6"
        role="dialog"
        aria-modal="true"
        aria-labelledby="community-owner-modal-title"
        onMouseDown={(event) => {
          if (event.target === event.currentTarget && !isBusy) onClose();
        }}
      >
        <section className="flex max-h-[calc(100dvh-2rem)] w-full max-w-2xl flex-col overflow-hidden rounded-[28px] border border-[var(--wp-border)] bg-[var(--wp-surface-strong)] text-[var(--wp-text)] shadow-[var(--wp-shadow-floating)] sm:max-h-[calc(100dvh-3rem)]">
          <header className="flex items-start justify-between gap-4 border-b border-[var(--wp-border)] px-5 py-5 sm:px-6">
            <div className="min-w-0">
              <h2
                id="community-owner-modal-title"
                className="text-lg font-black text-[var(--wp-text-strong)]"
              >
                {title}
              </h2>
              <p className="mt-1 truncate text-xs text-[var(--wp-text-muted)]">
                {communityName}
              </p>
            </div>
            <button
              type="button"
              disabled={isBusy}
              onClick={onClose}
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border border-[var(--wp-border)] text-[var(--wp-text-muted)] transition hover:bg-[var(--wp-surface-hover)] hover:text-[var(--wp-text-strong)] disabled:opacity-50"
              aria-label="Pəncərəni bağla"
            >
              <X size={17} />
            </button>
          </header>

          <div className="overflow-y-auto px-5 py-5 sm:px-6">
            {mode === "invite" && (
              <div>
                <label className="block text-xs font-bold text-[var(--wp-text-muted)]">
                  İstifadəçi adı
                </label>
                <div className="relative mt-2">
                  <Search
                    size={16}
                    className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-[var(--wp-text-muted)]"
                  />
                  <input
                    value={searchValue}
                    onChange={(event) => setSearchValue(event.target.value)}
                    placeholder="Ən azı 2 simvol yaz..."
                    className="h-12 w-full rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] pl-10 pr-4 text-sm text-[var(--wp-text-strong)] outline-none transition focus:border-teal-300/35"
                  />
                </div>

                <div className="mt-4 space-y-2">
                  {candidatesQuery.isFetching ? (
                    <div className="flex items-center justify-center gap-2 py-8 text-sm text-[var(--wp-text-muted)]">
                      <LoaderCircle size={17} className="animate-spin" />
                      Axtarılır...
                    </div>
                  ) : debouncedSearch.length < 2 ? (
                    <p className="py-8 text-center text-sm text-[var(--wp-text-muted)]">
                      İstifadəçi axtarmaq üçün ən azı 2 simvol yazın.
                    </p>
                  ) : (candidatesQuery.data ?? []).length === 0 ? (
                    <p className="py-8 text-center text-sm text-[var(--wp-text-muted)]">
                      Uyğun istifadəçi tapılmadı.
                    </p>
                  ) : (
                    candidatesQuery.data?.map((candidate) => {
                      const canInvite = candidate.membershipStatus === null;
                      return (
                        <div
                          key={candidate.userId}
                          className="flex items-center gap-3 rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] p-3"
                        >
                          <Link
                            to={`/users/${encodeURIComponent(candidate.userName)}`}
                            onClick={onClose}
                          >
                            <UserAvatar
                              userName={candidate.userName}
                              profileImageUrl={candidate.profileImageUrl}
                            />
                          </Link>
                          <Link
                            to={`/users/${encodeURIComponent(candidate.userName)}`}
                            onClick={onClose}
                            className="min-w-0 flex-1 truncate text-sm font-bold text-[var(--wp-text-strong)] transition hover:text-teal-300"
                          >
                            {candidate.userName}
                          </Link>
                          <button
                            type="button"
                            disabled={!canInvite || isBusy}
                            onClick={() => void handleInvite(candidate)}
                            className="flex h-9 shrink-0 items-center gap-1.5 rounded-xl bg-teal-300 px-3 text-xs font-black text-[#07100f] transition hover:bg-teal-200 disabled:cursor-default disabled:bg-[var(--wp-surface-hover)] disabled:text-[var(--wp-text-muted)]"
                          >
                            {activeUserId === candidate.userId ? (
                              <LoaderCircle size={14} className="animate-spin" />
                            ) : (
                              <Send size={14} />
                            )}
                            {getCandidateActionLabel(candidate)}
                          </button>
                        </div>
                      );
                    })
                  )}
                </div>
              </div>
            )}

            {mode === "members" && (
              <div>
                <div className="relative">
                  <Search
                    size={16}
                    className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-[var(--wp-text-muted)]"
                  />
                  <input
                    value={memberSearch}
                    onChange={(event) => setMemberSearch(event.target.value)}
                    placeholder="Üzvləri ada görə axtar..."
                    className="h-12 w-full rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] pl-10 pr-4 text-sm text-[var(--wp-text-strong)] outline-none transition focus:border-teal-300/35"
                  />
                </div>

                {!isOwner && (
                  <p className="mt-3 rounded-2xl border border-violet-400/15 bg-violet-400/[0.06] px-4 py-3 text-xs leading-5 text-violet-200">
                    Moderator olaraq yalnız adi üzvləri icmadan çıxara bilərsiniz.
                  </p>
                )}

                <div className="mt-4 space-y-2">
                  {membersQuery.isLoading ? (
                    <div className="flex items-center justify-center gap-2 py-8 text-sm text-[var(--wp-text-muted)]">
                      <LoaderCircle size={17} className="animate-spin" />
                      Üzvlər yüklənir...
                    </div>
                  ) : filteredMembers.length === 0 ? (
                    <div className="py-8 text-center">
                      <UsersRound
                        size={24}
                        className="mx-auto text-[var(--wp-text-muted)]"
                      />
                      <p className="mt-3 text-sm text-[var(--wp-text-muted)]">
                        Uyğun üzv tapılmadı.
                      </p>
                    </div>
                  ) : (
                    filteredMembers.map((member) => {
                      const canRemove =
                        isOwner || member.role === "Member";
                      return (
                        <div
                          key={member.membershipId}
                          className="flex flex-wrap items-center gap-3 rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] p-3 sm:flex-nowrap"
                        >
                          <Link
                            to={`/users/${encodeURIComponent(member.userName)}`}
                            onClick={onClose}
                          >
                            <UserAvatar
                              userName={member.userName}
                              profileImageUrl={member.profileImageUrl}
                            />
                          </Link>
                          <div className="min-w-0 flex-1">
                            <Link
                              to={`/users/${encodeURIComponent(member.userName)}`}
                              onClick={onClose}
                              className="block truncate text-sm font-bold text-[var(--wp-text-strong)] transition hover:text-teal-300"
                            >
                              {member.userName}
                            </Link>
                            <div className="mt-1.5">
                              <RoleBadge role={member.role} />
                            </div>
                          </div>

                          <div className="ml-auto flex shrink-0 items-center gap-2">
                            {isOwner && (
                              <button
                                type="button"
                                disabled={isBusy}
                                onClick={() => void handleRoleChange(member)}
                                className={`flex h-9 items-center gap-1.5 rounded-xl border px-3 text-xs font-bold transition disabled:opacity-50 ${
                                  member.role === "Moderator"
                                    ? "border-violet-400/20 bg-violet-400/[0.07] text-violet-300 hover:bg-violet-400/[0.13]"
                                    : "border-teal-300/20 bg-teal-300/[0.06] text-teal-300 hover:bg-teal-300/[0.12]"
                                }`}
                              >
                                {activeUserId === member.userId ? (
                                  <LoaderCircle size={14} className="animate-spin" />
                                ) : member.role === "Moderator" ? (
                                  <ShieldOff size={14} />
                                ) : (
                                  <Shield size={14} />
                                )}
                                {member.role === "Moderator"
                                  ? "Rolu götür"
                                  : "Moderator et"}
                              </button>
                            )}

                            {canRemove && (
                              <button
                                type="button"
                                disabled={isBusy}
                                onClick={() => setMemberToRemove(member)}
                                className="flex h-9 items-center gap-1.5 rounded-xl border border-rose-400/20 bg-rose-400/[0.065] px-3 text-xs font-bold text-rose-300 transition hover:bg-rose-400/[0.12] disabled:opacity-50"
                              >
                                <UserMinus size={14} />
                                Çıxar
                              </button>
                            )}
                          </div>
                        </div>
                      );
                    })
                  )}
                </div>
              </div>
            )}

            {mode === "delete" && (
              <div className="py-2 text-center">
                <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl border border-rose-400/20 bg-rose-400/[0.08] text-rose-300">
                  <Trash2 size={24} />
                </div>
                <h3 className="mt-5 text-lg font-black text-[var(--wp-text-strong)]">
                  {language === "en"
                    ? `Delete the “${communityName}” community?`
                    : `“${communityName}” icmasını silmək istəyirsiniz?`}
                </h3>
                <p className="mx-auto mt-3 max-w-md text-sm leading-7 text-[var(--wp-text-muted)]">
                  İcma və onun daxilindəki müzakirələr artıq istifadəçilərə
                  göstərilməyəcək. Bu əməliyyat geri qaytarılmır.
                </p>
                <div className="mt-7 grid gap-3 sm:grid-cols-2">
                  <button
                    type="button"
                    disabled={isBusy}
                    onClick={onClose}
                    className="h-11 rounded-2xl border border-[var(--wp-border)] text-sm font-bold text-[var(--wp-text-muted)] transition hover:bg-[var(--wp-surface-hover)] hover:text-[var(--wp-text-strong)] disabled:opacity-50"
                  >
                    Ləğv et
                  </button>
                  <button
                    type="button"
                    disabled={isBusy}
                    onClick={() => void handleDeleteCommunity()}
                    className="flex h-11 items-center justify-center gap-2 rounded-2xl bg-rose-500 px-4 text-sm font-black text-white transition hover:bg-rose-400 disabled:opacity-50"
                  >
                    {isBusy ? (
                      <LoaderCircle size={16} className="animate-spin" />
                    ) : (
                      <Trash2 size={16} />
                    )}
                    İcmanı sil
                  </button>
                </div>
              </div>
            )}

            {mode === "leave" && (
              <div className="py-2 text-center">
                <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl border border-amber-400/20 bg-amber-400/[0.08] text-amber-300">
                  <LogOut size={24} />
                </div>
                <h3 className="mt-5 text-lg font-black text-[var(--wp-text-strong)]">
                  {language === "en"
                    ? `Leave the “${communityName}” community?`
                    : `“${communityName}” icmasından ayrılmaq istəyirsiniz?`}
                </h3>
                <p className="mx-auto mt-3 max-w-lg text-sm leading-7 text-[var(--wp-text-muted)]">
                  Sahiblik əvvəlcə icmaya ən tez qoşulan moderatora təklif
                  ediləcək. Moderator yoxdursa və ya təklifi rədd edirsə, təklif
                  ən uzun müddətli adi üzvlərə sıra ilə göndəriləcək. Təklif qəbul
                  edilənədək siz icma sahibi olaraq qalacaqsınız.
                </p>
                <div className="mt-7 grid gap-3 sm:grid-cols-2">
                  <button
                    type="button"
                    disabled={isBusy}
                    onClick={onClose}
                    className="h-11 rounded-2xl border border-[var(--wp-border)] text-sm font-bold text-[var(--wp-text-muted)] transition hover:bg-[var(--wp-surface-hover)] hover:text-[var(--wp-text-strong)] disabled:opacity-50"
                  >
                    Ləğv et
                  </button>
                  <button
                    type="button"
                    disabled={isBusy}
                    onClick={() => void handleLeaveCommunity()}
                    className="flex h-11 items-center justify-center gap-2 rounded-2xl bg-amber-500 px-4 text-sm font-black text-white transition hover:bg-amber-400 disabled:opacity-50"
                  >
                    {isBusy ? (
                      <LoaderCircle size={16} className="animate-spin" />
                    ) : (
                      <LogOut size={16} />
                    )}
                    Sahiblik təklifi göndər
                  </button>
                </div>
              </div>
            )}

            {error && (
              <p className="mt-4 rounded-2xl border border-rose-400/15 bg-rose-400/[0.06] px-4 py-3 text-sm text-rose-300">
                {error}
              </p>
            )}
            {successMessage && (
              <p className="mt-4 rounded-2xl border border-teal-300/15 bg-teal-300/[0.06] px-4 py-3 text-sm leading-6 text-teal-300">
                {successMessage}
              </p>
            )}
          </div>
        </section>
      </div>

      {memberToRemove && (
        <div
          className="fixed inset-0 z-[1100] grid place-items-center bg-black/75 p-4 backdrop-blur-sm"
          role="alertdialog"
          aria-modal="true"
          aria-labelledby="remove-community-member-title"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget && !isBusy) {
              setMemberToRemove(null);
            }
          }}
        >
          <section className="w-full max-w-md rounded-[26px] border border-[var(--wp-border)] bg-[var(--wp-surface-strong)] p-6 text-center text-[var(--wp-text)] shadow-[var(--wp-shadow-floating)]">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl border border-rose-400/20 bg-rose-400/[0.08] text-rose-300">
              <AlertTriangle size={22} />
            </div>
            <h3
              id="remove-community-member-title"
              className="mt-5 text-lg font-black text-[var(--wp-text-strong)]"
            >
              İstifadəçini icmadan çıxar
            </h3>
            <p className="mt-3 text-sm leading-7 text-[var(--wp-text-muted)]">
              {language === "en" ? (
                <>
                  Are you sure you want to remove{" "}
                  <strong className="text-[var(--wp-text-strong)]">
                    {memberToRemove.userName}
                  </strong>{" "}
                  from the community?
                </>
              ) : (
                <>
                  <strong className="text-[var(--wp-text-strong)]">
                    {memberToRemove.userName}
                  </strong>{" "}
                  istifadəçisini icmadan çıxarmaq istədiyinizə əminsiniz?
                </>
              )}
            </p>
            <div className="mt-6 grid grid-cols-2 gap-3">
              <button
                type="button"
                disabled={isBusy}
                onClick={() => setMemberToRemove(null)}
                className="h-11 rounded-2xl border border-[var(--wp-border)] text-sm font-bold text-[var(--wp-text-muted)] transition hover:bg-[var(--wp-surface-hover)] hover:text-[var(--wp-text-strong)] disabled:opacity-50"
              >
                Ləğv et
              </button>
              <button
                type="button"
                disabled={isBusy}
                onClick={() => void handleRemoveMember()}
                className="flex h-11 items-center justify-center gap-2 rounded-2xl bg-rose-500 text-sm font-black text-white transition hover:bg-rose-400 disabled:opacity-50"
              >
                {isBusy ? (
                  <LoaderCircle size={16} className="animate-spin" />
                ) : (
                  <UserMinus size={16} />
                )}
                Çıxar
              </button>
            </div>
          </section>
        </div>
      )}
    </>,
    document.body,
  );
}
