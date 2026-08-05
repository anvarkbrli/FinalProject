import { api, endServerSession, restoreAuthSession } from "../lib/api";
import type {
  AuthResponseDTO,
  ConfirmEmailRequestDTO,
  EmailOperationResultDTO,
  ForgotPasswordRequestDTO,
  ResetPasswordRequestDTO,
  AuthDeviceSessionDTO,
  RevokeAuthDeviceSessionResultDTO,
  LoginRequestDTO,
  RegisterRequestDTO,
  RegisterResultDTO,
  ResendEmailConfirmationRequestDTO,
  UsernameAvailabilityDTO,
  GoogleAvailabilityDTO,
  ExternalAuthExchangeRequestDTO,
  ExternalAuthExchangeResultDTO,
  CompleteExternalRegistrationRequestDTO,
  ExternalLoginStatusDTO,
  ExternalLoginOperationResultDTO,
  GoogleLinkIntentDTO,
} from "../types/auth";

export async function login(
  request: LoginRequestDTO,
): Promise<AuthResponseDTO> {
  const response = await api.post<AuthResponseDTO>(
    "/auth/login",
    request,
  );

  return response.data;
}


export async function checkUsernameAvailability(
  userName: string,
  signal?: AbortSignal,
): Promise<UsernameAvailabilityDTO> {
  const response = await api.get<UsernameAvailabilityDTO>(
    "/auth/username-availability",
    {
      params: { userName },
      signal,
    },
  );

  return response.data;
}

export async function register(
  request: RegisterRequestDTO,
): Promise<RegisterResultDTO> {
  const response = await api.post<RegisterResultDTO>(
    "/auth/register",
    request,
  );

  return response.data;
}

export async function resendEmailConfirmation(
  request: ResendEmailConfirmationRequestDTO,
): Promise<EmailOperationResultDTO> {
  const response =
    await api.post<EmailOperationResultDTO>(
      "/auth/resend-email-confirmation",
      request,
    );

  return response.data;
}


export async function confirmEmail(
  request: ConfirmEmailRequestDTO,
): Promise<EmailOperationResultDTO> {
  const response =
    await api.post<EmailOperationResultDTO>(
      "/auth/confirm-email",
      request,
    );

  return response.data;
}

export async function restoreSession(): Promise<AuthResponseDTO | null> {
  return restoreAuthSession();
}

export async function logout(): Promise<void> {
  await endServerSession();
}

export async function forgotPassword(
  request: ForgotPasswordRequestDTO,
): Promise<EmailOperationResultDTO> {
  const response = await api.post<EmailOperationResultDTO>(
    "/auth/forgot-password",
    request,
  );
  return response.data;
}

export async function resetPassword(
  request: ResetPasswordRequestDTO,
): Promise<EmailOperationResultDTO> {
  const response = await api.post<EmailOperationResultDTO>(
    "/auth/reset-password",
    request,
  );
  return response.data;
}

export async function getAuthDeviceSessions(
  signal?: AbortSignal,
): Promise<AuthDeviceSessionDTO[]> {
  const response = await api.get<AuthDeviceSessionDTO[]>(
    "/auth/sessions",
    { signal },
  );
  return response.data;
}

export async function revokeAuthDeviceSession(
  sessionId: string,
): Promise<RevokeAuthDeviceSessionResultDTO> {
  const response = await api.delete<RevokeAuthDeviceSessionResultDTO>(
    `/auth/sessions/${sessionId}`,
  );
  return response.data;
}


export async function getGoogleAvailability(): Promise<GoogleAvailabilityDTO> {
  const response = await api.get<GoogleAvailabilityDTO>(
    "/auth/google/availability",
  );
  return response.data;
}

export function getGoogleLoginStartUrl(returnPath = "/"): string {
  const baseUrl = String(import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/$/, "");
  const normalizedReturnPath = returnPath.startsWith("/") ? returnPath : "/";
  return `${baseUrl}/auth/google/start?returnPath=${encodeURIComponent(normalizedReturnPath)}`;
}

export async function exchangeGoogleAuthCode(
  request: ExternalAuthExchangeRequestDTO,
): Promise<ExternalAuthExchangeResultDTO> {
  const response = await api.post<ExternalAuthExchangeResultDTO>(
    "/auth/google/exchange",
    request,
  );
  return response.data;
}

export async function completeGoogleRegistration(
  request: CompleteExternalRegistrationRequestDTO,
): Promise<ExternalAuthExchangeResultDTO> {
  const response = await api.post<ExternalAuthExchangeResultDTO>(
    "/auth/google/complete-registration",
    request,
  );
  return response.data;
}

export async function getExternalLoginStatus(
  signal?: AbortSignal,
): Promise<ExternalLoginStatusDTO> {
  const response = await api.get<ExternalLoginStatusDTO>(
    "/auth/google/status",
    { signal },
  );
  return response.data;
}

export async function createGoogleLinkIntent(): Promise<GoogleLinkIntentDTO> {
  const response = await api.post<GoogleLinkIntentDTO>(
    "/auth/google/link-intent",
  );
  return response.data;
}

export async function unlinkGoogleAccount(): Promise<ExternalLoginOperationResultDTO> {
  const response = await api.delete<ExternalLoginOperationResultDTO>(
    "/auth/google/link",
  );
  return response.data;
}
