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
