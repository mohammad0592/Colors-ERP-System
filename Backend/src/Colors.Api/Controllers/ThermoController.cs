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
    // The operator counts too: on the floor the man who ran the roll is usually the one
    // who counts what came out.
    private const string CanTest =
        $"{RoleNames.Administrator},{RoleNames.ThermoTestPerson},{RoleNames.ThermoOperator}";

    // Corrections (section 19.27). Changing saved counts adds or cancels bags, so it is a
    // supervisor's; taking back a wrong start is the operator's own fresh mistake too; and
    // a defective bag is found by whoever packs it.
    private const string CanCorrect = $"{RoleNames.Administrator},{RoleNames.Supervisor}";
    private const string CanUndoStart =
        $"{RoleNames.Administrator},{RoleNames.Supervisor},{RoleNames.ThermoOperator}";
    private const string CanMarkDefective =
        $"{RoleNames.Administrator},{RoleNames.Supervisor},{RoleNames.ThermoOperator},{RoleNames.PackagingOperator}";

    // ---------- runs ----------

    /// <summary>
    /// Runs. <c>currentShiftOnly</c> is the line screen's view (specification section 19.6).
    /// </summary>
    [HttpGet("runs")]
    public async Task<IActionResult> GetRuns(
        [FromQuery] int? shiftLineId = null,
        [FromQuery] bool openOnly = false,
        [FromQuery] bool currentShiftOnly = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await thermo.GetRunsAsync(
            shiftLineId, openOnly, currentShiftOnly, cancellationToken));
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

    /// <summary>
    /// Takes the roll out before it is finished, with the reason (section 19.23). It goes
    /// back to stock as Not completed; what it made so far is counted next.
    /// </summary>
    [HttpPost("runs/{id:int}/unfinished")]
    [Authorize(Roles = CanForm)]
    public async Task<IActionResult> LeaveUnfinished(
        int id,
        [FromBody] LeaveRunUnfinishedRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await thermo.LeaveUnfinishedAsync(id, request, cancellationToken));
    }

    /// <summary>
    /// Corrects counts saved wrong (section 19.27). A supervisor's job: it adds or cancels
    /// bags, and those may already be on the floor.
    /// </summary>
    [HttpPost("runs/{id:int}/test/correct")]
    [Authorize(Roles = CanCorrect)]
    public async Task<IActionResult> CorrectTest(
        int id,
        [FromBody] CorrectThermoTestRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await thermo.CorrectTestReportAsync(id, request, CurrentUserId(), cancellationToken));
    }

    /// <summary>
    /// Takes back a run started with the wrong roll, while it is in the machine (section
    /// 19.27). The operator who scanned it may, as may a supervisor.
    /// </summary>
    [HttpPost("runs/{id:int}/undo-start")]
    [Authorize(Roles = CanUndoStart)]
    public async Task<IActionResult> UndoStart(
        int id,
        [FromBody] UndoThermoStartRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await thermo.UndoStartAsync(id, request, CurrentUserId(), cancellationToken),
            "ThermoProduction");
    }

    /// <summary>Marks a bag not on a pallet defective (section 19.27). Floor work.</summary>
    [HttpPost("bags/{id:int}/defective")]
    [Authorize(Roles = CanMarkDefective)]
    public async Task<IActionResult> MarkBagDefective(
        int id,
        [FromBody] MarkBagDefectiveRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await thermo.MarkBagDefectiveAsync(id, request, CurrentUserId(), cancellationToken));
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
