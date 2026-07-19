import { api } from "../lib/api";
import type {
  AuthResponseDTO,
  EmailOperationResultDTO,
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
