import { useQuery } from "@tanstack/react-query";
import {
  Gamepad2,
  ListChecks,
  LoaderCircle,
  MessageSquareText,
  Search,
  Star,
  UserRound,
} from "lucide-react";
import type { ReactNode } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import {
  formatDateTime,
  getReleaseYear,
  resolveAssetUrl,
} from "../lib/helpers";
import { globalSearch } from "../services/searchService";

type SearchTab =
  | "games"
  | "users"
  | "discussions"
  | "reviews"
  | "lists";

export function SearchPage() {
  const [searchParams, setSearchParams] =
    useSearchParams();
  const query =
    searchParams.get("q")?.trim() ?? "";
  const tabValue = searchParams.get("tab");
  const activeTab: SearchTab =
    tabValue === "users" ||
    tabValue === "discussions" ||
    tabValue === "reviews" ||
    tabValue === "lists"
      ? tabValue
      : "games";

  const resultQuery = useQuery({
    queryKey: ["global-search-page", query],
    queryFn: () => globalSearch(query, 40),
    enabled: query.length >= 2,
    staleTime: 30_000,
  });

  const data = resultQuery.data;
  const tabs: Array<{
    id: SearchTab;
    label: string;
    count: number;
  }> = [
    {
      id: "games",
      label: "Oyunlar",
      count: data?.counts.games ?? 0,
    },
    {
      id: "users",
      label: "İstifadəçilər",
      count: data?.counts.users ?? 0,
    },
    {
      id: "discussions",
      label: "Müzakirələr",
      count: data?.counts.discussions ?? 0,
    },
    {
      id: "reviews",
      label: "Rəylər",
      count: data?.counts.reviews ?? 0,
    },
    {
      id: "lists",
      label: "Siyahılar",
      count: data?.counts.lists ?? 0,
    },
  ];

  function changeTab(tab: SearchTab) {
    const next = new URLSearchParams(searchParams);
    next.set("tab", tab);
    setSearchParams(next);
  }

  return (
    <div className="min-h-screen bg-[#07070a] text-zinc-100">
      <Navbar />

      <main className="mx-auto min-h-[70vh] max-w-7xl px-5 py-10 lg:px-8">
        <div className="flex items-center gap-3">
          <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-violet-500/10 text-violet-300">
            <Search size={22} />
          </span>

          <div>
            <h1 className="text-2xl font-black text-white">
              Axtarış nəticələri
            </h1>
            <p className="mt-1 text-sm text-zinc-500">
              “{query}” üçün tapılan məzmun
            </p>
          </div>
        </div>

        {query.length < 2 ? (
          <EmptyState text="Axtarış üçün ən azı 2 simvol yaz." />
        ) : resultQuery.isLoading ? (
          <div className="flex min-h-72 items-center justify-center">
            <LoaderCircle
              className="animate-spin text-violet-300"
              size={30}
            />
          </div>
        ) : resultQuery.isError || !data ? (
          <div className="mt-10 rounded-3xl border border-red-400/15 bg-red-500/[0.05] p-10 text-center text-red-300">
            Axtarış nəticələri yüklənmədi.
          </div>
        ) : (
          <>
            <div className="mt-8 flex gap-2 overflow-x-auto border-b border-white/[0.07] pb-3">
              {tabs.map((tab) => (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => changeTab(tab.id)}
                  className={`shrink-0 rounded-xl px-4 py-2.5 text-sm font-bold transition ${
                    activeTab === tab.id
                      ? "bg-violet-600 text-white"
                      : "bg-white/[0.035] text-zinc-400 hover:bg-white/[0.07] hover:text-white"
                  }`}
                >
                  {tab.label} ({tab.count})
                </button>
              ))}
            </div>

            <div className="mt-6 grid gap-3">
              {activeTab === "games" &&
                data.games.map((item) => (
                  <ResultLink
                    key={`${item.source}-${item.id}`}
                    to={`/games/${item.source}/${item.id}`}
                    imageUrl={item.coverImageUrl}
                    icon={<Gamepad2 size={20} />}
                    title={item.title}
                    description={`${getReleaseYear(
                      item.releaseDate,
                    )}${
                      item.averageRating !== null
                        ? ` · ${item.averageRating.toFixed(1)} puan`
                        : ""
                    }`}
                  />
                ))}

              {activeTab === "users" &&
                data.users.map((item) => (
                  <ResultLink
                    key={item.id}
                    to={`/users/${item.userName}`}
                    imageUrl={item.profileImageUrl}
                    icon={<UserRound size={20} />}
                    title={item.userName}
                    description={
                      item.bio ?? "Bio əlavə edilməyib"
                    }
                    rounded
                  />
                ))}

              {activeTab === "discussions" &&
                data.discussions.map((item) => (
                  <ResultLink
                    key={item.id}
                    to="/#forum"
                    icon={<MessageSquareText size={20} />}
                    title={item.title}
                    description={`${item.categoryName} · ${item.authorUserName} · ${formatDateTime(item.createdAt)}`}
                  />
                ))}

              {activeTab === "reviews" &&
                data.reviews.map((item) => (
                  <ResultLink
                    key={item.id}
                    to={`/games/${item.source}/${item.gameId}#reviews`}
                    imageUrl={item.gameCoverImageUrl}
                    icon={<Star size={20} />}
                    title={item.gameName}
                    eyebrow={`${item.userName} tərəfindən rəy`}
                    description={item.contentPreview}
                  />
                ))}

              {activeTab === "lists" &&
                data.lists.map((item) => (
                  <ResultLink
                    key={item.id}
                    to={`/lists/${item.id}`}
                    icon={<ListChecks size={20} />}
                    title={item.name}
                    description={`${item.ownerUserName} · ${item.gameCount} oyun${item.isRanked ? " · Sıralı" : ""}`}
                  />
                ))}

              {tabs.find(
                (item) => item.id === activeTab,
              )?.count === 0 && (
                <EmptyState text="Bu kateqoriyada nəticə tapılmadı." />
              )}
            </div>
          </>
        )}
      </main>

      <Footer />
    </div>
  );
}

function ResultLink({
  to,
  imageUrl,
  icon,
  title,
  eyebrow,
  description,
  rounded = false,
}: {
  to: string;
  imageUrl?: string | null;
  icon: ReactNode;
  title: string;
  eyebrow?: string;
  description: string;
  rounded?: boolean;
}) {
  const resolvedImageUrl = resolveAssetUrl(imageUrl);

  return (
    <Link
      to={to}
      className="flex items-center gap-4 rounded-2xl border border-white/[0.08] bg-[#111116] p-4 transition hover:border-violet-400/25 hover:bg-[#15131d]"
    >
      <span
        className={`flex shrink-0 items-center justify-center overflow-hidden border border-white/[0.08] bg-violet-500/10 text-violet-300 ${
          rounded
            ? "h-14 w-14 rounded-full"
            : "h-16 w-14 rounded-xl"
        }`}
      >
        {resolvedImageUrl ? (
          <img
            src={resolvedImageUrl}
            alt=""
            className="h-full w-full object-cover"
          />
        ) : (
          icon
        )}
      </span>

      <span className="min-w-0">
        <span className="block truncate font-bold text-white">
          {title}
        </span>
        {eyebrow && (
          <span className="mt-1 block text-xs font-semibold text-violet-300">
            {eyebrow}
          </span>
        )}
        <span className="mt-1 line-clamp-2 block text-sm text-zinc-500">
          {description}
        </span>
      </span>
    </Link>
  );
}

function EmptyState({ text }: { text: string }) {
  return (
    <div className="mt-10 rounded-3xl border border-white/[0.08] bg-white/[0.025] p-10 text-center text-zinc-500">
      {text}
    </div>
  );
}
