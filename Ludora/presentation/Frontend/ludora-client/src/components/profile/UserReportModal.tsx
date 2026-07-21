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
import { getApiErrorMessage } from "../../lib/api";
import { createUserReport } from "../../services/userReportsService";
import type { UserReportCategory } from "../../types/userReports";

interface UserReportModalProps {
  isOpen: boolean;
  reportedUserId: string;
  reportedUserName: string;
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
  onClose,
  onSubmitted,
}: UserReportModalProps) {
  const [category, setCategory] =
    useState<UserReportCategory>("Spam");
  const [description, setDescription] = useState("");
  const [evidenceImage, setEvidenceImage] =
    useState<File | null>(null);
  const [validationMessage, setValidationMessage] =
    useState<string | null>(null);

  const imagePreviewUrl = useMemo(
    () =>
      evidenceImage
        ? URL.createObjectURL(evidenceImage)
        : null,
    [evidenceImage],
  );

  useEffect(() => {
    return () => {
      if (imagePreviewUrl) {
        URL.revokeObjectURL(imagePreviewUrl);
      }
    };
  }, [imagePreviewUrl]);

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
      setEvidenceImage(null);
      setValidationMessage(null);
      onSubmitted();
    },
  });

  if (!isOpen) {
    return null;
  }

  const normalizedDescription = description.trim();

  function handleImageChange(
    event: ChangeEvent<HTMLInputElement>,
  ) {
    const file = event.target.files?.[0] ?? null;
    event.target.value = "";

    if (!file) {
      return;
    }

    if (!allowedImageTypes.includes(file.type)) {
      setValidationMessage(
        "Yalnız JPEG, PNG və WEBP şəkilləri qəbul edilir.",
      );
      return;
    }

    if (file.size > maxImageSize) {
      setValidationMessage(
        "Sübut şəkli maksimum 5 MB ola bilər.",
      );
      return;
    }

    setValidationMessage(null);
    setEvidenceImage(file);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setValidationMessage(null);

    if (!normalizedDescription && !evidenceImage) {
      setValidationMessage(
        "Sübut kimi ən azı açıqlama və ya şəkil əlavə et.",
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
      evidenceImage,
    });
  }

  const errorMessage = reportMutation.isError
    ? getApiErrorMessage(
        reportMutation.error,
        "Şikayəti göndərmək mümkün olmadı.",
      )
    : null;

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-black/75 px-4 py-8 backdrop-blur-sm"
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
        className="w-full max-w-xl overflow-hidden rounded-3xl border border-white/[0.09] bg-[#111116] shadow-2xl shadow-black/60"
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
                İstifadəçini şikayət et
              </h2>
              <p className="mt-1 truncate text-sm text-zinc-500">
                @{reportedUserName} haqqında şikayət göndərilir
              </p>
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

        <div className="space-y-5 px-6 py-6">
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
                Açıqlama və sübut
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
              placeholder="Nə baş verdiyini, tarix və kontekst kimi yoxlanıla bilən detalları yaz..."
              className="w-full resize-none rounded-2xl border border-white/[0.09] bg-white/[0.035] px-4 py-3 text-sm leading-6 text-zinc-200 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/40 focus:ring-2 focus:ring-violet-500/10 disabled:opacity-60"
            />
          </div>

          <div>
            <p className="mb-2 text-xs font-bold uppercase tracking-[0.14em] text-zinc-500">
              Şəkil sübutu
            </p>

            {imagePreviewUrl ? (
              <div className="overflow-hidden rounded-2xl border border-white/[0.09] bg-black/20">
                <img
                  src={imagePreviewUrl}
                  alt="Şikayət sübutunun önizləməsi"
                  className="max-h-64 w-full object-contain"
                />
                <div className="flex items-center justify-between gap-3 border-t border-white/[0.07] px-4 py-3">
                  <span className="flex min-w-0 items-center gap-2 text-xs text-zinc-500">
                    <FileImage size={15} />
                    <span className="truncate">
                      {evidenceImage?.name}
                    </span>
                  </span>
                  <button
                    type="button"
                    onClick={() => setEvidenceImage(null)}
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

        <div className="flex flex-col-reverse gap-3 border-t border-white/[0.07] px-6 py-5 sm:flex-row sm:justify-end">
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
    </div>
  );
}
