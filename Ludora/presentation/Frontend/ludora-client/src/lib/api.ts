import axios from "axios";
import { useAuthStore } from "../stores/authStore";

const baseURL = import.meta.env.VITE_API_BASE_URL;

if (!baseURL) {
  throw new Error(
    "VITE_API_BASE_URL tapılmadı. .env.development faylını yoxla.",
  );
}

export interface ApiErrorResponse {
  statusCode?: number;
  message?: string;
  title?: string;
  errors?: Record<string, string[]>;
}

export function getApiFieldErrors(
  error: unknown,
): Record<string, string[]> {
  if (!axios.isAxiosError<ApiErrorResponse>(error)) {
    return {};
  }

  const responseData = error.response?.data;

  if (
    !responseData ||
    typeof responseData !== "object" ||
    !responseData.errors
  ) {
    return {};
  }

  return responseData.errors;
}

export const api = axios.create({
  baseURL,
  timeout: 15_000,
  headers: {
    "Content-Type": "application/json",
  },
});

api.interceptors.request.use((config) => {
  const accessToken =
    useAuthStore.getState().accessToken;

  if (accessToken) {
    config.headers.set(
      "Authorization",
      `Bearer ${accessToken}`,
    );
  }

  return config;
});

api.interceptors.response.use(
  (response) => response,

  (error: unknown) => {
    if (
      axios.isAxiosError(error) &&
      error.response?.status === 401 &&
      useAuthStore.getState().accessToken
    ) {
      useAuthStore.getState().clearSession();
    }

    return Promise.reject(error);
  },
);

export function getApiErrorMessage(
  error: unknown,
  fallbackMessage = "Sorğu yerinə yetirilərkən xəta baş verdi.",
): string {
  if (axios.isAxiosError<ApiErrorResponse>(error)) {
    const responseData = error.response?.data as
      | ApiErrorResponse
      | string
      | undefined;

    if (
      typeof responseData === "string" &&
      responseData.trim()
    ) {
      return responseData.trim();
    }

    const apiError =
      typeof responseData === "object"
        ? responseData
        : undefined;

    if (apiError?.message) {
      return apiError.message;
    }

    if (apiError?.errors) {
      const firstValidationMessage =
        Object.values(apiError.errors)
          .flat()
          .find(Boolean);

      if (firstValidationMessage) {
        return firstValidationMessage;
      }
    }

    if (apiError?.title) {
      return apiError.title;
    }

    if (error.response?.status === 429) {
      return "Çox sayda sorğu göndərmisiniz. Bir qədər sonra yenidən cəhd edin.";
    }

    if (!error.response) {
      return "Serverlə əlaqə yaratmaq mümkün olmadı. API-nin işlədiyini yoxlayın.";
    }

    return fallbackMessage;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return fallbackMessage;
}
