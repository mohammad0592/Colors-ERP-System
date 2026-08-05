using System.Security.Claims;
using Colors.Application.Features.Inventory;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// The store — what is in it, what moved, and the two ways stock goes up
/// (specification section 6).
///
/// Reading is open to any signed-in worker: an operator about to start a batch needs
/// to know whether the material is there. Receiving belongs to the Inventory Manager.
/// Adjusting belongs to the Supervisor, because it overrides what the ledger says —
/// and section 6 is explicit that the supervisor fixes a wrong balance on the tablet
/// rather than going to find an administrator.
/// </summary>
[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController(
    IInventoryService inventory,
    IProducedStockService produced) : ApiControllerBase
{
    private const string CanReceive = $"{RoleNames.Administrator},{RoleNames.InventoryManager}";
    private const string CanAdjust = $"{RoleNames.Administrator},{RoleNames.Supervisor}";

    [HttpGet]
    public async Task<IActionResult> GetStock(
        [FromQuery] bool belowMinimumOnly = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await inventory.GetStockAsync(belowMinimumOnly, cancellationToken));
    }

    /// <summary>
    /// Rolls, bags and pallets in one list. Reading is open to any signed-in worker:
    /// anyone may need to find where a label went.
    /// </summary>
    [HttpGet("produced")]
    public async Task<IActionResult> GetProduced(
        [FromQuery] string? kind = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] bool availableOnly = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await produced.GetAsync(kind, status, search, availableOnly, cancellationToken));
    }

    /// <summary>Everything printed on the label for one barcode.</summary>
    [HttpGet("produced/label/{barcode}")]
    public async Task<IActionResult> GetLabel(string barcode, CancellationToken cancellationToken)
    {
        return ToResponse(await produced.GetLabelAsync(barcode, cancellationToken));
    }

    /// <summary>
    /// A whole run's labels in one call. A POST because a thermo run can make a couple
    /// of hundred bags, and that many codes in a query string is a length limit waiting
    /// to be hit.
    /// </summary>
    [HttpPost("produced/labels")]
    public async Task<IActionResult> GetLabels(
        [FromBody] LabelSheetRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await produced.GetLabelsAsync(request.Barcodes, cancellationToken));
    }

    /// <summary>The units a material may be received in — pallet, bag, kilogram.</summary>
    [HttpGet("materials/{materialId:int}/units")]
    public async Task<IActionResult> GetReceivingUnits(
        int materialId,
        CancellationToken cancellationToken)
    {
        return ToResponse(await inventory.GetReceivingUnitsAsync(materialId, cancellationToken));
    }

    [HttpGet("movements")]
    public async Task<IActionResult> GetMovements(
        [FromQuery] int? materialId = null,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        return Ok(await inventory.GetMovementsAsync(materialId, take, cancellationToken));
    }

    [HttpPost("receive")]
    [Authorize(Roles = CanReceive)]
    public async Task<IActionResult> Receive(
        [FromBody] ReceiveMaterialRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await inventory.ReceiveAsync(request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Corrects a balance after a stock count. Always with a reason.</summary>
    [HttpPost("adjust")]
    [Authorize(Roles = CanAdjust)]
    public async Task<IActionResult> Adjust(
        [FromBody] AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await inventory.AdjustAsync(request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Who is acting. From the token, never from the body.</summary>
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
