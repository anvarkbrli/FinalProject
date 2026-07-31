import type { ReactNode } from "react";
import { ArrowLeft, Gamepad2, Home } from "lucide-react";
import { Link, useNavigate } from "react-router-dom";

interface StatusPageProps {
  code: string;
  title: string;
  description: string;
  icon?: ReactNode;
}

export function StatusPage({
  code,
  title,
  description,
  icon,
}: StatusPageProps) {
  const navigate = useNavigate();

  return (
    <main className="relative grid min-h-screen place-items-center overflow-hidden bg-[#09090d] px-5 py-16 text-zinc-100">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_top,rgba(124,58,237,0.20),transparent_42%),radial-gradient(circle_at_bottom_right,rgba(217,70,239,0.12),transparent_36%)]" />
      <section className="relative w-full max-w-2xl overflow-hidden rounded-[2rem] border border-white/10 bg-white/[0.045] p-8 text-center shadow-2xl shadow-violet-950/30 backdrop-blur-xl sm:p-12">
        <div className="mx-auto mb-6 grid size-16 place-items-center rounded-2xl border border-violet-400/20 bg-violet-500/10 text-violet-300">
          {icon ?? <Gamepad2 size={30} aria-hidden="true" />}
        </div>

        <p className="mb-3 text-sm font-black uppercase tracking-[0.35em] text-violet-300">
          {code}
        </p>
        <h1 className="text-3xl font-black tracking-tight sm:text-5xl">
          {title}
        </h1>
        <p className="mx-auto mt-5 max-w-xl text-sm leading-7 text-zinc-400 sm:text-base">
          {description}
        </p>

        <div className="mt-9 flex flex-col justify-center gap-3 sm:flex-row">
          <button
            type="button"
            onClick={() => navigate(-1)}
            className="inline-flex items-center justify-center gap-2 rounded-xl border border-white/10 bg-white/[0.04] px-5 py-3 text-sm font-bold text-zinc-200 transition hover:border-white/20 hover:bg-white/[0.08]"
          >
            <ArrowLeft size={17} aria-hidden="true" />
            Geri qayıt
          </button>
          <Link
            to="/"
            className="inline-flex items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 py-3 text-sm font-bold text-white transition hover:bg-violet-500"
          >
            <Home size={17} aria-hidden="true" />
            Ana səhifə
          </Link>
        </div>
      </section>
    </main>
  );
}
