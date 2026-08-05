using Colors.Application.Features.Barcodes;
using Colors.Domain.Constants;
using Colors.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Scanning (specification section 12).
///
/// There is no endpoint for issuing a barcode, on purpose. A barcode is created with
/// the roll, bag or pallet it names, in the same transaction, by the phase that
/// creates it — never by a client asking for one. Otherwise a code could exist for an
/// object that does not.
///
/// Looking up is open to any signed-in worker: scanning is what the floor does all day.
/// </summary>
[ApiController]
[Route("api/barcodes")]
[Authorize]
public class BarcodesController(IBarcodeService barcodes) : ApiControllerBase
{
    /// <summary>
    /// Scan anything, or type it when the label is torn.
    ///
    /// Pass <c>expected</c> when the screen only wants one kind of thing, and a bag
    /// scanned into a pallet field is told what it actually is.
    /// </summary>
    [HttpGet("{value}")]
    public async Task<IActionResult> Lookup(
        string value,
        [FromQuery] BarcodeObjectType? expected = null,
        CancellationToken cancellationToken = default)
    {
        return ToResponse(expected is null
            ? await barcodes.LookupAsync(value, cancellationToken)
            : await barcodes.LookupAsync(value, expected.Value, cancellationToken));
    }

    /// <summary>Every barcode an object has had, newest first — for reprinting.</summary>
    [HttpGet("object/{objectType}/{objectId:int}")]
    public async Task<IActionResult> GetForObject(
        BarcodeObjectType objectType,
        int objectId,
        CancellationToken cancellationToken)
    {
        return Ok(await barcodes.GetForObjectAsync(objectType, objectId, cancellationToken));
    }

    /// <summary>
    /// Retires a label too damaged to scan and issues a new one for the same object.
    ///
    /// A supervisor's job: the old code keeps resolving for ever, so this is a
    /// decision about a physical label, not a correction anyone should make casually.
    /// </summary>
    [HttpPost("{value}/replace")]
    [Authorize(Roles = $"{RoleNames.Administrator},{RoleNames.Supervisor}")]
    public async Task<IActionResult> Replace(string value, CancellationToken cancellationToken)
    {
        return ToResponse(await barcodes.ReplaceAsync(value, cancellationToken));
    }
}
