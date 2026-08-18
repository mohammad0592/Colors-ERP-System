using System.Security.Claims;
using Colors.Api.Auditing;
using Colors.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Turns a <see cref="Result{T}"/> into the right HTTP response, in one place.
/// Mapping on the error code rather than the message means the wording can change
/// freely without breaking any client.
///
/// It is also where every refusal in the system passes, which is why the audit log's
/// rejected lines are written here — a refused action changes no data, so the
/// <c>SaveChanges</c> interceptor never sees it (specification section 15).
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
    /// <param name="result">What the service decided, and the value if it succeeded.</param>
    /// <param name="objectType">
    /// What the refusal was about, when the result type does not say. A delete returns
    /// <c>Result&lt;bool&gt;</c>, so the log would otherwise record a refused delete
    /// against "Boolean" — a line no filter matches and nobody can read.
    /// </param>
    protected IActionResult ToResponse<T>(Result<T> result, string? objectType = null)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        // Only writes that were refused. A read that found nothing is not somebody
        // trying and failing to do something — it is a screen asking a question.
        if (IsAWrite())
        {
            // Read now, while the request still exists. The write itself happens on its
            // own scope afterwards and swallows its own errors, so a slow or broken log
            // cannot hold up or break the refusal the man is waiting for.
            var userId = int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;

            _ = HttpContext.RequestServices
                .GetRequiredService<RefusalLog>()
                .WriteAsync(
                    userId,
                    $"{ControllerContext.ActionDescriptor.ControllerName}"
                        + $".{ControllerContext.ActionDescriptor.ActionName}",
                    objectType ?? typeof(T).Name,
                    result.Message);
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

        // The name of this particular refusal, and the numbers that belong in it, so the
        // screen can say it in the language the man chose (specification section 12).
        // Detail above still carries the English, which is what a log reads and what an
        // older screen shows if it has never heard of this code.
        if (result.MessageCode is not null)
        {
            problem.Extensions["messageCode"] = result.MessageCode;

            if (result.MessageArgs is { Count: > 0 })
            {
                problem.Extensions["messageArgs"] = result.MessageArgs;
            }
        }

        return StatusCode(problem.Status.Value, problem);
    }

    private bool IsAWrite() =>
        HttpContext.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE";

}
