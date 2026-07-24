import { useQuery } from "@tanstack/react-query";
import {
  ArrowLeft,
  Bell,
  BellOff,
  Check,
  ChevronRight,
  Flag,
  LoaderCircle,
  LockKeyhole,
  MoreHorizontal,
  Send,
  ShieldCheck,
  UserPlus,
  UsersRound,
} from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Navbar } from "../components/layout/Navbar";
import { UserReportModal } from "../components/profile/UserReportModal";
import { getApiErrorMessage } from "../lib/api";
import {
  formatRelativeDate,
  resolveAssetUrl,
} from "../lib/helpers";
import {
  acceptCommunityInvite,
  approveCommunityMembership,
  getCommunityMembershipRequests,
  getForumCommunityBySlug,
  inviteToCommunity,
  joinForumCommunity,
  leaveForumCommunity,
  setForumCommunityFollowed,
} from "../services/forumCommunityService";
import { getForumDiscussions } from "../services/forumService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../stores/authStore";
import type {
  ForumCommunityCategory,
  ForumCommunityVisibility,
} from "../types/community";

const categoryLabels: Record<ForumCommunityCategory, string> = {
  GameNews: "Oyun xəbərləri",
  BugsAndSolutions: "Buglar və həlləri",
  Speedrunning: "Speedrunning",
  HardwareAndGamingSetup: "Hardware və gaming setup",
  IndieGames: "Indie oyunlar",
  ModdingAndGameDevelopment: "Modding və oyun inkişafı",
  Esports: "Esports",
  Other: "Digər",
};

const visibilityLabels: Record<ForumCommunityVisibility, string> = {
  Open: "Açıq icma",
  Restricted: "Məhdud icma",
  Private: "Gizli icma",
};

export function ForumCommunityPage() {
  const { slug = "" } = useParams();
  const menuRef = useRef<HTMLDivElement>(null);
  const user = useAuthStore((state) => state.user);
  const accessToken = useAuthStore((state) => state.accessToken);
  const expiresAtUtc = useAuthStore((state) => state.expiresAtUtc);
  const isAuthenticated =
    user !== null && isAuthSessionValid(accessToken, expiresAtUtc);
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const [isReportOpen, setIsReportOpen] = useState(false);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState("");
  const [inviteUserName, setInviteUserName] = useState("");

  const communityQuery = useQuery({
    queryKey: ["forum", "community", slug],
    queryFn: ({ signal }) => getForumCommunityBySlug(slug, signal),
    enabled: Boolean(slug),
  });

  const community = communityQuery.data;

  const discussionsQuery = useQuery({
    queryKey: ["forum", "feed", "community", slug],
    queryFn: ({ signal }) =>
      getForumDiscussions("newest", "", signal, slug),
    enabled: Boolean(community),
  });

  const membershipRequestsQuery = useQuery({
    queryKey: [
      "forum",
      "community",
      community?.id,
      "membership-requests",
    ],
    queryFn: ({ signal }) =>
      getCommunityMembershipRequests(community!.id, signal),
    enabled: Boolean(community?.canManage),
  });

  useEffect(() => {
    if (!isMenuOpen) return;
    const close = (event: PointerEvent) => {
      if (
        menuRef.current &&
        !menuRef.current.contains(event.target as Node)
      ) {
        setIsMenuOpen(false);
      }
    };
    document.addEventListener("pointerdown", close);
    return () => document.removeEventListener("pointerdown", close);
  }, [isMenuOpen]);

  async function refreshCommunity() {
    await Promise.all([
      communityQuery.refetch(),
      discussionsQuery.refetch(),
    ]);
  }

  async function runAction(action: () => Promise<void>) {
    setError("");
    setIsBusy(true);
    try {
      await action();
      await refreshCommunity();
    } catch (actionError) {
      setError(
        getApiErrorMessage(
          actionError,
          "Əməliyyatı tamamlamaq mümkün olmadı.",
        ),
      );
    } finally {
      setIsBusy(false);
    }
  }

  if (communityQuery.isLoading) {
    return (
      <div className="min-h-screen bg-[#07100f] text-zinc-100">
        <Navbar />
        <main className="mx-auto flex min-h-[60vh] max-w-6xl items-center justify-center gap-3">
          <LoaderCircle size={20} className="animate-spin text-teal-300" />
          <span className="text-sm text-zinc-500">İcma yüklənir...</span>
        </main>
      </div>
    );
  }

  if (!community) {
    return (
      <div className="min-h-screen bg-[#07100f] text-zinc-100">
        <Navbar />
        <main className="mx-auto max-w-3xl px-5 py-20 text-center">
          <LockKeyhole size={34} className="mx-auto text-zinc-700" />
          <h1 className="mt-5 text-2xl font-bold">İcma tapılmadı</h1>
          <p className="mt-2 text-sm text-zinc-500">
            İcma mövcud deyil və ya onu görmək icazəniz yoxdur.
          </p>
          <Link
            to="/forum"
            className="mt-6 inline-flex items-center gap-2 text-sm font-bold text-teal-300"
          >
            <ArrowLeft size={16} />
            Foruma qayıt
          </Link>
        </main>
      </div>
    );
  }

  const iconUrl = resolveAssetUrl(community.iconUrl);
  const coverUrl = resolveAssetUrl(community.coverImageUrl);
  const membership = community.currentUserMembershipStatus;
  const isOwner = community.ownerId === user?.id;

  return (
    <div className="min-h-screen bg-[#07100f] text-zinc-100">
      <Navbar />
      <main className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:py-8">
        <Link
          to="/forum"
          className="inline-flex items-center gap-2 text-xs font-bold text-zinc-500 transition hover:text-teal-300"
        >
          <ArrowLeft size={15} />
          İcmalara qayıt
        </Link>

        <section className="mt-5 overflow-hidden rounded-[28px] border border-white/[0.08] bg-[#0c1716]/90">
          <div
            className="h-44 bg-gradient-to-br from-teal-400/20 via-[#13201f] to-violet-500/15 bg-cover bg-center sm:h-56"
            style={
              coverUrl ? { backgroundImage: `url("${coverUrl}")` } : undefined
            }
          />
          <div className="relative px-5 pb-6 pt-14 sm:px-7">
            <div className="absolute -top-12 left-5 flex h-24 w-24 items-center justify-center overflow-hidden rounded-[28px] border-[6px] border-[#0c1716] bg-teal-300 text-3xl font-black text-[#07100f] sm:left-7">
              {iconUrl ? (
                <img
                  src={iconUrl}
                  alt=""
                  className="h-full w-full object-cover"
                />
              ) : (
                community.name.charAt(0).toUpperCase()
              )}
            </div>

            <div className="flex flex-col gap-5 sm:flex-row sm:items-start sm:justify-between">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <h1 className="text-2xl font-black text-white sm:text-3xl">
                    {community.name}
                  </h1>
                  <span className="rounded-full border border-white/[0.08] bg-white/[0.04] px-2.5 py-1 text-[10px] font-bold text-zinc-500">
                    {visibilityLabels[community.visibility]}
                  </span>
                </div>
                <p className="mt-2 text-xs font-semibold text-teal-300/75">
                  {categoryLabels[community.category]}
                </p>
                <p className="mt-4 max-w-3xl text-sm leading-7 text-zinc-400">
                  {community.description}
                </p>
                <div className="mt-4 flex flex-wrap gap-4 text-xs text-zinc-600">
                  <span>{community.memberCount} üzv</span>
                  <span>{community.followerCount} izləyici</span>
                  <span>{community.discussionCount} müzakirə</span>
                  <span>Sahib: @{community.ownerUserName}</span>
                </div>
              </div>

              <div className="flex shrink-0 items-center gap-2">
                {isAuthenticated && !isOwner && (
                  <>
                    {membership === "Active" ? (
                      <button
                        type="button"
                        disabled={isBusy}
                        onClick={() =>
                          runAction(() =>
                            leaveForumCommunity(community.id),
                          )
                        }
                        className="h-10 rounded-xl border border-white/10 px-4 text-xs font-bold text-zinc-400 transition hover:text-white disabled:opacity-50"
                      >
                        İcmadan ayrıl
                      </button>
                    ) : membership === "Pending" ? (
                      <button
                        type="button"
                        disabled
                        className="flex h-10 items-center gap-2 rounded-xl border border-amber-300/15 bg-amber-300/[0.06] px-4 text-xs font-bold text-amber-200/70"
                      >
                        <ShieldCheck size={15} />
                        Sorğu gözləyir
                      </button>
                    ) : membership === "Invited" ? (
                      <button
                        type="button"
                        disabled={isBusy}
                        onClick={() =>
                          runAction(() =>
                            acceptCommunityInvite(community.id),
                          )
                        }
                        className="flex h-10 items-center gap-2 rounded-xl bg-teal-300 px-4 text-xs font-black text-[#07100f] disabled:opacity-50"
                      >
                        <Check size={15} />
                        Dəvəti qəbul et
                      </button>
                    ) : community.visibility !== "Private" ? (
                      <button
                        type="button"
                        disabled={isBusy}
                        onClick={() =>
                          runAction(() =>
                            joinForumCommunity(community.id),
                          )
                        }
                        className="flex h-10 items-center gap-2 rounded-xl bg-teal-300 px-4 text-xs font-black text-[#07100f] disabled:opacity-50"
                      >
                        <UserPlus size={15} />
                        {community.visibility === "Restricted"
                          ? "Qoşulma sorğusu göndər"
                          : "İcmaya qoşul"}
                      </button>
                    ) : null}
                  </>
                )}

                {isAuthenticated && (
                  <div ref={menuRef} className="relative">
                    <button
                      type="button"
                      onClick={() => setIsMenuOpen((current) => !current)}
                      className="flex h-10 w-10 items-center justify-center rounded-xl border border-white/10 text-zinc-500 transition hover:text-white"
                      aria-label="İcma seçimləri"
                    >
                      <MoreHorizontal size={19} />
                    </button>
                    {isMenuOpen && (
                      <div className="absolute right-0 top-full z-40 mt-2 w-56 rounded-2xl border border-white/[0.09] bg-[#111c1b] p-1.5 shadow-2xl shadow-black/50">
                        <button
                          type="button"
                          disabled={isBusy}
                          onClick={() => {
                            setIsMenuOpen(false);
                            void runAction(() =>
                              setForumCommunityFollowed(
                                community.id,
                                !community.isFollowedByCurrentUser,
                              ),
                            );
                          }}
                          className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-xs font-semibold text-zinc-300 transition hover:bg-white/[0.055] disabled:opacity-50"
                        >
                          {community.isFollowedByCurrentUser ? (
                            <BellOff size={16} className="text-teal-300" />
                          ) : (
                            <Bell size={16} />
                          )}
                          {community.isFollowedByCurrentUser
                            ? "İzləməni dayandır"
                            : "İcmanı izlə"}
                        </button>
                        {!isOwner && (
                          <button
                            type="button"
                            onClick={() => {
                              setIsMenuOpen(false);
                              setIsReportOpen(true);
                            }}
                            className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-xs font-semibold text-rose-300 transition hover:bg-rose-400/[0.08]"
                          >
                            <Flag size={16} />
                            Şikayət et
                          </button>
                        )}
                      </div>
                    )}
                  </div>
                )}
              </div>
            </div>

            {error && (
              <p className="mt-5 rounded-2xl border border-rose-400/15 bg-rose-400/[0.06] px-4 py-3 text-sm text-rose-200">
                {error}
              </p>
            )}
          </div>
        </section>

        <div className="mt-6 grid gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
          <section>
            <h2 className="text-lg font-bold text-white">İcma müzakirələri</h2>
            <div className="mt-4 space-y-3">
              {discussionsQuery.isLoading ? (
                <div className="h-40 animate-pulse rounded-[22px] bg-white/[0.03]" />
              ) : (discussionsQuery.data?.items.length ?? 0) === 0 ? (
                <div className="rounded-[22px] border border-dashed border-white/10 p-10 text-center">
                  <UsersRound size={25} className="mx-auto text-zinc-700" />
                  <p className="mt-3 text-sm text-zinc-500">
                    Bu icmada hələ müzakirə yoxdur.
                  </p>
                </div>
              ) : (
                discussionsQuery.data?.items.map((discussion) => (
                  <Link
                    key={discussion.id}
                    to={`/forum/discussions/${discussion.slug}`}
                    className="group block rounded-[22px] border border-white/[0.075] bg-[#0d1716]/80 p-5 transition hover:border-teal-300/15"
                  >
                    <p className="text-xs text-zinc-600">
                      @{discussion.authorUserName} ·{" "}
                      {formatRelativeDate(discussion.createdAt)}
                    </p>
                    <h3 className="mt-2 font-bold text-zinc-100 transition group-hover:text-white">
                      {discussion.title}
                    </h3>
                    {discussion.contentPreview && (
                      <p className="mt-2 line-clamp-2 text-sm leading-6 text-zinc-500">
                        {discussion.contentPreview}
                      </p>
                    )}
                    <div className="mt-4 flex items-center justify-between text-[11px] text-zinc-600">
                      <span>
                        {discussion.likeCount} bəyənmə ·{" "}
                        {discussion.replyCount} cavab
                      </span>
                      <ChevronRight
                        size={15}
                        className="transition group-hover:translate-x-0.5 group-hover:text-teal-300"
                      />
                    </div>
                  </Link>
                ))
              )}
            </div>
          </section>

          {community.canManage && (
            <aside className="space-y-4">
              <section className="rounded-[22px] border border-white/[0.075] bg-[#0d1716]/80 p-5">
                <h2 className="text-sm font-bold text-white">
                  Üzvlük sorğuları
                </h2>
                <div className="mt-4 space-y-2">
                  {(membershipRequestsQuery.data ?? []).length === 0 ? (
                    <p className="text-xs leading-5 text-zinc-600">
                      Gözləyən üzvlük sorğusu yoxdur.
                    </p>
                  ) : (
                    membershipRequestsQuery.data?.map((request) => (
                      <div
                        key={request.membershipId}
                        className="flex items-center gap-3 rounded-xl bg-white/[0.03] p-3"
                      >
                        <span className="min-w-0 flex-1 truncate text-xs font-semibold text-zinc-300">
                          @{request.userName}
                        </span>
                        <button
                          type="button"
                          disabled={isBusy}
                          onClick={() =>
                            runAction(async () => {
                              await approveCommunityMembership(
                                community.id,
                                request.membershipId,
                              );
                              await membershipRequestsQuery.refetch();
                            })
                          }
                          className="text-xs font-bold text-teal-300 disabled:opacity-50"
                        >
                          Təsdiqlə
                        </button>
                      </div>
                    ))
                  )}
                </div>
              </section>

              {community.visibility === "Private" && (
                <section className="rounded-[22px] border border-white/[0.075] bg-[#0d1716]/80 p-5">
                  <h2 className="text-sm font-bold text-white">
                    İstifadəçi dəvət et
                  </h2>
                  <p className="mt-2 text-xs leading-5 text-zinc-600">
                    Gizli icmaya yalnız dəvət etdiyiniz istifadəçilər daxil ola bilər.
                  </p>
                  <div className="mt-4 flex gap-2">
                    <input
                      value={inviteUserName}
                      onChange={(event) =>
                        setInviteUserName(event.target.value)
                      }
                      placeholder="İstifadəçi adı"
                      className="h-10 min-w-0 flex-1 rounded-xl border border-white/[0.08] bg-white/[0.03] px-3 text-xs text-white outline-none focus:border-teal-300/30"
                    />
                    <button
                      type="button"
                      disabled={isBusy || !inviteUserName.trim()}
                      onClick={() =>
                        runAction(async () => {
                          await inviteToCommunity(
                            community.id,
                            inviteUserName.trim(),
                          );
                          setInviteUserName("");
                        })
                      }
                      className="flex h-10 w-10 items-center justify-center rounded-xl bg-teal-300 text-[#07100f] disabled:opacity-50"
                      aria-label="Dəvət göndər"
                    >
                      <Send size={16} />
                    </button>
                  </div>
                </section>
              )}
            </aside>
          )}
        </div>
      </main>

      {!isOwner && (
        <UserReportModal
          isOpen={isReportOpen}
          reportedUserId={community.ownerId}
          reportedUserName={community.name}
          reportedContentType="Community"
          reportedContentId={community.id}
          reportedContentLabel={community.name}
          subjectKind="community"
          onClose={() => setIsReportOpen(false)}
          onSubmitted={() => setIsReportOpen(false)}
        />
      )}
    </div>
  );
}
