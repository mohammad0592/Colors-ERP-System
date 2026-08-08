using Colors.Application.Features.Audit;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// The audit log (specification section 15).
///
/// The administrator and the supervisor: one answers for the system, the other for the
/// shift. Read-only — there is no endpoint to write a line, and none to remove one.
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize(Roles = $"{RoleNames.Administrator},{RoleNames.Supervisor}")]
public class AuditController(IAuditService audit) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? shiftReportId = null,
        [FromQuery] string? objectType = null,
        [FromQuery] bool refusalsOnly = false,
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default)
    {
        // Comma separated, because one thing on the floor is several names in the log —
        // a reversal is against the entity, a refused scan against what the screen asked
        // for.
        var types = objectType?.Split(',', StringSplitOptions.RemoveEmptyEntries
                                            | StringSplitOptions.TrimEntries);

        return Ok(await audit.GetAsync(
            shiftReportId, types, refusalsOnly, take, cancellationToken));
    }
}
