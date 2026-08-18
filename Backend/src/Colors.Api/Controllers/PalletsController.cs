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

    // Sending a pallet out is not floor work. It takes the pallet out of the factory's
    // stock for good, so it sits with the supervisor, the same as a reversal does.
    private const string CanShip = $"{RoleNames.Administrator},{RoleNames.Supervisor}";

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

    /// <summary>
    /// Gives up on an empty pallet and sends its wooden pallet back to the store.
    ///
    /// The operator's own call, not a supervisor's: no bag was ever on it, nothing in
    /// the record changes, and the wood is standing in front of them.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = CanPack)]
    public async Task<IActionResult> CancelPallet(
        int id,
        [FromBody] CancelPalletRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await pallets.CancelPalletAsync(id, request, CurrentUserId(), cancellationToken));
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

    /// <summary>
    /// Finished pallets still standing in the factory.
    ///
    /// Open to any signed-in worker, like the other reads. It is also the only place the
    /// system answers "what finished goods are here", so a supervisor looking at stock
    /// should not need the right to ship in order to look.
    /// </summary>
    [HttpGet("in-stock")]
    public async Task<IActionResult> GetPalletsInStock(CancellationToken cancellationToken)
    {
        return Ok(await pallets.GetPalletsInStockAsync(cancellationToken));
    }

    /// <summary>Sends a finished pallet out. Scanned, as the floor does it.</summary>
    [HttpPost("ship")]
    [Authorize(Roles = CanShip)]
    public async Task<IActionResult> ShipPallet(
        [FromBody] ShipPalletRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await pallets.ShipPalletAsync(request, CurrentUserId(), cancellationToken));
    }

    /// <summary>
    /// Puts a pallet shipped by mistake back into the factory. The reason is required,
    /// and the whole correction is in the audit log either way.
    /// </summary>
    [HttpPost("{id:int}/unship")]
    [Authorize(Roles = CanShip)]
    public async Task<IActionResult> ReverseShipment(
        int id,
        [FromBody] ReverseShipmentRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await pallets.ReverseShipmentAsync(id, request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Who is acting. From the token, never from the body.</summary>
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
