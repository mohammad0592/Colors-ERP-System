using Colors.Application.Features.Trace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Where one thing came from, and what it became (specification section 13).
///
/// Open to any signed-in worker. It writes nothing, and anybody holding a label may
/// need to know what is behind it — the supervisor checking a complaint, the storekeeper
/// finding where a roll went, the operator making sure he scanned the right bag.
/// </summary>
[ApiController]
[Route("api/trace")]
[Authorize]
public class TraceController(ITraceService trace) : ApiControllerBase
{
    /// <summary>Scan or type any barcode — a roll, a bag or a pallet.</summary>
    [HttpGet("{barcode}")]
    public async Task<IActionResult> Get(string barcode, CancellationToken cancellationToken)
    {
        return ToResponse(await trace.GetAsync(barcode, cancellationToken));
    }
}
