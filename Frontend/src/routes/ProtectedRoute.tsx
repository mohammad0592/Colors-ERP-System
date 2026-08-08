import type { ReactElement } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

interface ProtectedRouteProps {
  /**
   * When given, the worker must hold at least one of these roles.
   *
   * `| undefined` is spelled out because the list is passed straight from
   * `routes/access.ts`, where a screen open to everybody is written as `undefined`.
   * Under `exactOptionalPropertyTypes` an optional prop will not accept an explicit
   * `undefined` unless it says so.
   */
  roles?: readonly string[] | undefined;
}

/**
 * Blocks a page until the worker is signed in, and optionally until he holds one of
 * the listed roles.
 *
 * This is convenience, not security. The real check is on the server, where every
 * endpoint carries [Authorize]. Hiding a button does not protect anything — it only
 * stops a worker reaching a screen he cannot use.
 */
export function ProtectedRoute({ roles }: ProtectedRouteProps): ReactElement {
  const { isSignedIn, isRestoring, hasRole } = useAuth();
  const location = useLocation();

  // Still working out whether the saved session is good. Showing the login page here
  // would make it flash on every reload for someone who is already signed in.
  if (isRestoring) {
    return (
      <div className="grid min-h-dvh place-items-center bg-canvas text-ink-muted">
        Loading…
      </div>
    );
  }

  if (!isSignedIn) {
    // Remember where they were going, so sign-in can send them back there.
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  if (roles !== undefined && roles.length > 0 && !hasRole(...roles)) {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}
