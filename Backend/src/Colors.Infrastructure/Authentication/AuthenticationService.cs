using Colors.Application.Common.Models;
using Colors.Application.Features.Authentication;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Colors.Infrastructure.Authentication;

/// <inheritdoc cref="IAuthenticationService"/>
public class AuthenticationService(
    UserManager<ApplicationUser> userManager,
    ColorsDbContext dbContext,
    JwtTokenGenerator tokenGenerator,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    private readonly JwtOptions _options = options.Value;

    public async Task<Result<AuthenticationResult>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(request.EmployeeNumber);

        // Same message whether the employee number is unknown or the password is wrong.
        // Saying which would let someone discover valid employee numbers.
        if (user is null)
        {
            logger.LogWarning(
                "Failed login for unknown employee number {EmployeeNumber}.",
                request.EmployeeNumber);
            return InvalidCredentials();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Login refused for {EmployeeNumber}: account locked.", request.EmployeeNumber);
            return Result<AuthenticationResult>.Failure(
                ErrorCode.AccountLocked,
                "Too many failed attempts. Try again in a few minutes.", "auth.tooManyAttempts");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            // Counts towards the lockout. Without this the lockout would never trigger.
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("Failed login for {EmployeeNumber}: wrong password.", request.EmployeeNumber);
            return InvalidCredentials();
        }

        // Checked after the password so that a wrong password on an inactive account
        // still looks like a wrong password, rather than confirming the account exists.
        if (!user.IsActive)
        {
            logger.LogWarning("Login refused for {EmployeeNumber}: account inactive.", request.EmployeeNumber);
            return Result<AuthenticationResult>.Failure(
                ErrorCode.AccountInactive,
                "This account is no longer active. Ask an administrator.", "auth.inactive");
        }

        await userManager.ResetAccessFailedCountAsync(user);

        logger.LogInformation("{EmployeeNumber} signed in.", user.EmployeeNumber);
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<Result<AuthenticationResult>> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var hash = JwtTokenGenerator.Hash(request.RefreshToken);

        var stored = await dbContext.Set<RefreshToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            return InvalidRefreshToken();
        }

        // An already-revoked token being presented means someone kept a copy.
        // Revoke every token this user holds and make them sign in again.
        if (stored.RevokedAt is not null)
        {
            logger.LogWarning(
                "Refresh token reuse detected for user {UserId}. Revoking every session.",
                stored.UserId);

            await RevokeAllForUserAsync(stored.UserId, now, cancellationToken);
            return InvalidRefreshToken();
        }

        if (!stored.IsActive(now))
        {
            return InvalidRefreshToken();
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            // A worker deactivated mid-shift loses access at the next refresh — which is
            // why access tokens are short (specification section 15).
            stored.RevokedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<AuthenticationResult>.Failure(
                ErrorCode.AccountInactive,
                "This account is no longer active. Ask an administrator.", "auth.inactive");
        }

        var issued = await IssueTokensAsync(user, cancellationToken, replacing: stored);
        return issued;
    }

    public async Task<Result<bool>> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var hash = JwtTokenGenerator.Hash(request.RefreshToken);

        var stored = await dbContext.Set<RefreshToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Always reports success: telling a caller that a token was unknown would let
        // them probe for valid ones.
        return Result<bool>.Success(true);
    }

    private async Task<Result<AuthenticationResult>> IssueTokensAsync(
        ApplicationUser user,
        CancellationToken cancellationToken,
        RefreshToken? replacing = null)
    {
        var now = timeProvider.GetUtcNow();
        var roles = await userManager.GetRolesAsync(user);

        var (accessToken, accessExpiresAt) = tokenGenerator.CreateAccessToken(user, roles, now);

        var refreshToken = JwtTokenGenerator.CreateRefreshToken();
        var refreshHash = JwtTokenGenerator.Hash(refreshToken);
        var refreshExpiresAt = now.AddHours(_options.RefreshTokenHours);

        dbContext.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt,
        });

        // Rotation: the token just used is retired in the same transaction as the new
        // one is stored, so each refresh token works exactly once.
        if (replacing is not null)
        {
            replacing.RevokedAt = now;
            replacing.ReplacedByTokenHash = refreshHash;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AuthenticationResult>.Success(new AuthenticationResult(
            accessToken,
            accessExpiresAt,
            refreshToken,
            refreshExpiresAt,
            new AuthenticatedUser(user.Id, user.EmployeeNumber, user.FullName, [.. roles])));
    }

    private async Task RevokeAllForUserAsync(int userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.Set<RefreshToken>()
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.RevokedAt, now), cancellationToken);

    private static Result<AuthenticationResult> InvalidCredentials() =>
        Result<AuthenticationResult>.Failure(
            ErrorCode.InvalidCredentials,
            "Employee number or password is wrong.", "auth.wrongCredentials");

    private static Result<AuthenticationResult> InvalidRefreshToken() =>
        Result<AuthenticationResult>.Failure(
            ErrorCode.InvalidRefreshToken,
            "Your session has ended. Please sign in again.", "auth.sessionEnded");
}
