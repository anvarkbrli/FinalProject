import axios from "axios";
import {
  api,
  type ApiErrorResponse,
} from "../lib/api";
import type {
  FpsEstimateRequest,
  FpsEstimateResponse,
  FpsEstimatorError,
  FpsFormField,
  FpsHardwareProfile,
} from "../types/fpsEstimator";

const fieldNameMap: Record<string, FpsFormField> = {
  gpu: "gpu",
  cpu: "cpu",
  ramgb: "ramGb",
  vramgb: "vramGb",
  resolution: "resolution",
  graphicspreset: "graphicsPreset",
  upscaler: "upscaler",
  upscalermode: "upscalerMode",
  additionalnotes: "additionalNotes",
};

export async function estimateFps(
  request: FpsEstimateRequest,
  signal?: AbortSignal,
): Promise<FpsEstimateResponse> {
  const response =
    await api.post<FpsEstimateResponse>(
      "/fps-estimator/estimate",
      request,
      {
        signal,
        timeout: 90_000,
      },
    );

  return response.data;
}

export async function getFpsHardwareProfile(
  signal?: AbortSignal,
): Promise<FpsHardwareProfile> {
  const response =
    await api.get<FpsHardwareProfile>(
      "/fps-estimator/hardware-profile",
      { signal },
    );

  return response.data;
}

export function getFpsEstimatorError(
  error: unknown,
): FpsEstimatorError {
  if (!axios.isAxiosError<ApiErrorResponse>(error)) {
    return {
      message: "FPS hesablanarkən xəta baş verdi.",
      fieldErrors: {},
      canRetry: false,
    };
  }

  const status = error.response?.status;
  const responseData = error.response?.data;
  const serverMessage =
    responseData && typeof responseData === "object"
      ? responseData.message?.trim()
      : undefined;
  const fieldErrors: Partial<
    Record<FpsFormField, string>
  > = {};

  if (
    responseData &&
    typeof responseData === "object" &&
    responseData.errors
  ) {
    Object.entries(responseData.errors).forEach(
      ([rawFieldName, messages]) => {
        const normalizedName = rawFieldName
          .split(".")
          .at(-1)
          ?.replace(/[^a-z]/gi, "")
          .toLowerCase();
        const fieldName = normalizedName
          ? fieldNameMap[normalizedName]
          : undefined;
        const firstMessage = messages.find(Boolean);

        if (
          fieldName &&
          firstMessage &&
          !fieldErrors[fieldName]
        ) {
          fieldErrors[fieldName] = firstMessage;
        }
      },
    );
  }

  if (status === 401) {
    return {
      status,
      message:
        "FPS təxminindən istifadə etmək üçün hesabınıza daxil olun.",
      fieldErrors,
      canRetry: false,
    };
  }

  if (status === 403) {
    return {
      status,
      message:
        "Bu əməliyyat üçün icazəniz yoxdur.",
      fieldErrors,
      canRetry: false,
    };
  }

  if (status === 429) {
    return {
      status,
      message:
        serverMessage ||
        "AI xidmətinin sorğu limiti müvəqqəti dolub. Bir qədər sonra yenidən yoxlayın.",
      fieldErrors,
      canRetry: true,
    };
  }

  if (status === 503) {
    return {
      status,
      message:
        serverMessage ||
        "FPS təxmin xidməti hazırda əlçatan deyil. Bir qədər sonra yenidən yoxlayın.",
      fieldErrors,
      canRetry: true,
    };
  }

  if (
    !error.response ||
    error.code === "ECONNABORTED" ||
    error.code === "ETIMEDOUT"
  ) {
    return {
      status,
      message:
        "FPS təxmin xidməti ilə əlaqə yaratmaq mümkün olmadı.",
      fieldErrors,
      canRetry: true,
    };
  }

  if (
    status === 400 &&
    Object.keys(fieldErrors).length > 0
  ) {
    return {
      status,
      message:
        "Daxil etdiyiniz məlumatları yoxlayın.",
      fieldErrors,
      canRetry: false,
    };
  }

  return {
    status,
    message:
      status && status >= 500
        ? "FPS təxmin xidməti hazırda əlçatan deyil. Bir qədər sonra yenidən yoxlayın."
        : "FPS hesablanarkən xəta baş verdi.",
    fieldErrors,
    canRetry: Boolean(status && status >= 500),
  };
}
