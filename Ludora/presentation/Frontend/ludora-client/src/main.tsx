import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import {
  QueryClient,
  QueryClientProvider,
} from "@tanstack/react-query";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import { SessionBootstrap } from "./components/auth/SessionBootstrap";
import { ConfirmationDialogProvider } from "./components/common/ConfirmationDialogProvider";
import { AppErrorBoundary } from "./components/errors/AppErrorBoundary";
import { I18nProvider } from "./i18n/I18nProvider";
import "./index.css";
import "./components/forum/forum.css";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 60_000,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

const rootElement = document.getElementById("root");

if (!rootElement) {
  throw new Error("Root elementi tapılmadı.");
}

createRoot(rootElement).render(
  <StrictMode>
    <AppErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <I18nProvider>
            <SessionBootstrap>
              <ConfirmationDialogProvider>
                <App />
              </ConfirmationDialogProvider>
            </SessionBootstrap>
          </I18nProvider>
        </BrowserRouter>
      </QueryClientProvider>
    </AppErrorBoundary>
  </StrictMode>,
);
