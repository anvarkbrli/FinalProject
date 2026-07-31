import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { canAccessAdminPanel } from "../../lib/authRoles";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../../stores/authStore";

interface AdminRouteProps {
  children: ReactNode;
}

export function AdminRoute({ children }: AdminRouteProps) {
  const location = useLocation();
  const accessToken = useAuthStore((state) => state.accessToken);
  const expiresAtUtc = useAuthStore((state) => state.expiresAtUtc);
  const user = useAuthStore((state) => state.user);
  const isInitialized = useAuthStore((state) => state.isInitialized);

  if (!isInitialized) {
    return (
      <main className="grid min-h-screen place-items-center bg-[#09090d] text-zinc-300">
        Sessiya yoxlanılır...
      </main>
    );
  }

  if (!isAuthSessionValid(accessToken, expiresAtUtc) || !user) {
    return (
      <Navigate
        to="/login"
        replace
        state={{
          from: `${location.pathname}${location.search}${location.hash}`,
        }}
      />
    );
  }

  if (!canAccessAdminPanel(accessToken, user.roles ?? [])) {
    return <Navigate to="/forbidden" replace />;
  }

  return children;
}
