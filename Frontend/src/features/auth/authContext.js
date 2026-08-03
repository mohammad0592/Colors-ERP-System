import { createContext } from 'react';

/**
 * Holds who is signed in.
 *
 * Kept apart from the provider component so that editing the provider does not
 * force a full page reload during development — a file may export components or
 * other values, not both.
 *
 * Read it through the `useAuth` hook rather than directly.
 */
export const AuthContext = createContext(null);
