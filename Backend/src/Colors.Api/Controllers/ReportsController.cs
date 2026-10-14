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
public class ReportsController(
    IReportsService reports,
    IThermoShiftReportService thermoReports,
    IThermoShiftReportExcel thermoExcel,
    IExtruderShiftReportService extruderReports,
    IExtruderShiftReportExcel extruderExcel) : ApiControllerBase
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

    /// <summary>The thermoforming line's report for one shift.</summary>
    [HttpGet("thermo-shift/{shiftReportId:int}")]
    public async Task<IActionResult> GetThermoShift(
        int shiftReportId,
        CancellationToken cancellationToken)
    {
        return ToResponse(await thermoReports.GetAsync(shiftReportId, cancellationToken));
    }

    /// <summary>
    /// The same report as the factory's Excel form, ready to print or send on.
    /// </summary>
    [HttpGet("thermo-shift/{shiftReportId:int}/excel")]
    public async Task<IActionResult> GetThermoShiftExcel(
        int shiftReportId,
        CancellationToken cancellationToken)
    {
        var report = await thermoReports.GetAsync(shiftReportId, cancellationToken);
        if (!report.IsSuccess)
        {
            return ToResponse(report);
        }

        var shift = report.Value!;
        var name = $"Thermo report {shift.ProductionDate:yyyy-MM-dd} shift {shift.ShiftName}.xlsx";

        return File(
            thermoExcel.Build(shift),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            name);
    }

    /// <summary>The extruder's report for one shift: every roll made on it.</summary>
    [HttpGet("extruder-shift/{shiftReportId:int}")]
    public async Task<IActionResult> GetExtruderShift(
        int shiftReportId,
        CancellationToken cancellationToken)
    {
        return ToResponse(await extruderReports.GetAsync(shiftReportId, cancellationToken));
    }

    /// <summary>
    /// The same report as an Excel file. <c>utcOffsetMinutes</c> is the browser's own, so
    /// the out times read as the factory's clock: the server knows no time zone.
    /// </summary>
    [HttpGet("extruder-shift/{shiftReportId:int}/excel")]
    public async Task<IActionResult> GetExtruderShiftExcel(
        int shiftReportId,
        CancellationToken cancellationToken,
        [FromQuery] int utcOffsetMinutes = 0)
    {
        var report = await extruderReports.GetAsync(shiftReportId, cancellationToken);
        if (!report.IsSuccess)
        {
            return ToResponse(report);
        }

        // No clock on earth is more than fourteen hours from UTC.
        var offset = TimeSpan.FromMinutes(Math.Clamp(utcOffsetMinutes, -14 * 60, 14 * 60));
        var shift = report.Value!;
        var name = $"Extruder report {shift.ProductionDate:yyyy-MM-dd} shift {shift.ShiftName}.xlsx";

        return File(
            extruderExcel.Build(shift, offset),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            name);
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
        var (first, last) = Range(from, to);

        return ToResponse(
            await reports.GetConsumptionAsync(first, last, groupBy, cancellationToken));
    }

    /// <summary>Pallets finished over a stretch of days, by the product on them.</summary>
    [HttpGet("pallet-production")]
    public async Task<IActionResult> GetPalletProduction(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var (first, last) = Range(from, to);

        return ToResponse(
            await reports.GetPalletProductionAsync(first, last, cancellationToken));
    }

    /// <summary>Recycled material made, against how much the mixer took back out.</summary>
    [HttpGet("recycled-material")]
    public async Task<IActionResult> GetRecycledMaterial(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var (first, last) = Range(from, to);

        return ToResponse(
            await reports.GetRecycledMaterialAsync(first, last, cancellationToken));
    }

    /// <summary>
    /// The last month when no dates are given, ending <b>tomorrow</b>: a night shift
    /// starting this evening carries tomorrow's production date, and a range ending today
    /// would hide the shift that is running while the report is read.
    /// </summary>
    private static (DateOnly First, DateOnly Last) Range(DateOnly? from, DateOnly? to)
    {
        var last = to ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        return (from ?? last.AddDays(-31), last);
    }
}
