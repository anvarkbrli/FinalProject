export type FpsResolution =
  | "1280x720"
  | "1920x1080"
  | "2560x1440"
  | "3440x1440"
  | "3840x2160";

export type FpsGraphicsPreset =
  | "Low"
  | "Medium"
  | "High"
  | "Ultra"
  | "Custom";

export type FpsUpscaler =
  | "None"
  | "DLSS"
  | "FSR"
  | "XeSS";

export type FpsUpscalerMode =
  | "Native"
  | "Quality"
  | "Balanced"
  | "Performance"
  | "Ultra Performance";

export interface FpsEstimateRequest {
  language: "az" | "en";
  gameId?: number;
  gameName: string;
  gameReleaseYear?: number;
  gpu: string;
  cpu: string;
  ramGb: number;
  vramGb?: number;
  resolution: FpsResolution;
  graphicsPreset: FpsGraphicsPreset;
  rayTracingEnabled: boolean;
  upscaler: FpsUpscaler;
  upscalerMode: FpsUpscalerMode;
  frameGenerationEnabled: boolean;
  additionalNotes?: string;
  rememberHardware?: boolean;
}

export type FpsEvidenceQuality =
  | "Exact"
  | "Close"
  | "Inferred"
  | "Insufficient";

export interface FpsBenchmarkSource {
  title: string;
  url: string;
  publishedDate: string | null;
}

export interface FpsEstimateResponse {
  normalizedGameName: string;
  normalizedGpuName: string;
  normalizedCpuName: string;
  resolution: string;
  graphicsPreset: string;
  averageFps: number;
  onePercentLowFps: number;
  minimumExpectedFps: number;
  maximumExpectedFps: number;
  confidencePercent: number;
  performanceLevel: string;
  summary: string;
  recommendations: string[];
  importantFactors: string[];
  evidenceQuality: FpsEvidenceQuality;
  evidenceSummary: string;
  benchmarkSampleCount: number;
  benchmarkSources: FpsBenchmarkSource[];
  disclaimer: string;
  isCached: boolean;
}

export interface FpsHardwareProfile {
  isConfigured: boolean;
  gpu: string | null;
  cpu: string | null;
  ramGb: number | null;
  vramGb: number | null;
  updatedAtUtc: string | null;
}

export type FpsFormField =
  | "gpu"
  | "cpu"
  | "ramGb"
  | "vramGb"
  | "resolution"
  | "graphicsPreset"
  | "upscaler"
  | "upscalerMode"
  | "additionalNotes";

export interface FpsEstimatorError {
  message: string;
  status?: number;
  fieldErrors: Partial<Record<FpsFormField, string>>;
  canRetry: boolean;
}
