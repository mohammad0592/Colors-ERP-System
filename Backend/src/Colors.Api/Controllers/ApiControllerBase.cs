using Colors.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Turns a <see cref="Result{T}"/> into the right HTTP response, in one place.
/// Mapping on the error code rather than the message means the wording can change
/// freely without breaking any client.
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ToResponse<T>(Result<T> result)
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
