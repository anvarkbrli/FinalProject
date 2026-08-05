import { createContext, useContext } from "react";

export type ConfirmationTone =
  | "danger"
  | "warning"
  | "neutral";

export type ConfirmationKind =
  | "delete"
  | "leave"
  | "remove-user"
  | "disconnect"
  | "reset"
  | "moderation";

export interface ConfirmationOptions {
  title: string;
  description: string;
  confirmLabel: string;
  cancelLabel?: string;
  detail?: string;
  tone?: ConfirmationTone;
  kind?: ConfirmationKind;
}

export type RequestConfirmation = (
  options: ConfirmationOptions,
) => Promise<boolean>;

export const ConfirmationContext =
  createContext<RequestConfirmation | null>(null);

export function useConfirmation(): RequestConfirmation {
  const requestConfirmation = useContext(
    ConfirmationContext,
  );

  if (!requestConfirmation) {
    throw new Error(
      "useConfirmation ConfirmationDialogProvider daxilində istifadə edilməlidir.",
    );
  }

  return requestConfirmation;
}
