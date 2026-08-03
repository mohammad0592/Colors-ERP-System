using System.ComponentModel.DataAnnotations;

namespace Colors.Infrastructure.Authentication;

/// <summary>
/// Token settings, from configuration. The signing key comes from user secrets in
/// development and an environment variable on the factory server — never from a file
/// that reaches GitHub.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Who issued the token. Checked on every request.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = "Colors.ERP";

    /// <summary>Who the token is for. Checked on every request.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = "Colors.ERP.Api";

    /// <summary>
    /// The secret used to sign tokens. At least 32 characters, because HMAC-SHA256
    /// needs a 256-bit key and a shorter one is refused outright.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters (256 bits) for HMAC-SHA256.")]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// How long an access token lives. Short on purpose: it is the window during which
    /// a deactivated worker still has access.
    /// </summary>
    [Range(5, 120)]
    public int AccessTokenMinutes { get; set; } = 30;

    /// <summary>
    /// How long a refresh token lives. Long enough to cover a whole shift plus overtime,
    /// so nobody is signed out while holding a roll.
    /// </summary>
    [Range(1, 24)]
    public int RefreshTokenHours { get; set; } = 12;
}
