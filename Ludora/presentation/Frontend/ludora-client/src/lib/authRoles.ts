const roleClaimKeys = [
  "role",
  "roles",
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
] as const;

function decodeJwtPayload(
  accessToken: string | null,
): Record<string, unknown> | null {
  if (!accessToken) {
    return null;
  }

  const parts = accessToken.split(".");

  if (parts.length < 2) {
    return null;
  }

  try {
    const base64 = parts[1]
      .replace(/-/g, "+")
      .replace(/_/g, "/");

    const padded = base64.padEnd(
      Math.ceil(base64.length / 4) * 4,
      "=",
    );

    const json = decodeURIComponent(
      Array.from(atob(padded))
        .map(
          (character) =>
            `%${character
              .charCodeAt(0)
              .toString(16)
              .padStart(2, "0")}`,
        )
        .join(""),
    );

    const payload: unknown = JSON.parse(json);

    return payload !== null &&
      typeof payload === "object" &&
      !Array.isArray(payload)
      ? (payload as Record<string, unknown>)
      : null;
  } catch {
    return null;
  }
}

export function getAuthRoles(
  accessToken: string | null,
): string[] {
  const payload = decodeJwtPayload(accessToken);

  if (!payload) {
    return [];
  }

  const roles = new Set<string>();

  for (const key of roleClaimKeys) {
    const value = payload[key];

    if (typeof value === "string") {
      roles.add(value);
      continue;
    }

    if (Array.isArray(value)) {
      for (const item of value) {
        if (typeof item === "string") {
          roles.add(item);
        }
      }
    }
  }

  return Array.from(roles);
}

export function hasAuthRole(
  accessToken: string | null,
  roleName: string,
  explicitRoles: readonly string[] = [],
): boolean {
  const roles = new Set([
    ...getAuthRoles(accessToken),
    ...explicitRoles,
  ]);

  return Array.from(roles).some(
    (role) =>
      role.localeCompare(roleName, undefined, {
        sensitivity: "base",
      }) === 0,
  );
}

export function canAccessAdminPanel(
  accessToken: string | null,
  explicitRoles: readonly string[] = [],
): boolean {
  return (
    hasAuthRole(accessToken, "Admin", explicitRoles) ||
    hasAuthRole(accessToken, "Moderator", explicitRoles)
  );
}

export function canDeleteAdminContent(
  accessToken: string | null,
  explicitRoles: readonly string[] = [],
): boolean {
  return hasAuthRole(accessToken, "Admin", explicitRoles);
}

export function canViewCatalogSources(
  accessToken: string | null,
): boolean {
  return canAccessAdminPanel(accessToken);
}


export function hasAuthCapability(
  capabilities: readonly string[] | undefined,
  capabilityName: string,
): boolean {
  return (capabilities ?? []).some(
    (capability) => capability === capabilityName,
  );
}
