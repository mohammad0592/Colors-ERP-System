using System.Security.Claims;
using Colors.Application.Features.Pallets;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Pallets (specification section 10).
///
/// Building a pallet is the Packaging Operator's job. Undoing a scan is not: a bag comes
/// off only with a supervisor's reason, because the row is kept for ever and the
/// correction is part of the history.
///
/// Reading is open to any signed-in worker — the supervisor closing a shift needs to see
/// what is still part-built.
/// </summary>
[ApiController]
[Route("api/pallets")]
[Authorize]
public class PalletsController(IPalletService pallets) : ApiControllerBase
{
    private const string CanPack = $"{RoleNames.Administrator},{RoleNames.PackagingOperator}";
    private const string CanReverse = $"{RoleNames.Administrator},{RoleNames.Supervisor}";

    [HttpGet]
    public async Task<IActionResult> GetPallets(
        [FromQuery] int? shiftLineId = null,
        [FromQuery] bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await pallets.GetPalletsAsync(shiftLineId, openOnly, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPallet(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await pallets.GetPalletAsync(id, cancellationToken));
    }

    /// <summary>
    /// Bags not yet on a pallet. Pass a pallet and only the bags it can actually take
    /// come back.
    /// </summary>
    [HttpGet("available-bags")]
    public async Task<IActionResult> GetAvailableBags(
        [FromQuery] int? palletId = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await pallets.GetAvailableBagsAsync(palletId, cancellationToken));
    }

    /// <summary>Starts an empty pallet and prints its label.</summary>
    [HttpPost]
    [Authorize(Roles = CanPack)]
    public async Task<IActionResult> StartPallet(
        [FromBody] StartPalletRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await pallets.StartPalletAsync(request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Puts one bag on the pallet. The first one decides what the pallet is.</summary>
    [HttpPost("{id:int}/bags")]
    [Authorize(Roles = CanPack)]
    public async Task<IActionResult> ScanBag(
        int id,
        [FromBody] ScanBagRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await pallets.ScanBagAsync(id, request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Takes a bag back off. The scan stays in the history with its reason.</summary>
    [HttpPost("assignments/{assignmentId:int}/reverse")]
    [Authorize(Roles = CanReverse)]
    public async Task<IActionResult> ReverseAssignment(
        int assignmentId,
        [FromBody] ReverseAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await pallets.ReverseAssignmentAsync(
                assignmentId, request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Who is acting. From the token, never from the body.</summary>
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
