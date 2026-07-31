import { ShieldX } from "lucide-react";
import { StatusPage } from "../components/status/StatusPage";

export function ForbiddenPage() {
  return (
    <StatusPage
      code="403"
      title="Bu səhifəyə girişiniz yoxdur"
      description="Hesabınız bu əməliyyat üçün tələb olunan icazəyə sahib deyil. Fərqli hesabla daxil olun və ya əvvəlki səhifəyə qayıdın."
      icon={<ShieldX size={30} aria-hidden="true" />}
    />
  );
}
