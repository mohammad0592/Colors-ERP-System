using Colors.Application.Features.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// The home screen (specification section 13).
///
/// Open to every signed-in worker, unlike the reports. It answers "what is running, and
/// what is waiting for someone" — which the man on the thermo needs as much as the
/// supervisor does — and it writes nothing.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(IDashboardService dashboard) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        return ToResponse(await dashboard.GetAsync(cancellationToken));
    }
}
