import { useContext } from 'react';
import { AuthContext } from '../features/auth/authContext';

/** Who is signed in, and what they may do. */
export function useAuth() {
  const context = useContext(AuthContext);

  if (context === null) {
    throw new Error('useAuth must be used inside <AuthProvider>.');
  }

  return context;
}
