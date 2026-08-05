import axios, {
  AxiosHeaders,
  type InternalAxiosRequestConfig,
} from "axios";
import { getAcceptLanguage, t } from "../i18n/localization";
import { useAuthStore } from "../stores/authStore";
import type { AuthResponseDTO } from "../types/auth";

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
  traceId?: string;
  retryAfterSeconds?: number;
  errors?: Record<string, string[]>;
}

interface RetryableRequestConfig extends InternalAxiosRequestConfig {
  _authRetry?: boolean;
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
  withCredentials: true,
  headers: {
    "Content-Type": "application/json",
  },
});

const authTransport = axios.create({
  baseURL,
  timeout: 15_000,
  withCredentials: true,
  headers: {
    "Content-Type": "application/json",
    "X-WellPlayd-CSRF": "1",
  },
});

let refreshRequest: Promise<AuthResponseDTO> | null = null;

async function requestSessionRefresh(): Promise<AuthResponseDTO> {
  if (!refreshRequest) {
    refreshRequest = authTransport
      .post<AuthResponseDTO>("/auth/refresh", undefined, {
        headers: {
          "Accept-Language": getAcceptLanguage(),
        },
      })
      .then((response) => response.data)
      .finally(() => {
        refreshRequest = null;
      });
  }

  return refreshRequest;
}

export async function restoreAuthSession(): Promise<AuthResponseDTO | null> {
  try {
    const response = await requestSessionRefresh();
    useAuthStore.getState().setSession(response);
    return response;
  } catch {
    useAuthStore.getState().clearSession();
    return null;
  }
}

export async function endServerSession(): Promise<void> {
  try {
    await authTransport.post("/auth/logout", undefined, {
      headers: {
        "Accept-Language": getAcceptLanguage(),
      },
    });
  } finally {
    useAuthStore.getState().clearSession();
  }
}

api.interceptors.request.use((config) => {
  config.headers.set("Accept-Language", getAcceptLanguage());
  const accessToken = useAuthStore.getState().accessToken;

  if (accessToken) {
    config.headers.set("Authorization", `Bearer ${accessToken}`);
  }

  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error: unknown) => {
    if (!axios.isAxiosError(error) || error.response?.status !== 401) {
      return Promise.reject(error);
    }

    const originalRequest = error.config as
      | RetryableRequestConfig
      | undefined;
    const requestUrl = originalRequest?.url ?? "";
    const isAuthRequest = requestUrl.startsWith("/auth/");

    if (!originalRequest || originalRequest._authRetry || isAuthRequest) {
      if (!isAuthRequest) {
        useAuthStore.getState().clearSession();
      }

      return Promise.reject(error);
    }

    originalRequest._authRetry = true;

    try {
      const session = await requestSessionRefresh();
      useAuthStore.getState().setSession(session);

      if (!(originalRequest.headers instanceof AxiosHeaders)) {
        originalRequest.headers = new AxiosHeaders(originalRequest.headers);
      }

      originalRequest.headers.set(
        "Authorization",
        `Bearer ${session.accessToken}`,
      );

      return api(originalRequest);
    } catch {
      useAuthStore.getState().clearSession();
      return Promise.reject(error);
    }
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
    const status = error.response?.status;

    const rawMessage =
      typeof responseData === "string"
        ? responseData.trim()
        : responseData?.message?.trim() || responseData?.title?.trim();

    if (
      status === 403 &&
      (!rawMessage ||
        /^(?:forbidden\.?|bu (?:profil|hesab) gizlidir\.?|this (?:profile|account) is private\.?)$/i.test(
          rawMessage,
        ))
    ) {
      return t("profile:private");
    }

    const firstValidationMessage =
      typeof responseData === "object" && responseData?.errors
        ? Object.values(responseData.errors).flat().find(Boolean)
        : undefined;

    const isValidationSummary =
      !!rawMessage &&
      /^(?:göndərilən məlumatlarda səhv var(?:\. zəhmət olmasa sahələri yoxlayın)?|one or more validation errors occurred)\.?$/i.test(
        rawMessage,
      );

    if (firstValidationMessage && (!rawMessage || isValidationSummary)) {
      return firstValidationMessage;
    }

    const isTechnicalMessage =
      !rawMessage ||
      /^(?:bad request|unauthorized|forbidden|not found|conflict|internal server error|network error|request failed with status code \d+|errors\.[a-z0-9_.-]+)\.?$/i.test(
        rawMessage,
      );

    if (!isTechnicalMessage) {
      return rawMessage;
    }

    if (firstValidationMessage) {
      return firstValidationMessage;
    }

    if (!error.response) {
      return "Serverlə əlaqə yaratmaq mümkün olmadı. İnternet bağlantısını yoxlayıb yenidən cəhd edin.";
    }

    switch (status) {
      case 400:
        return fallbackMessage === "Sorğu yerinə yetirilərkən xəta baş verdi."
          ? "Göndərilən məlumatları yoxlayın."
          : fallbackMessage;
      case 401:
        return "Sessiyanın vaxtı bitib və ya giriş məlumatları etibarlı deyil. Yenidən daxil olun.";
      case 403:
        return "Bu əməliyyat üçün icazəniz yoxdur.";
      case 404:
        return fallbackMessage;
      case 409:
        return "Bu əməliyyat mövcud məlumatla ziddiyyət yaradır.";
      case 429:
        return "Çox sayda sorğu göndərmisiniz. Bir qədər sonra yenidən cəhd edin.";
      case 503:
        return "Xidmət müvəqqəti olaraq əlçatan deyil. Bir qədər sonra yenidən cəhd edin.";
      default:
        return fallbackMessage;
    }
  }

  if (error instanceof Error) {
    const message = error.message.trim();
    if (
      message &&
      !/^(?:bad request|unauthorized|forbidden|not found|network error|request failed with status code \d+)\.?$/i.test(
        message,
      )
    ) {
      return message;
    }
  }

  return fallbackMessage;
}
