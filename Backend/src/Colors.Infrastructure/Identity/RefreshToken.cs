namespace Colors.Infrastructure.Identity;

/// <summary>
/// A long-lived token that buys a new access token without asking for the password again.
///
/// Specification section 15 asks for a short access token plus a refresh token, so that
/// a twelve-hour shift never logs a man out mid-roll, while a deactivated worker still
/// loses access quickly.
///
/// The token itself is never stored. Only a SHA-256 hash is kept, exactly as passwords
/// are handled: if the database is ever copied, the tokens in it cannot be used.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>SHA-256 of the token that was handed to the client. Never the token itself.</summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set when the token is used, replaced, or the worker logs out.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// The hash of the token issued in its place. Together these form a chain, which is
    /// how a stolen token is detected: if an already-revoked token is presented, someone
    /// has a copy, and the whole chain is revoked at once.
    /// </summary>
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
