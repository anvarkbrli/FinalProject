import {
  BookOpenText,
  Gamepad2,
  Heart,
  Mail,
  MessageSquareText,
  ShieldCheck,
} from "lucide-react";
import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/useI18n";
import { BrandLogo } from "../common/BrandLogo";

interface FooterLink {
  label: string;
  to: string;
}

export function Footer() {
  const { language } = useI18n();
  const currentYear = new Date().getFullYear();
  const isEnglish = language === "en";

  const navigationLinks: FooterLink[] = [
    { label: isEnglish ? "Home" : "Ana səhifə", to: "/" },
    { label: isEnglish ? "Games" : "Oyunlar", to: "/games" },
    { label: isEnglish ? "Forum" : "Forum", to: "/forum" },
    { label: isEnglish ? "About" : "Haqqımızda", to: "/about" },
  ];

  const supportLinks: FooterLink[] = [
    { label: isEnglish ? "Contact" : "Əlaqə", to: "/contact" },
    { label: isEnglish ? "Terms of use" : "İstifadə şərtləri", to: "/terms" },
    { label: isEnglish ? "Privacy" : "Məxfilik", to: "/privacy" },
  ];

  return (
    <footer className="site-footer relative overflow-hidden border-t">
      <div className="site-footer-aurora pointer-events-none absolute inset-0" />
      <div className="site-footer-grid pointer-events-none absolute inset-0" />

      <div className="relative mx-auto max-w-7xl px-5 py-12 lg:px-8 lg:py-14">
        <div className="grid gap-10 lg:grid-cols-[1.35fr_0.75fr_0.75fr_0.95fr]">
          <div>
            <Link
              to="/"
              className="inline-flex items-center gap-3"
              aria-label={isEnglish ? "WellPlayd home" : "WellPlayd ana səhifə"}
            >
              <BrandLogo className="h-11 w-14" />
              <span className="text-xl font-bold tracking-tight text-zinc-100">
                WellPlayd
              </span>
            </Link>

            <p className="mt-4 max-w-sm text-sm leading-7 text-zinc-500">
              {isEnglish
                ? "A game tracking and community platform where your library, reviews, lists and discussions live together."
                : "Oyun kitabxananı, rəyləri, siyahıları və icma müzakirələrini bir araya gətirən oyun platforması."}
            </p>

            <div className="mt-5 inline-flex items-center gap-2 rounded-full border border-white/[0.07] bg-white/[0.025] px-3 py-2 text-xs text-zinc-500">
              <Heart size={14} className="text-fuchsia-300" />
              {isEnglish ? "Built for people who love games." : "Oyun sevənlər üçün hazırlanır."}
            </div>
          </div>

          <div>
            <h2 className="text-sm font-bold text-zinc-200">
              {isEnglish ? "Explore" : "Kəşf et"}
            </h2>
            <nav className="mt-4 flex flex-col items-start gap-3" aria-label={isEnglish ? "Footer navigation" : "Footer naviqasiyası"}>
              {navigationLinks.map((link) => (
                <Link
                  key={link.to}
                  to={link.to}
                  className="site-footer-link text-sm text-zinc-500 transition"
                >
                  {link.label}
                </Link>
              ))}
            </nav>
          </div>

          <div>
            <h2 className="text-sm font-bold text-zinc-200">
              {isEnglish ? "Support" : "Dəstək"}
            </h2>
            <nav className="mt-4 flex flex-col items-start gap-3" aria-label={isEnglish ? "Support links" : "Dəstək linkləri"}>
              {supportLinks.map((link) => (
                <Link
                  key={link.to}
                  to={link.to}
                  className="site-footer-link text-sm text-zinc-500 transition"
                >
                  {link.label}
                </Link>
              ))}
            </nav>
          </div>

          <div>
            <h2 className="text-sm font-bold text-zinc-200">
              {isEnglish ? "Platform" : "Platforma"}
            </h2>

            <div className="mt-4 space-y-3">
              <div className="site-footer-feature flex items-center gap-3 rounded-xl border px-3 py-2.5 text-sm text-zinc-500">
                <Gamepad2 size={16} className="text-violet-300" />
                {isEnglish ? "Game tracking" : "Oyun izləmə"}
              </div>
              <div className="site-footer-feature flex items-center gap-3 rounded-xl border px-3 py-2.5 text-sm text-zinc-500">
                <MessageSquareText size={16} className="text-emerald-300" />
                {isEnglish ? "Community discussions" : "İcma müzakirələri"}
              </div>
              <div className="site-footer-feature flex items-center gap-3 rounded-xl border px-3 py-2.5 text-sm text-zinc-500">
                <BookOpenText size={16} className="text-fuchsia-300" />
                {isEnglish ? "Reviews and lists" : "Rəylər və siyahılar"}
              </div>
            </div>
          </div>
        </div>

        <div className="mt-10 flex flex-col gap-4 border-t border-white/[0.06] pt-6 text-sm text-zinc-600 sm:flex-row sm:items-center sm:justify-between">
          <p>© {currentYear} WellPlayd. {isEnglish ? "All rights reserved." : "Bütün hüquqlar qorunur."}</p>

          <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
            <span className="inline-flex items-center gap-2">
              <ShieldCheck size={14} />
              {isEnglish ? "Privacy-minded" : "Məxfiliyə önəm verilir"}
            </span>
            <Link to="/contact" className="site-footer-link inline-flex items-center gap-2 transition">
              <Mail size={14} />
              {isEnglish ? "Get in touch" : "Bizimlə əlaqə"}
            </Link>
          </div>
        </div>
      </div>
    </footer>
  );
}
