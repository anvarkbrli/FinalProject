import { useMutation } from "@tanstack/react-query";
import {
  AlertTriangle,
  FileImage,
  Flag,
  ImagePlus,
  LoaderCircle,
  ShieldAlert,
  X,
} from "lucide-react";
import {
  useEffect,
  useMemo,
  useState,
  type ChangeEvent,
  type FormEvent,
} from "react";
import { createPortal } from "react-dom";
import { getApiErrorMessage } from "../../lib/api";
import { createUserReport } from "../../services/userReportsService";
import type {
  UserReportCategory,
  UserReportContentType,
} from "../../types/userReports";

interface UserReportModalProps {
  isOpen: boolean;
  reportedUserId: string;
  reportedUserName: string;
  reportedContentType?: UserReportContentType;
  reportedContentId?: number | null;
  reportedContentLabel?: string;
  subjectKind?: "user" | "community";
  onClose: () => void;
  onSubmitted: () => void;
}

const categoryOptions: Array<{
  value: UserReportCategory;
  label: string;
}> = [
  {
    value: "Spam",
    label: "Spam və təkrarlanan məzmun",
  },
  {
    value: "HarassmentOrInsult",
    label: "Təhqir və narahat etmə",
  },
  {
    value: "HateSpeech",
    label: "Nifrət nitqi",
  },
  {
    value: "InappropriateContent",
    label: "Uyğunsuz məzmun",
  },
  {
    value: "Impersonation",
    label: "Başqasını təqlid etmə",
  },
  {
    value: "ScamOrFraud",
    label: "Dələduzluq və saxtakarlıq",
  },
  {
    value: "ThreatsOrViolence",
    label: "Təhdid və zorakılıq",
  },
  {
    value: "PrivacyViolation",
    label: "Məxfilik pozuntusu",
  },
  {
    value: "Other",
    label: "Digər",
  },
];

const allowedImageTypes = [
  "image/jpeg",
  "image/png",
  "image/webp",
];
const maxImageSize = 5 * 1024 * 1024;

export function UserReportModal({
  isOpen,
  reportedUserId,
  reportedUserName,
  reportedContentType = "UserProfile",
  reportedContentId = null,
  reportedContentLabel,
  subjectKind = "user",
  onClose,
  onSubmitted,
}: UserReportModalProps) {
  const [category, setCategory] =
    useState<UserReportCategory>("Spam");
  const [description, setDescription] = useState("");
  const [evidenceImages, setEvidenceImages] =
    useState<File[]>([]);
  const [validationMessage, setValidationMessage] =
    useState<string | null>(null);

  const imagePreviews = useMemo(
    () =>
      evidenceImages.map((file) => ({
        file,
        url: URL.createObjectURL(file),
      })),
    [evidenceImages],
  );

  useEffect(() => {
    return () => {
      imagePreviews.forEach((preview) =>
        URL.revokeObjectURL(preview.url),
      );
    };
  }, [imagePreviews]);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    document.body.style.overflow = "hidden";

    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      document.body.style.overflow = "";
    };
  }, [isOpen, onClose]);

  const reportMutation = useMutation({
    mutationFn: createUserReport,
    onSuccess: () => {
      setCategory("Spam");
      setDescription("");
      setEvidenceImages([]);
      setValidationMessage(null);
      onSubmitted();
    },
  });

  if (!isOpen) {
    return null;
  }

  const normalizedDescription = description.trim();
  const hasConcreteContent =
    reportedContentType !== "UserProfile" &&
    reportedContentId !== null;

  function handleImageChange(
    event: ChangeEvent<HTMLInputElement>,
  ) {
    const files = Array.from(event.target.files ?? []);
    event.target.value = "";

    if (files.length === 0) {
      return;
    }

    if (evidenceImages.length + files.length > 5) {
      setValidationMessage(
        "Maksimum 5 sübut şəkli əlavə edə bilərsən.",
      );
      return;
    }

    if (files.some((file) => !allowedImageTypes.includes(file.type))) {
      setValidationMessage(
        "Yalnız JPEG, PNG və WEBP şəkilləri qəbul edilir.",
      );
      return;
    }

    if (files.some((file) => file.size > maxImageSize)) {
      setValidationMessage(
        "Sübut şəkli maksimum 5 MB ola bilər.",
      );
      return;
    }

    setValidationMessage(null);
    setEvidenceImages((current) => [...current, ...files]);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setValidationMessage(null);

    if (
      !hasConcreteContent &&
      !normalizedDescription &&
      evidenceImages.length === 0
    ) {
      setValidationMessage(
        "Profil şikayəti üçün ən azı açıqlama və ya şəkil əlavə et.",
      );
      return;
    }

    if (
      normalizedDescription &&
      normalizedDescription.length < 10
    ) {
      setValidationMessage(
        "Açıqlama ən azı 10 simvol olmalıdır.",
      );
      return;
    }

    reportMutation.mutate({
      reportedUserId,
      category,
      description: normalizedDescription,
      evidenceImages,
      reportedContentType,
      reportedContentId,
    });
  }

  const errorMessage = reportMutation.isError
    ? getApiErrorMessage(
        reportMutation.error,
        "Şikayəti göndərmək mümkün olmadı.",
      )
    : null;

  return createPortal(
    <div
      className="fixed inset-0 z-[1000] grid place-items-center overflow-y-auto bg-black/75 p-4 backdrop-blur-sm sm:p-6"
      role="dialog"
      aria-modal="true"
      aria-labelledby="user-report-title"
      onMouseDown={(event) => {
        if (
          event.target === event.currentTarget &&
          !reportMutation.isPending
        ) {
          onClose();
        }
      }}
    >
      <form
        onSubmit={handleSubmit}
        className="flex max-h-[calc(100dvh-2rem)] w-full max-w-xl flex-col overflow-hidden rounded-3xl border border-white/[0.09] bg-[#111116] shadow-2xl shadow-black/60 sm:max-h-[calc(100dvh-3rem)]"
      >
        <div className="flex items-start justify-between gap-4 border-b border-white/[0.07] px-6 py-5">
          <div className="flex min-w-0 items-start gap-3">
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl border border-red-400/15 bg-red-500/[0.08] text-red-300">
              <ShieldAlert size={21} />
            </div>

            <div className="min-w-0">
              <h2
                id="user-report-title"
                className="text-lg font-bold text-white"
              >
                {subjectKind === "community"
                  ? "İcmanı şikayət et"
                  : "İstifadəçini şikayət et"}
              </h2>
              <p className="mt-1 truncate text-sm text-zinc-500">
                {subjectKind === "community"
                  ? `${reportedUserName} icması haqqında şikayət göndərilir`
                  : `@${reportedUserName} haqqında şikayət göndərilir`}
              </p>
              {reportedContentLabel ? (
                <p className="mt-1 truncate text-xs text-zinc-600">
                  Məzmun: {reportedContentLabel}
                </p>
              ) : null}
            </div>
          </div>

          <button
            type="button"
            onClick={onClose}
            disabled={reportMutation.isPending}
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-zinc-500 transition hover:bg-white/[0.06] hover:text-white disabled:opacity-50"
            aria-label="Şikayət pəncərəsini bağla"
          >
            <X size={18} />
          </button>
        </div>

        <div className="min-h-0 flex-1 space-y-5 overflow-y-auto px-6 py-6">
          <div>
            <label
              htmlFor="report-category"
              className="mb-2 block text-xs font-bold uppercase tracking-[0.14em] text-zinc-500"
            >
              Şikayət kateqoriyası
            </label>
            <select
              id="report-category"
              value={category}
              onChange={(event) => {
                setCategory(
                  event.target.value as UserReportCategory,
                );
              }}
              disabled={reportMutation.isPending}
              className="h-12 w-full rounded-2xl border border-white/[0.09] bg-white/[0.04] px-4 text-sm text-zinc-200 outline-none transition focus:border-violet-400/40 focus:ring-2 focus:ring-violet-500/10 disabled:opacity-60"
            >
              {categoryOptions.map((option) => (
                <option
                  key={option.value}
                  value={option.value}
                  className="bg-[#15151b]"
                >
                  {option.label}
                </option>
              ))}
            </select>
          </div>

          <div>
            <div className="mb-2 flex items-center justify-between gap-3">
              <label
                htmlFor="report-description"
                className="text-xs font-bold uppercase tracking-[0.14em] text-zinc-500"
              >
                {hasConcreteContent
                  ? "Əlavə açıqlama"
                  : "Açıqlama və sübut"}
              </label>
              <span className="text-xs text-zinc-600">
                {description.length}/2000
              </span>
            </div>

            <textarea
              id="report-description"
              value={description}
              onChange={(event) => {
                setDescription(event.target.value);
                setValidationMessage(null);
              }}
              maxLength={2000}
              rows={6}
              disabled={reportMutation.isPending}
              placeholder={
                hasConcreteContent
                  ? "Əlavə kontekst yazmaq istəsən burada qeyd et..."
                  : "Nə baş verdiyini, tarix və kontekst kimi yoxlanıla bilən detalları yaz..."
              }
              className="w-full resize-none rounded-2xl border border-white/[0.09] bg-white/[0.035] px-4 py-3 text-sm leading-6 text-zinc-200 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/40 focus:ring-2 focus:ring-violet-500/10 disabled:opacity-60"
            />
          </div>

          <div>
            <p className="mb-2 text-xs font-bold uppercase tracking-[0.14em] text-zinc-500">
              Şəkil sübutu
            </p>

            {imagePreviews.length > 0 ? (
              <div className="overflow-hidden rounded-2xl border border-white/[0.09] bg-black/20">
                <img
                  src={imagePreviews[0].url}
                  alt="Şikayət sübutunun önizləməsi"
                  className="h-64 max-h-64 w-full object-cover"
                />
                <div className="flex items-center justify-between gap-3 border-t border-white/[0.07] px-4 py-3">
                  <span className="flex min-w-0 items-center gap-2 text-xs text-zinc-500">
                    <FileImage size={15} />
                    <span className="truncate">
                      {imagePreviews[0].file.name}
                      {imagePreviews.length > 1
                        ? ` və daha ${imagePreviews.length - 1} şəkil`
                        : ""}
                    </span>
                  </span>
                  <button
                    type="button"
                    onClick={() => setEvidenceImages([])}
                    disabled={reportMutation.isPending}
                    className="text-xs font-semibold text-red-300 transition hover:text-red-200 disabled:opacity-50"
                  >
                    Sil
                  </button>
                </div>
              </div>
            ) : (
              <label className="flex cursor-pointer flex-col items-center justify-center rounded-2xl border border-dashed border-white/[0.12] bg-white/[0.025] px-5 py-8 text-center transition hover:border-violet-400/30 hover:bg-violet-500/[0.05]">
                <ImagePlus
                  size={25}
                  className="text-violet-300"
                />
                <span className="mt-3 text-sm font-semibold text-zinc-300">
                  Sübut şəklini seç
                </span>
                <span className="mt-1 text-xs text-zinc-600">
                  JPEG, PNG və ya WEBP · maksimum 5 MB
                </span>
                <input
                  type="file"
                  multiple
                  accept="image/jpeg,image/png,image/webp"
                  onChange={handleImageChange}
                  disabled={reportMutation.isPending}
                  className="sr-only"
                />
              </label>
            )}
            {imagePreviews.length > 0 && imagePreviews.length < 5 && (
              <label className="mt-3 flex cursor-pointer items-center justify-center gap-2 rounded-xl border border-dashed border-white/[0.1] px-4 py-3 text-xs font-semibold text-zinc-400 transition hover:border-violet-400/25 hover:text-violet-200">
                <ImagePlus size={15} />
                Başqa şəkil əlavə et ({imagePreviews.length}/5)
                <input
                  type="file"
                  multiple
                  accept="image/jpeg,image/png,image/webp"
                  onChange={handleImageChange}
                  disabled={reportMutation.isPending}
                  className="sr-only"
                />
              </label>
            )}
          </div>

          <div className="flex items-start gap-3 rounded-2xl border border-amber-400/10 bg-amber-500/[0.045] px-4 py-3 text-xs leading-5 text-amber-100/65">
            <AlertTriangle
              size={17}
              className="mt-0.5 shrink-0 text-amber-300"
            />
            Saxta və təkrarlanan şikayətlər moderasiya prosesini
            ləngidə bilər. Yalnız yoxlanıla bilən məlumat göndər.
          </div>

          {(validationMessage || errorMessage) && (
            <div className="rounded-2xl border border-red-400/15 bg-red-500/[0.07] px-4 py-3 text-sm text-red-200">
              {validationMessage ?? errorMessage}
            </div>
          )}
        </div>

        <div className="flex shrink-0 flex-col-reverse gap-3 border-t border-white/[0.07] px-6 py-5 sm:flex-row sm:justify-end">
          <button
            type="button"
            onClick={onClose}
            disabled={reportMutation.isPending}
            className="h-11 rounded-xl border border-white/[0.09] bg-white/[0.04] px-5 text-sm font-semibold text-zinc-400 transition hover:bg-white/[0.08] hover:text-white disabled:opacity-50"
          >
            Ləğv et
          </button>
          <button
            type="submit"
            disabled={reportMutation.isPending}
            className="flex h-11 items-center justify-center gap-2 rounded-xl bg-red-500 px-5 text-sm font-semibold text-white transition hover:bg-red-400 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {reportMutation.isPending ? (
              <LoaderCircle
                size={17}
                className="animate-spin"
              />
            ) : (
              <Flag size={17} />
            )}
            Şikayəti göndər
          </button>
        </div>
      </form>
    </div>,
    document.body,
  );
}
