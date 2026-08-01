import { create } from "zustand";
import type { AuthResponseDTO, CurrentUserDTO } from "../types/auth";

interface AuthState {
  accessToken: string | null;
  expiresAtUtc: string | null;
  user: CurrentUserDTO | null;
  isInitialized: boolean;

  setSession: (response: AuthResponseDTO) => void;
  setUser: (user: CurrentUserDTO) => void;
  clearSession: () => void;
  finishInitialization: () => void;
}


export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  expiresAtUtc: null,
  user: null,
  isInitialized: false,

  setSession: (response) => {
    set({
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
      user: response.user,
      isInitialized: true,
    });
  },

  setUser: (user) => {
    set({ user });
  },

  clearSession: () => {
    set({
      accessToken: null,
      expiresAtUtc: null,
      user: null,
      isInitialized: true,
    });
  },

  finishInitialization: () => {
    set({ isInitialized: true });
  },
}));

export function isAuthSessionValid(
  accessToken: string | null,
  expiresAtUtc: string | null,
): boolean {
  if (!accessToken || !expiresAtUtc) {
    return false;
  }

  const expiresAt = new Date(expiresAtUtc).getTime();
  return !Number.isNaN(expiresAt) && expiresAt > Date.now();
}
