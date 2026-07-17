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
    const responseData = error.response?.data;

    if (responseData?.message) {
      return responseData.message;
    }

    if (responseData?.errors) {
      const firstValidationMessage =
        Object.values(responseData.errors)
          .flat()
          .find(Boolean);

      if (firstValidationMessage) {
        return firstValidationMessage;
      }
    }

    if (responseData?.title) {
      return responseData.title;
    }

    return fallbackMessage;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return fallbackMessage;
}