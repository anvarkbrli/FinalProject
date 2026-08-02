import { ArrowLeft, ShieldCheck } from "lucide-react";
import { Link } from "react-router-dom";
import { BrandLogo } from "../components/common/BrandLogo";
import { useI18n } from "../i18n/useI18n";

export function PrivacyPage() {
  const { language } = useI18n();
  const isEnglish = language === "en";

  const sections = isEnglish
    ? [
        ["Data we collect", "We collect account details, profile information, game tracking activity, community content, messages, device sessions and security logs needed to operate the service."],
        ["How data is used", "Data is used to provide account features, personalize your experience, protect the community, investigate reports and improve service reliability."],
        ["Visibility controls", "Profile privacy, posts and lists follow the visibility choices available in the product. Private messages and report evidence are not publicly displayed."],
        ["Third-party services", "When you connect Steam or use game catalog data, limited information may be exchanged with those services according to their own policies."],
        ["Retention and deletion", "Account data is retained while your account is active. You can request deletion, subject to limited retention required for security, fraud prevention and moderation records."],
        ["Your choices", "You can edit profile information, change privacy settings, disconnect devices and request account deletion from the settings page."],
      ]
    : [
        ["Toplanan məlumatlar", "Xidmətin işləməsi üçün hesab məlumatları, profil məlumatları, oyun fəaliyyəti, icma məzmunu, mesajlar, cihaz sessiyaları və təhlükəsizlik qeydləri toplanır."],
        ["Məlumatların istifadəsi", "Məlumatlar hesab funksiyalarını təqdim etmək, təcrübəni fərdiləşdirmək, icmanı qorumaq, şikayətləri araşdırmaq və xidmətin etibarlılığını yaxşılaşdırmaq üçün istifadə olunur."],
        ["Görünürlük seçimləri", "Profil məxfiliyi, post və siyahılar məhsuldakı görünürlük seçimlərinə tabedir. Şəxsi mesajlar və report sübutları ictimai göstərilmir."],
        ["Üçüncü tərəf xidmətləri", "Steam hesabını bağladıqda və ya oyun kataloqu məlumatlarından istifadə etdikdə məhdud məlumat həmin xidmətlərlə onların siyasətlərinə uyğun mübadilə oluna bilər."],
        ["Saxlanma və silinmə", "Hesab aktiv olduğu müddətdə məlumatlar saxlanılır. Təhlükəsizlik, dələduzluğun qarşısının alınması və moderasiya qeydləri üçün zəruri məhdud saxlanma istisna olmaqla hesabın silinməsini istəyə bilərsən."],
        ["Seçimlərin", "Profil məlumatlarını redaktə edə, məxfilik ayarlarını dəyişə, cihaz sessiyalarını bağlaya və ayarlar səhifəsindən hesabın silinməsini istəyə bilərsən."],
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
          <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-emerald-500/10 text-emerald-300">
            <ShieldCheck size={23} />
          </span>
          <p className="mt-6 text-xs font-bold uppercase tracking-[0.2em] text-emerald-400">WellPlayd</p>
          <h1 className="mt-2 text-3xl font-bold text-white">{isEnglish ? "Privacy Policy" : "Məxfilik siyasəti"}</h1>
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
