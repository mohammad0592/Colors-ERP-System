/**
 * Where the tokens live.
 *
 * The access token is kept in memory only. It dies when the page is closed, and no
 * script can read it out of storage.
 *
 * The refresh token has to survive a page reload — a worker who reloads the screen
 * must not be thrown back to the login page — so it goes in localStorage.
 *
 * Known trade-off (specification section 15): anything in localStorage is readable by
 * any script running on the page. This application loads no third-party scripts and
 * renders no user-written HTML, and it runs on a factory network with no internet, so
 * the exposure is small. The stronger fix is to have the API set the refresh token as
 * an httpOnly cookie, which no script can read at all. That is worth doing when the
 * API starts serving the React files from the same address.
 */

const REFRESH_TOKEN_KEY = 'colors.refreshToken';

let accessToken = null;
let accessTokenExpiresAt = null;

export function getAccessToken() {
  return accessToken;
}

export function setAccessToken(token, expiresAt) {
  accessToken = token;
  accessTokenExpiresAt = expiresAt ? new Date(expiresAt) : null;
}

/** True when the access token is missing or about to run out. */
export function isAccessTokenExpired() {
  if (!accessToken || !accessTokenExpiresAt) return true;
  // Renew half a minute early, so a request never arrives just after expiry.
  return accessTokenExpiresAt.getTime() - Date.now() < 30_000;
}

export function getRefreshToken() {
  return localStorage.getItem(REFRESH_TOKEN_KEY);
}

export function setRefreshToken(token) {
  if (token) {
    localStorage.setItem(REFRESH_TOKEN_KEY, token);
  } else {
    localStorage.removeItem(REFRESH_TOKEN_KEY);
  }
}

export function clearTokens() {
  accessToken = null;
  accessTokenExpiresAt = null;
  localStorage.removeItem(REFRESH_TOKEN_KEY);
}
