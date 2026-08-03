using Colors.Application.Common.Models;

namespace Colors.Application.Features.Authentication;

/// <summary>
/// Signing in and staying signed in.
///
/// Declared here, implemented in Infrastructure. This layer must not know that
/// ASP.NET Identity, JWTs or PostgreSQL exist — see specification section 0.1.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Check an employee number and password, and issue tokens.
    /// Fails with <see cref="ErrorCode.InvalidCredentials"/>, <see cref="ErrorCode.AccountLocked"/>
    /// or <see cref="ErrorCode.AccountInactive"/>.
    /// </summary>
    Task<Result<AuthenticationResult>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchange a valid refresh token for a fresh pair. The old refresh token is
    /// revoked in the same step, so each one works exactly once.
    /// </summary>
    Task<Result<AuthenticationResult>> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Revoke a refresh token, ending the session on that device.</summary>
    Task<Result<bool>> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default);
}
