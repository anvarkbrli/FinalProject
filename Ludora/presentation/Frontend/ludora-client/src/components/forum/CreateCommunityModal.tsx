import {
  ArrowLeft,
  ArrowRight,
  Bug,
  Code2,
  Cpu,
  Gem,
  Globe2,
  ImagePlus,
  LoaderCircle,
  LockKeyhole,
  Newspaper,
  Rocket,
  Shapes,
  ShieldCheck,
  TimerReset,
  Trophy,
  Upload,
  X,
  type LucideIcon,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { getApiErrorMessage } from "../../lib/api";
import { createForumCommunity } from "../../services/forumCommunityService";
import type {
  ForumCommunityCategory,
  ForumCommunityDTO,
  ForumCommunityVisibility,
} from "../../types/community";
import "./CreateCommunityModal.css";

interface CreateCommunityModalProps {
  open: boolean;
  onClose: () => void;
  onCreated: (community: ForumCommunityDTO) => void;
}

interface CategoryOption {
  value: ForumCommunityCategory;
  label: string;
  description: string;
  icon: LucideIcon;
  accent: string;
}

const categoryOptions: CategoryOption[] = [
  {
    value: "GameNews",
    label: "Oyun xəbərləri",
    description: "Yeni elanlar, yeniləmələr və sənaye xəbərləri",
    icon: Newspaper,
    accent: "text-sky-200 bg-sky-400/10",
  },
  {
    value: "BugsAndSolutions",
    label: "Buglar və həlləri",
    description: "Texniki problemlər, düzəlişlər və praktik həllər",
    icon: Bug,
    accent: "text-rose-200 bg-rose-400/10",
  },
  {
    value: "Speedrunning",
    label: "Speedrunning",
    description: "Rekord cəhdləri, marşrutlar və strategiyalar",
    icon: TimerReset,
    accent: "text-amber-200 bg-amber-400/10",
  },
  {
    value: "HardwareAndGamingSetup",
    label: "Hardware və gaming setup",
    description: "Komponentlər, periferiyalar və oyun məkanları",
    icon: Cpu,
    accent: "text-cyan-200 bg-cyan-400/10",
  },
  {
    value: "IndieGames",
    label: "Indie oyunlar",
    description: "Müstəqil studiyalar və gizli qalmış oyunlar",
    icon: Gem,
    accent: "text-violet-200 bg-violet-400/10",
  },
  {
    value: "ModdingAndGameDevelopment",
    label: "Modding və oyun inkişafı",
    description: "Modlar, alətlər, kod və yaradıcı inkişaf prosesi",
    icon: Code2,
    accent: "text-emerald-200 bg-emerald-400/10",
  },
  {
    value: "Esports",
    label: "Esports",
    description: "Turnirlər, komandalar və rəqabətli səhnə",
    icon: Trophy,
    accent: "text-orange-200 bg-orange-400/10",
  },
  {
    value: "Other",
    label: "Digər",
    description: "Digər oyun mövzuları üçün sərbəst icma",
    icon: Shapes,
    accent: "text-zinc-200 bg-white/[0.07]",
  },
];

const visibilityOptions: Array<{
  value: ForumCommunityVisibility;
  label: string;
  description: string;
  icon: LucideIcon;
}> = [
  {
    value: "Open",
    label: "Açıq",
    description:
      "Hər kəs icmanı görə və heç bir təsdiq gözləmədən rahatlıqla qoşula bilər.",
    icon: Globe2,
  },
  {
    value: "Restricted",
    label: "Məhdud",
    description:
      "Hər kəs icmanı görə bilər, amma qoşulmaq üçün icma sahibinin təsdiqi lazımdır.",
    icon: ShieldCheck,
  },
  {
    value: "Private",
    label: "Gizli",
    description:
      "Yalnız üzvlər icmanı görə bilər. Yeni üzvlər yalnız sahibin dəvəti ilə qəbul olunur.",
    icon: LockKeyhole,
  },
];

export function CreateCommunityModal({
  open,
  onClose,
  onCreated,
}: CreateCommunityModalProps) {
  const [step, setStep] = useState(0);
  const [category, setCategory] =
    useState<ForumCommunityCategory | null>(null);
  const [visibility, setVisibility] =
    useState<ForumCommunityVisibility | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [icon, setIcon] = useState<File | null>(null);
  const [coverImage, setCoverImage] = useState<File | null>(null);
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const iconPreview = useMemo(
    () => (icon ? URL.createObjectURL(icon) : null),
    [icon],
  );
  const coverPreview = useMemo(
    () => (coverImage ? URL.createObjectURL(coverImage) : null),
    [coverImage],
  );

  useEffect(() => {
    return () => {
      if (iconPreview) URL.revokeObjectURL(iconPreview);
      if (coverPreview) URL.revokeObjectURL(coverPreview);
    };
  }, [coverPreview, iconPreview]);

  useEffect(() => {
    if (!open) return;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = previousOverflow;
    };
  }, [open]);

  if (!open) return null;

  function resetAndClose() {
    if (isSubmitting) return;
    setStep(0);
    setCategory(null);
    setVisibility(null);
    setName("");
    setDescription("");
    setIcon(null);
    setCoverImage(null);
    setError("");
    onClose();
  }

  function pickImage(
    file: File | undefined,
    setter: (value: File | null) => void,
    maxSizeMb: number,
  ) {
    if (!file) return;
    if (!["image/jpeg", "image/png", "image/webp"].includes(file.type)) {
      setError("Yalnız JPEG, PNG və WEBP şəkilləri qəbul edilir.");
      return;
    }
    if (file.size > maxSizeMb * 1024 * 1024) {
      setError(`Şəkil maksimum ${maxSizeMb} MB ola bilər.`);
      return;
    }
    setError("");
    setter(file);
  }

  function nextStep() {
    if (step === 0 && !category) {
      setError("Davam etmək üçün icma kateqoriyası seçin.");
      return;
    }
    if (step === 1 && !visibility) {
      setError("Davam etmək üçün icmanın görünürlüyünü seçin.");
      return;
    }
    setError("");
    setStep((current) => Math.min(2, current + 1));
  }

  async function submit() {
    if (!category || !visibility) return;
    if (name.trim().length < 3) {
      setError("İcma adı ən azı 3 simvol olmalıdır.");
      return;
    }
    if (description.trim().length < 10) {
      setError("İcma açıqlaması ən azı 10 simvol olmalıdır.");
      return;
    }

    setError("");
    setIsSubmitting(true);
    try {
      const community = await createForumCommunity({
        name: name.trim(),
        description: description.trim(),
        category,
        visibility,
        icon,
        coverImage,
      });
      onCreated(community);
      setStep(0);
      setCategory(null);
      setVisibility(null);
      setName("");
      setDescription("");
      setIcon(null);
      setCoverImage(null);
    } catch (submitError) {
      setError(
        getApiErrorMessage(
          submitError,
          "İcmanı yaratmaq mümkün olmadı.",
        ),
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-[95] flex items-end justify-center bg-black/80 backdrop-blur-md sm:items-center sm:p-5"
      role="dialog"
      aria-modal="true"
      aria-labelledby="create-community-title"
    >
      <button
        type="button"
        className="absolute inset-0"
        onClick={resetAndClose}
        aria-label="İcma yaratma pəncərəsini bağla"
      />

      <section className="relative flex max-h-[96vh] w-full max-w-5xl flex-col overflow-hidden rounded-t-[30px] border border-teal-200/15 bg-[#091312] shadow-2xl shadow-black/70 sm:rounded-[30px]">
        <header className="flex items-start justify-between gap-5 border-b border-white/[0.07] px-5 py-5 sm:px-7">
          <div>
            <p className="text-[10px] font-black uppercase tracking-[0.22em] text-teal-300">
              Yeni icma · Addım {step + 1}/3
            </p>
            <h2
              id="create-community-title"
              className="mt-2 text-xl font-bold text-white sm:text-2xl"
            >
              {step === 0
                ? "İcmanın mövzusunu seç"
                : step === 1
                  ? "Kimlər görə və qoşula bilər?"
                  : "İcmanın görünüşünü tamamla"}
            </h2>
          </div>
          <button
            type="button"
            onClick={resetAndClose}
            disabled={isSubmitting}
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border border-white/10 text-zinc-500 transition hover:text-white disabled:opacity-50"
            aria-label="Bağla"
          >
            <X size={18} />
          </button>
        </header>

        <div className="flex items-center gap-2 px-5 pt-5 sm:px-7">
          {[0, 1, 2].map((index) => (
            <span
              key={index}
              className={`h-2 rounded-full transition-all duration-300 ${
                index === step
                  ? "w-9 bg-teal-300"
                  : index < step
                    ? "w-4 bg-teal-300/45"
                    : "w-4 bg-white/15"
              }`}
            />
          ))}
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-6 sm:px-7">
          <div key={step} className="community-step-enter">
            {step === 0 && (
              <div className="grid gap-3 sm:grid-cols-2">
                {categoryOptions.map((option) => {
                  const Icon = option.icon;
                  const selected = category === option.value;
                  return (
                    <button
                      key={option.value}
                      type="button"
                      onClick={() => {
                        setCategory(option.value);
                        setError("");
                      }}
                      className={`flex items-start gap-4 rounded-2xl border p-4 text-left transition ${
                        selected
                          ? "border-teal-300/40 bg-teal-300/[0.09] shadow-lg shadow-teal-950/20"
                          : "border-white/[0.08] bg-white/[0.025] hover:border-white/15 hover:bg-white/[0.04]"
                      }`}
                    >
                      <span
                        className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl ${option.accent}`}
                      >
                        <Icon size={20} />
                      </span>
                      <span>
                        <span className="block text-sm font-bold text-zinc-100">
                          {option.label}
                        </span>
                        <span className="mt-1 block text-xs leading-5 text-zinc-600">
                          {option.description}
                        </span>
                      </span>
                    </button>
                  );
                })}
              </div>
            )}

            {step === 1 && (
              <div className="grid gap-4">
                {visibilityOptions.map((option) => {
                  const Icon = option.icon;
                  const selected = visibility === option.value;
                  return (
                    <button
                      key={option.value}
                      type="button"
                      onClick={() => {
                        setVisibility(option.value);
                        setError("");
                      }}
                      className={`flex items-start gap-4 rounded-2xl border p-5 text-left transition ${
                        selected
                          ? "border-teal-300/40 bg-teal-300/[0.09]"
                          : "border-white/[0.08] bg-white/[0.025] hover:border-white/15"
                      }`}
                    >
                      <span
                        className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl ${
                          selected
                            ? "bg-teal-300/15 text-teal-200"
                            : "bg-white/[0.05] text-zinc-500"
                        }`}
                      >
                        <Icon size={22} />
                      </span>
                      <span>
                        <span className="text-base font-bold text-white">
                          {option.label}
                        </span>
                        <span className="mt-1.5 block max-w-2xl text-sm leading-6 text-zinc-500">
                          {option.description}
                        </span>
                      </span>
                    </button>
                  );
                })}
              </div>
            )}

            {step === 2 && (
              <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_360px]">
                <div className="space-y-5">
                  <label className="block">
                    <span className="flex items-center justify-between text-xs font-bold text-zinc-300">
                      İcmanın adı <span className="text-zinc-600">{name.length}/60</span>
                    </span>
                    <input
                      value={name}
                      onChange={(event) => setName(event.target.value)}
                      maxLength={60}
                      placeholder="Məsələn: Azərbaycan Speedrunnerləri"
                      className="mt-2 h-12 w-full rounded-2xl border border-white/[0.09] bg-white/[0.035] px-4 text-sm text-white outline-none transition focus:border-teal-300/35"
                      autoFocus
                    />
                  </label>

                  <label className="block">
                    <span className="flex items-center justify-between text-xs font-bold text-zinc-300">
                      Açıqlama
                      <span className="text-zinc-600">
                        {description.length}/1000
                      </span>
                    </span>
                    <textarea
                      value={description}
                      onChange={(event) => setDescription(event.target.value)}
                      maxLength={1000}
                      rows={6}
                      placeholder="İcmanın məqsədini və burada nələrin paylaşılacağını yaz..."
                      className="mt-2 w-full resize-y rounded-2xl border border-white/[0.09] bg-white/[0.035] p-4 text-sm leading-6 text-white outline-none transition focus:border-teal-300/35"
                    />
                  </label>

                  <div className="grid gap-3 sm:grid-cols-2">
                    <label className="flex cursor-pointer items-center gap-3 rounded-2xl border border-dashed border-white/10 bg-white/[0.02] p-4 transition hover:border-teal-300/25">
                      <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-teal-300/10 text-teal-200">
                        <ImagePlus size={18} />
                      </span>
                      <span>
                        <span className="block text-xs font-bold text-zinc-300">
                          İcma şəkli
                        </span>
                        <span className="mt-1 block text-[11px] text-zinc-600">
                          Kvadrat · maksimum 5 MB
                        </span>
                      </span>
                      <input
                        type="file"
                        accept="image/jpeg,image/png,image/webp"
                        className="sr-only"
                        onChange={(event) => {
                          pickImage(event.target.files?.[0], setIcon, 5);
                          event.target.value = "";
                        }}
                      />
                    </label>

                    <label className="flex cursor-pointer items-center gap-3 rounded-2xl border border-dashed border-white/10 bg-white/[0.02] p-4 transition hover:border-teal-300/25">
                      <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-violet-400/10 text-violet-200">
                        <Upload size={18} />
                      </span>
                      <span>
                        <span className="block text-xs font-bold text-zinc-300">
                          Arxa fon şəkli
                        </span>
                        <span className="mt-1 block text-[11px] text-zinc-600">
                          Geniş format · maksimum 10 MB
                        </span>
                      </span>
                      <input
                        type="file"
                        accept="image/jpeg,image/png,image/webp"
                        className="sr-only"
                        onChange={(event) => {
                          pickImage(
                            event.target.files?.[0],
                            setCoverImage,
                            10,
                          );
                          event.target.value = "";
                        }}
                      />
                    </label>
                  </div>
                </div>

                <aside>
                  <p className="mb-2 text-[10px] font-black uppercase tracking-[0.18em] text-zinc-600">
                    Canlı önizləmə
                  </p>
                  <div className="overflow-hidden rounded-[24px] border border-white/[0.09] bg-[#101a19] shadow-xl shadow-black/30">
                    <div
                      className="h-32 bg-gradient-to-br from-teal-500/20 via-[#172221] to-violet-500/15 bg-cover bg-center"
                      style={
                        coverPreview
                          ? { backgroundImage: `url("${coverPreview}")` }
                          : undefined
                      }
                    />
                    <div className="relative px-5 pb-5 pt-11">
                      <div className="absolute -top-8 left-5 flex h-16 w-16 items-center justify-center overflow-hidden rounded-[20px] border-4 border-[#101a19] bg-teal-300 text-xl font-black text-[#07100f]">
                        {iconPreview ? (
                          <img
                            src={iconPreview}
                            alt=""
                            className="h-full w-full object-cover"
                          />
                        ) : (
                          (name.trim().charAt(0) || "L").toUpperCase()
                        )}
                      </div>
                      <h3 className="truncate text-lg font-black text-white">
                        {name.trim() || "İcmanın adı"}
                      </h3>
                      <p className="mt-1 text-[11px] text-zinc-600">
                        1 üzv · yeni yaradılır
                      </p>
                      <p className="mt-4 line-clamp-4 text-sm leading-6 text-zinc-400">
                        {description.trim() ||
                          "İcmanın açıqlaması yazıldıqca burada görünəcək."}
                      </p>
                    </div>
                  </div>
                </aside>
              </div>
            )}
          </div>

          {error && (
            <p className="mt-5 rounded-2xl border border-rose-400/15 bg-rose-400/[0.06] px-4 py-3 text-sm text-rose-200">
              {error}
            </p>
          )}
        </div>

        <footer className="flex shrink-0 items-center justify-between gap-3 border-t border-white/[0.07] px-5 py-4 sm:px-7">
          <button
            type="button"
            onClick={() => {
              if (step === 0) resetAndClose();
              else {
                setError("");
                setStep((current) => current - 1);
              }
            }}
            disabled={isSubmitting}
            className="flex h-11 items-center gap-2 rounded-xl px-3 text-sm font-bold text-zinc-500 transition hover:text-white disabled:opacity-50"
          >
            <ArrowLeft size={17} />
            {step === 0 ? "Ləğv et" : "Geri"}
          </button>

          {step < 2 ? (
            <button
              type="button"
              onClick={nextStep}
              className="flex h-11 items-center gap-2 rounded-xl bg-teal-300 px-5 text-sm font-black text-[#07100f] transition hover:bg-teal-200"
            >
              Davam et
              <ArrowRight size={17} />
            </button>
          ) : (
            <button
              type="button"
              onClick={submit}
              disabled={isSubmitting}
              className="flex h-11 items-center gap-2 rounded-xl bg-teal-300 px-5 text-sm font-black text-[#07100f] transition hover:bg-teal-200 disabled:opacity-50"
            >
              {isSubmitting ? (
                <LoaderCircle size={17} className="animate-spin" />
              ) : (
                <Rocket size={17} />
              )}
              İcmanı yarat
            </button>
          )}
        </footer>
      </section>
    </div>
  );
}
