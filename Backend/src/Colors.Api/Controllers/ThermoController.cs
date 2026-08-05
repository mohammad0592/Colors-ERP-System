using System.Security.Claims;
using Colors.Application.Features.Thermo;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Line 2 — thermoforming (specification section 9).
///
/// The run belongs to the Thermo Operator; the counting belongs to the Thermo Test
/// Person. Today one man holds both roles, and the endpoints are separated now so
/// splitting the jobs later needs no code change.
///
/// Reading is open to any signed-in worker: the packaging operator needs to see which
/// bags exist before he can build a pallet.
/// </summary>
[ApiController]
[Route("api/thermo")]
[Authorize]
public class ThermoController(IThermoService thermo) : ApiControllerBase
{
    private const string CanForm = $"{RoleNames.Administrator},{RoleNames.ThermoOperator}";
    private const string CanTest = $"{RoleNames.Administrator},{RoleNames.ThermoTestPerson}";

    // ---------- runs ----------

    [HttpGet("runs")]
    public async Task<IActionResult> GetRuns(
        [FromQuery] int? shiftLineId = null,
        [FromQuery] bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await thermo.GetRunsAsync(shiftLineId, openOnly, cancellationToken));
    }

    [HttpGet("runs/{id:int}")]
    public async Task<IActionResult> GetRun(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await thermo.GetRunAsync(id, cancellationToken));
    }

    /// <summary>Rolls that have been measured and not yet formed.</summary>
    [HttpGet("available-rolls")]
    public async Task<IActionResult> GetAvailableRolls(CancellationToken cancellationToken)
    {
        return Ok(await thermo.GetAvailableRollsAsync(cancellationToken));
    }

    /// <summary>Scan a roll to start forming. Everything else is inherited from it.</summary>
    [HttpPost("runs")]
    [Authorize(Roles = CanForm)]
    public async Task<IActionResult> StartRun(
        [FromBody] StartThermoRunRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await thermo.StartRunAsync(request, CurrentUserId(), cancellationToken));
    }

    [HttpPost("runs/{id:int}/finish")]
    [Authorize(Roles = CanForm)]
    public async Task<IActionResult> FinishRun(
        int id,
        [FromBody] FinishThermoRunRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await thermo.FinishRunAsync(id, request, cancellationToken));
    }

    /// <summary>The counts. Saving them creates the bags and prints their labels.</summary>
    [HttpPost("runs/{id:int}/test")]
    [Authorize(Roles = CanTest)]
    public async Task<IActionResult> SaveTest(
        int id,
        [FromBody] SaveThermoTestRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await thermo.SaveTestReportAsync(id, request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Who is acting. From the token, never from the body.</summary>
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
