export type AuthErrorCode = "UNAUTHORIZED" | "FORBIDDEN" | "NETWORK" | "UNKNOWN";

export interface AuthError {
  code: AuthErrorCode;
  message: string;
  status?: number;
}

const DEFAULT_AUTH_ERROR_MESSAGE =
  "Authentication failed. Verify the token and try again.";

export const mapAuthFailure = (status?: number): AuthError => {
  if (status === 401) {
    return {
      code: "UNAUTHORIZED",
      status,
      message: "Token was rejected (401 Unauthorized). Please generate a new token.",
    };
  }

  if (status === 403) {
    return {
      code: "FORBIDDEN",
      status,
      message:
        "Token is valid but lacks required scope (403 Forbidden). Update token scopes.",
    };
  }

  if (typeof status === "number") {
    return {
      code: "UNKNOWN",
      status,
      message: `${DEFAULT_AUTH_ERROR_MESSAGE} (HTTP ${status})`,
    };
  }

  return {
    code: "NETWORK",
    message:
      "Unable to reach authentication endpoint. Check your network and try again.",
  };
};
