using System.Security.Claims;
using Colors.Application.Features.Staff;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Workers — people who work the floor without an account (specification section 19.19).
/// Read by anyone who fills in a shift; added and changed by the administrator only.
/// </summary>
[ApiController]
[Route("api/staff")]
[Authorize]
public class StaffController(IStaffService staff) : ApiControllerBase
{
    // The people the factory has — staff with accounts and workers without — are the
    // administrator's alone to add and change (section 19.19), the same as the users. The
    // shift configuration only chooses from them.
    private const string CanManage = RoleNames.Administrator;

    /// <summary>For the pickers. Names only, like the paper forms on the wall.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await staff.GetAllAsync(includeInactive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = CanManage)]
    public async Task<IActionResult> Create(
        [FromBody] SaveStaffMemberRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await staff.CreateAsync(request, CurrentUserId(), cancellationToken));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = CanManage)]
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
