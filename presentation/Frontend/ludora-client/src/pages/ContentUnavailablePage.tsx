import { LockKeyhole, Trash2 } from "lucide-react";
import { useSearchParams } from "react-router-dom";
import { StatusPage } from "../components/status/StatusPage";

export function ContentUnavailablePage() {
  const [searchParams] = useSearchParams();
  const reason = searchParams.get("reason");

  if (reason === "private") {
    return (
      <StatusPage
        code="PRIVATE"
        title="Bu kontent gizlidir"
        description="Kontentin sahibi görünüşü məhdudlaşdırıb. İcazəniz olduqda və ya sahiblik ayarı dəyişdikdə bu səhifə əlçatan olacaq."
        icon={<LockKeyhole size={30} aria-hidden="true" />}
      />
    );
  }

  return (
    <StatusPage
      code="REMOVED"
      title="Bu kontent artıq mövcud deyil"
      description="Kontent sahibi və ya moderator tərəfindən silinib. Köhnə link artıq istifadə edilə bilməz."
      icon={<Trash2 size={30} aria-hidden="true" />}
    />
  );
}
