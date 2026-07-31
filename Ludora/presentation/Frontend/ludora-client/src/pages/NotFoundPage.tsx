import { SearchX } from "lucide-react";
import { StatusPage } from "../components/status/StatusPage";

export function NotFoundPage() {
  return (
    <StatusPage
      code="404"
      title="Bu səhifə tapılmadı"
      description="Link səhv ola, səhifə köçürülə və ya kontent artıq mövcud olmaya bilər. Ünvanı yoxlayın və ya ana səhifədən davam edin."
      icon={<SearchX size={30} aria-hidden="true" />}
    />
  );
}
