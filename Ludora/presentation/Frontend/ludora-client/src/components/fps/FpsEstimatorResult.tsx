import {
  AlertTriangle,
  Cpu,
  Database,
  ExternalLink,
  Gauge,
  Info,
  SearchCheck,
  Settings2,
  Sparkles,
  Target,
} from "lucide-react";
import { useMemo } from "react";
import type { FpsEstimateResponse } from "../../types/fpsEstimator";

interface FpsEstimatorResultProps {
  result: FpsEstimateResponse;
  isLoading: boolean;
}

const fallbackDisclaimer =
  "Bu nəticə AI tərəfindən yaradılmış təxmini göstəricidir. Real FPS driver, temperatur, oyun versiyası, sistem konfiqurasiyası və arxa plandakı proqramlara görə dəyişə bilər.";

const blockedRecommendationTerms = [
  "gpu-nu dəyiş",
  "cpu-nu dəyiş",
  "ram artır",
  "videokartı dəyiş",
  "prosessoru dəyiş",
  "yeni videokart",
  "yeni prosessor",
  "yeni kompüter al",
  "upgrade your",
  "replace your",
  "buy a new",
  "purchase a",
  "upgrade gpu",
  "upgrade cpu",
  "more ram",
];

function normalizeRecommendation(value: string) {
  return value
    .normalize("NFKD")
    .replace(/\p{Diacritic}/gu, "")
    .toLocaleLowerCase("az")
    .replaceAll("ə", "e")
    .replaceAll("ı", "i");
}

function isSafeRecommendation(value: string) {
  const normalized = normalizeRecommendation(value);

  return !blockedRecommendationTerms.some((term) =>
    normalized.includes(
      normalizeRecommendation(term),
    ),
  );
}

function getPerformanceTone(averageFps: number) {
  if (averageFps < 30) {
    return "border-red-400/20 bg-red-500/[0.08] text-red-200";
  }

  if (averageFps < 45) {
    return "border-amber-300/20 bg-amber-400/[0.08] text-amber-200";
  }

  if (averageFps < 60) {
    return "border-sky-300/20 bg-sky-400/[0.08] text-sky-200";
  }

  if (averageFps < 100) {
    return "border-emerald-300/20 bg-emerald-400/[0.08] text-emerald-200";
  }

  return "border-violet-300/20 bg-violet-400/[0.09] text-violet-200";
}

function getEvidenceLabel(quality?: string) {
  switch (quality) {
    case "Exact":
      return "Dəqiq uyğun benchmark";
    case "Close":
      return "Yaxın uyğunluq";
    case "Inferred":
      return "Müqayisəli təxmin";
    case "Insufficient":
      return "Məhdud məlumat";
    default:
      return "Mənbəli təxmin";
  }
}

export function FpsEstimatorResult({
  result,
  isLoading,
}: FpsEstimatorResultProps) {
  const safeRecommendations = useMemo(
    () =>
      (result.recommendations ?? []).filter(
        isSafeRecommendation,
      ),
    [result.recommendations],
  );

  const benchmarkSources =
    result.benchmarkSources ?? [];
  const importantFactors =
    result.importantFactors ?? [];

  const performanceTone = getPerformanceTone(
    result.averageFps,
  );

  return (
    <section
      aria-labelledby="fps-result-title"
      className="relative mt-8 border-t border-[var(--wp-border)] pt-7"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-[10px] font-black uppercase tracking-[0.18em] text-[var(--wp-text-muted)]">
            Təxmini nəticə
          </p>
          <h3
            id="fps-result-title"
            className="mt-1 text-xl font-black text-[var(--wp-text-strong)]"
          >
            {result.normalizedGameName}
          </h3>
        </div>

        {result.isCached && (
          <span className="inline-flex items-center gap-2 rounded-full border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] px-3 py-1.5 text-xs font-semibold text-[var(--wp-text-muted)]">
            <Database size={14} />
            Yaddaşdakı nəticə
          </span>
        )}
      </div>

      {result.confidencePercent < 60 && (
        <div className="mt-5 flex items-start gap-3 rounded-2xl border border-amber-300/20 bg-amber-400/[0.07] px-4 py-3 text-sm leading-6 text-amber-100">
          <AlertTriangle
            size={18}
            className="mt-0.5 shrink-0"
          />
          <p>
            Bu kombinasiya üçün kifayət qədər
            etibarlı məlumat yoxdur. Nəticə geniş
            təxmini aralıqdır.
          </p>
        </div>
      )}

      <div className="mt-5 grid grid-cols-1 gap-3 min-[400px]:grid-cols-2 lg:grid-cols-5">
        <article className="rounded-2xl border border-violet-300/20 bg-violet-400/[0.09] p-4 lg:col-span-1">
          <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-violet-200">
            <Gauge size={16} />
            Orta FPS
          </div>
          <p className="mt-4 text-4xl font-black tracking-tight text-[var(--wp-text-strong)]">
            {result.averageFps}
          </p>
          <p className="mt-1 text-xs text-[var(--wp-text-muted)]">
            FPS
          </p>
        </article>

        <article className="rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] p-4">
          <p className="text-xs font-bold uppercase tracking-wider text-[var(--wp-text-muted)]">
            1% Low
          </p>
          <p className="mt-4 text-2xl font-black text-[var(--wp-text-strong)]">
            {result.onePercentLowFps}
          </p>
          <p className="mt-1 text-xs text-[var(--wp-text-muted)]">
            FPS
          </p>
        </article>

        <article className="rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] p-4 min-[400px]:col-span-2 lg:col-span-1">
          <p className="text-xs font-bold uppercase tracking-wider text-[var(--wp-text-muted)]">
            Gözlənilən aralıq
          </p>
          <p className="mt-4 break-words text-2xl font-black text-[var(--wp-text-strong)]">
            {result.minimumExpectedFps}–
            {result.maximumExpectedFps}
          </p>
          <p className="mt-1 text-xs text-[var(--wp-text-muted)]">
            FPS
          </p>
        </article>

        <article className="rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] p-4">
          <p className="text-xs font-bold uppercase tracking-wider text-[var(--wp-text-muted)]">
            Etibarlılıq
          </p>
          <p className="mt-4 text-2xl font-black text-[var(--wp-text-strong)]">
            {result.confidencePercent}%
          </p>
          <div
            className="mt-3 h-1.5 overflow-hidden rounded-full bg-black/10"
            aria-hidden="true"
          >
            <span
              className="block h-full rounded-full bg-violet-400"
              style={{
                width: `${Math.min(
                  100,
                  Math.max(
                    0,
                    result.confidencePercent,
                  ),
                )}%`,
              }}
            />
          </div>
        </article>

        <article
          className={`rounded-2xl border p-4 ${performanceTone}`}
        >
          <p className="text-xs font-bold uppercase tracking-wider opacity-75">
            Performans
          </p>
          <p className="mt-4 break-words text-lg font-black">
            {result.performanceLevel}
          </p>
        </article>
      </div>

      <article className="mt-5 rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] p-5">
        <div className="flex items-center gap-2">
          <Sparkles
            size={18}
            className="text-violet-300"
          />
          <h4 className="font-black text-[var(--wp-text-strong)]">
            AI izahı
          </h4>
        </div>

        <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2">
          <div className="min-w-0">
            <dt className="text-xs text-[var(--wp-text-muted)]">
              Sistem
            </dt>
            <dd className="mt-1 break-words font-semibold text-[var(--wp-text-strong)]">
              {result.normalizedGpuName} ·{" "}
              {result.normalizedCpuName}
            </dd>
          </div>
          <div>
            <dt className="text-xs text-[var(--wp-text-muted)]">
              Oyun ayarları
            </dt>
            <dd className="mt-1 font-semibold text-[var(--wp-text-strong)]">
              {result.resolution} ·{" "}
              {result.graphicsPreset}
            </dd>
          </div>
        </dl>

        <p className="mt-4 whitespace-pre-wrap text-sm leading-7 text-[var(--wp-text)]">
          {result.summary}
        </p>
      </article>

      {(result.evidenceSummary ||
        benchmarkSources.length > 0) && (
        <article className="mt-5 rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] p-5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-2">
              <SearchCheck
                size={18}
                className="text-emerald-300"
              />
              <h4 className="font-black text-[var(--wp-text-strong)]">
                Benchmark əsaslandırması
              </h4>
            </div>

            <span className="rounded-full border border-[var(--wp-border)] bg-[var(--wp-surface)] px-3 py-1.5 text-xs font-bold text-[var(--wp-text)]">
              {getEvidenceLabel(
                result.evidenceQuality,
              )}
            </span>
          </div>

          {result.evidenceSummary && (
            <p className="mt-4 whitespace-pre-wrap text-sm leading-7 text-[var(--wp-text)]">
              {result.evidenceSummary}
            </p>
          )}

          {result.benchmarkSampleCount > 0 && (
            <p className="mt-3 text-xs font-semibold text-[var(--wp-text-muted)]">
              İstifadə olunan benchmark müşahidəsi:{" "}
              {result.benchmarkSampleCount}
            </p>
          )}

          {benchmarkSources.length > 0 && (
            <div className="mt-4 border-t border-[var(--wp-border)] pt-4">
              <p className="text-xs font-black uppercase tracking-wider text-[var(--wp-text-muted)]">
                Axtarış mənbələri
              </p>
              <ul className="mt-3 grid gap-2 sm:grid-cols-2">
                {benchmarkSources.map(
                  (source, index) => (
                    <li
                      key={`${source.url}-${index}`}
                      className="min-w-0"
                    >
                      <a
                        href={source.url}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="flex min-h-11 items-start gap-2 rounded-xl border border-[var(--wp-border)] bg-[var(--wp-surface)] px-3 py-2.5 text-sm text-[var(--wp-text)] transition hover:border-violet-300/40 hover:text-[var(--wp-text-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400"
                      >
                        <ExternalLink
                          size={15}
                          className="mt-0.5 shrink-0 text-violet-300"
                        />
                        <span className="min-w-0">
                          <span className="block break-words font-semibold">
                            {source.title}
                          </span>
                          {source.publishedDate && (
                            <span className="mt-0.5 block text-xs text-[var(--wp-text-muted)]">
                              {source.publishedDate}
                            </span>
                          )}
                        </span>
                      </a>
                    </li>
                  ),
                )}
              </ul>
            </div>
          )}
        </article>
      )}

      {safeRecommendations.length > 0 && (
        <article className="mt-5 rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] p-5">
          <div className="flex items-center gap-2">
            <Settings2
              size={18}
              className="text-violet-300"
            />
            <h4 className="font-black text-[var(--wp-text-strong)]">
              Oyun ayarları üzrə tövsiyələr
            </h4>
          </div>

          <ul className="mt-4 space-y-3">
            {safeRecommendations.map(
              (recommendation, index) => (
                <li
                  key={`${recommendation}-${index}`}
                  className="flex items-start gap-3 text-sm leading-6 text-[var(--wp-text)]"
                >
                  <span className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-violet-400" />
                  <span>{recommendation}</span>
                </li>
              ),
            )}
          </ul>
        </article>
      )}

      {importantFactors.length > 0 && (
        <article className="mt-5 rounded-2xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] p-5">
          <div className="flex items-center gap-2">
            <Target
              size={18}
              className="text-sky-300"
            />
            <h4 className="font-black text-[var(--wp-text-strong)]">
              Nəticəyə təsir edən faktorlar
            </h4>
          </div>

          <div className="mt-4 flex flex-wrap gap-2">
            {importantFactors.map(
              (factor, index) => (
                <span
                  key={`${factor}-${index}`}
                  className="max-w-full break-words rounded-full border border-[var(--wp-border)] bg-[var(--wp-surface)] px-3 py-2 text-xs font-semibold text-[var(--wp-text)]"
                >
                  {factor}
                </span>
              ),
            )}
          </div>
        </article>
      )}

      <div className="mt-5 flex items-start gap-3 rounded-2xl border border-sky-300/15 bg-sky-400/[0.06] px-4 py-3 text-xs leading-6 text-[var(--wp-text)]">
        <Info
          size={17}
          className="mt-0.5 shrink-0 text-sky-300"
        />
        <p>
          {result.disclaimer?.trim() ||
            fallbackDisclaimer}
        </p>
      </div>

      {isLoading && (
        <div className="absolute inset-0 z-10 grid place-items-center rounded-2xl bg-[var(--wp-surface-strong)]/80 px-5 text-center backdrop-blur-sm">
          <div>
            <Cpu
              size={28}
              className="mx-auto animate-pulse text-violet-300"
            />
            <p className="mt-3 text-sm font-bold text-[var(--wp-text-strong)]">
              Yeni nəticə hesablanır
            </p>
            <p className="mt-1 text-xs text-[var(--wp-text-muted)]">
              Əvvəlki nəticə saxlanılır.
            </p>
          </div>
        </div>
      )}
    </section>
  );
}
