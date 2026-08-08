using Colors.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Is the server up (specification section 15)?
///
/// The developer is two hours away. This answers the question from a phone without
/// anybody driving there, and it answers the <i>useful</i> version of it: not "is the
/// web server running" — which a browser already tells you — but "can it reach the
/// database", which is what actually fails.
///
/// <b>Open, and deliberately so.</b> It is checked before anybody can sign in, often by
/// a monitor with no account at all. It says nothing a stranger could use: no version, no
/// connection string, no counts.
/// </summary>
[ApiController]
[Route("health")]
[AllowAnonymous]
public class HealthController(ColorsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        bool database;

        try
        {
            // The cheapest question that proves the whole chain works: the connection is
            // open, the credentials are right, and the database is answering.
            database = await db.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            // Any failure here is the answer, not an error to raise. A health check that
            // returns 500 with a stack trace tells a stranger more than it tells us.
            database = false;
        }

        var body = new { status = database ? "healthy" : "degraded", database };

        return database
            ? Ok(body)
            // 503, so a monitor that only reads the status code still gets it right.
            : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}
