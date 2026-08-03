import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

/**
 * Blocks a page until the worker is signed in, and optionally until he holds one of
 * the listed roles.
 *
 * This is convenience, not security. The real check is on the server, where every
 * endpoint carries [Authorize]. Hiding a button does not protect anything — it only
 * stops a worker reaching a screen he cannot use.
 */
export function ProtectedRoute({ roles }) {
  const { isSignedIn, isRestoring, hasRole } = useAuth();
  const location = useLocation();

  // Still working out whether the saved session is good. Showing the login page here
  // would make it flash on every reload for someone who is already signed in.
  if (isRestoring) {
    return <div className="route-loading">Loading…</div>;
  }

  if (!isSignedIn) {
    // Remember where they were going, so sign-in can send them back there.
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  if (roles?.length && !hasRole(...roles)) {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}
