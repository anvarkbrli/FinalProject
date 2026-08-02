import { Check, Share2, X } from "lucide-react";
import { useEffect, useState } from "react";
import { useI18n } from "../../i18n/useI18n";

interface CopyShareButtonProps {
  path?: string;
  iconOnly?: boolean;
  className?: string;
  labelClassName?: string;
}

type CopyState = "idle" | "copied" | "error";

function buildShareUrl(path?: string) {
  if (!path) return window.location.href;

  try {
    return new URL(path, window.location.origin).toString();
  } catch {
    return window.location.href;
  }
}

async function copyText(value: string) {
  if (navigator.clipboard && window.isSecureContext) {
    await navigator.clipboard.writeText(value);
    return;
  }

  const input = document.createElement("textarea");
  input.value = value;
  input.setAttribute("readonly", "");
  input.style.position = "fixed";
  input.style.left = "-9999px";
  input.style.opacity = "0";
  document.body.appendChild(input);
  input.select();

  const copied = document.execCommand("copy");
  input.remove();

  if (!copied) {
    throw new Error("Link could not be copied.");
  }
}

export function CopyShareButton({
  path,
  iconOnly = false,
  className = "",
  labelClassName = "",
}: CopyShareButtonProps) {
  const { t } = useI18n();
  const [copyState, setCopyState] = useState<CopyState>("idle");

  useEffect(() => {
    if (copyState === "idle") return;
    const timeoutId = window.setTimeout(() => setCopyState("idle"), 1800);
    return () => window.clearTimeout(timeoutId);
  }, [copyState]);

  const label =
    copyState === "copied"
      ? t("common:share.copied")
      : copyState === "error"
        ? t("common:share.error")
        : t("common:share.action");
  const Icon = copyState === "copied" ? Check : copyState === "error" ? X : Share2;

  return (
    <button
      type="button"
      onClick={async () => {
        try {
          await copyText(buildShareUrl(path));
          setCopyState("copied");
        } catch {
          setCopyState("error");
        }
      }}
      className={className}
      aria-label={label}
      title={label}
    >
      <Icon size={16} />
      {!iconOnly && <span className={labelClassName}>{label}</span>}
    </button>
  );
}
