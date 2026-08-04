import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import axios from "axios";
import {
  AlertCircle,
  Cpu,
  Gauge,
  LoaderCircle,
  LogIn,
  Save,
  Sparkles,
  X,
} from "lucide-react";
import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
  type ReactNode,
} from "react";
import { createPortal } from "react-dom";
import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/useI18n";
import {
  estimateFps,
  getFpsEstimatorError,
  getFpsHardwareProfile,
} from "../../services/fpsEstimatorService";
import type {
  FpsEstimateRequest,
  FpsEstimateResponse,
  FpsEstimatorError,
  FpsFormField,
  FpsGraphicsPreset,
  FpsHardwareProfile,
  FpsResolution,
  FpsUpscaler,
  FpsUpscalerMode,
} from "../../types/fpsEstimator";
import { FpsEstimatorResult } from "./FpsEstimatorResult";

interface FpsEstimatorModalProps {
  isOpen: boolean;
  isAuthenticated: boolean;
  gameId: number;
  gameName: string;
  gameReleaseYear?: number;
  onClose: () => void;
}

interface FpsFormValues {
  gpu: string;
  cpu: string;
  ramGb: string;
  vramGb: string;
  resolution: FpsResolution;
  graphicsPreset: FpsGraphicsPreset;
  rayTracingEnabled: boolean;
  upscaler: FpsUpscaler;
  upscalerMode: FpsUpscalerMode;
  frameGenerationEnabled: boolean;
  additionalNotes: string;
}

const resolutionOptions: Array<{
  value: FpsResolution;
  label: string;
}> = [
  {
    value: "1280x720",
    label: "720p — 1280×720",
  },
  {
    value: "1920x1080",
    label: "1080p — 1920×1080",
  },
  {
    value: "2560x1440",
    label: "1440p — 2560×1440",
  },
  {
    value: "3440x1440",
    label: "Ultrawide — 3440×1440",
  },
  {
    value: "3840x2160",
    label: "4K — 3840×2160",
  },
];

const presetOptions: Array<{
  value: FpsGraphicsPreset;
  label: string;
}> = [
  { value: "Low", label: "Aşağı" },
  { value: "Medium", label: "Orta" },
  { value: "High", label: "Yüksək" },
  { value: "Ultra", label: "Ultra" },
  { value: "Custom", label: "Xüsusi" },
];

const upscalerOptions: Array<{
  value: FpsUpscaler;
  label: string;
}> = [
  {
    value: "None",
    label: "İstifadə edilmir",
  },
  { value: "DLSS", label: "DLSS" },
  { value: "FSR", label: "FSR" },
  { value: "XeSS", label: "XeSS" },
];

const upscalerModeOptions: FpsUpscalerMode[] = [
  "Native",
  "Quality",
  "Balanced",
  "Performance",
  "Ultra Performance",
];

const initialValues: FpsFormValues = {
  gpu: "",
  cpu: "",
  ramGb: "16",
  vramGb: "",
  resolution: "1920x1080",
  graphicsPreset: "High",
  rayTracingEnabled: false,
  upscaler: "None",
  upscalerMode: "Native",
  frameGenerationEnabled: false,
  additionalNotes: "",
};

const inputBaseClass =
  "mt-2 h-12 w-full rounded-2xl border bg-[var(--wp-surface-soft)] px-4 text-sm text-[var(--wp-text-strong)] outline-none transition placeholder:text-[var(--wp-text-muted)] focus:ring-2 disabled:cursor-not-allowed disabled:opacity-60";

function getInputClass(hasError: boolean) {
  return `${inputBaseClass} ${
    hasError
      ? "border-red-400/45 focus:border-red-400 focus:ring-red-400/10"
      : "border-[var(--wp-border)] focus:border-violet-400/50 focus:ring-violet-400/10"
  }`;
}

function FieldError({
  children,
  id,
}: {
  children?: ReactNode;
  id?: string;
}) {
  if (!children) {
    return null;
  }

  return (
    <p
      id={id}
      className="mt-2 text-xs leading-5 text-red-300"
    >
      {children}
    </p>
  );
}

function ToggleField({
  id,
  label,
  description,
  checked,
  disabled,
  onChange,
}: {
  id: string;
  label: string;
  description: string;
  checked: boolean;
  disabled: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <div className="flex items-center justify-between gap-4 rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] p-4">
      <div className="min-w-0">
        <label
          htmlFor={id}
          className="text-sm font-bold text-[var(--wp-text-strong)]"
        >
          {label}
        </label>
        <p className="mt-1 text-xs leading-5 text-[var(--wp-text-muted)]">
          {description}
        </p>
      </div>

      <button
        id={id}
        type="button"
        role="switch"
        aria-checked={checked}
        disabled={disabled}
        onClick={() => onChange(!checked)}
        className={`relative h-7 w-12 shrink-0 rounded-full border transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400/60 disabled:cursor-not-allowed disabled:opacity-50 ${
          checked
            ? "border-violet-400/50 bg-violet-500"
            : "border-[var(--wp-border-strong)] bg-black/15"
        }`}
      >
        <span
          className={`absolute left-0 top-1 h-[18px] w-[18px] rounded-full bg-white shadow-sm transition-transform ${
            checked
              ? "translate-x-[25px]"
              : "translate-x-1"
          }`}
        />
        <span className="sr-only">
          {checked ? "Aktivdir" : "Deaktivdir"}
        </span>
      </button>
    </div>
  );
}

export function FpsEstimatorModal({
  isOpen,
  isAuthenticated,
  gameId,
  gameName,
  gameReleaseYear,
  onClose,
}: FpsEstimatorModalProps) {
  const { language } = useI18n();
  const queryClient = useQueryClient();
  const modalRef = useRef<HTMLFormElement>(null);
  const gpuInputRef = useRef<HTMLInputElement>(null);
  const abortControllerRef =
    useRef<AbortController | null>(null);
  const isLoadingRef = useRef(false);
  const hydratedForOpenRef = useRef(false);
  const hardwareEditedRef = useRef(false);

  const [values, setValues] =
    useState<FpsFormValues>(initialValues);
  const [fieldErrors, setFieldErrors] = useState<
    Partial<Record<FpsFormField, string>>
  >({});
  const [apiError, setApiError] =
    useState<FpsEstimatorError | null>(null);
  const [result, setResult] =
    useState<FpsEstimateResponse | null>(null);

  const hardwareQuery = useQuery({
    queryKey: ["fps-hardware-profile"],
    queryFn: ({ signal }) =>
      getFpsHardwareProfile(signal),
    enabled: isOpen && isAuthenticated,
    staleTime: 5 * 60 * 1000,
    retry: false,
  });

  const estimateMutation = useMutation({
    mutationFn: async (
      request: FpsEstimateRequest,
    ) => {
      abortControllerRef.current?.abort();
      const controller = new AbortController();
      abortControllerRef.current = controller;

      return estimateFps(
        request,
        controller.signal,
      );
    },
    onSuccess: (response, request) => {
      setResult(response);
      setApiError(null);
      setFieldErrors({});

      queryClient.setQueryData<FpsHardwareProfile>(
        ["fps-hardware-profile"],
        {
          isConfigured: true,
          gpu: request.gpu,
          cpu: request.cpu,
          ramGb: request.ramGb,
          vramGb: request.vramGb ?? null,
          updatedAtUtc: new Date().toISOString(),
        },
      );
    },
    onError: (error) => {
      if (axios.isCancel(error)) {
        return;
      }

      const normalizedError =
        getFpsEstimatorError(error);
      setApiError(normalizedError);
      setFieldErrors((current) => ({
        ...current,
        ...normalizedError.fieldErrors,
      }));
    },
    onSettled: () => {
      abortControllerRef.current = null;
    },
  });

  const isLoading = estimateMutation.isPending;

  useEffect(() => {
    isLoadingRef.current = isLoading;
  }, [isLoading]);

  const requestClose = useCallback(() => {
    if (!isLoadingRef.current) {
      onClose();
    }
  }, [onClose]);

  useEffect(() => {
    if (!isOpen) {
      hydratedForOpenRef.current = false;
      hardwareEditedRef.current = false;
      return;
    }

    const previousOverflow =
      document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const focusTimer = window.setTimeout(() => {
      if (isAuthenticated) {
        gpuInputRef.current?.focus();
      } else {
        modalRef.current
          ?.querySelector<HTMLElement>(
            'a[href="/login"]',
          )
          ?.focus();
      }
    }, 0);

    const handleKeyDown = (
      event: KeyboardEvent,
    ) => {
      if (event.key === "Escape") {
        event.preventDefault();
        requestClose();
        return;
      }

      if (event.key !== "Tab") {
        return;
      }

      const focusableElements = Array.from(
        modalRef.current?.querySelectorAll<HTMLElement>(
          'button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
        ) ?? [],
      );

      if (focusableElements.length === 0) {
        return;
      }

      const firstElement = focusableElements[0];
      const lastElement =
        focusableElements[
          focusableElements.length - 1
        ];

      if (
        event.shiftKey &&
        document.activeElement === firstElement
      ) {
        event.preventDefault();
        lastElement.focus();
      } else if (
        !event.shiftKey &&
        document.activeElement === lastElement
      ) {
        event.preventDefault();
        firstElement.focus();
      }
    };

    document.addEventListener(
      "keydown",
      handleKeyDown,
    );

    return () => {
      window.clearTimeout(focusTimer);
      document.body.style.overflow =
        previousOverflow;
      document.removeEventListener(
        "keydown",
        handleKeyDown,
      );
    };
  }, [
    isAuthenticated,
    isOpen,
    requestClose,
  ]);

  useEffect(
    () => () => {
      abortControllerRef.current?.abort();
    },
    [],
  );

  useEffect(() => {
    if (
      !isOpen ||
      hydratedForOpenRef.current ||
      hardwareEditedRef.current ||
      !hardwareQuery.data?.isConfigured
    ) {
      return;
    }

    const profile = hardwareQuery.data;
    hydratedForOpenRef.current = true;
    setValues((current) => ({
      ...current,
      gpu: profile.gpu ?? current.gpu,
      cpu: profile.cpu ?? current.cpu,
      ramGb:
        profile.ramGb?.toString() ??
        current.ramGb,
      vramGb:
        profile.vramGb?.toString() ?? "",
    }));
  }, [
    hardwareQuery.data,
    isOpen,
  ]);

  function updateField<Field extends keyof FpsFormValues>(
    field: Field,
    value: FpsFormValues[Field],
  ) {
    setValues((current) => ({
      ...current,
      [field]: value,
    }));
    setFieldErrors((current) => {
      if (!(field in current)) {
        return current;
      }

      const next = { ...current };
      delete next[field as FpsFormField];
      return next;
    });
    setApiError(null);

    if (
      field === "gpu" ||
      field === "cpu" ||
      field === "ramGb" ||
      field === "vramGb"
    ) {
      hardwareEditedRef.current = true;
    }
  }

  function buildRequest():
    | FpsEstimateRequest
    | null {
    const nextErrors: Partial<
      Record<FpsFormField, string>
    > = {};
    const gpu = values.gpu.trim();
    const cpu = values.cpu.trim();
    const notes = values.additionalNotes.trim();
    const ramGb = Number(values.ramGb);
    const vramGb = values.vramGb.trim()
      ? Number(values.vramGb)
      : undefined;

    if (!gpu) {
      nextErrors.gpu = "GPU boş ola bilməz.";
    }

    if (!cpu) {
      nextErrors.cpu = "CPU boş ola bilməz.";
    }

    if (
      !Number.isInteger(ramGb) ||
      ramGb < 1 ||
      ramGb > 512
    ) {
      nextErrors.ramGb =
        "RAM 1–512 GB aralığında olmalıdır.";
    }

    if (
      vramGb !== undefined &&
      (!Number.isInteger(vramGb) ||
        vramGb < 1 ||
        vramGb > 128)
    ) {
      nextErrors.vramGb =
        "VRAM 1–128 GB aralığında olmalıdır.";
    }

    if (!values.resolution) {
      nextErrors.resolution =
        "Görüntü ölçüsü seçilməlidir.";
    }

    if (!values.graphicsPreset) {
      nextErrors.graphicsPreset =
        "Qrafika səviyyəsi seçilməlidir.";
    }

    if (notes.length > 500) {
      nextErrors.additionalNotes =
        "Əlavə qeyd maksimum 500 simvol ola bilər.";
    }

    setFieldErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      window.setTimeout(() => {
        modalRef.current
          ?.querySelector<HTMLElement>(
            '[aria-invalid="true"]',
          )
          ?.focus();
      }, 0);
      return null;
    }

    setValues((current) => ({
      ...current,
      gpu,
      cpu,
      additionalNotes: notes,
    }));

    return {
      language,
      gameId,
      gameName,
      gameReleaseYear,
      gpu,
      cpu,
      ramGb,
      vramGb,
      resolution: values.resolution,
      graphicsPreset:
        values.graphicsPreset,
      rayTracingEnabled:
        values.rayTracingEnabled,
      upscaler: values.upscaler,
      upscalerMode:
        values.upscaler === "None"
          ? "Native"
          : values.upscalerMode,
      frameGenerationEnabled:
        values.frameGenerationEnabled,
      additionalNotes: notes || undefined,
      rememberHardware: true,
    };
  }

  function submitEstimate() {
    if (
      isLoading ||
      !isAuthenticated
    ) {
      return;
    }

    setApiError(null);
    const request = buildRequest();

    if (request) {
      estimateMutation.mutate(request);
    }
  }

  function handleSubmit(
    event: FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault();
    submitEstimate();
  }

  if (!isOpen) {
    return null;
  }

  return createPortal(
    <div
      className="fixed inset-0 z-[1600] grid place-items-center overflow-y-auto bg-black/70 p-2 backdrop-blur-md min-[375px]:p-3 sm:p-6"
      role="presentation"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          requestClose();
        }
      }}
    >
      <form
        ref={modalRef}
        onSubmit={handleSubmit}
        role="dialog"
        aria-modal="true"
        aria-labelledby="fps-estimator-title"
        aria-describedby="fps-estimator-description"
        aria-busy={isLoading}
        className="flex max-h-[calc(100dvh-1rem)] w-full max-w-5xl flex-col overflow-hidden rounded-[26px] border border-[var(--wp-border-strong)] bg-[var(--wp-surface-strong)] text-[var(--wp-text)] shadow-[var(--wp-shadow-floating)] min-[375px]:max-h-[calc(100dvh-1.5rem)] sm:max-h-[calc(100dvh-3rem)] sm:rounded-[30px]"
      >
        <header className="flex shrink-0 items-start justify-between gap-4 border-b border-[var(--wp-border)] px-4 py-4 min-[375px]:px-5 sm:px-7 sm:py-5">
          <div className="flex min-w-0 items-start gap-3">
            <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl border border-violet-300/20 bg-violet-400/10 text-violet-300">
              <Sparkles size={21} />
            </span>

            <div className="min-w-0">
              <h2
                id="fps-estimator-title"
                className="text-lg font-black text-[var(--wp-text-strong)] sm:text-xl"
              >
                AI FPS təxmini
              </h2>
              <p
                id="fps-estimator-description"
                className="mt-1 truncate text-xs text-[var(--wp-text-muted)] sm:text-sm"
              >
                {gameName}
                {gameReleaseYear
                  ? ` (${gameReleaseYear})`
                  : ""}
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={requestClose}
            disabled={isLoading}
            aria-label="FPS təxmin pəncərəsini bağla"
            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-[var(--wp-border)] text-[var(--wp-text-muted)] transition hover:bg-[var(--wp-surface-hover)] hover:text-[var(--wp-text-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400/60 disabled:cursor-not-allowed disabled:opacity-40"
          >
            <X size={18} />
          </button>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto px-4 py-5 min-[375px]:px-5 sm:px-7 sm:py-7">
          {!isAuthenticated ? (
            <div className="mx-auto max-w-lg py-10 text-center">
              <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl border border-violet-300/20 bg-violet-400/10 text-violet-300">
                <LogIn size={24} />
              </span>
              <h3 className="mt-5 text-xl font-black text-[var(--wp-text-strong)]">
                Hesabına daxil ol
              </h3>
              <p className="mt-3 text-sm leading-7 text-[var(--wp-text-muted)]">
                FPS təxminindən istifadə etmək və
                sistem məlumatlarını yadda saxlamaq
                üçün hesabınıza daxil olun.
              </p>
              <Link
                to="/login"
                className="mt-6 inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-black text-white transition hover:bg-violet-500 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400/60"
              >
                <LogIn size={17} />
                Daxil ol
              </Link>
            </div>
          ) : (
            <>
              <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] px-4 py-3">
                <div className="flex min-w-0 items-center gap-3">
                  <Gauge
                    size={18}
                    className="shrink-0 text-violet-300"
                  />
                  <div className="min-w-0">
                    <p className="text-xs text-[var(--wp-text-muted)]">
                      Seçilmiş oyun
                    </p>
                    <p className="truncate text-sm font-bold text-[var(--wp-text-strong)]">
                      {gameName}
                      {gameReleaseYear
                        ? ` (${gameReleaseYear})`
                        : ""}
                    </p>
                  </div>
                </div>

                {hardwareQuery.isLoading && (
                  <span className="inline-flex items-center gap-2 text-xs text-[var(--wp-text-muted)]">
                    <LoaderCircle
                      size={14}
                      className="animate-spin"
                    />
                    Sistem məlumatları yüklənir
                  </span>
                )}
              </div>

              <div className="mt-6 grid gap-x-4 gap-y-5 sm:grid-cols-2">
                <div>
                  <label
                    htmlFor="fps-gpu"
                    className="text-sm font-bold text-[var(--wp-text-strong)]"
                  >
                    GPU
                  </label>
                  <input
                    ref={gpuInputRef}
                    id="fps-gpu"
                    type="text"
                    value={values.gpu}
                    maxLength={150}
                    disabled={isLoading}
                    aria-invalid={Boolean(
                      fieldErrors.gpu,
                    )}
                    aria-describedby={
                      fieldErrors.gpu
                        ? "fps-gpu-error"
                        : undefined
                    }
                    placeholder="Məsələn: RTX 3060"
                    onChange={(event) =>
                      updateField(
                        "gpu",
                        event.target.value,
                      )
                    }
                    className={getInputClass(
                      Boolean(fieldErrors.gpu),
                    )}
                  />
                  <FieldError id="fps-gpu-error">
                    {fieldErrors.gpu}
                  </FieldError>
                </div>

                <div>
                  <label
                    htmlFor="fps-cpu"
                    className="text-sm font-bold text-[var(--wp-text-strong)]"
                  >
                    CPU
                  </label>
                  <input
                    id="fps-cpu"
                    type="text"
                    value={values.cpu}
                    maxLength={150}
                    disabled={isLoading}
                    aria-invalid={Boolean(
                      fieldErrors.cpu,
                    )}
                    aria-describedby={
                      fieldErrors.cpu
                        ? "fps-cpu-error"
                        : undefined
                    }
                    placeholder="Məsələn: Ryzen 5 5600"
                    onChange={(event) =>
                      updateField(
                        "cpu",
                        event.target.value,
                      )
                    }
                    className={getInputClass(
                      Boolean(fieldErrors.cpu),
                    )}
                  />
                  <FieldError id="fps-cpu-error">
                    {fieldErrors.cpu}
                  </FieldError>
                </div>

                <div>
                  <label
                    htmlFor="fps-ram"
                    className="text-sm font-bold text-[var(--wp-text-strong)]"
                  >
                    RAM
                  </label>
                  <div className="relative">
                    <input
                      id="fps-ram"
                      type="number"
                      inputMode="numeric"
                      min={1}
                      max={512}
                      step={1}
                      value={values.ramGb}
                      disabled={isLoading}
                      aria-invalid={Boolean(
                        fieldErrors.ramGb,
                      )}
                      aria-describedby={
                        fieldErrors.ramGb
                          ? "fps-ram-error"
                          : undefined
                      }
                      onChange={(event) =>
                        updateField(
                          "ramGb",
                          event.target.value,
                        )
                      }
                      className={`${getInputClass(
                        Boolean(
                          fieldErrors.ramGb,
                        ),
                      )} pr-12`}
                    />
                    <span className="pointer-events-none absolute right-4 top-[26px] text-xs font-semibold text-[var(--wp-text-muted)]">
                      GB
                    </span>
                  </div>
                  <FieldError id="fps-ram-error">
                    {fieldErrors.ramGb}
                  </FieldError>
                </div>

                <div>
                  <label
                    htmlFor="fps-vram"
                    className="text-sm font-bold text-[var(--wp-text-strong)]"
                  >
                    VRAM{" "}
                    <span className="font-normal text-[var(--wp-text-muted)]">
                      (istəyə bağlı)
                    </span>
                  </label>
                  <div className="relative">
                    <input
                      id="fps-vram"
                      type="number"
                      inputMode="numeric"
                      min={1}
                      max={128}
                      step={1}
                      value={values.vramGb}
                      disabled={isLoading}
                      aria-invalid={Boolean(
                        fieldErrors.vramGb,
                      )}
                      aria-describedby={
                        fieldErrors.vramGb
                          ? "fps-vram-error"
                          : undefined
                      }
                      placeholder="Məsələn: 12"
                      onChange={(event) =>
                        updateField(
                          "vramGb",
                          event.target.value,
                        )
                      }
                      className={`${getInputClass(
                        Boolean(
                          fieldErrors.vramGb,
                        ),
                      )} pr-12`}
                    />
                    <span className="pointer-events-none absolute right-4 top-[26px] text-xs font-semibold text-[var(--wp-text-muted)]">
                      GB
                    </span>
                  </div>
                  <FieldError id="fps-vram-error">
                    {fieldErrors.vramGb}
                  </FieldError>
                </div>

                <div>
                  <label
                    htmlFor="fps-resolution"
                    className="text-sm font-bold text-[var(--wp-text-strong)]"
                  >
                    Görüntü ölçüsü
                  </label>
                  <select
                    id="fps-resolution"
                    value={values.resolution}
                    disabled={isLoading}
                    aria-invalid={Boolean(
                      fieldErrors.resolution,
                    )}
                    onChange={(event) =>
                      updateField(
                        "resolution",
                        event.target
                          .value as FpsResolution,
                      )
                    }
                    className={getInputClass(
                      Boolean(
                        fieldErrors.resolution,
                      ),
                    )}
                  >
                    {resolutionOptions.map(
                      (option) => (
                        <option
                          key={option.value}
                          value={option.value}
                          className="bg-[var(--wp-surface-strong)]"
                        >
                          {option.label}
                        </option>
                      ),
                    )}
                  </select>
                  <FieldError>
                    {fieldErrors.resolution}
                  </FieldError>
                </div>

                <div>
                  <label
                    htmlFor="fps-preset"
                    className="text-sm font-bold text-[var(--wp-text-strong)]"
                  >
                    Qrafika səviyyəsi
                  </label>
                  <select
                    id="fps-preset"
                    value={values.graphicsPreset}
                    disabled={isLoading}
                    aria-invalid={Boolean(
                      fieldErrors.graphicsPreset,
                    )}
                    onChange={(event) =>
                      updateField(
                        "graphicsPreset",
                        event.target
                          .value as FpsGraphicsPreset,
                      )
                    }
                    className={getInputClass(
                      Boolean(
                        fieldErrors.graphicsPreset,
                      ),
                    )}
                  >
                    {presetOptions.map((option) => (
                      <option
                        key={option.value}
                        value={option.value}
                        className="bg-[var(--wp-surface-strong)]"
                      >
                        {option.label}
                      </option>
                    ))}
                  </select>
                  <FieldError>
                    {fieldErrors.graphicsPreset}
                  </FieldError>
                </div>

                <div>
                  <label
                    htmlFor="fps-upscaler"
                    className="text-sm font-bold text-[var(--wp-text-strong)]"
                  >
                    Upscaler
                  </label>
                  <select
                    id="fps-upscaler"
                    value={values.upscaler}
                    disabled={isLoading}
                    onChange={(event) => {
                      const upscaler =
                        event.target
                          .value as FpsUpscaler;
                      updateField(
                        "upscaler",
                        upscaler,
                      );

                      if (upscaler === "None") {
                        updateField(
                          "upscalerMode",
                          "Native",
                        );
                      }
                    }}
                    className={getInputClass(false)}
                  >
                    {upscalerOptions.map(
                      (option) => (
                        <option
                          key={option.value}
                          value={option.value}
                          className="bg-[var(--wp-surface-strong)]"
                        >
                          {option.label}
                        </option>
                      ),
                    )}
                  </select>
                </div>

                <div>
                  <label
                    htmlFor="fps-upscaler-mode"
                    className="text-sm font-bold text-[var(--wp-text-strong)]"
                  >
                    Upscaler rejimi
                  </label>
                  <select
                    id="fps-upscaler-mode"
                    value={values.upscalerMode}
                    disabled={
                      isLoading ||
                      values.upscaler === "None"
                    }
                    onChange={(event) =>
                      updateField(
                        "upscalerMode",
                        event.target
                          .value as FpsUpscalerMode,
                      )
                    }
                    className={getInputClass(false)}
                  >
                    {upscalerModeOptions.map(
                      (mode) => (
                        <option
                          key={mode}
                          value={mode}
                          className="bg-[var(--wp-surface-strong)]"
                        >
                          {mode}
                        </option>
                      ),
                    )}
                  </select>
                  {values.upscaler === "None" && (
                    <p className="mt-2 text-xs text-[var(--wp-text-muted)]">
                      Upscaler seçildikdə aktivləşəcək.
                    </p>
                  )}
                </div>
              </div>

              <div className="mt-5 grid gap-3 sm:grid-cols-2">
                <ToggleField
                  id="fps-ray-tracing"
                  label="Ray Tracing"
                  description="Şüa izləmə effektlərini hesablamaya daxil et."
                  checked={
                    values.rayTracingEnabled
                  }
                  disabled={isLoading}
                  onChange={(checked) =>
                    updateField(
                      "rayTracingEnabled",
                      checked,
                    )
                  }
                />
                <ToggleField
                  id="fps-frame-generation"
                  label="Frame Generation"
                  description="Dəstəklənirsə əlavə kadr yaratmanı nəzərə al."
                  checked={
                    values.frameGenerationEnabled
                  }
                  disabled={isLoading}
                  onChange={(checked) =>
                    updateField(
                      "frameGenerationEnabled",
                      checked,
                    )
                  }
                />
              </div>

              <div className="mt-5">
                <div className="flex items-center justify-between gap-3">
                  <label
                    htmlFor="fps-notes"
                    className="text-sm font-bold text-[var(--wp-text-strong)]"
                  >
                    Əlavə qeyd{" "}
                    <span className="font-normal text-[var(--wp-text-muted)]">
                      (istəyə bağlı)
                    </span>
                  </label>
                  <span className="text-xs text-[var(--wp-text-muted)]">
                    {values.additionalNotes.length}
                    /500
                  </span>
                </div>
                <textarea
                  id="fps-notes"
                  rows={4}
                  maxLength={500}
                  value={values.additionalNotes}
                  disabled={isLoading}
                  aria-invalid={Boolean(
                    fieldErrors.additionalNotes,
                  )}
                  placeholder="Məsələn: xüsusi modlar, laptop modeli və ya əlavə oyun ayarları"
                  onChange={(event) =>
                    updateField(
                      "additionalNotes",
                      event.target.value,
                    )
                  }
                  className={`${getInputClass(
                    Boolean(
                      fieldErrors.additionalNotes,
                    ),
                  )} h-auto min-h-28 resize-y py-3 leading-6`}
                />
                <FieldError>
                  {fieldErrors.additionalNotes}
                </FieldError>
              </div>

              {apiError && (
                <div
                  role="alert"
                  className="mt-5 flex flex-col gap-3 rounded-2xl border border-red-400/20 bg-red-500/[0.07] px-4 py-3 text-sm text-red-200 sm:flex-row sm:items-center sm:justify-between"
                >
                  <div className="flex items-start gap-3">
                    <AlertCircle
                      size={18}
                      className="mt-0.5 shrink-0"
                    />
                    <p className="leading-6">
                      {apiError.message}
                    </p>
                  </div>

                  {apiError.canRetry && (
                    <button
                      type="button"
                      onClick={submitEstimate}
                      disabled={isLoading}
                      className="shrink-0 rounded-xl border border-red-300/20 px-3 py-2 text-xs font-bold transition hover:bg-red-400/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-300/40 disabled:opacity-50"
                    >
                      Yenidən yoxla
                    </button>
                  )}
                </div>
              )}

              {isLoading && !result && (
                <div className="mt-5 flex items-center gap-3 rounded-2xl border border-violet-300/15 bg-violet-400/[0.07] px-4 py-3">
                  <LoaderCircle
                    size={20}
                    className="shrink-0 animate-spin text-violet-300"
                  />
                  <div>
                    <p className="text-sm font-bold text-[var(--wp-text-strong)]">
                      FPS hesablanır...
                    </p>
                    <p className="mt-1 text-xs text-[var(--wp-text-muted)]">
                      AI oyun və sistem məlumatlarını
                      analiz edir.
                    </p>
                  </div>
                </div>
              )}

              {result && (
                <FpsEstimatorResult
                  result={result}
                  isLoading={isLoading}
                />
              )}
            </>
          )}
        </div>

        <footer className="shrink-0 border-t border-[var(--wp-border)] bg-[var(--wp-surface-strong)] px-4 py-4 min-[375px]:px-5 sm:px-7">
          {isAuthenticated ? (
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <span className="inline-flex items-center gap-2 text-xs leading-5 text-[var(--wp-text-muted)]">
                <Save
                  size={15}
                  className="shrink-0"
                />
                Sistem məlumatların hesabında
                saxlanacaq.
              </span>

              <div className="grid grid-cols-2 gap-2 sm:flex">
                <button
                  type="button"
                  onClick={requestClose}
                  disabled={isLoading}
                  className="h-11 rounded-xl border border-[var(--wp-border)] px-4 text-sm font-bold text-[var(--wp-text)] transition hover:bg-[var(--wp-surface-hover)] hover:text-[var(--wp-text-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400/50 disabled:cursor-not-allowed disabled:opacity-40"
                >
                  Bağla
                </button>
                <button
                  type="submit"
                  disabled={isLoading}
                  className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-black text-white transition hover:bg-violet-500 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400/60 disabled:cursor-not-allowed disabled:opacity-60 sm:px-5"
                >
                  {isLoading ? (
                    <LoaderCircle
                      size={17}
                      className="animate-spin"
                    />
                  ) : (
                    <Cpu size={17} />
                  )}
                  <span>
                    {isLoading
                      ? "FPS hesablanır..."
                      : result
                        ? "Yenidən hesabla"
                        : "FPS-i hesabla"}
                  </span>
                </button>
              </div>
            </div>
          ) : (
            <button
              type="button"
              onClick={requestClose}
              className="ml-auto block h-11 rounded-xl border border-[var(--wp-border)] px-5 text-sm font-bold text-[var(--wp-text)] transition hover:bg-[var(--wp-surface-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400/50"
            >
              Bağla
            </button>
          )}
        </footer>
      </form>
    </div>,
    document.body,
  );
}
