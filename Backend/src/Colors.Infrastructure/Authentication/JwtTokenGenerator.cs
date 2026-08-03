using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Colors.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Colors.Infrastructure.Authentication;

/// <summary>
/// Creates signed access tokens and random refresh tokens.
///
/// Uses <see cref="JsonWebTokenHandler"/> — the current handler. The older
/// <c>JwtSecurityTokenHandler</c> from System.IdentityModel.Tokens.Jwt is legacy and is
/// deliberately not used.
/// </summary>
public class JwtTokenGenerator(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    /// <summary>
    /// Builds the access token the client sends on every request.
    /// Roles go inside it, so <c>[Authorize(Roles = ...)]</c> needs no database lookup.
    /// </summary>
    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(
        ApplicationUser user,
        IEnumerable<string> roles,
        DateTimeOffset now)
    {
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.EmployeeNumber),
            new(ColorsClaimTypes.EmployeeNumber, user.EmployeeNumber),
            new(ColorsClaimTypes.FullName, user.FullName),
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        return (_handler.CreateToken(descriptor), expiresAt);
    }

    /// <summary>
    /// A refresh token is only a large random number — it carries no information and is
    /// never parsed. 256 bits from a cryptographic generator, so it cannot be guessed.
    /// </summary>
    public static string CreateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// What gets stored. The token itself never touches the database, so a copy of the
    /// database yields nothing usable — the same reasoning as password hashing.
    /// </summary>
    public static string Hash(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>Claim names specific to this factory.</summary>
public static class ColorsClaimTypes
{
    public const string EmployeeNumber = "employee_number";
    public const string FullName = "full_name";
}
