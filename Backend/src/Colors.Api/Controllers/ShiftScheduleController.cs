using System.Security.Claims;
using Colors.Application.Features.ShiftSchedule;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// The shift schedule — tomorrow's shifts, planned the day before (specification section
/// 19.30). Planning a shift is opening it in advance, so it belongs to the same people who
/// open shifts: the Supervisor and the Administrator.
/// </summary>
[ApiController]
[Route("api/shift-schedule")]
[Authorize(Roles = $"{RoleNames.Administrator},{RoleNames.Supervisor}")]
public class ShiftScheduleController(IShiftScheduleService schedule) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        return Ok(await schedule.GetAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Schedule(
        [FromBody] ScheduleShiftRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await schedule.ScheduleAsync(request, CurrentUserId(), cancellationToken));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateScheduledShiftRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await schedule.UpdateAsync(id, request, CurrentUserId(), cancellationToken));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var result = await schedule.CancelAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToResponse(result);
    }

    /// <summary>Who is acting. From the token, never from the body.</summary>
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
