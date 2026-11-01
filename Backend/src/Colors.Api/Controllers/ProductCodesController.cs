using Colors.Application.Features.MasterData;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// The factory's product codes — the code printed on every bag of a product in a colour
/// (specification section 19.25). Read by anyone signed in; written by the administrator,
/// like the rest of the master data.
/// </summary>
[ApiController]
[Route("api/product-codes")]
[Authorize]
public class ProductCodesController(IProductCodeService codes) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await codes.GetAllAsync(cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Administrator)]
    public async Task<IActionResult> Create(
        [FromBody] SaveProductCodeRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await codes.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.Administrator)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] SaveProductCodeRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await codes.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Administrator)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await codes.DeleteAsync(id, cancellationToken);

        // Named after the row, not the bool that carries the answer, for the audit log.
        return result.IsSuccess ? NoContent() : ToResponse(result, nameof(ProductCodeDto));
    }
}
