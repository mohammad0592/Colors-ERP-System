using Colors.Application.Features.Reports;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Reports (specification section 13).
///
/// Read-only, and for the people who answer for the shift: the administrator, the
/// supervisor, and the inventory manager whose material these figures account for.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = CanRead)]
public class ReportsController(IReportsService reports) : ApiControllerBase
{
    private const string CanRead =
        $"{RoleNames.Administrator},{RoleNames.Supervisor},{RoleNames.InventoryManager}";

    /// <summary>What the shift took out of the store against what its recipe asks for.</summary>
    [HttpGet("material-waste/{shiftReportId:int}")]
    public async Task<IActionResult> GetMaterialWaste(
        int shiftReportId,
        CancellationToken cancellationToken)
    {
        return ToResponse(await reports.GetMaterialWasteAsync(shiftReportId, cancellationToken));
    }

    /// <summary>The paper form's summary block, worked out rather than typed.</summary>
    [HttpGet("shift-summary/{shiftReportId:int}")]
    public async Task<IActionResult> GetShiftSummary(
        int shiftReportId,
        CancellationToken cancellationToken)
    {
        return ToResponse(await reports.GetShiftSummaryAsync(shiftReportId, cancellationToken));
    }

    /// <summary>
    /// What was consumed over a stretch of days, by shift or by recipe.
    ///
    /// The last thirty days when no dates are given — the range a supervisor asks about
    /// without having to think about it.
    /// </summary>
    [HttpGet("consumption")]
    public async Task<IActionResult> GetConsumption(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] ConsumptionGrouping groupBy = ConsumptionGrouping.Shift,
        CancellationToken cancellationToken = default)
    {
        // Tomorrow, not today: a night shift that starts this evening carries tomorrow's
        // production date, and a range ending today would hide the shift now running.
        var last = to ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var first = from ?? last.AddDays(-31);

        return ToResponse(
            await reports.GetConsumptionAsync(first, last, groupBy, cancellationToken));
    }
}
