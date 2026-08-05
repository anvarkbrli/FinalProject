import { useQuery } from "@tanstack/react-query";
import { LoaderCircle } from "lucide-react";
import { useState } from "react";
import { useI18n } from "../../i18n/useI18n";
import {
  getGoogleAvailability,
  getGoogleLoginStartUrl,
} from "../../services/authService";
import { GoogleIcon } from "./GoogleIcon";

interface GoogleSignInButtonProps {
  returnPath?: string;
  className?: string;
}

export function GoogleSignInButton({
  returnPath = "/",
  className = "",
}: GoogleSignInButtonProps) {
  const { language } = useI18n();
  const [isRedirecting, setIsRedirecting] = useState(false);
  const availabilityQuery = useQuery({
    queryKey: ["auth", "google", "availability"],
    queryFn: getGoogleAvailability,
    staleTime: 10 * 60 * 1000,
    retry: false,
  });

  if (!availabilityQuery.data?.enabled) {
    return null;
  }

  return (
    <button
      type="button"
      disabled={isRedirecting}
      onClick={() => {
        setIsRedirecting(true);
        window.location.assign(getGoogleLoginStartUrl(returnPath));
      }}
      className={`flex h-12 w-full items-center justify-center gap-3 rounded-xl border border-white/[0.1] bg-white/[0.035] px-4 text-sm font-semibold text-zinc-200 transition hover:border-white/[0.18] hover:bg-white/[0.06] disabled:cursor-wait disabled:opacity-60 ${className}`}
    >
      {isRedirecting ? (
        <LoaderCircle size={18} className="animate-spin" />
      ) : (
        <GoogleIcon className="h-5 w-5" />
      )}
      {language === "en" ? "Continue with Google" : "Google ilə davam et"}
    </button>
  );
}
