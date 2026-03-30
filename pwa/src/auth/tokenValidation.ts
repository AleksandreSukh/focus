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
  const fetcher = resolveFetchImplementation(options.fetchImpl);

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

function resolveFetchImplementation(fetchImpl?: typeof fetch): typeof fetch {
  const candidate = fetchImpl ?? globalThis.fetch;
  if (typeof candidate !== "function") {
    throw new Error("Fetch API is unavailable in this browser environment.");
  }

  return ((input: RequestInfo | URL, init?: RequestInit) =>
    candidate.call(globalThis, input, init)) as typeof fetch;
}
