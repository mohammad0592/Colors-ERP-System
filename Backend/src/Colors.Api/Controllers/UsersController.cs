using System.Security.Claims;
using Colors.Application.Features.Users;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// User administration (specification section 3).
///
/// The administrator's job alone: adding a worker, changing what they may do, and
/// setting a new password when one is forgotten. There is no self-service reset — the
/// factory has no email, so the flow that normally carries a reset link does not exist.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = RoleNames.Administrator)]
public class UsersController(IUserService users) : ApiControllerBase
{
    /// <summary>Everyone, including those who have left — their work is still in the record.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        return Ok(await users.GetAllAsync(includeInactive, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await users.GetAsync(id, cancellationToken));
    }

    /// <summary>Adds a worker. Their employee number is also how they sign in.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await users.CreateAsync(request, cancellationToken));
    }

    /// <summary>
    /// Changes the name, the number, the roles, and whether they still work here.
    ///
    /// Nobody is ever deleted: production records name people for ever, so leaving is
    /// recorded by making the account inactive.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await users.UpdateAsync(id, request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Sets a new password and frees the account if wrong tries had locked it.</summary>
    [HttpPost("{id:int}/password")]
    public async Task<IActionResult> ResetPassword(
        int id,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await users.ResetPasswordAsync(id, request, cancellationToken));
    }

    /// <summary>Frees an account locked by wrong passwords, without waiting five minutes.</summary>
    [HttpPost("{id:int}/unlock")]
    public async Task<IActionResult> Unlock(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await users.UnlockAsync(id, cancellationToken));
    }

    /// <summary>Who is acting. From the token, never from the body.</summary>
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
