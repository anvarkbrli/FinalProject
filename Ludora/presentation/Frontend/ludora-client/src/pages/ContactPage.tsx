import { useMutation } from "@tanstack/react-query";
import {
  Check,
  Clipboard,
  LoaderCircle,
  Mail,
  MessageSquareText,
  Send,
  ShieldAlert,
  Sparkles,
} from "lucide-react";
import { useMemo, useState } from "react";
import type { FormEvent } from "react";
import { Link } from "react-router-dom";
import { Footer } from "../components/layout/Footer";
import { Navbar } from "../components/layout/Navbar";
import { useI18n } from "../i18n/useI18n";
import { getApiErrorMessage } from "../lib/api";
import { submitContactMessage } from "../services/contactService";
import { useAuthStore } from "../stores/authStore";
import type { ContactMessageSubject } from "../types/contact";

interface ContactFormState {
  name: string;
  email: string;
  subject: ContactMessageSubject;
  message: string;
}

const initialFormState: ContactFormState = {
  name: "",
  email: "",
  subject: "general",
  message: "",
};

export function ContactPage() {
  const { language } = useI18n();
  const isEnglish = language === "en";
  const user = useAuthStore((state) => state.user);
  const [form, setForm] = useState<ContactFormState>(() => ({
    ...initialFormState,
    name: user?.userName ?? "",
    email: user?.email ?? "",
  }));
  const [copied, setCopied] = useState(false);



  const subjectOptions = useMemo(
    () => [
      { value: "general" as const, label: isEnglish ? "General question" : "Ümumi sual" },
      { value: "account" as const, label: isEnglish ? "Account and access" : "Hesab və giriş" },
      { value: "bug" as const, label: isEnglish ? "Bug report" : "Xəta bildirişi" },
      { value: "feedback" as const, label: isEnglish ? "Feedback and suggestion" : "Rəy və təklif" },
      { value: "moderation" as const, label: isEnglish ? "Safety and moderation" : "Təhlükəsizlik və moderasiya" },
    ],
    [isEnglish],
  );

  const selectedSubject = subjectOptions.find(
    (option) => option.value === form.subject,
  )?.label ?? subjectOptions[0].label;

  const submitMutation = useMutation({
    mutationFn: submitContactMessage,
    onSuccess: () => {
      setForm((current) => ({ ...current, message: "" }));
      setCopied(false);
    },
  });

  const buildMessage = () => {
    const lines = [
      `${isEnglish ? "Topic" : "Mövzu"}: ${selectedSubject}`,
      `${isEnglish ? "Name" : "Ad"}: ${form.name || "—"}`,
      `Email: ${form.email || "—"}`,
      "",
      form.message.trim(),
    ];

    return lines.join("\n");
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setCopied(false);
    submitMutation.mutate({
      name: form.name.trim(),
      email: form.email.trim(),
      subject: form.subject,
      message: form.message.trim(),
      language,
    });
  };

  const handleCopy = async () => {
    const value = buildMessage();

    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
    } catch {
      const textarea = document.createElement("textarea");
      textarea.value = value;
      textarea.style.position = "fixed";
      textarea.style.opacity = "0";
      document.body.appendChild(textarea);
      textarea.select();
      document.execCommand("copy");
      document.body.removeChild(textarea);
      setCopied(true);
    }
  };

  return (
    <div className="info-page min-h-screen overflow-x-hidden wellplayd-page-bg text-zinc-100">
      <Navbar />

      <main>
        <section className="info-page-hero relative overflow-hidden border-b border-white/[0.06]">
          <div className="info-page-aurora pointer-events-none absolute inset-0" />
          <div className="info-page-grid pointer-events-none absolute inset-0" />

          <div className="relative mx-auto max-w-7xl px-5 py-16 lg:px-8 lg:py-20">
            <div className="max-w-3xl">
              <div className="info-page-kicker inline-flex items-center gap-2 rounded-full border px-3 py-1.5 text-xs font-semibold text-violet-300">
                <Sparkles size={14} />
                {isEnglish ? "Contact WellPlayd" : "WellPlayd ilə əlaqə"}
              </div>

              <h1 className="mt-7 text-4xl font-bold tracking-[-0.04em] text-zinc-100 sm:text-5xl">
                {isEnglish ? "Questions, feedback or a problem?" : "Sualın, təklifin və ya problemin var?"}
              </h1>
              <p className="mt-5 max-w-2xl text-base leading-8 text-zinc-500">
                {isEnglish
                  ? "Send your message directly to the WellPlayd support inbox. Administrators and moderators can review it and reply to the email address you provide."
                  : "Mesajını birbaşa WellPlayd dəstək qutusuna göndər. Admin və moderatorlar müraciətə baxıb yazdığın email ünvanına cavab verə biləcəklər."}
              </p>
            </div>
          </div>
        </section>

        <section className="mx-auto max-w-7xl px-5 py-14 lg:px-8 lg:py-16">
          <div className="grid gap-5 lg:grid-cols-[0.72fr_1.28fr]">
            <aside className="space-y-4">
              <article className="contact-channel-card rounded-3xl border p-5">
                <span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-violet-500/10 text-violet-300">
                  <MessageSquareText size={20} />
                </span>
                <h2 className="mt-4 text-lg font-bold text-zinc-100">
                  {isEnglish ? "Community help" : "İcma dəstəyi"}
                </h2>
                <p className="mt-2 text-sm leading-7 text-zinc-500">
                  {isEnglish
                    ? "Questions about games, lists and platform use can also be discussed openly with the community."
                    : "Oyunlar, siyahılar və platformadan istifadə ilə bağlı sualları icmada açıq müzakirə də edə bilərsən."}
                </p>
                <Link
                  to="/forum"
                  className="mt-5 inline-flex items-center gap-2 text-sm font-semibold text-violet-300 transition hover:text-violet-200"
                >
                  {isEnglish ? "Open the forum" : "Forumu aç"}
                </Link>
              </article>

              <article className="contact-channel-card rounded-3xl border p-5">
                <span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-emerald-500/10 text-emerald-300">
                  <ShieldAlert size={20} />
                </span>
                <h2 className="mt-4 text-lg font-bold text-zinc-100">
                  {isEnglish ? "Safety matters" : "Təhlükəsizlik məsələləri"}
                </h2>
                <p className="mt-2 text-sm leading-7 text-zinc-500">
                  {isEnglish
                    ? "Use the in-platform report tools for harmful content or users. They preserve the relevant context for moderators."
                    : "Zərərli məzmun və ya istifadəçi üçün platformadakı şikayət alətlərindən istifadə et. Onlar moderatorlar üçün vacib konteksti saxlayır."}
                </p>
              </article>

              <article className="contact-channel-card rounded-3xl border p-5">
                <span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-fuchsia-500/10 text-fuchsia-300">
                  <Mail size={20} />
                </span>
                <h2 className="mt-4 text-lg font-bold text-zinc-100">
                  {isEnglish ? "Email reply" : "Email cavabı"}
                </h2>
                <p className="mt-2 text-sm leading-7 text-zinc-500">
                  {isEnglish
                    ? "When support replies, the response is sent to the email address entered in this form."
                    : "Dəstək komandası cavab verdikdə cavab bu formada yazdığın email ünvanına göndərilir."}
                </p>
              </article>
            </aside>

            <section className="contact-form-card rounded-[28px] border p-5 sm:p-7">
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.18em] text-violet-300">
                  {isEnglish ? "Send a support message" : "Dəstək mesajı göndər"}
                </p>
                <h2 className="mt-2 text-2xl font-bold tracking-tight text-zinc-100">
                  {isEnglish ? "Tell us what happened." : "Nə baş verdiyini yaz."}
                </h2>
                <p className="mt-2 text-sm leading-6 text-zinc-500">
                  {isEnglish
                    ? "Your message is saved in the admin support inbox. Include an email address you can access so the team can reply."
                    : "Mesajın admin dəstək qutusunda saxlanılır. Komandanın cavab verə bilməsi üçün istifadə etdiyin email ünvanını yaz."}
                </p>
              </div>

              <form className="mt-7 space-y-5" onSubmit={handleSubmit}>
                <div className="grid gap-4 sm:grid-cols-2">
                  <label className="contact-field-label">
                    <span>{isEnglish ? "Name" : "Ad"}</span>
                    <input
                      value={form.name}
                      onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))}
                      className="contact-field mt-2 w-full rounded-xl border px-4 py-3 text-sm outline-none transition"
                      placeholder={isEnglish ? "Your name" : "Adın"}
                      autoComplete="name"
                      maxLength={120}
                      required
                    />
                  </label>

                  <label className="contact-field-label">
                    <span>Email</span>
                    <input
                      type="email"
                      value={form.email}
                      onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
                      className="contact-field mt-2 w-full rounded-xl border px-4 py-3 text-sm outline-none transition"
                      placeholder="name@example.com"
                      autoComplete="email"
                      maxLength={320}
                      required
                    />
                  </label>
                </div>

                <label className="contact-field-label block">
                  <span>{isEnglish ? "Topic" : "Mövzu"}</span>
                  <select
                    value={form.subject}
                    onChange={(event) => setForm((current) => ({
                      ...current,
                      subject: event.target.value as ContactMessageSubject,
                    }))}
                    className="contact-field mt-2 w-full rounded-xl border px-4 py-3 text-sm outline-none transition"
                  >
                    {subjectOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>

                <label className="contact-field-label block">
                  <span>{isEnglish ? "Message" : "Mesaj"}</span>
                  <textarea
                    value={form.message}
                    onChange={(event) => setForm((current) => ({ ...current, message: event.target.value }))}
                    className="contact-field mt-2 min-h-44 w-full resize-y rounded-xl border px-4 py-3 text-sm leading-6 outline-none transition"
                    placeholder={isEnglish ? "Include the important details..." : "Vacib detalları qeyd et..."}
                    minLength={10}
                    maxLength={5000}
                    required
                  />
                  <span className="mt-2 block text-right text-xs text-zinc-600">
                    {form.message.length}/5000
                  </span>
                </label>

                {submitMutation.isSuccess && (
                  <div className="rounded-xl border border-emerald-400/15 bg-emerald-500/[0.07] px-4 py-3 text-sm text-emerald-300">
                    <div className="flex items-center gap-2 font-semibold">
                      <Check size={17} />
                      {isEnglish ? "Your message was sent." : "Mesajın göndərildi."}
                    </div>
                    <p className="mt-1 text-xs leading-5 text-emerald-300/80">
                      {isEnglish
                        ? `Reference number: #${submitMutation.data.id}. A reply will be sent to ${form.email}.`
                        : `Müraciət nömrəsi: #${submitMutation.data.id}. Cavab ${form.email} ünvanına göndəriləcək.`}
                    </p>
                  </div>
                )}

                {submitMutation.isError && (
                  <div className="rounded-xl border border-red-400/15 bg-red-500/[0.07] px-4 py-3 text-sm leading-6 text-red-300">
                    {getApiErrorMessage(
                      submitMutation.error,
                      isEnglish
                        ? "The message could not be sent. Please try again."
                        : "Mesaj göndərilmədi. Yenidən cəhd et.",
                    )}
                  </div>
                )}

                {copied && (
                  <div className="flex items-center gap-2 rounded-xl border border-violet-400/15 bg-violet-500/[0.07] px-4 py-3 text-sm text-violet-300">
                    <Check size={17} />
                    {isEnglish ? "Message copied." : "Mesaj kopyalandı."}
                  </div>
                )}

                <div className="flex flex-col gap-3 sm:flex-row">
                  <button
                    type="submit"
                    disabled={submitMutation.isPending}
                    className="contact-primary-action inline-flex items-center justify-center gap-2 rounded-xl px-5 py-3 text-sm font-semibold text-white transition disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    {submitMutation.isPending ? (
                      <LoaderCircle size={16} className="animate-spin" />
                    ) : (
                      <Send size={16} />
                    )}
                    {submitMutation.isPending
                      ? isEnglish ? "Sending..." : "Göndərilir..."
                      : isEnglish ? "Send message" : "Mesajı göndər"}
                  </button>
                  <button
                    type="button"
                    onClick={handleCopy}
                    className="contact-secondary-action inline-flex items-center justify-center gap-2 rounded-xl border px-5 py-3 text-sm font-semibold text-zinc-300 transition"
                  >
                    <Clipboard size={16} />
                    {isEnglish ? "Copy message" : "Mesajı kopyala"}
                  </button>
                </div>
              </form>
            </section>
          </div>
        </section>
      </main>

      <Footer />
    </div>
  );
}
