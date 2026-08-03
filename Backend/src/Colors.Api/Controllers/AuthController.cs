using System.Security.Claims;
using Colors.Application.Common.Models;
using Colors.Application.Features.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Signing in and out.
///
/// Controllers do HTTP and nothing else — read the request, call the service, turn the
/// result into a status code. No business rules live here (specification section 0.1).
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(IAuthenticationService authentication) : ControllerBase
{
    /// <summary>Sign in with an employee number and password.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthenticationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authentication.LoginAsync(request, cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Swap a refresh token for a new pair. The old one stops working.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthenticationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authentication.RefreshAsync(request, cancellationToken);
        return ToResponse(result);
    }

    /// <summary>End the session on this device.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        await authentication.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Who am I? Used by the client after a page refresh to rebuild the menu.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<AuthenticatedUser>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(id, out var userId))
        {
            return Unauthorized();
        }

        return Ok(new AuthenticatedUser(
            userId,
            User.FindFirstValue("employee_number") ?? string.Empty,
            User.FindFirstValue("full_name") ?? string.Empty,
            [.. User.FindAll(ClaimTypes.Role).Select(c => c.Value)]));
    }

    /// <summary>
    /// Turns a <see cref="Result{T}"/> into the right HTTP status. Mapping on the error
    /// code rather than the message means the wording can change freely.
    /// </summary>
    private IActionResult ToResponse<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var problem = new ProblemDetails
        {
            Title = result.Message,
            Detail = result.Message,
            Status = result.ErrorCode switch
            {
                ErrorCode.InvalidCredentials => StatusCodes.Status401Unauthorized,
                ErrorCode.InvalidRefreshToken => StatusCodes.Status401Unauthorized,
                ErrorCode.AccountLocked => StatusCodes.Status423Locked,
                ErrorCode.AccountInactive => StatusCodes.Status403Forbidden,
                ErrorCode.ValidationFailed => StatusCodes.Status400BadRequest,
                ErrorCode.NotFound => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            },
        };

        problem.Extensions["errorCode"] = result.ErrorCode.ToString();

        return StatusCode(problem.Status.Value, problem);
    }
}
