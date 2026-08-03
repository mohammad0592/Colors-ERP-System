using System.Security.Claims;
using Colors.Domain.Constants;
using Colors.Infrastructure.Authentication;
using Colors.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Colors.UnitTests.Application;

public class JwtTokenGeneratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);

    private static JwtTokenGenerator CreateGenerator(int accessTokenMinutes = 30) =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "Colors.ERP",
            Audience = "Colors.ERP.Api",
            SigningKey = "test-signing-key-that-is-long-enough-for-hmac-sha256",
            AccessTokenMinutes = accessTokenMinutes,
        }));

    private static ApplicationUser CreateUser() => new()
    {
        Id = 7,
        EmployeeNumber = "EMP0006",
        FullName = "م. علي حمدان",
        IsActive = true,
    };

    // ---------- access token ----------

    [Fact]
    public void Access_token_carries_who_the_worker_is()
    {
        var (token, _) = CreateGenerator().CreateAccessToken(CreateUser(), [RoleNames.ExtruderOperator], Now);

        var claims = ReadClaims(token);

        Assert.Equal("7", claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal("EMP0006", claims[ColorsClaimTypes.EmployeeNumber]);
        Assert.Equal("م. علي حمدان", claims[ColorsClaimTypes.FullName]);
    }

    [Fact]
    public void Access_token_carries_every_role_the_worker_holds()
    {
        // The factory's normal case: one man is both operator and test person.
        string[] roles = [RoleNames.ExtruderOperator, RoleNames.ExtruderTestPerson];

        var (token, _) = CreateGenerator().CreateAccessToken(CreateUser(), roles, Now);

        var roleClaims = new JsonWebTokenHandler().ReadJsonWebToken(token)
            .Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .ToList();

        Assert.Equal(2, roleClaims.Count);
        Assert.Contains(RoleNames.ExtruderOperator, roleClaims);
        Assert.Contains(RoleNames.ExtruderTestPerson, roleClaims);
    }

    [Fact]
    public void Access_token_expires_after_the_configured_minutes()
    {
        var (_, expiresAt) = CreateGenerator(accessTokenMinutes: 45).CreateAccessToken(CreateUser(), [], Now);

        Assert.Equal(Now.AddMinutes(45), expiresAt);
    }

    [Fact]
    public void Access_token_names_the_issuer_and_audience()
    {
        // Both are checked on every request. A token minted elsewhere must not be accepted.
        var (token, _) = CreateGenerator().CreateAccessToken(CreateUser(), [], Now);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        Assert.Equal("Colors.ERP", jwt.Issuer);
        Assert.Contains("Colors.ERP.Api", jwt.Audiences);
    }

    [Fact]
    public void Two_tokens_for_the_same_worker_are_still_different()
    {
        // Each carries its own jti, so one can be traced without matching another.
        var generator = CreateGenerator();
        var user = CreateUser();

        var (first, _) = generator.CreateAccessToken(user, [], Now);
        var (second, _) = generator.CreateAccessToken(user, [], Now);

        Assert.NotEqual(first, second);
    }

    // ---------- refresh token ----------

    [Fact]
    public void Refresh_tokens_are_never_repeated()
    {
        var tokens = Enumerable.Range(0, 500)
            .Select(_ => JwtTokenGenerator.CreateRefreshToken())
            .ToHashSet();

        Assert.Equal(500, tokens.Count);
    }

    [Fact]
    public void Refresh_token_is_long_enough_to_be_unguessable()
    {
        // 32 random bytes. Base64 of 32 bytes is 44 characters.
        var token = JwtTokenGenerator.CreateRefreshToken();

        Assert.Equal(44, token.Length);
    }

    // ---------- hashing ----------

    [Fact]
    public void The_same_token_always_hashes_the_same_way()
    {
        // Otherwise a refresh could never be looked up.
        const string token = "some-refresh-token";

        Assert.Equal(JwtTokenGenerator.Hash(token), JwtTokenGenerator.Hash(token));
    }

    [Fact]
    public void Different_tokens_hash_differently()
    {
        Assert.NotEqual(JwtTokenGenerator.Hash("token-a"), JwtTokenGenerator.Hash("token-b"));
    }

    [Fact]
    public void The_hash_does_not_contain_the_token()
    {
        // What is stored must be useless to anyone who copies the database.
        const string token = "refresh-token-value";

        var hash = JwtTokenGenerator.Hash(token);

        Assert.DoesNotContain(token, hash, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ReadClaims(string token) =>
        new JsonWebTokenHandler().ReadJsonWebToken(token)
            .Claims
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.First().Value);
}
