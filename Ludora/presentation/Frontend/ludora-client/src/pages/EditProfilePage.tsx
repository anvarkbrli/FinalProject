import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  Check,
  ImagePlus,
  Link2,
  LoaderCircle,
  Plus,
  Save,
  Trash2,
  UserRoundPen,
  X,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { SocialPlatformIcon } from "../components/profile/SocialPlatformIcon";
import {
  detectSocialPlatform,
  socialPlatformLabels,
} from "../lib/socialPlatforms";
import { getApiErrorMessage } from "../lib/api";
import { resolveAssetUrl } from "../lib/helpers";
import {
  getCurrentUser,
  updateProfile,
  updateProfileImage,
} from "../services/settingsService";
import { useAuthStore } from "../stores/authStore";

interface NoticeState {
  type: "success" | "error";
  message: string;
}

function useObjectUrl(file: File | null): string | null {
  const objectUrl = useMemo(
    () => (file ? URL.createObjectURL(file) : null),
    [file],
  );

  useEffect(() => {
    return () => {
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [objectUrl]);

  return objectUrl;
}

export function EditProfilePage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const storedUser = useAuthStore((state) => state.user);
  const setUser = useAuthStore((state) => state.setUser);
  const [userName, setUserName] = useState(() => storedUser?.userName ?? "");
  const [bio, setBio] = useState(() => storedUser?.bio ?? "");
  const [socialLinks, setSocialLinks] = useState<string[]>(() =>
    storedUser?.socialLinks?.length
      ? storedUser.socialLinks.map((link) => link.url)
      : [""],
  );
  const [image, setImage] = useState<File | null>(null);
  const [notice, setNotice] = useState<NoticeState | null>(null);
  const imagePreview = useObjectUrl(image);

  const userQuery = useQuery({
    queryKey: ["current-user"],
    queryFn: getCurrentUser,
    initialData: storedUser ?? undefined,
  });

  const saveMutation = useMutation({
    mutationFn: async () => {
      const normalizedUserName = userName.trim();
      const normalizedLinks = socialLinks
        .map((link) => link.trim())
        .filter(Boolean);

      if (normalizedUserName.length < 3 || normalizedUserName.length > 30) {
        throw new Error("İstifadəçi adı 3-30 simvol aralığında olmalıdır.");
      }

      if (!/^[a-zA-Z0-9_.]+$/.test(normalizedUserName)) {
        throw new Error("İstifadəçi adında yalnız hərf, rəqəm, nöqtə və alt xətt ola bilər.");
      }

      if (bio.trim().length > 500) {
        throw new Error("Bio maksimum 500 simvol ola bilər.");
      }

      if (normalizedLinks.length > 8) {
        throw new Error("Maksimum 8 sosial link əlavə etmək olar.");
      }

      for (const link of normalizedLinks) {
        if (!detectSocialPlatform(link)) {
          throw new Error(`Sosial link tanınmadı: ${link}`);
        }
      }

      let updatedUser = await updateProfile({
        userName: normalizedUserName,
        bio: bio.trim() || null,
        socialLinks: normalizedLinks,
      });

      if (image) {
        if (image.size > 5 * 1024 * 1024) {
          throw new Error("Profil şəkli maksimum 5 MB ola bilər.");
        }

        updatedUser = await updateProfileImage(image);
      }

      return updatedUser;
    },
    onSuccess: async (updatedUser) => {
      setUser(updatedUser);
      setImage(null);
      setNotice({
        type: "success",
        message: "Profil məlumatları uğurla yeniləndi.",
      });

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["current-user"] }),
        queryClient.invalidateQueries({ queryKey: ["user-profile"] }),
      ]);

      if (updatedUser.userName !== storedUser?.userName) {
        navigate(`/users/${updatedUser.userName}`, { replace: true });
      }
    },
    onError: (error) => {
      setNotice({
        type: "error",
        message: error instanceof Error && !('response' in error)
          ? error.message
          : getApiErrorMessage(error, "Profil məlumatları yenilənmədi."),
      });
    },
  });

  const profileImageUrl = imagePreview ?? resolveAssetUrl(userQuery.data?.profileImageUrl ?? null);

  if (userQuery.isLoading || !userQuery.data) {
    return (
      <div className="min-h-screen wellplayd-page-bg text-zinc-100">
        <Navbar />
        <main className="flex min-h-[70vh] items-center justify-center gap-3 text-sm text-zinc-500">
          <LoaderCircle size={20} className="animate-spin" />
          Profil redaktəsi yüklənir...
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen wellplayd-page-bg text-zinc-100">
      <Navbar />
      <main className="mx-auto max-w-5xl px-5 py-10 lg:px-8 lg:py-14">
        <Link
          to={`/users/${userQuery.data.userName}`}
          className="inline-flex items-center gap-2 text-sm font-semibold text-zinc-500 transition hover:text-violet-300"
        >
          <ArrowLeft size={16} /> Profilə qayıt
        </Link>

        <div className="mt-6 flex items-start gap-4">
          <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-violet-500/12 text-violet-300">
            <UserRoundPen size={22} />
          </span>
          <div>
            <h1 className="text-3xl font-bold tracking-tight text-white">Profil redaktəsi</h1>
            <p className="mt-2 text-sm leading-6 text-zinc-500">
              İctimai profil məlumatlarını və sosial keçidlərini burada idarə et.
            </p>
          </div>
        </div>

        {notice && (
          <div className={`mt-6 flex items-start gap-3 rounded-2xl border px-4 py-3 text-sm ${
            notice.type === "success"
              ? "border-emerald-400/15 bg-emerald-500/[0.06] text-emerald-200"
              : "border-red-400/15 bg-red-500/[0.06] text-red-200"
          }`}>
            {notice.type === "success" ? <Check size={18} /> : <X size={18} />}
            {notice.message}
          </div>
        )}

        <div className="mt-7 grid gap-6 lg:grid-cols-[0.78fr_1.4fr]">
          <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
            <h2 className="font-semibold text-white">Profil şəkli</h2>
            <div className="mt-5 flex flex-col items-center">
              <div className="flex h-36 w-36 items-center justify-center overflow-hidden rounded-[2rem] border border-white/[0.09] bg-violet-500/10 text-4xl font-bold text-violet-300">
                {profileImageUrl ? (
                  <img src={profileImageUrl} alt="Profil şəkli" className="h-full w-full object-cover" />
                ) : (
                  userQuery.data.userName.charAt(0).toUpperCase()
                )}
              </div>
              <label className="mt-5 inline-flex h-11 cursor-pointer items-center gap-2 rounded-xl border border-white/[0.09] bg-white/[0.04] px-4 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.08] hover:text-white">
                <ImagePlus size={17} /> Şəkli dəyiş
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  className="hidden"
                  onChange={(event) => {
                    setImage(event.target.files?.[0] ?? null);
                    event.currentTarget.value = "";
                  }}
                />
              </label>
              <p className="mt-3 text-center text-xs leading-5 text-zinc-600">
                JPG, PNG və ya WEBP. Maksimum 5 MB.
              </p>
            </div>
          </section>

          <div className="space-y-6">
            <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
              <h2 className="font-semibold text-white">Əsas məlumatlar</h2>
              <div className="mt-5 space-y-5">
                <label className="block">
                  <span className="text-sm font-semibold text-zinc-300">İstifadəçi adı</span>
                  <input
                    value={userName}
                    onChange={(event) => setUserName(event.target.value)}
                    maxLength={30}
                    className="mt-2 h-12 w-full rounded-xl border border-white/[0.08] bg-white/[0.03] px-4 text-sm text-zinc-100 outline-none transition focus:border-violet-400/50 focus:ring-4 focus:ring-violet-500/10"
                  />
                </label>
                <label className="block">
                  <span className="text-sm font-semibold text-zinc-300">Bioqrafiya</span>
                  <textarea
                    value={bio}
                    onChange={(event) => setBio(event.target.value)}
                    maxLength={500}
                    rows={6}
                    className="mt-2 w-full resize-none rounded-xl border border-white/[0.08] bg-white/[0.03] px-4 py-3 text-sm leading-7 text-zinc-100 outline-none transition focus:border-violet-400/50 focus:ring-4 focus:ring-violet-500/10"
                  />
                  <span className="mt-1 block text-right text-xs text-zinc-600">{bio.length}/500</span>
                </label>
              </div>
            </section>

            <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h2 className="font-semibold text-white">Sosial linklər</h2>
                  <p className="mt-1 text-xs leading-5 text-zinc-600">
                    Linki daxil et; platforma avtomatik tanınıb uyğun ikon göstəriləcək.
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => {
                    if (socialLinks.length < 8) setSocialLinks((current) => [...current, ""]);
                  }}
                  disabled={socialLinks.length >= 8}
                  className="flex h-10 items-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.035] px-3 text-xs font-semibold text-zinc-300 transition hover:text-white disabled:opacity-40"
                >
                  <Plus size={15} /> Əlavə et
                </button>
              </div>

              <div className="mt-5 space-y-3">
                {socialLinks.map((link, index) => {
                  const platform = detectSocialPlatform(link);
                  return (
                    <div key={index} className="flex items-center gap-2">
                      <span className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl border ${
                        platform
                          ? "border-violet-400/20 bg-violet-500/10 text-violet-300"
                          : "border-white/[0.07] bg-white/[0.03] text-zinc-600"
                      }`} title={platform ? socialPlatformLabels[platform] : "Platforma gözlənilir"}>
                        {platform ? <SocialPlatformIcon platform={platform} /> : <Link2 size={18} />}
                      </span>
                      <input
                        value={link}
                        onChange={(event) => {
                          const value = event.target.value;
                          setSocialLinks((current) => current.map((item, itemIndex) => itemIndex === index ? value : item));
                        }}
                        placeholder="https://steamcommunity.com/id/..."
                        className="h-11 min-w-0 flex-1 rounded-xl border border-white/[0.08] bg-white/[0.03] px-3 text-sm text-zinc-100 outline-none transition focus:border-violet-400/50"
                      />
                      <button
                        type="button"
                        onClick={() => {
                          setSocialLinks((current) => {
                            const next = current.filter((_, itemIndex) => itemIndex !== index);
                            return next.length > 0 ? next : [""];
                          });
                        }}
                        className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl border border-red-400/10 bg-red-500/[0.04] text-red-300 transition hover:bg-red-500/10"
                        aria-label="Sosial linki sil"
                      >
                        <Trash2 size={17} />
                      </button>
                    </div>
                  );
                })}
              </div>
            </section>

            <button
              type="button"
              onClick={() => {
                setNotice(null);
                saveMutation.mutate();
              }}
              disabled={saveMutation.isPending}
              className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:opacity-50"
            >
              {saveMutation.isPending ? <LoaderCircle size={18} className="animate-spin" /> : <Save size={18} />}
              Dəyişiklikləri yadda saxla
            </button>
          </div>
        </div>
      </main>
      <Footer />
    </div>
  );
}
