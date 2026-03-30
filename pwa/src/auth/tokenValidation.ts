import { mapAuthFailure, type AuthError } from "./errors";

export interface TokenProbeOptions {
  probeUrl: string;
  fetchImpl?: typeof fetch;
}

export type TokenValidationResult =
  | { ok: true }
  | { ok: false; error: AuthError };

export const validateToken = async (
  token: string,
  options: TokenProbeOptions,
): Promise<TokenValidationResult> => {
  const fetcher = options.fetchImpl ?? fetch;

  try {
    const response = await fetcher(options.probeUrl, {
      method: "GET",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (response.ok) {
      return { ok: true };
    }

    return { ok: false, error: mapAuthFailure(response.status) };
  } catch {
    return { ok: false, error: mapAuthFailure() };
  }
};
