using System.Security.Claims;
using Colors.Application.Common.Auditing;

namespace Colors.Api.Auditing;

/// <summary>
/// Who is acting, read from the signed-in worker's token (specification section 15).
///
/// It lives in the API layer because that is the only place a web request exists. The
/// interceptor that writes the audit log knows nothing about HTTP and simply asks.
///
/// <b>No database here.</b> The context's own options resolve the interceptor, so
/// anything the interceptor depends on must not need the context back — that circle
/// hangs the application on startup rather than failing with a message.
/// </summary>
public class CurrentActor(IHttpContextAccessor http) : ICurrentActor
{
    public int? UserId =>
        int.TryParse(
            http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
}
