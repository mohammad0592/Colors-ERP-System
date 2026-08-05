using System.Security.Claims;
using Colors.Application.Features.ShiftReports;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Shift reports — one date, one shift, for the whole factory, with the lines that
/// ran hanging underneath (specification section 2).
///
/// Any signed-in worker may read them: an operator needs to know which shift is open
/// before recording anything against it. Opening, filling in and closing belong to the
/// Supervisor and the Administrator, which is what section 3 says. Reopening a closed
/// shift is the Administrator's alone — it changes figures somebody may already have
/// acted on.
/// </summary>
[ApiController]
[Route("api/shift-reports")]
[Authorize]
public class ShiftReportsController(IShiftReportService reports) : ApiControllerBase
{
    private const string CanRun = $"{RoleNames.Administrator},{RoleNames.Supervisor}";

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? productionLineId = null,
        [FromQuery] bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await reports.GetAllAsync(productionLineId, openOnly, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await reports.GetAsync(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = CanRun)]
    public async Task<IActionResult> Open(
        [FromBody] OpenShiftReportRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await reports.OpenAsync(request, CurrentUserId(), cancellationToken));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = CanRun)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateShiftReportRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await reports.UpdateAsync(id, request, cancellationToken));
    }

    // ---------- the lines that ran ----------

    /// <summary>Adds a line that started after the shift was opened.</summary>
    [HttpPost("{id:int}/lines")]
    [Authorize(Roles = CanRun)]
    public async Task<IActionResult> AddLine(
        int id,
        [FromBody] AddShiftLineRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await reports.AddLineAsync(id, request, cancellationToken));
    }

    [HttpPut("{id:int}/lines/{lineId:int}")]
    [Authorize(Roles = CanRun)]
    public async Task<IActionResult> UpdateLine(
        int id,
        int lineId,
        [FromBody] UpdateShiftLineRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await reports.UpdateLineAsync(id, lineId, request, cancellationToken));
    }

    /// <summary>Takes a line off a shift when it turned out not to run.</summary>
    [HttpDelete("{id:int}/lines/{lineId:int}")]
    [Authorize(Roles = CanRun)]
    public async Task<IActionResult> RemoveLine(
        int id,
        int lineId,
        CancellationToken cancellationToken)
    {
        return ToResponse(await reports.RemoveLineAsync(id, lineId, cancellationToken));
    }

    [HttpPost("{id:int}/close")]
    [Authorize(Roles = CanRun)]
    public async Task<IActionResult> Close(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await reports.CloseAsync(id, CurrentUserId(), cancellationToken));
    }

    /// <summary>Reopens a closed shift. Administrator only, and the reason is kept.</summary>
    [HttpPost("{id:int}/reopen")]
    [Authorize(Roles = RoleNames.Administrator)]
    public async Task<IActionResult> Reopen(
        int id,
        [FromBody] ReopenShiftReportRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await reports.ReopenAsync(id, request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Removes an empty shift opened by mistake — never one with work on it.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = CanRun)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await reports.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToResponse(result);
    }

    /// <summary>Who is acting. From the token, never from the body.</summary>
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
