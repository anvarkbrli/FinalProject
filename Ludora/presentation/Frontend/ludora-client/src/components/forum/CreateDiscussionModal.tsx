import {
  BarChart3,
  FileVideo2,
  ImagePlus,
  LoaderCircle,
  PenLine,
  Plus,
  Trash2,
  UserRound,
  UsersRound,
  X,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { getApiErrorMessage } from "../../lib/api";
import { resolveAssetUrl } from "../../lib/helpers";
import { createDiscussion } from "../../services/forumService";
import type { ForumCommunityDTO } from "../../types/community";
import type {
  ForumCategoryDTO,
  PollResultsVisibility,
} from "../../types/home";
import type { DiscussionDetailDTO } from "../../types/forum";

interface CreateDiscussionModalProps {
  open: boolean;
  categories: ForumCategoryDTO[];
  communities: ForumCommunityDTO[];
  onClose: () => void;
  onCreated: (discussion: DiscussionDetailDTO) => void;
}

export function CreateDiscussionModal({
  open,
  categories,
  communities,
  onClose,
  onCreated,
}: CreateDiscussionModalProps) {
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");
  const [categoryId, setCategoryId] = useState(0);
  const [communityId, setCommunityId] = useState<number | null>(null);
  const [isSpoiler, setIsSpoiler] = useState(false);
  const [pollEnabled, setPollEnabled] = useState(false);
  const [pollQuestion, setPollQuestion] = useState("");
  const [pollOptions, setPollOptions] = useState(["", ""]);
  const [pollDurationHours, setPollDurationHours] = useState(24);
  const [pollResultsVisibility, setPollResultsVisibility] =
    useState<PollResultsVisibility>("AfterVote");
  const [allowVoteChange, setAllowVoteChange] = useState(true);
  const [files, setFiles] = useState<File[]>([]);
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const previews = useMemo(
    () =>
      files.map((file) => ({
        file,
        url: URL.createObjectURL(file),
      })),
    [files],
  );

  useEffect(
    () => () => previews.forEach((item) => URL.revokeObjectURL(item.url)),
    [previews],
  );

  useEffect(() => {
    if (!open) return;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = previousOverflow;
    };
  }, [open]);

  if (!open) return null;

  function closeAndReset() {
    if (isSubmitting) return;
    setTitle("");
    setContent("");
    setCategoryId(0);
    setCommunityId(null);
    setIsSpoiler(false);
    resetPoll();
    setFiles([]);
    setError("");
    onClose();
  }

  function resetPoll() {
    setPollEnabled(false);
    setPollQuestion("");
    setPollOptions(["", ""]);
    setPollDurationHours(24);
    setPollResultsVisibility("AfterVote");
    setAllowVoteChange(true);
  }

  function addFiles(selected: FileList | null) {
    if (!selected) return;
    const accepted = Array.from(selected).filter(
      (file) =>
        file.type.startsWith("image/") ||
        file.type.startsWith("video/"),
    );
    setFiles((current) => [...current, ...accepted].slice(0, 8));
  }

  async function submit() {
    const normalizedTitle = title.trim();
    if (!normalizedTitle) {
      setError("Müzakirə başlığı məcburidir.");
      return;
    }
    if (!categoryId) {
      setError("Kateqoriya seçin.");
      return;
    }
    const normalizedPollOptions = pollOptions
      .map((option) => option.trim())
      .filter(Boolean);
    if (pollEnabled && !pollQuestion.trim()) {
      setError("Sorğu sualını yazın.");
      return;
    }
    if (pollEnabled && normalizedPollOptions.length < 2) {
      setError("Sorğu üçün ən azı 2 seçim yazın.");
      return;
    }
    if (!content.trim() && files.length === 0 && !pollEnabled) {
      setError("Mətn, media və ya sorğu əlavə edin.");
      return;
    }

    setError("");
    setIsSubmitting(true);
    try {
      const discussion = await createDiscussion({
        title: normalizedTitle,
        content: content.trim(),
        categoryId,
        communityId,
        isSpoiler,
        poll: pollEnabled
          ? {
              question: pollQuestion.trim(),
              options: normalizedPollOptions,
              endsAtUtc: new Date(
                Date.now() + pollDurationHours * 60 * 60 * 1000,
              ).toISOString(),
              resultsVisibility: pollResultsVisibility,
              allowVoteChange,
            }
          : null,
        mediaFiles: files,
      });
      onCreated(discussion);
      setTitle("");
      setContent("");
      setCategoryId(0);
      setCommunityId(null);
      setIsSpoiler(false);
      resetPoll();
      setFiles([]);
    } catch (submitError) {
      setError(
        getApiErrorMessage(
          submitError,
          "Müzakirə yaradıla bilmədi.",
        ),
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-[90] flex items-end justify-center bg-black/75 backdrop-blur-md sm:items-center sm:p-5"
      role="dialog"
      aria-modal="true"
      aria-labelledby="create-discussion-title"
    >
      <button
        type="button"
        className="absolute inset-0"
        onClick={closeAndReset}
        aria-label="Pəncərəni bağla"
      />
      <section className="relative max-h-[94vh] w-full max-w-3xl overflow-y-auto rounded-t-[28px] border border-teal-200/15 bg-[#0a1413] p-5 shadow-2xl sm:rounded-[28px] sm:p-7">
        <header className="flex items-start justify-between gap-4">
          <div>
            <p className="text-[10px] font-bold uppercase tracking-[0.22em] text-teal-300">
              Yeni mövzu
            </p>
            <h2
              id="create-discussion-title"
              className="mt-2 text-2xl font-bold text-white"
            >
              Müzakirə yarat
            </h2>
          </div>
          <button
            type="button"
            onClick={closeAndReset}
            className="flex h-9 w-9 items-center justify-center rounded-xl border border-white/10 text-zinc-500"
            aria-label="Bağla"
          >
            <X size={18} />
          </button>
        </header>

        <div className="mt-6 space-y-5">
          <div>
            <p className="text-xs font-bold text-zinc-300">
              Kim kimi paylaşır?
            </p>
            <div className="mt-2 grid gap-2 sm:grid-cols-2">
              <button
                type="button"
                onClick={() => setCommunityId(null)}
                className={`flex items-center gap-3 rounded-2xl border p-3 text-left transition ${
                  communityId === null
                    ? "border-teal-300/35 bg-teal-300/10 text-teal-100"
                    : "border-white/[0.08] bg-white/[0.025] text-zinc-500 hover:text-white"
                }`}
              >
                <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-white/[0.06]">
                  <UserRound size={17} />
                </span>
                <span>
                  <span className="block text-xs font-bold">
                    Fərdi paylaşım
                  </span>
                  <span className="mt-0.5 block text-[10px] opacity-60">
                    Öz adın və profil şəklinlə
                  </span>
                </span>
              </button>

              {communities.map((community) => (
                <button
                  key={community.id}
                  type="button"
                  onClick={() => setCommunityId(community.id)}
                  className={`flex items-center gap-3 rounded-2xl border p-3 text-left transition ${
                    communityId === community.id
                      ? "border-teal-300/35 bg-teal-300/10 text-teal-100"
                      : "border-white/[0.08] bg-white/[0.025] text-zinc-500 hover:text-white"
                  }`}
                >
                  <span className="flex h-9 w-9 items-center justify-center overflow-hidden rounded-xl bg-teal-300/10">
                    {community.iconUrl ? (
                      <img
                        src={resolveAssetUrl(community.iconUrl) ?? ""}
                        alt=""
                        className="h-full w-full object-cover"
                      />
                    ) : (
                      <UsersRound size={17} />
                    )}
                  </span>
                  <span className="min-w-0">
                    <span className="block truncate text-xs font-bold">
                      {community.name}
                    </span>
                    <span className="mt-0.5 block text-[10px] opacity-60">
                      İcma daxilində paylaş
                    </span>
                  </span>
                </button>
              ))}
            </div>
          </div>

          <label className="block">
            <span className="text-xs font-bold text-zinc-300">
              Başlıq <span className="text-rose-300">*</span>
            </span>
            <input
              value={title}
              onChange={(event) => setTitle(event.target.value)}
              maxLength={200}
              placeholder="Nə haqqında danışmaq istəyirsən?"
              className="mt-2 h-12 w-full rounded-2xl border border-white/[0.09] bg-white/[0.035] px-4 text-sm text-white outline-none focus:border-teal-300/35"
              autoFocus
            />
          </label>

          <label className="flex cursor-pointer items-start gap-3 rounded-2xl border border-amber-300/15 bg-amber-300/[0.05] p-4">
            <input
              type="checkbox"
              checked={isSpoiler}
              onChange={(event) => setIsSpoiler(event.target.checked)}
              className="mt-0.5 h-4 w-4 accent-amber-300"
            />
            <span>
              <span className="block text-xs font-bold text-amber-200">
                Spoiler xəbərdarlığı
              </span>
              <span className="mt-1 block text-[11px] leading-5 text-zinc-500">
                Mövzu mətni və media istifadəçi açana qədər gizlədiləcək.
              </span>
            </span>
          </label>

          <section className="rounded-2xl border border-teal-300/15 bg-teal-300/[0.04] p-4">
            <button
              type="button"
              onClick={() => setPollEnabled((current) => !current)}
              className="flex w-full items-center justify-between gap-3 text-left"
            >
              <span className="flex items-center gap-3">
                <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-teal-300/10 text-teal-200">
                  <BarChart3 size={18} />
                </span>
                <span>
                  <span className="block text-xs font-bold text-teal-100">
                    Sorğu əlavə et
                  </span>
                  <span className="mt-1 block text-[11px] text-zinc-500">
                    İstifadəçilər seçimlərdən birinə səs verə biləcək.
                  </span>
                </span>
              </span>
              <span
                className={`h-6 w-11 rounded-full p-1 transition ${
                  pollEnabled ? "bg-teal-300" : "bg-white/10"
                }`}
              >
                <span
                  className={`block h-4 w-4 rounded-full bg-[#07100f] transition ${
                    pollEnabled ? "translate-x-5" : ""
                  }`}
                />
              </span>
            </button>

            {pollEnabled && (
              <div className="mt-4 space-y-3 border-t border-white/[0.07] pt-4">
                <input
                  value={pollQuestion}
                  onChange={(event) => setPollQuestion(event.target.value)}
                  maxLength={300}
                  placeholder="Sorğu sualı"
                  className="h-11 w-full rounded-xl border border-white/[0.09] bg-black/20 px-3 text-sm text-white outline-none focus:border-teal-300/35"
                />

                {pollOptions.map((option, index) => (
                  <div key={index} className="flex gap-2">
                    <input
                      value={option}
                      onChange={(event) =>
                        setPollOptions((current) =>
                          current.map((item, itemIndex) =>
                            itemIndex === index ? event.target.value : item,
                          ),
                        )
                      }
                      maxLength={120}
                      placeholder={`${index + 1}. seçim`}
                      className="h-10 flex-1 rounded-xl border border-white/[0.09] bg-black/20 px-3 text-sm text-white outline-none focus:border-teal-300/35"
                    />
                    {pollOptions.length > 2 && (
                      <button
                        type="button"
                        onClick={() =>
                          setPollOptions((current) =>
                            current.filter((_, itemIndex) => itemIndex !== index),
                          )
                        }
                        className="flex h-10 w-10 items-center justify-center rounded-xl border border-white/[0.08] text-zinc-500 hover:text-rose-300"
                        aria-label="Seçimi sil"
                      >
                        <Trash2 size={15} />
                      </button>
                    )}
                  </div>
                ))}

                {pollOptions.length < 6 && (
                  <button
                    type="button"
                    onClick={() =>
                      setPollOptions((current) => [...current, ""])
                    }
                    className="flex items-center gap-2 text-xs font-bold text-teal-200"
                  >
                    <Plus size={15} />
                    Seçim əlavə et
                  </button>
                )}

                <div className="grid gap-3 sm:grid-cols-2">
                  <label className="text-[11px] font-semibold text-zinc-400">
                    Müddət
                    <select
                      value={pollDurationHours}
                      onChange={(event) =>
                        setPollDurationHours(Number(event.target.value))
                      }
                      className="mt-1.5 h-10 w-full rounded-xl border border-white/[0.09] bg-[#0a1413] px-3 text-sm text-white outline-none"
                    >
                      <option value={1}>1 saat</option>
                      <option value={24}>1 gün</option>
                      <option value={72}>3 gün</option>
                      <option value={168}>7 gün</option>
                      <option value={720}>30 gün</option>
                    </select>
                  </label>
                  <label className="text-[11px] font-semibold text-zinc-400">
                    Nəticələr
                    <select
                      value={pollResultsVisibility}
                      onChange={(event) =>
                        setPollResultsVisibility(
                          event.target.value as PollResultsVisibility,
                        )
                      }
                      className="mt-1.5 h-10 w-full rounded-xl border border-white/[0.09] bg-[#0a1413] px-3 text-sm text-white outline-none"
                    >
                      <option value="Always">Həmişə göstər</option>
                      <option value="AfterVote">Səsdən sonra göstər</option>
                      <option value="AfterEnd">Bitəndən sonra göstər</option>
                    </select>
                  </label>
                </div>

                <label className="flex items-center gap-2 text-xs text-zinc-400">
                  <input
                    type="checkbox"
                    checked={allowVoteChange}
                    onChange={(event) => setAllowVoteChange(event.target.checked)}
                    className="h-4 w-4 accent-teal-300"
                  />
                  İstifadəçi səsini dəyişə bilsin
                </label>
              </div>
            )}
          </section>

          <div>
            <p className="text-xs font-bold text-zinc-300">
              Kateqoriya <span className="text-rose-300">*</span>
            </p>
            <div className="mt-2 flex flex-wrap gap-2">
              {categories.map((category) => (
                <button
                  key={category.id}
                  type="button"
                  onClick={() => setCategoryId(category.id)}
                  className={`rounded-xl border px-3 py-2 text-xs font-semibold transition ${
                    categoryId === category.id
                      ? "border-teal-300/35 bg-teal-300/10 text-teal-100"
                      : "border-white/[0.08] bg-white/[0.025] text-zinc-500 hover:text-white"
                  }`}
                >
                  {category.name}
                </button>
              ))}
            </div>
          </div>

          <label className="block">
            <span className="text-xs font-bold text-zinc-300">
              Müzakirə mətni
            </span>
            <textarea
              value={content}
              onChange={(event) => setContent(event.target.value)}
              maxLength={10000}
              rows={6}
              placeholder="Fikrini ətraflı paylaş..."
              className="mt-2 w-full resize-y rounded-2xl border border-white/[0.09] bg-white/[0.035] p-4 text-sm leading-6 text-white outline-none focus:border-teal-300/35"
            />
          </label>

          <div>
            <div className="flex items-center justify-between">
              <p className="text-xs font-bold text-zinc-300">
                Şəkil və videolar
              </p>
              <span className="text-[11px] text-zinc-600">
                {files.length}/8
              </span>
            </div>

            <label className="mt-2 flex cursor-pointer items-center justify-center gap-3 rounded-2xl border border-dashed border-white/10 bg-white/[0.02] px-4 py-6 text-sm text-zinc-500 transition hover:border-teal-300/25 hover:text-teal-200">
              <ImagePlus size={19} />
              Bir və ya bir neçə media seç
              <input
                type="file"
                accept="image/jpeg,image/png,image/webp,video/mp4,video/webm"
                multiple
                className="sr-only"
                onChange={(event) => {
                  addFiles(event.target.files);
                  event.target.value = "";
                }}
              />
            </label>

            {previews.length > 0 && (
              <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-4">
                {previews.map(({ file, url }, index) => (
                  <div
                    key={`${file.name}-${file.lastModified}`}
                    className="relative h-28 overflow-hidden rounded-2xl border border-white/[0.08] bg-black/25"
                  >
                    {file.type.startsWith("video/") ? (
                      <div className="flex h-full items-center justify-center text-zinc-500">
                        <FileVideo2 size={28} />
                      </div>
                    ) : (
                      <img
                        src={url}
                        alt=""
                        className="h-full w-full object-cover"
                      />
                    )}
                    <button
                      type="button"
                      onClick={() =>
                        setFiles((current) =>
                          current.filter(
                            (_, currentIndex) =>
                              currentIndex !== index,
                          ),
                        )
                      }
                      className="absolute right-1.5 top-1.5 flex h-7 w-7 items-center justify-center rounded-lg bg-black/65 text-white"
                      aria-label={`${file.name} faylını sil`}
                    >
                      <X size={14} />
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        {error && (
          <p className="mt-5 rounded-xl border border-rose-400/15 bg-rose-400/[0.06] px-4 py-3 text-sm text-rose-200">
            {error}
          </p>
        )}

        <footer className="mt-6 flex justify-end gap-3 border-t border-white/[0.07] pt-5">
          <button
            type="button"
            onClick={closeAndReset}
            className="h-11 rounded-xl px-4 text-sm font-semibold text-zinc-500 hover:text-white"
          >
            Ləğv et
          </button>
          <button
            type="button"
            onClick={submit}
            disabled={isSubmitting}
            className="flex h-11 items-center gap-2 rounded-xl bg-teal-300 px-5 text-sm font-black text-[#07100f] disabled:opacity-50"
          >
            {isSubmitting ? (
              <LoaderCircle size={17} className="animate-spin" />
            ) : (
              <PenLine size={17} />
            )}
            Paylaş
          </button>
        </footer>
      </section>
    </div>
  );
}
