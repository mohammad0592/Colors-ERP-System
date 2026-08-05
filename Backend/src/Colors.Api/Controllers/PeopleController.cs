using Colors.Application.Features.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// People and roles, read only — for every screen that has to name somebody.
///
/// Open to any signed-in worker: names and employee numbers are already on the paper
/// forms taped to the wall. Nothing here can change an account.
/// </summary>
[ApiController]
[Route("api/people")]
[Authorize]
public class PeopleController(IPeopleService people) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPeople(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await people.GetPeopleAsync(includeInactive, cancellationToken));
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        return Ok(await people.GetRolesAsync(cancellationToken));
    }
}
