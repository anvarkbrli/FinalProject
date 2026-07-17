import {
  Code2,
  Gamepad2,
  Heart,
  MessageSquareText,
} from "lucide-react";

const footerLinks = [
  {
    label: "Ana səhifə",
    href: "#home",
  },
  {
    label: "Oyunlar",
    href: "#games",
  },
  {
    label: "Forum",
    href: "#forum",
  },
];

export function Footer() {
  const currentYear = new Date().getFullYear();

  return (
    <footer className="border-t border-white/5 bg-[#060608]">
      <div className="mx-auto max-w-7xl px-5 py-12 lg:px-8">
        <div className="grid gap-10 md:grid-cols-[1.3fr_0.7fr_0.7fr]">
          <div>
            <a
              href="#home"
              className="inline-flex items-center gap-3"
              aria-label="Ludora ana səhifə"
            >
              <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-violet-500/15 text-violet-300">
                <Gamepad2 size={20} />
              </span>

              <span className="text-lg font-bold text-white">
                Ludora
              </span>
            </a>

            <p className="mt-4 max-w-sm text-sm leading-7 text-zinc-500">
              Oyunlarını izlə, şəxsi siyahılarını yarat və
              oyun icmasındakı müzakirələrə qoşul.
            </p>

            <div className="mt-5 flex items-center gap-2 text-xs text-zinc-600">
              <Heart size={14} />
              Oyun sevənlər üçün hazırlanır.
            </div>
          </div>

          <div>
            <h2 className="text-sm font-semibold text-zinc-200">
              Naviqasiya
            </h2>

            <nav className="mt-4 flex flex-col items-start gap-3">
              {footerLinks.map((link) => (
                <a
                  key={link.href}
                  href={link.href}
                  className="text-sm text-zinc-500 transition hover:text-violet-300"
                >
                  {link.label}
                </a>
              ))}
            </nav>
          </div>

          <div>
            <h2 className="text-sm font-semibold text-zinc-200">
              Platforma
            </h2>

            <div className="mt-4 flex flex-col gap-3 text-sm text-zinc-500">
              <span className="flex items-center gap-2">
                <Gamepad2 size={15} />
                Oyun izləmə
              </span>

              <span className="flex items-center gap-2">
                <MessageSquareText size={15} />
                İcma forumu
              </span>

              <span className="flex items-center gap-2">
                <Code2 size={15} />
                Diplom layihəsi
              </span>
            </div>
          </div>
        </div>

        <div className="mt-10 flex flex-col gap-3 border-t border-white/5 pt-6 text-sm text-zinc-600 sm:flex-row sm:items-center sm:justify-between">
          <p>© {currentYear} Ludora. Bütün hüquqlar qorunur.</p>

          <p>Play. Track. Discuss.</p>
        </div>
      </div>
    </footer>
  );
}