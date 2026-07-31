import axios from "axios";
import {
  AlertTriangle,
  LockKeyhole,
  SearchX,
  Trash2,
} from "lucide-react";
import { StatusPage } from "./StatusPage";

interface ApiErrorStatusPageProps {
  error: unknown;
  entityName: string;
}

export function ApiErrorStatusPage({
  error,
  entityName,
}: ApiErrorStatusPageProps) {
  const status = axios.isAxiosError(error)
    ? error.response?.status
    : undefined;

  if (status === 403) {
    return (
      <StatusPage
        code="403"
        title={`${entityName} gizlidir`}
        description="Bu kontentin görünüşü məhdudlaşdırılıb və hesabınızın ona baxmaq icazəsi yoxdur."
        icon={<LockKeyhole size={30} aria-hidden="true" />}
      />
    );
  }

  if (status === 410) {
    return (
      <StatusPage
        code="REMOVED"
        title={`${entityName} artıq mövcud deyil`}
        description="Kontent sahibi və ya moderator tərəfindən silinib. Bu köhnə link artıq istifadə edilə bilməz."
        icon={<Trash2 size={30} aria-hidden="true" />}
      />
    );
  }

  if (status === 404 || error == null) {
    return (
      <StatusPage
        code="404"
        title={`${entityName} tapılmadı`}
        description="Link səhv ola, kontent köçürülə və ya artıq mövcud olmaya bilər."
        icon={<SearchX size={30} aria-hidden="true" />}
      />
    );
  }

  return (
    <StatusPage
      code={status ? String(status) : "ERROR"}
      title={`${entityName} açıla bilmədi`}
      description="Məlumatları yükləmək mümkün olmadı. Əvvəlki səhifəyə qayıdıb bir qədər sonra yenidən yoxlayın."
      icon={<AlertTriangle size={30} aria-hidden="true" />}
    />
  );
}
