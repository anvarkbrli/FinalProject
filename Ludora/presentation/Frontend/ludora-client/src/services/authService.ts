import { api } from "../lib/api";
import type {
  AuthResponseDTO,
  LoginRequestDTO,
  RegisterRequestDTO,
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
): Promise<AuthResponseDTO> {
  const response = await api.post<AuthResponseDTO>(
    "/auth/register",
    request,
  );

  return response.data;
}