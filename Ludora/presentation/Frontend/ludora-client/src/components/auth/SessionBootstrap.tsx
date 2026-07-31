import { useEffect, type ReactNode } from "react";
import { restoreAuthSession } from "../../lib/api";
import { useAuthStore } from "../../stores/authStore";

interface SessionBootstrapProps {
  children: ReactNode;
}

export function SessionBootstrap({ children }: SessionBootstrapProps) {
  const isInitialized = useAuthStore((state) => state.isInitialized);
  const finishInitialization = useAuthStore(
    (state) => state.finishInitialization,
  );

  useEffect(() => {
    if (isInitialized) {
      return;
    }

    void restoreAuthSession().finally(finishInitialization);
  }, [finishInitialization, isInitialized]);

  return children;
}
