import { resolveApiBaseUrl } from './apiBaseUrl';
import type { EntryMethod } from './barcodeScanner';
import type { AuthenticationResult, ErrorCode, ProblemResponse } from './apiTypes';
import {
  clearTokens,
  getAccessToken,
  getRefreshToken,
  isAccessTokenExpired,
  setAccessToken,
  setRefreshToken,
} from './tokenStorage';

/**
 * Where the API is. The reasoning lives in `apiBaseUrl.ts`, with its own tests.
 *
 * `import.meta.env.DEV` is the deciding fact: Vite sets it true only while developing,
 * and a built copy always has it false. That is what tells the screens whether the API
 * is on a port of its own or at the very address that served them.
 */
const BASE_URL: string = resolveApiBaseUrl(
  import.meta.env.VITE_API_URL,
  import.meta.env.DEV,
  window.location,
);

/**
 * An error the worker can be shown.
 * `code` is the backend's ErrorCode, so screens branch on that rather than on English text.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly code: ErrorCode;

  constructor(message: string, status: number, code: ErrorCode) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

/** Called when the session cannot be saved. The auth provider hooks in here to sign out. */
let onSessionLost: () => void = () => {
  // Nothing until the auth provider registers itself.
};

export function setSessionLostHandler(handler: () => void): void {
  onSessionLost = handler;
}

/**
 * Only one refresh may run at a time. Without this, three screens loading together
 * would each try to refresh, and rotation means the second and third would present an
 * already-used token — which the backend treats as theft and revokes every session.
 */
let refreshInFlight: Promise<boolean> | null = null;

async function refreshAccessToken(): Promise<boolean> {
  const refreshToken = getRefreshToken();
  if (refreshToken === null) {
    return false;
  }

  refreshInFlight ??= (async (): Promise<boolean> => {
    try {
      const response = await fetch(`${BASE_URL}/api/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });

      if (!response.ok) {
        clearTokens();
        onSessionLost();
        return false;
      }

      const result = (await response.json()) as AuthenticationResult;
      setAccessToken(result.accessToken, result.accessTokenExpiresAt);
      setRefreshToken(result.refreshToken);
      return true;
    } catch {
      // Network failure, not a rejected token — keep the tokens so a retry can work
      // once the tablet is back on the factory Wi-Fi.
      return false;
    } finally {
      refreshInFlight = null;
    }
  })();

  return refreshInFlight;
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  /** Send the access token. Off for login and refresh, which have none yet. */
  auth?: boolean;
  signal?: AbortSignal;
  /**
   * How the code in this request reached the screen — scanned, typed or picked
   * (specification section 12).
   *
   * A header rather than part of the body, so it rides along with every request that
   * carries a code without each one having to make room for it. The server records it
   * in the audit log and nothing else reads it.
   */
  entry?: EntryMethod;
}

/**
 * Every call to the API goes through here.
 * Screens never see a token, a header, or a status code.
 */
export async function apiRequest<TResponse>(
  path: string,
  { method = 'GET', body, auth = true, signal, entry }: RequestOptions = {},
): Promise<TResponse> {
  if (auth && isAccessTokenExpired() && getRefreshToken() !== null) {
    await refreshAccessToken();
  }

  const send = async (): Promise<Response> => {
    const headers: Record<string, string> = {};
    if (body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }

    const token = getAccessToken();
    if (auth && token !== null) {
      headers.Authorization = `Bearer ${token}`;
    }

    if (entry !== undefined) {
      headers['X-Entry-Method'] = entry;
    }

    // Properties are added only when they have a value. Passing `body: undefined`
    // is not the same as leaving it out, and fetch treats the two differently.
    return fetch(`${BASE_URL}${path}`, {
      method,
      headers,
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
      ...(signal === undefined ? {} : { signal }),
    });
  };

  let response: Response;
  try {
    response = await send();
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }

    throw new ApiError(
      'Cannot reach the server. Check the network connection.',
      0,
      'NetworkError',
    );
  }

  // The token expired between the check above and the request arriving.
  if (response.status === 401 && auth && getRefreshToken() !== null) {
    if (await refreshAccessToken()) {
      response = await send();
    }
  }

  if (response.status === 204) {
    return undefined as TResponse;
  }

  const text = await response.text();
  const payload: unknown = text === '' ? null : JSON.parse(text);

  if (!response.ok) {
    const problem = (payload ?? {}) as ProblemResponse;
    throw new ApiError(
      problem.detail ?? problem.title ?? 'Something went wrong.',
      response.status,
      problem.errorCode ?? 'Unknown',
    );
  }

  return payload as TResponse;
}
