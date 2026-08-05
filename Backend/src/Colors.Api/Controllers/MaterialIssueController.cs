using System.Security.Claims;
using Colors.Application.Features.MaterialIssue;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Material issue and return (specification section 7).
///
/// Issuing and taking the leftover back belong to the Inventory Manager — he is the
/// one at the store counter with the scale. Reading is open to any signed-in worker,
/// because the supervisor closing a shift needs to see what is still outstanding.
/// </summary>
[ApiController]
[Route("api/material-issue")]
[Authorize]
public class MaterialIssueController(IMaterialIssueService tickets) : ApiControllerBase
{
    private const string CanIssue = $"{RoleNames.Administrator},{RoleNames.InventoryManager}";

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? shiftReportId = null,
        [FromQuery] bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await tickets.GetAllAsync(shiftReportId, openOnly, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await tickets.GetAsync(id, cancellationToken));
    }

    /// <summary>Issues the material and takes it out of the store, both or neither.</summary>
    [HttpPost]
    [Authorize(Roles = CanIssue)]
    public async Task<IActionResult> Create(
        [FromBody] CreateIssueTicketRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await tickets.CreateAsync(request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Weighs the leftover back in. May be called again as more comes back.</summary>
    [HttpPost("{id:int}/returns")]
    [Authorize(Roles = CanIssue)]
    public async Task<IActionResult> RecordReturns(
        int id,
        [FromBody] RecordReturnsRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await tickets.RecordReturnsAsync(id, request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Closes the ticket. Whatever has not come back counts as used.</summary>
    [HttpPost("{id:int}/close")]
    [Authorize(Roles = CanIssue)]
    public async Task<IActionResult> Close(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await tickets.CloseAsync(id, CurrentUserId(), cancellationToken));
    }

    /// <summary>Who is acting. From the token, never from the body.</summary>
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
