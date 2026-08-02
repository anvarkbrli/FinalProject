import { ArrowLeft, FileText } from "lucide-react";
import { Link } from "react-router-dom";
import { BrandLogo } from "../components/common/BrandLogo";
import { useI18n } from "../i18n/useI18n";

export function TermsPage() {
  const { language } = useI18n();
  const isEnglish = language === "en";

  const sections = isEnglish
    ? [
        ["Account and eligibility", "You are responsible for the accuracy of your account information and for keeping your login credentials secure. You may not impersonate another person or use the service for unlawful activity."],
        ["Community conduct", "Posts, reviews, discussions, messages and lists must not contain harassment, hate speech, threats, scams, spam or content that violates another person’s rights."],
        ["User content", "You keep ownership of the content you create. You grant WellPlayd the limited permission needed to store, display and distribute that content inside the service."],
        ["Moderation", "WellPlayd may warn, temporarily restrict, remove content from or permanently restrict accounts that violate these terms or community safety rules."],
        ["Service availability", "Features may change and temporary interruptions may occur. WellPlayd is provided without a guarantee of uninterrupted availability."],
        ["Account deletion", "You may request account deletion from settings. Some security and moderation records may be retained where necessary to prevent abuse or comply with legal obligations."],
      ]
    : [
        ["Hesab və uyğunluq", "Hesab məlumatlarının düzgünlüyünə və giriş məlumatlarının təhlükəsiz saxlanmasına sən cavabdehsən. Başqasını təqlid etmək və xidmətdən qanunsuz məqsədlə istifadə etmək olmaz."],
        ["İcma davranışı", "Post, rəy, müzakirə, mesaj və siyahılarda təhqir, nifrət nitqi, təhdid, dələduzluq, spam və başqasının hüquqlarını pozan məzmun olmamalıdır."],
        ["İstifadəçi məzmunu", "Yaratdığın məzmunun sahibi olaraq qalırsan. WellPlayd-ə həmin məzmunu xidmət daxilində saxlamaq, göstərmək və paylaşmaq üçün zəruri məhdud icazə verirsən."],
        ["Moderasiya", "Bu şərtləri və icma təhlükəsizliyi qaydalarını pozan hesablar xəbərdar edilə, müvəqqəti məhdudlaşdırıla, məzmunları silinə və ya daimi məhdudlaşdırıla bilər."],
        ["Xidmətin əlçatanlığı", "Funksiyalar dəyişə və müvəqqəti fasilələr yarana bilər. WellPlayd fasiləsiz işləməyə dair zəmanət vermir."],
        ["Hesabın silinməsi", "Ayarlar bölməsindən hesabın silinməsini istəyə bilərsən. Sui-istifadənin qarşısını almaq və hüquqi öhdəliklər üçün bəzi təhlükəsizlik və moderasiya qeydləri saxlanıla bilər."],
      ];

  return (
    <main className="min-h-screen wellplayd-page-bg px-5 py-10 text-zinc-100">
      <div className="mx-auto max-w-3xl">
        <div className="flex items-center justify-between gap-4">
          <Link to="/" className="inline-flex items-center gap-3">
            <BrandLogo className="h-10 w-12" />
            <span className="font-bold text-white">WellPlayd</span>
          </Link>
          <Link to="/register" className="inline-flex items-center gap-2 text-sm text-zinc-400 hover:text-white">
            <ArrowLeft size={16} />
            {isEnglish ? "Back to registration" : "Qeydiyyata qayıt"}
          </Link>
        </div>

        <section className="mt-10 rounded-3xl border border-white/[0.08] bg-[#101015]/95 p-6 shadow-2xl shadow-black/30 sm:p-9">
          <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-violet-500/10 text-violet-300">
            <FileText size={23} />
          </span>
          <p className="mt-6 text-xs font-bold uppercase tracking-[0.2em] text-violet-400">WellPlayd</p>
          <h1 className="mt-2 text-3xl font-bold text-white">{isEnglish ? "Terms of Use" : "İstifadə şərtləri"}</h1>
          <p className="mt-3 text-sm leading-7 text-zinc-500">
            {isEnglish ? "Effective date: 1 August 2026" : "Qüvvəyə minmə tarixi: 1 avqust 2026"}
          </p>

          <div className="mt-8 space-y-7">
            {sections.map(([title, content]) => (
              <section key={title}>
                <h2 className="text-lg font-bold text-zinc-200">{title}</h2>
                <p className="mt-2 text-sm leading-7 text-zinc-500">{content}</p>
              </section>
            ))}
          </div>
        </section>
      </div>
    </main>
  );
}
