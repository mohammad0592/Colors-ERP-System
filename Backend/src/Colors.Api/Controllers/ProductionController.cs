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

    // ---------- batches ----------

    [HttpGet("batches")]
    public async Task<IActionResult> GetBatches(
        [FromQuery] int? shiftReportId = null,
        [FromQuery] bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await production.GetBatchesAsync(shiftReportId, openOnly, cancellationToken));
    }

    [HttpPost("batches")]
    [Authorize(Roles = CanProduce)]
    public async Task<IActionResult> StartBatch(
        [FromBody] StartBatchRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await production.StartBatchAsync(request, CurrentUserId(), cancellationToken));
    }

    [HttpPost("batches/{id:int}/finish")]
    [Authorize(Roles = CanProduce)]
    public async Task<IActionResult> FinishBatch(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await production.FinishBatchAsync(id, cancellationToken));
    }

    // ---------- rolls ----------

    [HttpGet("rolls")]
    public async Task<IActionResult> GetRolls(
        [FromQuery] int? batchId = null,
        [FromQuery] bool needsTestOnly = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await production.GetRollsAsync(batchId, needsTestOnly, cancellationToken));
    }

    [HttpGet("rolls/{id:int}")]
    public async Task<IActionResult> GetRoll(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await production.GetRollAsync(id, cancellationToken));
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
