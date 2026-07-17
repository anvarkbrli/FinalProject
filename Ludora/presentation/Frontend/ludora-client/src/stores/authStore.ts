import { create } from "zustand";
import { persist } from "zustand/middleware";
import type {
  AuthResponseDTO,
  CurrentUserDTO,
} from "../types/auth";

interface AuthState {
  accessToken: string | null;
  expiresAtUtc: string | null;
  user: CurrentUserDTO | null;

  setSession: (response: AuthResponseDTO) => void;
  setUser: (user: CurrentUserDTO) => void;
  clearSession: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      accessToken: null,
      expiresAtUtc: null,
      user: null,

      setSession: (response) => {
        set({
          accessToken: response.accessToken,
          expiresAtUtc: response.expiresAtUtc,
          user: response.user,
        });
      },

      setUser: (user) => {
        set({
          user,
        });
      },

      clearSession: () => {
        set({
          accessToken: null,
          expiresAtUtc: null,
          user: null,
        });
      },
    }),
    {
      name: "ludora-auth-session",
    },
  ),
);

export function isAuthSessionValid(
  accessToken: string | null,
  expiresAtUtc: string | null,
): boolean {
  if (!accessToken || !expiresAtUtc) {
    return false;
  }

  const expiresAt = new Date(expiresAtUtc).getTime();

  if (Number.isNaN(expiresAt)) {
    return false;
  }

  return expiresAt > Date.now();
}