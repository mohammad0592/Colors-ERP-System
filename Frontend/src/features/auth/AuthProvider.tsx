import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactElement,
  type ReactNode,
} from 'react';
import { setSessionLostHandler } from '../../lib/apiClient';
import type { AuthenticatedUser } from '../../lib/apiTypes';
import {
  clearTokens,
  getRefreshToken,
  setAccessToken,
  setRefreshToken,
} from '../../lib/tokenStorage';
import { AuthContext, type AuthContextValue } from './authContext';
import * as authApi from './authApi';

/**
 * Holds who is signed in, for the whole application.
 *
 * Screens ask this "who is the worker and what may he do". They never touch a token.
 */
export function AuthProvider({ children }: { children: ReactNode }): ReactElement {
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  // Starts true: on a page reload we may still have a refresh token, and the screen
  // must not flash the login page before we find out.
  const [isRestoring, setIsRestoring] = useState(true);

  const signOut = useCallback(async (): Promise<void> => {
    const refreshToken = getRefreshToken();
    clearTokens();
    setUser(null);

    if (refreshToken !== null) {
      // Tell the server too, so the token cannot be used again. If the network is
      // down the local session is already gone, which is what matters.
      try {
        await authApi.logout(refreshToken);
      } catch {
        /* ignore */
      }
    }
  }, []);

  // The API client calls this when a refresh is refused — an expired session, or a
  // worker deactivated mid-shift.
  useEffect(() => {
    setSessionLostHandler(() => {
      clearTokens();
      setUser(null);
    });
  }, []);

  // On a reload, get the worker back from the refresh token we still hold.
  useEffect(() => {
    let cancelled = false;

    async function restore(): Promise<void> {
      if (getRefreshToken() === null) {
        setIsRestoring(false);
        return;
      }

      try {
        const me = await authApi.getCurrentUser();
        if (!cancelled) {
          setUser(me);
        }
      } catch {
        if (!cancelled) {
          clearTokens();
          setUser(null);
        }
      } finally {
        if (!cancelled) {
          setIsRestoring(false);
        }
      }
    }

    void restore();

    return () => {
      cancelled = true;
    };
  }, []);

  const signIn = useCallback(
    async (employeeNumber: string, password: string): Promise<AuthenticatedUser> => {
      const result = await authApi.login(employeeNumber, password);
      setAccessToken(result.accessToken, result.accessTokenExpiresAt);
      setRefreshToken(result.refreshToken);
      setUser(result.user);
      return result.user;
    },
    [],
  );

  const hasRole = useCallback(
    (...roles: string[]): boolean =>
      roles.some((role) => user?.roles.includes(role) ?? false),
    [user],
  );

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isRestoring,
      isSignedIn: user !== null,
      signIn,
      signOut,
      hasRole,
    }),
    [user, isRestoring, signIn, signOut, hasRole],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
