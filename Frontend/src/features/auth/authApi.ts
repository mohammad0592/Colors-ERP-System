import { apiRequest } from '../../lib/apiClient';
import type { AuthenticatedUser, AuthenticationResult } from '../../lib/apiTypes';

/** Sign in with an employee number and password. */
export function login(
  employeeNumber: string,
  password: string,
): Promise<AuthenticationResult> {
  return apiRequest<AuthenticationResult>('/api/auth/login', {
    method: 'POST',
    auth: false,
    body: { employeeNumber, password },
  });
}

/**
 * Give up the refresh token so the session ends on this tablet.
 * The endpoint answers 204 with no body, hence `undefined`.
 */
export function logout(refreshToken: string): Promise<undefined> {
  return apiRequest<undefined>('/api/auth/logout', {
    method: 'POST',
    auth: false,
    body: { refreshToken },
  });
}

/** Who is signed in. Used after a page reload to rebuild the menu. */
export function getCurrentUser(): Promise<AuthenticatedUser> {
  return apiRequest<AuthenticatedUser>('/api/auth/me');
}
