import {
  BookOpenText,
  Gamepad2,
  HeartHandshake,
  ListChecks,
  MessageSquareText,
  ShieldCheck,
  Sparkles,
  UsersRound,
} from "lucide-react";
import { Link } from "react-router-dom";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { useI18n } from "../i18n/useI18n";

export function AboutPage() {
  const { language } = useI18n();
  const isEnglish = language === "en";

  const features = [
    {
      icon: Gamepad2,
      title: isEnglish ? "Track your games" : "Oyunlarını izlə",
      description: isEnglish
        ? "Keep played, playing, planned and abandoned games in one clear library."
        : "Oynadığın, oynadığın, planlaşdırdığın və yarım qoyduğun oyunları bir kitabxanada saxla.",
    },
    {
      icon: BookOpenText,
      title: isEnglish ? "Share useful reviews" : "Faydalı rəylər paylaş",
      description: isEnglish
        ? "Combine ratings, playtime and system details with the story behind your experience."
        : "Qiymətləndirməni, oyun müddətini və sistem məlumatlarını təcrübənin hekayəsi ilə birləşdir.",
    },
    {
      icon: ListChecks,
      title: isEnglish ? "Build personal lists" : "Şəxsi siyahılar yarat",
      description: isEnglish
        ? "Create ranked or unranked collections for every theme, mood and backlog."
        : "Hər mövzu, əhval və backlog üçün sıralanmış və ya sərbəst kolleksiyalar yarat.",
    },
    {
      icon: MessageSquareText,
      title: isEnglish ? "Join the community" : "İcmaya qoşul",
      description: isEnglish
        ? "Start discussions, vote in polls and follow the communities that match your interests."
        : "Müzakirələr başlat, sorğularda səs ver və maraqlarına uyğun icmaları izlə.",
    },
  ];

  const principles = [
    {
      icon: UsersRound,
      title: isEnglish ? "People before metrics" : "Rəqəmlərdən əvvəl insanlar",
      description: isEnglish
        ? "Stats help tell the story, but the player and their experience remain at the centre."
        : "Statistikalar hekayəni dəstəkləyir, amma mərkəzdə oyunçu və onun təcrübəsi qalır.",
    },
    {
      icon: ShieldCheck,
      title: isEnglish ? "Safer community spaces" : "Daha təhlükəsiz icma mühiti",
      description: isEnglish
        ? "Privacy controls, reporting and moderation tools are designed as core platform features."
        : "Məxfilik seçimləri, şikayət və moderasiya alətləri platformanın əsas hissəsi kimi hazırlanır.",
    },
    {
      icon: HeartHandshake,
      title: isEnglish ? "Useful, not noisy" : "Səs-küylü yox, faydalı",
      description: isEnglish
        ? "The interface aims to surface what matters without turning every interaction into a competition."
        : "İnterfeys hər ünsiyyəti yarışa çevirmədən vacib məlumatı önə çıxarmağa çalışır.",
    },
  ];

  return (
    <div className="info-page min-h-screen overflow-x-hidden wellplayd-page-bg text-zinc-100">
      <Navbar />

      <main>
        <section className="info-page-hero relative overflow-hidden border-b border-white/[0.06]">
          <div className="info-page-aurora pointer-events-none absolute inset-0" />
          <div className="info-page-grid pointer-events-none absolute inset-0" />

          <div className="relative mx-auto max-w-7xl px-5 py-20 lg:px-8 lg:py-24">
            <div className="max-w-3xl">
              <div className="info-page-kicker inline-flex items-center gap-2 rounded-full border px-3 py-1.5 text-xs font-semibold text-violet-300">
                <Sparkles size={14} />
                {isEnglish ? "About WellPlayd" : "WellPlayd haqqında"}
              </div>

              <h1 className="mt-7 text-4xl font-bold leading-tight tracking-[-0.04em] text-zinc-100 sm:text-5xl lg:text-6xl">
                {isEnglish ? "Your games, your story," : "Oyunların, hekayən,"}
                <span className="info-page-title-gradient mt-2 block bg-clip-text text-transparent">
                  {isEnglish ? "your community." : "sənin icman."}
                </span>
              </h1>

              <p className="mt-6 max-w-2xl text-base leading-8 text-zinc-500 sm:text-lg">
                {isEnglish
                  ? "WellPlayd brings game tracking and community conversations into one place. It is built for players who want more than a list of titles: a clear record of what they played, what they thought and who they shared it with."
                  : "WellPlayd oyun izləmə ilə icma söhbətlərini bir platformada birləşdirir. Bu, sadəcə oyun adlarından ibarət siyahı yox, nə oynadığını, nə düşündüyünü və bunu kimlərlə paylaşdığını göstərən şəxsi oyun tarixçəsidir."}
              </p>

              <div className="mt-8 flex flex-col gap-3 sm:flex-row">
                <Link
                  to="/games"
                  className="info-page-primary-action inline-flex items-center justify-center rounded-xl px-5 py-3 text-sm font-semibold text-white transition"
                >
                  {isEnglish ? "Explore games" : "Oyunları kəşf et"}
                </Link>
                <Link
                  to="/forum"
                  className="info-page-secondary-action inline-flex items-center justify-center rounded-xl border px-5 py-3 text-sm font-semibold text-zinc-300 transition"
                >
                  {isEnglish ? "Visit the forum" : "Foruma keç"}
                </Link>
              </div>
            </div>
          </div>
        </section>

        <section className="mx-auto max-w-7xl px-5 py-16 lg:px-8 lg:py-20">
          <div className="max-w-2xl">
            <p className="text-xs font-bold uppercase tracking-[0.18em] text-violet-300">
              {isEnglish ? "One connected platform" : "Bir-birinə bağlı platforma"}
            </p>
            <h2 className="mt-3 text-3xl font-bold tracking-tight text-zinc-100">
              {isEnglish
                ? "Everything around your gaming life, without the clutter."
                : "Oyun həyatınla bağlı hər şey, lazımsız qarışıqlıq olmadan."}
            </h2>
          </div>

          <div className="mt-9 grid gap-4 md:grid-cols-2">
            {features.map((feature) => {
              const Icon = feature.icon;
              return (
                <article key={feature.title} className="info-feature-card rounded-3xl border p-6">
                  <span className="info-feature-icon flex h-11 w-11 items-center justify-center rounded-2xl">
                    <Icon size={20} />
                  </span>
                  <h3 className="mt-5 text-lg font-bold text-zinc-100">{feature.title}</h3>
                  <p className="mt-2 text-sm leading-7 text-zinc-500">{feature.description}</p>
                </article>
              );
            })}
          </div>
        </section>

        <section className="info-principles-band border-y border-white/[0.06]">
          <div className="mx-auto max-w-7xl px-5 py-16 lg:px-8 lg:py-20">
            <div className="grid gap-8 lg:grid-cols-[0.8fr_1.2fr] lg:items-start">
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.18em] text-emerald-300">
                  {isEnglish ? "What guides the platform" : "Platformanı nə istiqamətləndirir"}
                </p>
                <h2 className="mt-3 text-3xl font-bold tracking-tight text-zinc-100">
                  {isEnglish ? "Designed around real players." : "Real oyunçular üçün hazırlanır."}
                </h2>
                <p className="mt-4 text-sm leading-7 text-zinc-500">
                  {isEnglish
                    ? "WellPlayd is shaped around clarity, expression and safer interaction rather than endless engagement loops."
                    : "WellPlayd sonsuz məşğuliyyət dövründən daha çox aydınlıq, özünü ifadə və təhlükəsiz ünsiyyət ətrafında formalaşır."}
                </p>
              </div>

              <div className="grid gap-4">
                {principles.map((principle) => {
                  const Icon = principle.icon;
                  return (
                    <article key={principle.title} className="info-principle-card flex gap-4 rounded-2xl border p-5">
                      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                        <Icon size={19} />
                      </span>
                      <div>
                        <h3 className="font-bold text-zinc-100">{principle.title}</h3>
                        <p className="mt-1 text-sm leading-6 text-zinc-500">{principle.description}</p>
                      </div>
                    </article>
                  );
                })}
              </div>
            </div>
          </div>
        </section>

        <section className="mx-auto max-w-7xl px-5 py-16 lg:px-8 lg:py-20">
          <div className="info-page-closing rounded-[28px] border p-7 sm:p-10">
            <div className="max-w-2xl">
              <p className="text-xs font-bold uppercase tracking-[0.18em] text-fuchsia-300">
                {isEnglish ? "Still growing" : "İnkişaf davam edir"}
              </p>
              <h2 className="mt-3 text-3xl font-bold tracking-tight text-zinc-100">
                {isEnglish
                  ? "WellPlayd improves through the people who use it."
                  : "WellPlayd onu istifadə edən insanların rəyi ilə yaxşılaşır."}
              </h2>
              <p className="mt-4 text-sm leading-7 text-zinc-500">
                {isEnglish
                  ? "Found something confusing, missing or worth improving? Share it through the contact page or start a community discussion."
                  : "Qarışıq, çatışmayan və ya yaxşılaşdırılmalı bir hissə gördün? Əlaqə səhifəsindən paylaş və ya icmada müzakirə başlat."}
              </p>
              <Link
                to="/contact"
                className="mt-6 inline-flex rounded-xl border border-violet-400/25 bg-violet-500/10 px-5 py-3 text-sm font-semibold text-violet-200 transition hover:border-violet-400/45 hover:bg-violet-500/15"
              >
                {isEnglish ? "Share feedback" : "Rəyini paylaş"}
              </Link>
            </div>
          </div>
        </section>
      </main>

      <Footer />
    </div>
  );
}
