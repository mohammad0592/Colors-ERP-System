using System.Security.Claims;
using Colors.Application.Features.Recycler;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// The recycler (specification section 11).
///
/// Recorded by the Recycler Operator at the end of the shift. Reading is open wider: the
/// supervisor closing a shift, and anyone comparing the weighed scrap against what the
/// thermo calculated, both need to see it.
/// </summary>
[ApiController]
[Route("api/recycler")]
[Authorize]
public class RecyclerController(IRecyclerService recycler) : ApiControllerBase
{
    private const string CanRecord = $"{RoleNames.Administrator},{RoleNames.RecyclerOperator}";

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? shiftReportId = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await recycler.GetAllAsync(shiftReportId, cancellationToken));
    }

    /// <summary>
    /// The form for one line of a shift, carrying what the thermo already calculated so
    /// the operator can see the free check while typing.
    /// </summary>
    [HttpGet("draft/{shiftLineId:int}")]
    public async Task<IActionResult> GetDraft(int shiftLineId, CancellationToken cancellationToken)
    {
        return ToResponse(await recycler.GetDraftAsync(shiftLineId, cancellationToken));
    }

    /// <summary>Records the two weights and adds the output to the store — both, or neither.</summary>
    [HttpPost]
    [Authorize(Roles = CanRecord)]
    public async Task<IActionResult> Save(
        [FromBody] SaveRecyclerProductionRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await recycler.SaveAsync(request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Who is acting. From the token, never from the body.</summary>
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
