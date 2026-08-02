import { useEffect, useLayoutEffect, type ReactNode } from "react";
import { restoreAuthSession } from "../../lib/api";
import { applyTheme } from "../../lib/theme";
import { useAuthStore } from "../../stores/authStore";

interface SessionBootstrapProps {
  children: ReactNode;
}

export function SessionBootstrap({ children }: SessionBootstrapProps) {
  const isInitialized = useAuthStore((state) => state.isInitialized);
  const finishInitialization = useAuthStore(
    (state) => state.finishInitialization,
  );
  const preferredTheme = useAuthStore(
    (state) => state.user?.preferredTheme,
  );

  useLayoutEffect(() => {
    if (preferredTheme === "light" || preferredTheme === "dark") {
      applyTheme(preferredTheme);
    }
  }, [preferredTheme]);

  useEffect(() => {
    if (isInitialized) {
      return;
    }

    void restoreAuthSession().finally(finishInitialization);
  }, [finishInitialization, isInitialized]);

  return children;
}
