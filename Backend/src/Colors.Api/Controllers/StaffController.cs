using System.Security.Claims;
using Colors.Application.Features.Staff;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Staff who work the floor without using the system (specification section 19.19).
/// </summary>
[ApiController]
[Route("api/staff")]
[Authorize]
public class StaffController(IStaffService staff) : ApiControllerBase
{
    // Whoever fills in a line's staff may add somebody missing from the list — a new man
    // on the floor tonight should not wait for the administrator.
    private const string CanAdd =
        $"{RoleNames.Administrator},{RoleNames.Supervisor},{RoleNames.ExtruderOperator},"
        + $"{RoleNames.ThermoOperator},{RoleNames.RecyclerOperator}";

    // Renaming or switching somebody off changes every shift that names him.
    private const string CanCorrect = $"{RoleNames.Administrator},{RoleNames.Supervisor}";

    /// <summary>For the pickers. Names only, like the paper forms on the wall.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await staff.GetAllAsync(includeInactive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = CanAdd)]
    public async Task<IActionResult> Create(
        [FromBody] SaveStaffMemberRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await staff.CreateAsync(request, CurrentUserId(), cancellationToken));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = CanCorrect)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] SaveStaffMemberRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await staff.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>Who is acting. From the token, never from the body.</summary>
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
