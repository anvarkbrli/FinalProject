import {
  Check,
  ListChecks,
  ListOrdered,
  LoaderCircle,
  LockKeyhole,
  Users,
  X,
} from "lucide-react";
import { useState } from "react";
import type { ListVisibility } from "../../types/profile";

export interface GameListEditorValues {
  name: string;
  description: string | null;
  visibility: ListVisibility;
  isRanked: boolean;
}

interface GameListEditorModalProps {
  open: boolean;
  title: string;
  submitLabel: string;
  initialValues?: GameListEditorValues;
  isPending: boolean;
  errorMessage?: string | null;
  onClose: () => void;
  onSubmit: (values: GameListEditorValues) => void;
}

const defaultValues: GameListEditorValues = {
  name: "",
  description: null,
  visibility: "Public",
  isRanked: false,
};

const visibilityOptions: Array<{
  value: ListVisibility;
  label: string;
  description: string;
  icon: typeof Users;
}> = [
  {
    value: "Public",
    label: "Hamı üçün",
    description: "Siyahını bütün istifadəçilər görə bilər.",
    icon: Users,
  },
  {
    value: "FollowersOnly",
    label: "Yalnız izləyicilər",
    description: "Siyahını yalnız qəbul etdiyin izləyicilər görür.",
    icon: Check,
  },
  {
    value: "Private",
    label: "Gizli",
    description: "Siyahını yalnız sən görə bilərsən.",
    icon: LockKeyhole,
  },
];

export function GameListEditorModal({
  open,
  title,
  submitLabel,
  initialValues,
  isPending,
  errorMessage,
  onClose,
  onSubmit,
}: GameListEditorModalProps) {
  const values = initialValues ?? defaultValues;
  const [name, setName] = useState(values.name);
  const [description, setDescription] = useState(
    values.description ?? "",
  );
  const [visibility, setVisibility] =
    useState<ListVisibility>(values.visibility);
  const [isRanked, setIsRanked] = useState(values.isRanked);
  const [localError, setLocalError] = useState<string | null>(
    null,
  );


  if (!open) {
    return null;
  }

  function handleSubmit() {
    const normalizedName = name.trim();
    const normalizedDescription = description.trim();

    if (!normalizedName) {
      setLocalError("Siyahının adını daxil et.");
      return;
    }

    onSubmit({
      name: normalizedName,
      description: normalizedDescription || null,
      visibility,
      isRanked,
    });
  }

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/75 px-4 py-8 backdrop-blur-sm">
      <div className="max-h-full w-full max-w-2xl overflow-y-auto rounded-3xl border border-white/[0.09] bg-[#101015] shadow-2xl shadow-black/60">
        <div className="flex items-start justify-between border-b border-white/[0.07] px-5 py-5 sm:px-7">
          <div>
            <div className="flex items-center gap-2 text-violet-300">
              <ListChecks size={20} />
              <h2 className="text-lg font-black text-white">
                {title}
              </h2>
            </div>
            <p className="mt-2 text-sm leading-6 text-zinc-600">
              Siyahının növünü və kimlərin görə biləcəyini seç.
            </p>
          </div>

          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl text-zinc-600 transition hover:bg-white/[0.06] hover:text-white disabled:opacity-50"
            aria-label="Pəncərəni bağla"
          >
            <X size={19} />
          </button>
        </div>

        <div className="space-y-6 p-5 sm:p-7">
          {(localError || errorMessage) && (
            <div className="rounded-2xl border border-red-400/15 bg-red-500/[0.07] px-4 py-3 text-sm text-red-200">
              {localError ?? errorMessage}
            </div>
          )}

          <label className="block">
            <span className="mb-2 block text-xs font-bold uppercase tracking-[0.14em] text-zinc-500">
              Siyahının adı
            </span>
            <input
              value={name}
              onChange={(event) => {
                setName(event.target.value);
                setLocalError(null);
              }}
              maxLength={100}
              placeholder="Məsələn: Bütün zamanların ən yaxşı 10 oyunu"
              className="h-12 w-full rounded-2xl border border-white/[0.08] bg-white/[0.035] px-4 text-sm text-zinc-100 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/45 focus:ring-4 focus:ring-violet-500/10"
            />
          </label>

          <label className="block">
            <span className="mb-2 block text-xs font-bold uppercase tracking-[0.14em] text-zinc-500">
              Açıqlama
            </span>
            <textarea
              value={description}
              onChange={(event) => {
                setDescription(event.target.value);
              }}
              rows={4}
              maxLength={1000}
              placeholder="Siyahının mövzusunu qısa şəkildə izah et."
              className="w-full resize-none rounded-2xl border border-white/[0.08] bg-white/[0.035] px-4 py-3 text-sm leading-7 text-zinc-100 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/45 focus:ring-4 focus:ring-violet-500/10"
            />
            <span className="mt-2 block text-right text-xs text-zinc-700">
              {description.length}/1000
            </span>
          </label>

          <div>
            <span className="mb-3 block text-xs font-bold uppercase tracking-[0.14em] text-zinc-500">
              Siyahının növü
            </span>
            <div className="grid gap-3 sm:grid-cols-2">
              <button
                type="button"
                onClick={() => {
                  setIsRanked(false);
                }}
                className={`rounded-2xl border p-4 text-left transition ${
                  !isRanked
                    ? "border-violet-400/35 bg-violet-500/[0.09]"
                    : "border-white/[0.07] bg-white/[0.025] hover:bg-white/[0.045]"
                }`}
              >
                <ListChecks
                  size={20}
                  className={
                    !isRanked
                      ? "text-violet-300"
                      : "text-zinc-600"
                  }
                />
                <span className="mt-3 block text-sm font-bold text-zinc-100">
                  Sırasız siyahı
                </span>
                <span className="mt-1 block text-xs leading-5 text-zinc-600">
                  Oyunlar sadəcə kolleksiya kimi saxlanılır.
                </span>
              </button>

              <button
                type="button"
                onClick={() => {
                  setIsRanked(true);
                }}
                className={`rounded-2xl border p-4 text-left transition ${
                  isRanked
                    ? "border-amber-300/35 bg-amber-500/[0.08]"
                    : "border-white/[0.07] bg-white/[0.025] hover:bg-white/[0.045]"
                }`}
              >
                <ListOrdered
                  size={20}
                  className={
                    isRanked
                      ? "text-amber-300"
                      : "text-zinc-600"
                  }
                />
                <span className="mt-3 block text-sm font-bold text-zinc-100">
                  Ranklanmış siyahı
                </span>
                <span className="mt-1 block text-xs leading-5 text-zinc-600">
                  İlk əlavə edilən oyun 1-ci, sonrakılar 2-ci, 3-cü olur.
                </span>
              </button>
            </div>
          </div>

          <div>
            <span className="mb-3 block text-xs font-bold uppercase tracking-[0.14em] text-zinc-500">
              Görünürlük
            </span>
            <div className="space-y-2">
              {visibilityOptions.map((option) => {
                const Icon = option.icon;
                const isSelected = visibility === option.value;

                return (
                  <button
                    key={option.value}
                    type="button"
                    onClick={() => {
                      setVisibility(option.value);
                    }}
                    className={`flex w-full items-center gap-4 rounded-2xl border px-4 py-3 text-left transition ${
                      isSelected
                        ? "border-violet-400/30 bg-violet-500/[0.08]"
                        : "border-white/[0.07] bg-white/[0.02] hover:bg-white/[0.04]"
                    }`}
                  >
                    <span
                      className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${
                        isSelected
                          ? "bg-violet-500/15 text-violet-300"
                          : "bg-white/[0.04] text-zinc-600"
                      }`}
                    >
                      <Icon size={18} />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block text-sm font-bold text-zinc-100">
                        {option.label}
                      </span>
                      <span className="mt-1 block text-xs leading-5 text-zinc-600">
                        {option.description}
                      </span>
                    </span>
                    <span
                      className={`h-4 w-4 rounded-full border-4 ${
                        isSelected
                          ? "border-violet-400 bg-white"
                          : "border-zinc-700"
                      }`}
                    />
                  </button>
                );
              })}
            </div>
          </div>
        </div>

        <div className="flex justify-end gap-3 border-t border-white/[0.07] px-5 py-5 sm:px-7">
          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            className="h-11 rounded-xl px-5 text-sm font-semibold text-zinc-500 transition hover:bg-white/[0.05] hover:text-zinc-200 disabled:opacity-50"
          >
            Ləğv et
          </button>
          <button
            type="button"
            onClick={handleSubmit}
            disabled={isPending}
            className="flex h-11 items-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-bold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {isPending && (
              <LoaderCircle size={16} className="animate-spin" />
            )}
            {submitLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
