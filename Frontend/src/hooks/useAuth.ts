import { useContext } from 'react';
import { AuthContext, type AuthContextValue } from '../features/auth/authContext';

/** Who is signed in, and what they may do. */
export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);

  if (context === null) {
    throw new Error('useAuth must be used inside <AuthProvider>.');
  }

  return context;
}
