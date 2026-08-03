import {
  clearTokens,
  getAccessToken,
  getRefreshToken,
  isAccessTokenExpired,
  setAccessToken,
  setRefreshToken,
} from './tokenStorage';

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5211';

/**
 * An error the worker can be shown.
 * `code` is the backend's ErrorCode, so screens branch on that rather than on English text.
 */
export class ApiError extends Error {
  constructor(message, status, code) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

/** Called when the session cannot be saved. The auth provider hooks in here to sign out. */
let onSessionLost = () => {};
export function setSessionLostHandler(handler) {
  onSessionLost = handler;
}

/**
 * Only one refresh may run at a time. Without this, three screens loading together
 * would each try to refresh, and rotation means the second and third would present an
 * already-used token — which the backend treats as theft and revokes every session.
 */
let refreshInFlight = null;

async function refreshAccessToken() {
  const refreshToken = getRefreshToken();
  if (!refreshToken) return false;

  refreshInFlight ??= (async () => {
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

      const result = await response.json();
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

/**
 * Every call to the API goes through here.
 * Screens never see a token, a header, or a status code.
 */
export async function apiRequest(path, { method = 'GET', body, auth = true, signal } = {}) {
  if (auth && isAccessTokenExpired() && getRefreshToken()) {
    await refreshAccessToken();
  }

  const send = async () => {
    const headers = {};
    if (body !== undefined) headers['Content-Type'] = 'application/json';

    const token = getAccessToken();
    if (auth && token) headers.Authorization = `Bearer ${token}`;

    return fetch(`${BASE_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    });
  };

  let response;
  try {
    response = await send();
  } catch (error) {
    if (error.name === 'AbortError') throw error;
    throw new ApiError(
      'Cannot reach the server. Check the network connection.',
      0,
      'NetworkError',
    );
  }

  // The token expired between the check above and the request arriving.
  if (response.status === 401 && auth && getRefreshToken()) {
    if (await refreshAccessToken()) {
      response = await send();
    }
  }

  if (response.status === 204) return null;

  const text = await response.text();
  const payload = text ? JSON.parse(text) : null;

  if (!response.ok) {
    throw new ApiError(
      payload?.detail ?? payload?.title ?? 'Something went wrong.',
      response.status,
      payload?.errorCode ?? 'Unknown',
    );
  }

  return payload;
}
