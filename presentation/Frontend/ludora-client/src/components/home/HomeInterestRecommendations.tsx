import { RefreshCw, Sparkles, WandSparkles } from "lucide-react";
import { memo } from "react";
import { useI18n } from "../../i18n/useI18n";
import type { HomeInterestRecommendationsDTO } from "../../types/home";
import { GameCard } from "./GameCard";
import { GameCardSkeleton } from "./GameCardSkeleton";

interface HomeInterestRecommendationsProps {
  data: HomeInterestRecommendationsDTO | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

export const HomeInterestRecommendations = memo(
  function HomeInterestRecommendations({
    data,
    isLoading,
    isError,
    onRetry,
  }: HomeInterestRecommendationsProps) {
    const { language } = useI18n();

    if (!isLoading && !isError && (data?.items.length ?? 0) === 0) {
      return null;
    }

    return (
      <section className="home-interest-section relative overflow-hidden border-b border-white/5 py-16">
        <div className="pointer-events-none absolute inset-0" />

        <div className="relative mx-auto max-w-7xl px-5 lg:px-8">
          <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <p className="flex items-center gap-2 text-xs font-bold uppercase tracking-[0.22em] text-teal-300">
                <WandSparkles size={15} />
                {language === "en" ? "Picked for you" : "Sənin üçün seçildi"}
              </p>

              <h2 className="mt-2 text-2xl font-bold tracking-tight text-white sm:text-3xl">
                {language === "en"
                  ? "Popular games matching your interests"
                  : "Maraqlarına uyğun məşhur oyunlar"}
              </h2>

            </div>

            {data?.isAiGenerated && (
              <span className="inline-flex w-fit items-center gap-1.5 rounded-full border border-violet-300/15 bg-violet-400/[0.08] px-3 py-1.5 text-[11px] font-bold text-violet-200">
                <Sparkles size={13} />
                {language === "en" ? "Ranked with Groq" : "Groq ilə sıralandı"}
              </span>
            )}
          </div>

          {isLoading ? (
            <div className="home-interest-grid grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
              {Array.from({ length: 8 }).map((_, index) => (
                <div key={index} className="home-interest-item">
                  <GameCardSkeleton />
                </div>
              ))}
            </div>
          ) : isError ? (
            <div className="rounded-2xl border border-rose-400/15 bg-rose-500/[0.05] p-7 text-center">
              <p className="text-sm font-semibold text-rose-100">
                {language === "en"
                  ? "Recommendations could not be loaded."
                  : "Tövsiyələri yükləmək mümkün olmadı."}
              </p>
              <button
                type="button"
                onClick={onRetry}
                className="mt-4 inline-flex items-center gap-2 rounded-xl border border-rose-300/15 px-4 py-2 text-xs font-bold text-rose-100 transition hover:bg-rose-300/[0.08]"
              >
                <RefreshCw size={14} />
                {language === "en" ? "Try again" : "Yenidən yoxla"}
              </button>
            </div>
          ) : (
            <div className="home-interest-grid grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
              {data?.items.map((item) => (
                <div
                  key={`${item.game.source}-${item.game.id}`}
                  className="home-interest-item flex min-w-0 flex-col"
                >
                  <GameCard game={item.game} />

                  <div className="home-interest-reason relative -mt-2 mx-2 flex-1 rounded-b-2xl border border-t-0 px-3 pb-3 pt-4">
                    <div className="mb-2 flex items-center justify-between gap-2">
                      <span className="text-[10px] font-bold uppercase tracking-[0.16em] text-teal-300">
                        {language === "en" ? "Why this game" : "Niyə bu oyun"}
                      </span>
                      <span className="rounded-full bg-teal-300/10 px-2 py-0.5 text-[10px] font-bold text-teal-200">
                        {item.matchScore}%
                      </span>
                    </div>
                    <p className="line-clamp-3 text-xs leading-5 text-zinc-400">
                      {item.reason}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </section>
    );
  },
);
