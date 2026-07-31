import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../../stores/authStore";

interface ProtectedRouteProps {
  children: ReactNode;
}

export function ProtectedRoute({ children }: ProtectedRouteProps) {
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

  const hasValidSession =
    isAuthSessionValid(accessToken, expiresAtUtc) && user !== null;

  if (!hasValidSession) {
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

  return children;
}
