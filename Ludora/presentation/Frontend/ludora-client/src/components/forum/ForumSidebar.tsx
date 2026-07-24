import {
  Compass,
  Flame,
  House,
  Menu,
  MessageCircleMore,
  PanelLeftClose,
  PanelLeftOpen,
  UsersRound,
  X,
} from "lucide-react";
import { useEffect, useState } from "react";

export type ForumSection =
  | "home"
  | "discussions"
  | "popular"
  | "communities"
  | "explore";

interface ForumSidebarProps {
  activeSection: ForumSection;
  onSectionChange: (section: ForumSection) => void;
}

const navigationItems = [
  {
    id: "home" as const,
    label: "Ana lent",
    description: "Sənin üçün seçilənlər",
    icon: House,
  },
  {
    id: "discussions" as const,
    label: "Müzakirələr",
    description: "Ən yeni söhbətlər",
    icon: MessageCircleMore,
  },
  {
    id: "popular" as const,
    label: "Populyar",
    description: "İcmanın gündəmi",
    icon: Flame,
  },
  {
    id: "communities" as const,
    label: "İcmalar",
    description: "Oyununa uyğun məkan",
    icon: UsersRound,
  },
  {
    id: "explore" as const,
    label: "Kəşf et",
    description: "Yeni mövzular tap",
    icon: Compass,
  },
];

export function ForumSidebar({
  activeSection,
  onSectionChange,
}: ForumSidebarProps) {
  const [isCollapsed, setIsCollapsed] = useState(() => {
    return localStorage.getItem("ludora-forum-sidebar") ===
      "collapsed";
  });
  const [isMobileOpen, setIsMobileOpen] = useState(false);

  useEffect(() => {
    localStorage.setItem(
      "ludora-forum-sidebar",
      isCollapsed ? "collapsed" : "expanded",
    );
  }, [isCollapsed]);

  function selectSection(section: ForumSection) {
    onSectionChange(section);
    setIsMobileOpen(false);
  }

  function renderSidebarContent(isMobile = false) {
    const isCompact = isCollapsed && !isMobile;

    return (
    <>
      <div
        className={`flex items-center ${
          isCompact ? "justify-center" : "justify-between"
        }`}
      >
        {!isCompact && (
          <div>
            <p className="text-[10px] font-bold uppercase tracking-[0.24em] text-teal-300/70">
              Forum məkanı
            </p>
            <p className="mt-1 text-sm font-semibold text-white">
              İcmanı kəşf et
            </p>
          </div>
        )}

        {isMobile ? (
          <button
            type="button"
            onClick={() => setIsMobileOpen(false)}
            className="flex h-9 w-9 items-center justify-center rounded-xl border border-white/10 text-zinc-400"
            aria-label="Forum menyusunu bağla"
          >
            <X size={18} />
          </button>
        ) : (
          <button
            type="button"
            onClick={() => setIsCollapsed((current) => !current)}
            className="hidden h-9 w-9 items-center justify-center rounded-xl border border-white/[0.08] bg-white/[0.035] text-zinc-400 transition hover:border-teal-300/25 hover:bg-teal-300/[0.07] hover:text-teal-200 lg:flex"
            aria-label={
              isCollapsed
                ? "Yan menyunu genişləndir"
                : "Yan menyunu yığ"
            }
            title={
              isCollapsed
                ? "Yan menyunu genişləndir"
                : "Yan menyunu yığ"
            }
          >
            {isCollapsed ? (
              <PanelLeftOpen size={18} />
            ) : (
              <PanelLeftClose size={18} />
            )}
          </button>
        )}
      </div>

      <nav
        className="mt-6 space-y-1.5"
        aria-label="Forum naviqasiyası"
      >
        {navigationItems.map((item) => {
          const Icon = item.icon;
          const isActive = item.id === activeSection;

          return (
            <button
              key={item.id}
              type="button"
              onClick={() => selectSection(item.id)}
              title={isCompact ? item.label : undefined}
              aria-current={isActive ? "page" : undefined}
              className={`group flex w-full items-center rounded-2xl border text-left transition ${
                isCompact
                  ? "h-12 justify-center px-0"
                  : "gap-3 px-3 py-2.5"
              } ${
                isActive
                  ? "border-teal-300/20 bg-teal-300/[0.09] text-teal-100 shadow-[inset_0_0_0_1px_rgba(94,234,212,0.03)]"
                  : "border-transparent text-zinc-400 hover:border-white/[0.06] hover:bg-white/[0.035] hover:text-white"
              }`}
            >
              <span
                className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-xl transition ${
                  isActive
                    ? "bg-teal-300/15 text-teal-200"
                    : "bg-white/[0.035] text-zinc-500 group-hover:text-zinc-200"
                }`}
              >
                <Icon size={17} />
              </span>

              {!isCompact && (
                <span className="min-w-0">
                  <span className="block text-sm font-semibold">
                    {item.label}
                  </span>
                  <span className="mt-0.5 block truncate text-[11px] text-zinc-600">
                    {item.description}
                  </span>
                </span>
              )}
            </button>
          );
        })}
      </nav>

      {!isCompact && (
        <div className="mt-auto rounded-2xl border border-violet-300/10 bg-gradient-to-br from-violet-500/[0.09] to-teal-300/[0.04] p-4">
          <p className="text-xs font-semibold text-zinc-200">
            Öz məkanını yarat
          </p>
          <p className="mt-1.5 text-[11px] leading-5 text-zinc-500">
            Sevdiyin oyun ətrafında icma qur və oyunçuları bir araya gətir.
          </p>
          <button
            type="button"
            className="mt-3 text-xs font-bold text-teal-300 transition hover:text-teal-200"
          >
            Tezliklə
          </button>
        </div>
      )}
    </>
  );
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setIsMobileOpen(true)}
        className="fixed bottom-5 left-5 z-40 flex h-12 items-center gap-2 rounded-2xl border border-teal-300/20 bg-[#0d1a19]/95 px-4 text-sm font-bold text-teal-100 shadow-2xl shadow-black/40 backdrop-blur-xl lg:hidden"
        aria-label="Forum menyusunu aç"
      >
        <Menu size={18} />
        Menyu
      </button>

      {isMobileOpen && (
        <div className="fixed inset-0 z-[70] lg:hidden">
          <button
            type="button"
            className="absolute inset-0 bg-black/70 backdrop-blur-sm"
            onClick={() => setIsMobileOpen(false)}
            aria-label="Forum menyusunu bağla"
          />
          <aside className="absolute inset-y-0 left-0 flex w-[min(86vw,320px)] flex-col border-r border-teal-200/10 bg-[#091211] p-5 shadow-2xl">
            <div className="flex min-h-0 flex-1 flex-col">
              {renderSidebarContent(true)}
            </div>
          </aside>
        </div>
      )}

      <aside
        className={`sticky top-[72px] hidden h-[calc(100vh-72px)] shrink-0 flex-col border-r border-teal-100/[0.07] bg-[#091211]/90 px-3 py-5 backdrop-blur-xl transition-[width] duration-300 lg:flex ${
          isCollapsed ? "w-[76px]" : "w-[252px]"
        }`}
      >
        {renderSidebarContent()}
      </aside>
    </>
  );
}
