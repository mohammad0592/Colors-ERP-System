using System.Security.Claims;
using Colors.Application.Features.Packaging;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Packaging consumption (specification section 10).
///
/// Recorded by the Packaging Operator at the end of the shift. Reading is open wider:
/// the supervisor closing a shift needs to see whether it was written down.
/// </summary>
[ApiController]
[Route("api/packaging")]
[Authorize]
public class PackagingController(IPackagingService packaging) : ApiControllerBase
{
    private const string CanRecord = $"{RoleNames.Administrator},{RoleNames.PackagingOperator}";

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? shiftReportId = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await packaging.GetAllAsync(shiftReportId, cancellationToken));
    }

    /// <summary>
    /// The form for one line of a shift, with the three counted materials already worked
    /// out from what it produced.
    /// </summary>
    [HttpGet("draft/{shiftLineId:int}")]
    public async Task<IActionResult> GetDraft(int shiftLineId, CancellationToken cancellationToken)
    {
        return ToResponse(await packaging.GetDraftAsync(shiftLineId, cancellationToken));
    }

    /// <summary>Records what was used and takes it out of the store — every line, or none.</summary>
    [HttpPost]
    [Authorize(Roles = CanRecord)]
    public async Task<IActionResult> Save(
        [FromBody] SavePackagingRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await packaging.SaveAsync(request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Who is acting. From the token, never from the body.</summary>
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
