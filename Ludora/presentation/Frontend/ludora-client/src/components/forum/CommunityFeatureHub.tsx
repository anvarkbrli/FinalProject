import { useEffect, useMemo, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  Crown,
  ExternalLink,
  Gamepad2,
  LoaderCircle,
  Pencil,
  Plus,
  Search,
  Shield,
  Sparkles,
  Trash2,
  UsersRound,
  X,
} from "lucide-react";
import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/useI18n";
import { getApiErrorMessage } from "../../lib/api";
import { useConfirmation } from "../../lib/confirmation";
import { resolveAssetUrl } from "../../lib/helpers";
import {
  cancelCommunityEvent,
  createCommunityEvent,
  getCommunityEvents,
  getCommunityFlairs,
  getCommunityModerators,
  getCommunityRules,
  replaceCommunityFlairs,
  replaceCommunityRules,
  searchCommunityMembers,
  setCommunityEventParticipation,
  setMyCommunityFlair,
  updateCommunityEvent,
} from "../../services/forumCommunityService";
import type {
  ForumCommunityEventDTO,
  ForumCommunityEventInput,
  ForumCommunityEventType,
  ForumCommunityFlairInput,
  ForumCommunityRuleInput,
} from "../../types/community";

interface Props {
  communityId: number;
  canManage: boolean;
  canModerate: boolean;
  isActiveMember: boolean;
  isAuthenticated: boolean;
}

type Tab = "overview" | "members" | "events";
type Editor = "rules" | "flairs" | "event" | null;

const eventTypes: ForumCommunityEventType[] = [
  "Multiplayer",
  "Community",
  "Tournament",
  "WatchParty",
];

function toLocalInputValue(iso?: string | null) {
  if (!iso) return "";
  const date = new Date(iso);
  const offset = date.getTimezoneOffset();
  return new Date(date.getTime() - offset * 60_000).toISOString().slice(0, 16);
}

function emptyEvent(): ForumCommunityEventInput {
  const start = new Date(Date.now() + 60 * 60 * 1000);
  return {
    title: "",
    description: "",
    type: "Multiplayer",
    gameName: null,
    platform: null,
    joinUrl: null,
    startsAtUtc: start.toISOString(),
    endsAtUtc: null,
    maxParticipants: null,
  };
}

export function CommunityFeatureHub({
  communityId,
  canManage,
  canModerate,
  isActiveMember,
  isAuthenticated,
}: Props) {
  const { language } = useI18n();
  const queryClient = useQueryClient();
  const requestConfirmation = useConfirmation();
  const isEn = language === "en";
  const [tab, setTab] = useState<Tab>("overview");
  const [editor, setEditor] = useState<Editor>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [memberSearch, setMemberSearch] = useState("");
  const [debouncedMemberSearch, setDebouncedMemberSearch] = useState("");
  const [memberPage, setMemberPage] = useState(1);
  const [showPastEvents, setShowPastEvents] = useState(false);
  const [rulesDraft, setRulesDraft] = useState<ForumCommunityRuleInput[]>([]);
  const [flairsDraft, setFlairsDraft] = useState<ForumCommunityFlairInput[]>([]);
  const [editingEvent, setEditingEvent] = useState<ForumCommunityEventDTO | null>(null);
  const [eventDraft, setEventDraft] = useState<ForumCommunityEventInput>(emptyEvent());

  const text = useMemo(
    () =>
      isEn
        ? {
            overview: "Overview",
            members: "Members",
            events: "Events",
            rules: "Community rules",
            rulesEmpty: "No community rules have been added yet.",
            editRules: "Edit rules",
            moderators: "Moderators",
            moderatorsEmpty: "No moderators have been assigned yet.",
            flair: "User flair",
            flairHelp: "Choose the label shown beside your name in this community.",
            noFlair: "No flair",
            manageFlairs: "Manage flairs",
            memberSearch: "Search members",
            memberRestricted: "Join the community to browse and search members.",
            upcoming: "Upcoming",
            past: "Past",
            createEvent: "Create event",
            noEvents: "No events in this section yet.",
            join: "Join event",
            leave: "Leave event",
            full: "Full",
            cancelled: "Cancelled",
            participants: "participants",
            hostedBy: "Hosted by",
            edit: "Edit",
            cancel: "Cancel event",
            save: "Save",
            close: "Close",
            addRule: "Add rule",
            addFlair: "Add flair",
            title: "Title",
            description: "Description",
            flairName: "Flair name",
            background: "Background",
            color: "Text color",
            eventTitle: "Event title",
            eventType: "Event type",
            game: "Game",
            platform: "Platform",
            start: "Starts",
            end: "Ends",
            capacity: "Capacity",
            joinUrl: "Lobby / voice link",
            multiplayer: "Multiplayer",
            community: "Community meetup",
            tournament: "Tournament",
            watchParty: "Watch party",
            previous: "Previous",
            next: "Next",
            owner: "Owner",
            moderator: "Moderator",
            member: "Member",
          }
        : {
            overview: "Məlumat",
            members: "Üzvlər",
            events: "Tədbirlər",
            rules: "İcma qaydaları",
            rulesEmpty: "İcmaya hələ qayda əlavə edilməyib.",
            editRules: "Qaydaları redaktə et",
            moderators: "Moderatorlar",
            moderatorsEmpty: "Hələ moderator təyin edilməyib.",
            flair: "User flair",
            flairHelp: "Bu icmada adının yanında görünəcək etiketi seç.",
            noFlair: "Flair yoxdur",
            manageFlairs: "Flair-ləri idarə et",
            memberSearch: "Üzvlərdə axtar",
            memberRestricted: "Üzvləri görmək və axtarmaq üçün icmaya qoşul.",
            upcoming: "Qarşıdan gələn",
            past: "Keçmiş",
            createEvent: "Tədbir yarat",
            noEvents: "Bu bölmədə hələ tədbir yoxdur.",
            join: "Tədbirə qoşul",
            leave: "Tədbirdən çıx",
            full: "Yer yoxdur",
            cancelled: "Ləğv edilib",
            participants: "iştirakçı",
            hostedBy: "Təşkilatçı",
            edit: "Redaktə et",
            cancel: "Tədbiri ləğv et",
            save: "Yadda saxla",
            close: "Bağla",
            addRule: "Qayda əlavə et",
            addFlair: "Flair əlavə et",
            title: "Başlıq",
            description: "Açıqlama",
            flairName: "Flair adı",
            background: "Fon rəngi",
            color: "Mətn rəngi",
            eventTitle: "Tədbirin adı",
            eventType: "Tədbir növü",
            game: "Oyun",
            platform: "Platforma",
            start: "Başlama vaxtı",
            end: "Bitmə vaxtı",
            capacity: "İştirakçı limiti",
            joinUrl: "Lobby / səsli chat linki",
            multiplayer: "Multiplayer",
            community: "İcma görüşü",
            tournament: "Turnir",
            watchParty: "Birlikdə izləmə",
            previous: "Əvvəlki",
            next: "Növbəti",
            owner: "Sahib",
            moderator: "Moderator",
            member: "Üzv",
          },
    [isEn],
  );

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setDebouncedMemberSearch(memberSearch.trim());
      setMemberPage(1);
    }, 300);
    return () => window.clearTimeout(timeout);
  }, [memberSearch]);

  const rulesQuery = useQuery({
    queryKey: ["community-features", communityId, "rules"],
    queryFn: ({ signal }) => getCommunityRules(communityId, signal),
  });
  const moderatorsQuery = useQuery({
    queryKey: ["community-features", communityId, "moderators"],
    queryFn: ({ signal }) => getCommunityModerators(communityId, signal),
  });
  const flairsQuery = useQuery({
    queryKey: ["community-features", communityId, "flairs"],
    queryFn: ({ signal }) => getCommunityFlairs(communityId, signal),
  });
  const membersQuery = useQuery({
    queryKey: ["community-features", communityId, "members", debouncedMemberSearch, memberPage],
    queryFn: ({ signal }) =>
      searchCommunityMembers(communityId, debouncedMemberSearch, memberPage, 20, signal),
    enabled: tab === "members" && isAuthenticated && isActiveMember,
  });
  const eventsQuery = useQuery({
    queryKey: ["community-features", communityId, "events", showPastEvents],
    queryFn: ({ signal }) => getCommunityEvents(communityId, showPastEvents, 1, 20, signal),
    enabled: tab === "events",
  });

  async function run(action: () => Promise<void>) {
    setError("");
    setBusy(true);
    try {
      await action();
    } catch (caught) {
      setError(
        isEn
          ? "The operation could not be completed. Check the details and try again."
          : getApiErrorMessage(caught, "Əməliyyatı tamamlamaq mümkün olmadı."),
      );
    } finally {
      setBusy(false);
    }
  }

  function openRulesEditor() {
    setRulesDraft((rulesQuery.data ?? []).map((rule) => ({ id: rule.id, title: rule.title, description: rule.description })));
    setEditor("rules");
  }

  function openFlairsEditor() {
    setFlairsDraft((flairsQuery.data ?? []).map((flair) => ({
      id: flair.id,
      name: flair.name,
      backgroundColor: flair.backgroundColor,
      textColor: flair.textColor,
    })));
    setEditor("flairs");
  }

  function openEventEditor(event?: ForumCommunityEventDTO) {
    setEditingEvent(event ?? null);
    setEventDraft(
      event
        ? {
            title: event.title,
            description: event.description,
            type: event.type,
            gameName: event.gameName,
            platform: event.platform,
            joinUrl: event.joinUrl,
            startsAtUtc: event.startsAtUtc,
            endsAtUtc: event.endsAtUtc,
            maxParticipants: event.maxParticipants,
          }
        : emptyEvent(),
    );
    setEditor("event");
  }

  const selectedFlair = flairsQuery.data?.find((flair) => flair.isSelectedByCurrentUser);
  const eventTypeLabel = (type: ForumCommunityEventType) => ({
    Multiplayer: text.multiplayer,
    Community: text.community,
    Tournament: text.tournament,
    WatchParty: text.watchParty,
  })[type];
  const roleLabel = (role: string) =>
    role === "Owner" ? text.owner : role === "Moderator" ? text.moderator : text.member;
  const dateTime = (value: string) =>
    new Intl.DateTimeFormat(isEn ? "en-US" : "az-AZ", {
      dateStyle: "medium",
      timeStyle: "short",
    }).format(new Date(value));

  return (
    <section className="mt-6 overflow-hidden rounded-[26px] border border-white/[0.08] bg-[#0b1514]/90">
      <div className="flex gap-1 border-b border-white/[0.07] p-2">
        {(["overview", "members", "events"] as Tab[]).map((item) => (
          <button
            key={item}
            type="button"
            onClick={() => setTab(item)}
            className={`rounded-xl px-4 py-2.5 text-xs font-bold transition ${
              tab === item ? "bg-teal-300 text-[#07100f]" : "text-zinc-500 hover:bg-white/[0.04] hover:text-zinc-200"
            }`}
          >
            {item === "overview" ? text.overview : item === "members" ? text.members : text.events}
          </button>
        ))}
      </div>

      {error && <p className="m-4 rounded-xl border border-rose-400/20 bg-rose-400/[0.07] px-4 py-3 text-sm text-rose-200">{error}</p>}

      {tab === "overview" && (
        <div className="grid gap-4 p-4 lg:grid-cols-3">
          <article className="rounded-[20px] border border-white/[0.07] bg-white/[0.025] p-5">
            <div className="flex items-center justify-between gap-3">
              <h3 className="flex items-center gap-2 text-sm font-black text-white"><Shield size={16} className="text-teal-300" />{text.rules}</h3>
              {canManage && <button type="button" onClick={openRulesEditor} className="text-xs font-bold text-teal-300 hover:text-teal-200">{text.editRules}</button>}
            </div>
            <div className="mt-4 space-y-3">
              {rulesQuery.isLoading ? <LoaderCircle size={18} className="animate-spin text-teal-300" /> : (rulesQuery.data?.length ?? 0) === 0 ? (
                <p className="text-xs leading-5 text-zinc-600">{text.rulesEmpty}</p>
              ) : rulesQuery.data?.map((rule, index) => (
                <div key={rule.id} className="flex gap-3">
                  <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-lg bg-teal-300/10 text-[10px] font-black text-teal-300">{index + 1}</span>
                  <div><p className="text-xs font-bold text-zinc-200">{rule.title}</p><p data-user-content className="mt-1 text-xs leading-5 text-zinc-600">{rule.description}</p></div>
                </div>
              ))}
            </div>
          </article>

          <article className="rounded-[20px] border border-white/[0.07] bg-white/[0.025] p-5">
            <h3 className="flex items-center gap-2 text-sm font-black text-white"><Crown size={16} className="text-amber-300" />{text.moderators}</h3>
            <div className="mt-4 space-y-2">
              {moderatorsQuery.isLoading ? <LoaderCircle size={18} className="animate-spin text-teal-300" /> : (moderatorsQuery.data?.length ?? 0) === 0 ? (
                <p className="text-xs text-zinc-600">{text.moderatorsEmpty}</p>
              ) : moderatorsQuery.data?.map((member) => (
                <Link key={member.membershipId} to={`/users/${encodeURIComponent(member.userName)}`} className="flex items-center gap-3 rounded-xl p-2 transition hover:bg-white/[0.04]">
                  <div className="h-9 w-9 overflow-hidden rounded-xl bg-teal-300/10">
                    {member.profileImageUrl ? <img src={resolveAssetUrl(member.profileImageUrl) ?? ""} alt="" className="h-full w-full object-cover" /> : <span className="flex h-full items-center justify-center text-xs font-black text-teal-300">{member.userName.charAt(0).toUpperCase()}</span>}
                  </div>
                  <div className="min-w-0"><p className="truncate text-xs font-bold text-zinc-200">{member.userName}</p><p className={`text-[10px] font-semibold ${member.role === "Owner" ? "text-amber-300" : "text-violet-300"}`}>{roleLabel(member.role)}</p></div>
                </Link>
              ))}
            </div>
          </article>

          <article className="rounded-[20px] border border-white/[0.07] bg-white/[0.025] p-5">
            <div className="flex items-center justify-between gap-3">
              <h3 className="flex items-center gap-2 text-sm font-black text-white"><Sparkles size={16} className="text-violet-300" />{text.flair}</h3>
              {canManage && <button type="button" onClick={openFlairsEditor} className="text-xs font-bold text-violet-300 hover:text-violet-200">{text.manageFlairs}</button>}
            </div>
            <p className="mt-2 text-xs leading-5 text-zinc-600">{text.flairHelp}</p>
            {isActiveMember ? (
              <div className="mt-4 flex flex-wrap gap-2">
                <button type="button" disabled={busy} onClick={() => run(async () => { await setMyCommunityFlair(communityId, null); await flairsQuery.refetch(); })} className={`rounded-full border px-3 py-1.5 text-[11px] font-bold ${!selectedFlair ? "border-teal-300/40 bg-teal-300/10 text-teal-200" : "border-white/10 text-zinc-500"}`}>{text.noFlair}</button>
                {flairsQuery.data?.map((flair) => (
                  <button key={flair.id} type="button" disabled={busy} onClick={() => run(async () => { await setMyCommunityFlair(communityId, flair.id); await flairsQuery.refetch(); })} className="rounded-full border border-white/10 px-3 py-1.5 text-[11px] font-black transition hover:scale-[1.02]" style={{ backgroundColor: flair.backgroundColor, color: flair.textColor, outline: flair.isSelectedByCurrentUser ? "2px solid rgba(94,234,212,.8)" : "none" }}>{flair.name}</button>
                ))}
              </div>
            ) : <p className="mt-4 text-xs text-zinc-600">{isEn ? "Join the community to choose a flair." : "Flair seçmək üçün icmaya qoşul."}</p>}
          </article>
        </div>
      )}

      {tab === "members" && (
        <div className="p-4">
          {!isAuthenticated || !isActiveMember ? <div className="rounded-[20px] border border-dashed border-white/10 p-10 text-center text-sm text-zinc-600">{text.memberRestricted}</div> : (
            <>
              <div className="relative max-w-md"><Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-600" /><input value={memberSearch} onChange={(event) => setMemberSearch(event.target.value)} placeholder={text.memberSearch} className="h-11 w-full rounded-xl border border-white/10 bg-black/20 pl-10 pr-4 text-sm text-zinc-200 outline-none focus:border-teal-300/30" /></div>
              <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                {membersQuery.isLoading ? <LoaderCircle size={20} className="animate-spin text-teal-300" /> : membersQuery.data?.items.map((member) => (
                  <Link key={member.membershipId} to={`/users/${encodeURIComponent(member.userName)}`} className="flex items-center gap-3 rounded-[18px] border border-white/[0.07] bg-white/[0.025] p-4 transition hover:border-teal-300/20 hover:bg-white/[0.04]">
                    <div className="h-11 w-11 overflow-hidden rounded-2xl bg-teal-300/10">{member.profileImageUrl ? <img src={resolveAssetUrl(member.profileImageUrl) ?? ""} alt="" className="h-full w-full object-cover" /> : <span className="flex h-full items-center justify-center font-black text-teal-300">{member.userName.charAt(0).toUpperCase()}</span>}</div>
                    <div className="min-w-0"><p className="truncate text-sm font-bold text-zinc-200">{member.userName}</p><div className="mt-1 flex flex-wrap items-center gap-2"><span className="text-[10px] font-semibold text-zinc-600">{roleLabel(member.role)}</span>{member.flairName && <span className="rounded-full px-2 py-0.5 text-[9px] font-black" style={{ backgroundColor: member.flairBackgroundColor ?? undefined, color: member.flairTextColor ?? undefined }}>{member.flairName}</span>}</div></div>
                  </Link>
                ))}
              </div>
              {(membersQuery.data?.totalPages ?? 0) > 1 && <div className="mt-5 flex items-center justify-center gap-3"><button type="button" disabled={memberPage <= 1} onClick={() => setMemberPage((page) => Math.max(1, page - 1))} className="flex h-9 items-center gap-1 rounded-xl border border-white/10 px-3 text-xs font-bold text-zinc-400 disabled:opacity-30"><ChevronLeft size={14} />{text.previous}</button><span className="text-xs text-zinc-600">{memberPage} / {membersQuery.data?.totalPages}</span><button type="button" disabled={memberPage >= (membersQuery.data?.totalPages ?? 1)} onClick={() => setMemberPage((page) => page + 1)} className="flex h-9 items-center gap-1 rounded-xl border border-white/10 px-3 text-xs font-bold text-zinc-400 disabled:opacity-30">{text.next}<ChevronRight size={14} /></button></div>}
            </>
          )}
        </div>
      )}

      {tab === "events" && (
        <div className="p-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex rounded-xl border border-white/10 p-1"><button type="button" onClick={() => setShowPastEvents(false)} className={`rounded-lg px-3 py-2 text-xs font-bold ${!showPastEvents ? "bg-teal-300 text-[#07100f]" : "text-zinc-500"}`}>{text.upcoming}</button><button type="button" onClick={() => setShowPastEvents(true)} className={`rounded-lg px-3 py-2 text-xs font-bold ${showPastEvents ? "bg-teal-300 text-[#07100f]" : "text-zinc-500"}`}>{text.past}</button></div>
            {canModerate && <button type="button" onClick={() => openEventEditor()} className="flex h-10 items-center gap-2 rounded-xl bg-teal-300 px-4 text-xs font-black text-[#07100f]"><Plus size={15} />{text.createEvent}</button>}
          </div>
          <div className="mt-4 grid gap-4 lg:grid-cols-2">
            {eventsQuery.isLoading ? <LoaderCircle size={20} className="animate-spin text-teal-300" /> : (eventsQuery.data?.items.length ?? 0) === 0 ? <div className="col-span-full rounded-[20px] border border-dashed border-white/10 p-10 text-center text-sm text-zinc-600">{text.noEvents}</div> : eventsQuery.data?.items.map((event) => {
              const full = event.maxParticipants !== null && event.participantCount >= event.maxParticipants;
              return <article key={event.id} className="relative overflow-hidden rounded-[22px] border border-white/[0.08] bg-gradient-to-br from-teal-300/[0.06] via-white/[0.025] to-violet-400/[0.05] p-5">
                <div className="flex items-start justify-between gap-3"><div><span className="inline-flex items-center gap-1.5 rounded-full bg-teal-300/10 px-2.5 py-1 text-[10px] font-black text-teal-300"><Gamepad2 size={12} />{eventTypeLabel(event.type)}</span><h3 className="mt-3 text-lg font-black text-white">{event.title}</h3></div>{event.canManage && !event.isCancelled && <div className="flex gap-1"><button type="button" onClick={() => openEventEditor(event)} className="flex h-8 w-8 items-center justify-center rounded-lg text-zinc-500 hover:bg-white/[0.05] hover:text-white"><Pencil size={14} /></button><button
                      type="button"
                      onClick={async () => {
                        const confirmed = await requestConfirmation({
                          title: isEn ? "Cancel this event?" : "Bu tədbir ləğv edilsin?",
                          description: isEn
                            ? "Members will see the event as cancelled and new participation will be disabled."
                            : "Tədbir ləğv edilmiş kimi görünəcək və yeni iştirak dayandırılacaq.",
                          confirmLabel: isEn ? "Cancel event" : "Tədbiri ləğv et",
                          kind: "delete",
                          tone: "danger",
                        });
                        if (confirmed) {
                          await run(async () => {
                            await cancelCommunityEvent(communityId, event.id);
                            await eventsQuery.refetch();
                          });
                        }
                      }}
                      className="flex h-8 w-8 items-center justify-center rounded-lg text-rose-400/70 hover:bg-rose-400/10"
                    >
                      <Trash2 size={14} />
                    </button></div>}</div>
                <p data-user-content className="mt-3 line-clamp-3 text-sm leading-6 text-zinc-500">{event.description}</p>
                <div className="mt-4 space-y-2 text-xs text-zinc-500"><p className="flex items-center gap-2"><CalendarDays size={14} className="text-teal-300" />{dateTime(event.startsAtUtc)}</p>{event.gameName && <p className="flex items-center gap-2"><Gamepad2 size={14} className="text-violet-300" />{event.gameName}{event.platform ? ` · ${event.platform}` : ""}</p>}<p className="flex items-center gap-2"><UsersRound size={14} />{event.participantCount}{event.maxParticipants ? ` / ${event.maxParticipants}` : ""} {text.participants}</p><p>{text.hostedBy}: <Link to={`/users/${encodeURIComponent(event.createdByUserName)}`} className="font-bold text-zinc-300 hover:text-teal-300">{event.createdByUserName}</Link></p></div>
                <div className="mt-5 flex flex-wrap items-center gap-2">{event.isCancelled ? <span className="rounded-xl border border-rose-400/20 bg-rose-400/10 px-3 py-2 text-xs font-bold text-rose-300">{text.cancelled}</span> : isActiveMember && !showPastEvents ? <button type="button" disabled={busy || (full && !event.isJoinedByCurrentUser)} onClick={() => run(async () => { await setCommunityEventParticipation(communityId, event.id, !event.isJoinedByCurrentUser); await eventsQuery.refetch(); })} className={`rounded-xl px-4 py-2 text-xs font-black ${event.isJoinedByCurrentUser ? "border border-white/10 text-zinc-300" : "bg-teal-300 text-[#07100f]"}`}>{event.isJoinedByCurrentUser ? text.leave : full ? text.full : text.join}</button> : null}{event.joinUrl && event.isJoinedByCurrentUser && <a href={event.joinUrl} target="_blank" rel="noreferrer" className="flex items-center gap-1.5 rounded-xl border border-violet-400/20 px-3 py-2 text-xs font-bold text-violet-300"><ExternalLink size={13} />{isEn ? "Open link" : "Linki aç"}</a>}</div>
              </article>;
            })}
          </div>
        </div>
      )}

      {editor && <div className="fixed inset-0 z-[90] flex items-center justify-center bg-black/75 p-4 backdrop-blur-sm" onMouseDown={(event) => { if (event.target === event.currentTarget) setEditor(null); }}><div className="max-h-[88vh] w-full max-w-2xl overflow-y-auto rounded-[26px] border border-white/10 bg-[#101918] p-5 shadow-2xl"><div className="flex items-center justify-between"><h2 className="text-lg font-black text-white">{editor === "rules" ? text.rules : editor === "flairs" ? text.flair : editingEvent ? text.edit : text.createEvent}</h2><button type="button" onClick={() => setEditor(null)} className="flex h-9 w-9 items-center justify-center rounded-xl text-zinc-500 hover:bg-white/[0.05] hover:text-white"><X size={17} /></button></div>
        {editor === "rules" && <div className="mt-5 space-y-3">{rulesDraft.map((rule, index) => <div key={rule.id ?? `new-${index}`} className="rounded-2xl border border-white/[0.08] p-4"><div className="flex items-center justify-between"><span className="text-xs font-black text-teal-300">#{index + 1}</span><button type="button" onClick={() => setRulesDraft((items) => items.filter((_, itemIndex) => itemIndex !== index))} className="text-rose-400"><Trash2 size={15} /></button></div><input value={rule.title} onChange={(event) => setRulesDraft((items) => items.map((item, itemIndex) => itemIndex === index ? { ...item, title: event.target.value } : item))} placeholder={text.title} className="mt-3 h-10 w-full rounded-xl border border-white/10 bg-black/20 px-3 text-sm text-white outline-none" /><textarea value={rule.description} onChange={(event) => setRulesDraft((items) => items.map((item, itemIndex) => itemIndex === index ? { ...item, description: event.target.value } : item))} placeholder={text.description} className="mt-2 min-h-24 w-full rounded-xl border border-white/10 bg-black/20 p-3 text-sm text-white outline-none" /></div>)}<button type="button" onClick={() => setRulesDraft((items) => [...items, { title: "", description: "" }])} className="flex items-center gap-2 text-xs font-bold text-teal-300"><Plus size={14} />{text.addRule}</button><button type="button" disabled={busy} onClick={() => run(async () => { await replaceCommunityRules(communityId, rulesDraft); await rulesQuery.refetch(); setEditor(null); })} className="h-11 w-full rounded-xl bg-teal-300 text-sm font-black text-[#07100f]">{text.save}</button></div>}
        {editor === "flairs" && <div className="mt-5 space-y-3">{flairsDraft.map((flair, index) => <div key={flair.id ?? `new-${index}`} className="grid gap-3 rounded-2xl border border-white/[0.08] p-4 sm:grid-cols-[1fr_110px_110px_auto]"><input value={flair.name} onChange={(event) => setFlairsDraft((items) => items.map((item, itemIndex) => itemIndex === index ? { ...item, name: event.target.value } : item))} placeholder={text.flairName} className="h-10 rounded-xl border border-white/10 bg-black/20 px-3 text-sm text-white outline-none" /><input type="color" value={flair.backgroundColor} title={text.background} onChange={(event) => setFlairsDraft((items) => items.map((item, itemIndex) => itemIndex === index ? { ...item, backgroundColor: event.target.value } : item))} className="h-10 w-full rounded-xl border border-white/10 bg-black/20 p-1" /><input type="color" value={flair.textColor} title={text.color} onChange={(event) => setFlairsDraft((items) => items.map((item, itemIndex) => itemIndex === index ? { ...item, textColor: event.target.value } : item))} className="h-10 w-full rounded-xl border border-white/10 bg-black/20 p-1" /><button type="button" onClick={() => setFlairsDraft((items) => items.filter((_, itemIndex) => itemIndex !== index))} className="flex h-10 w-10 items-center justify-center text-rose-400"><Trash2 size={15} /></button></div>)}<button type="button" onClick={() => setFlairsDraft((items) => [...items, { name: "", backgroundColor: "#134e4a", textColor: "#99f6e4" }])} className="flex items-center gap-2 text-xs font-bold text-violet-300"><Plus size={14} />{text.addFlair}</button><button type="button" disabled={busy} onClick={() => run(async () => { await replaceCommunityFlairs(communityId, flairsDraft); await flairsQuery.refetch(); setEditor(null); })} className="h-11 w-full rounded-xl bg-teal-300 text-sm font-black text-[#07100f]">{text.save}</button></div>}
        {editor === "event" && <div className="mt-5 grid gap-3 sm:grid-cols-2"><input value={eventDraft.title} onChange={(event) => setEventDraft((value) => ({ ...value, title: event.target.value }))} placeholder={text.eventTitle} className="h-11 rounded-xl border border-white/10 bg-black/20 px-3 text-sm text-white outline-none sm:col-span-2" /><textarea value={eventDraft.description} onChange={(event) => setEventDraft((value) => ({ ...value, description: event.target.value }))} placeholder={text.description} className="min-h-28 rounded-xl border border-white/10 bg-black/20 p-3 text-sm text-white outline-none sm:col-span-2" /><select value={eventDraft.type} onChange={(event) => setEventDraft((value) => ({ ...value, type: event.target.value as ForumCommunityEventType }))} className="h-11 rounded-xl border border-white/10 bg-[#111b1a] px-3 text-sm text-white">{eventTypes.map((type) => <option key={type} value={type}>{eventTypeLabel(type)}</option>)}</select><input value={eventDraft.gameName ?? ""} onChange={(event) => setEventDraft((value) => ({ ...value, gameName: event.target.value || null }))} placeholder={text.game} className="h-11 rounded-xl border border-white/10 bg-black/20 px-3 text-sm text-white outline-none" /><input value={eventDraft.platform ?? ""} onChange={(event) => setEventDraft((value) => ({ ...value, platform: event.target.value || null }))} placeholder={text.platform} className="h-11 rounded-xl border border-white/10 bg-black/20 px-3 text-sm text-white outline-none" /><input type="number" min={2} max={500} value={eventDraft.maxParticipants ?? ""} onChange={(event) => setEventDraft((value) => ({ ...value, maxParticipants: event.target.value ? Number(event.target.value) : null }))} placeholder={text.capacity} className="h-11 rounded-xl border border-white/10 bg-black/20 px-3 text-sm text-white outline-none" /><label className="text-xs font-bold text-zinc-500">{text.start}<input type="datetime-local" value={toLocalInputValue(eventDraft.startsAtUtc)} onChange={(event) => {
        if (!event.target.value) return;
        setEventDraft((value) => ({
          ...value,
          startsAtUtc: new Date(event.target.value).toISOString(),
        }));
      }} className="mt-2 h-11 w-full rounded-xl border border-white/10 bg-black/20 px-3 text-sm text-white outline-none" /></label><label className="text-xs font-bold text-zinc-500">{text.end}<input type="datetime-local" value={toLocalInputValue(eventDraft.endsAtUtc)} onChange={(event) => setEventDraft((value) => ({ ...value, endsAtUtc: event.target.value ? new Date(event.target.value).toISOString() : null }))} className="mt-2 h-11 w-full rounded-xl border border-white/10 bg-black/20 px-3 text-sm text-white outline-none" /></label><input value={eventDraft.joinUrl ?? ""} onChange={(event) => setEventDraft((value) => ({ ...value, joinUrl: event.target.value || null }))} placeholder={text.joinUrl} className="h-11 rounded-xl border border-white/10 bg-black/20 px-3 text-sm text-white outline-none sm:col-span-2" /><button type="button" disabled={busy} onClick={() => run(async () => { if (editingEvent) await updateCommunityEvent(communityId, editingEvent.id, eventDraft); else await createCommunityEvent(communityId, eventDraft); await queryClient.invalidateQueries({ queryKey: ["community-features", communityId, "events"] }); setEditor(null); })} className="h-11 rounded-xl bg-teal-300 text-sm font-black text-[#07100f] sm:col-span-2">{text.save}</button></div>}
      </div></div>}
    </section>
  );
}
