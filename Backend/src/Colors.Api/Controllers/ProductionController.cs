using System.Security.Claims;
using Colors.Application.Features.Production;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Line 1 — the mixer and the extruder (specification section 8).
///
/// Batches and rolls belong to the Extruder Operator; the measurements belong to the
/// Extruder Test Person. Today one man holds both roles, but the endpoints are
/// separated now so splitting the jobs later needs no code change — which is exactly
/// what section 3 asks for.
///
/// Reading is open to any signed-in worker: the thermo operator needs to see what is
/// in stock.
/// </summary>
[ApiController]
[Route("api/production")]
[Authorize]
public class ProductionController(IProductionService production) : ApiControllerBase
{
    private const string CanProduce = $"{RoleNames.Administrator},{RoleNames.ExtruderOperator}";
    private const string CanTest = $"{RoleNames.Administrator},{RoleNames.ExtruderTestPerson}";
    // Weighing moved from the roll test to the production screen (section 19.12), so both
    // the man logging rolls and the man testing them may do it.
    private const string CanMeasure =
        $"{RoleNames.Administrator},{RoleNames.Supervisor},{RoleNames.ExtruderOperator},{RoleNames.ExtruderTestPerson}";
    private const string CanReset = $"{RoleNames.Administrator},{RoleNames.Supervisor}";
    private const string CanCorrect =
        $"{RoleNames.Administrator},{RoleNames.Supervisor},{RoleNames.ExtruderOperator}";

    // ---------- batches ----------

    [HttpGet("batches")]
    public async Task<IActionResult> GetBatches(
        [FromQuery] int? shiftReportId = null,
        [FromQuery] bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await production.GetBatchesAsync(shiftReportId, openOnly, cancellationToken));
    }




    // ---------- rolls ----------

    /// <summary>
    /// Rolls. <c>currentShiftOnly</c> is what the line screen asks for — this shift and
    /// nothing older, with the history left to inventory (specification section 19.6).
    /// </summary>
    [HttpGet("rolls")]
    public async Task<IActionResult> GetRolls(
        [FromQuery] int? batchId = null,
        [FromQuery] bool needsTestOnly = false,
        [FromQuery] bool currentShiftOnly = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await production.GetRollsAsync(
            batchId, needsTestOnly, currentShiftOnly, cancellationToken));
    }

    [HttpGet("rolls/{id:int}")]
    public async Task<IActionResult> GetRoll(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await production.GetRollAsync(id, cancellationToken));
    }

    /// <summary>
    /// The code the next roll would get, shown before it is saved. <paramref name="at"/> is
    /// the out time with the tablet's own offset, so the time in the code is the factory's.
    /// </summary>
    [HttpGet("rolls/next-code")]
    [Authorize(Roles = CanProduce)]
    public async Task<IActionResult> SuggestRollCode(
        [FromQuery] int shiftLineId,
        [FromQuery] int recipeVersionId,
        [FromQuery] int colorId,
        [FromQuery] DateTimeOffset? at,
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await production.SuggestRollCodeAsync(shiftLineId, recipeVersionId, colorId, at, cancellationToken));
    }

    /// <summary>The roll's weight and length, taken on the production screen.</summary>
    [HttpPost("rolls/{id:int}/measure")]
    [Authorize(Roles = CanMeasure)]
    public async Task<IActionResult> MeasureRoll(
        int id,
        [FromBody] MeasureRollRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await production.MeasureRollAsync(id, request, CurrentUserId(), cancellationToken));
    }

    /// <summary>The number the next roll will get, and when it was last reset.</summary>
    [HttpGet("roll-number")]
    public async Task<IActionResult> GetRollNumber(CancellationToken cancellationToken)
    {
        return Ok(await production.GetRollNumberAsync(cancellationToken));
    }

    /// <summary>
    /// Puts the roll number back to 1. The supervisor's decision, because every roll code
    /// after it changes with it.
    /// </summary>
    [HttpPost("roll-number/reset")]
    [Authorize(Roles = CanReset)]
    public async Task<IActionResult> ResetRollNumber(CancellationToken cancellationToken)
    {
        return Ok(await production.ResetRollNumberAsync(CurrentUserId(), cancellationToken));
    }

    /// <summary>
    /// Corrects a roll's code or out time. The supervisor too, because correcting the
    /// shift's record is his job.
    /// </summary>
    [HttpPut("rolls/{id:int}")]
    [Authorize(Roles = CanCorrect)]
    public async Task<IActionResult> UpdateRoll(
        int id,
        [FromBody] UpdateRollRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await production.UpdateRollAsync(id, request, cancellationToken));
    }

    /// <summary>Logs a roll off the extruder and prints its barcode.</summary>
    [HttpPost("rolls")]
    [Authorize(Roles = CanProduce)]
    public async Task<IActionResult> CreateRoll(
        [FromBody] CreateRollRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await production.CreateRollAsync(request, CurrentUserId(), cancellationToken));
    }

    /// <summary>The measurements. Saving them is what makes the roll usable.</summary>
    [HttpPost("rolls/{id:int}/test")]
    [Authorize(Roles = CanTest)]
    public async Task<IActionResult> SaveTest(
        int id,
        [FromBody] SaveRollTestRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await production.SaveTestReportAsync(id, request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Who is acting. From the token, never from the body.</summary>
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
