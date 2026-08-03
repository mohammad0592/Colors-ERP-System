/**
 * Shapes the API returns.
 *
 * These mirror the C# records in Colors.Application. Keeping them here means a field
 * renamed on the server becomes a build error on the client, instead of `undefined`
 * appearing on a tablet in the middle of a shift.
 */

/**
 * Why an operation failed. Mirrors Colors.Application.Common.Models.ErrorCode,
 * plus `NetworkError`, which the client raises when the server cannot be reached.
 */
export type ErrorCode =
  | 'None'
  | 'InvalidCredentials'
  | 'AccountLocked'
  | 'AccountInactive'
  | 'InvalidRefreshToken'
  | 'ValidationFailed'
  | 'NotFound'
  | 'NetworkError'
  | 'Unknown';

/** Mirrors AuthenticatedUser. */
export interface AuthenticatedUser {
  id: number;
  employeeNumber: string;
  fullName: string;
  roles: string[];
}

/** Mirrors AuthenticationResult. Dates arrive as ISO strings over JSON. */
export interface AuthenticationResult {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: AuthenticatedUser;
}

/** ASP.NET Core's ProblemDetails, with the errorCode we add to it. */
export interface ProblemResponse {
  title?: string;
  detail?: string;
  status?: number;
  errorCode?: ErrorCode;
}
