import { createContext } from 'react';
import type { AuthenticatedUser } from '../../lib/apiTypes';

/** What every screen may ask about the signed-in worker. */
export interface AuthContextValue {
  user: AuthenticatedUser | null;
  /** True while the saved session is being checked after a page reload. */
  isRestoring: boolean;
  isSignedIn: boolean;
  signIn: (employeeNumber: string, password: string) => Promise<AuthenticatedUser>;
  signOut: () => Promise<void>;
  /** True when the worker holds at least one of the given roles. */
  hasRole: (...roles: string[]) => boolean;
}

/**
 * Holds who is signed in.
 *
 * Kept apart from the provider component so that editing the provider does not
 * force a full page reload during development — a file may export components or
 * other values, not both.
 *
 * Read it through the `useAuth` hook rather than directly.
 */
export const AuthContext = createContext<AuthContextValue | null>(null);
