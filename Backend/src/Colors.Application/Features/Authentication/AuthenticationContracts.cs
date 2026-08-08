namespace Colors.Application.Features.Authentication;

/// <summary>
/// Login. Workers sign in with their employee number — the number already printed on
/// the factory's paper forms. Many have no company email.
/// </summary>
/// <param name="EmployeeNumber">For example <c>EMP0006</c>.</param>
/// <param name="Password">Never logged, never stored in plain text.</param>
public sealed record LoginRequest(string EmployeeNumber, string Password);

/// <summary>Exchange a refresh token for a new access token.</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Give up a refresh token, ending the session on this device.</summary>
public sealed record LogoutRequest(string RefreshToken);

/// <summary>
/// What a successful login returns.
///
/// The access token is short lived so that deactivating a worker takes effect quickly.
/// The refresh token covers a whole shift, so nobody is logged out mid-roll
/// (specification section 15).
/// </summary>
public sealed record AuthenticationResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthenticatedUser User);

/// <summary>Who is signed in, for the screen header and to decide which menus to show.</summary>
public sealed record AuthenticatedUser(
    int Id,
    string EmployeeNumber,
    string FullName,
    IReadOnlyList<string> Roles);
