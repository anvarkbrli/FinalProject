import {
  FileVideo2,
  ImagePlus,
  LoaderCircle,
  PenLine,
  UserRound,
  UsersRound,
  X,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { getApiErrorMessage } from "../../lib/api";
import { resolveAssetUrl } from "../../lib/helpers";
import { createDiscussion } from "../../services/forumService";
import type { ForumCommunityDTO } from "../../types/community";
import type { ForumCategoryDTO } from "../../types/home";
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
    setFiles([]);
    setError("");
    onClose();
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
    if (!content.trim() && files.length === 0) {
      setError("Mətn və ya ən azı bir media əlavə edin.");
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
        mediaFiles: files,
      });
      onCreated(discussion);
      setTitle("");
      setContent("");
      setCategoryId(0);
      setCommunityId(null);
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
