using System.Security.Claims;
using Colors.Application.Features.Printing;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// The label printer's queue (specification section 19.13).
///
/// Anyone signed in may send labels to it — every screen that makes a roll, a bag or a
/// pallet prints one. Reading and working the queue is for the computer with the printer:
/// the Label Printer role, the supervisor and the administrator.
/// </summary>
[ApiController]
[Route("api/print-jobs")]
[Authorize]
public class PrintJobsController(IPrintQueueService queue) : ApiControllerBase
{
    private const string CanPrint =
        $"{RoleNames.Administrator},{RoleNames.Supervisor},{RoleNames.LabelPrinter}";

    [HttpPost]
    public async Task<IActionResult> Queue(
        [FromBody] QueuePrintJobRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await queue.QueueAsync(request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Waiting jobs, oldest first; or the recent ones of any status.</summary>
    [HttpGet]
    [Authorize(Roles = CanPrint)]
    public async Task<IActionResult> Get(
        [FromQuery] bool pendingOnly = true,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await queue.GetAsync(pendingOnly, take, cancellationToken));
    }

    [HttpPost("{id:int}/printed")]
    [Authorize(Roles = CanPrint)]
    public async Task<IActionResult> MarkPrinted(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await queue.MarkPrintedAsync(id, CurrentUserId(), cancellationToken));
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = CanPrint)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await queue.CancelAsync(id, CurrentUserId(), cancellationToken));
    }

    /// <summary>Who is acting. From the token, never from the body.</summary>
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
