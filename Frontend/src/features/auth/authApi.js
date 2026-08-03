import { apiRequest } from '../../lib/apiClient';

/** Sign in with an employee number and password. */
export function login(employeeNumber, password) {
  return apiRequest('/api/auth/login', {
    method: 'POST',
    auth: false,
    body: { employeeNumber, password },
  });
}

/** Give up the refresh token so the session ends on this tablet. */
export function logout(refreshToken) {
  return apiRequest('/api/auth/logout', {
    method: 'POST',
    auth: false,
    body: { refreshToken },
  });
}

/** Who is signed in. Used after a page reload to rebuild the menu. */
export function getCurrentUser() {
  return apiRequest('/api/auth/me');
}
