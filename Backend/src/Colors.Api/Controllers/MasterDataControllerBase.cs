using Colors.Application.Features.MasterData;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// The four endpoints every master data list exposes. A concrete controller adds
/// only its route and its service.
///
/// Reading is open to any signed-in worker — production screens fill their pickers
/// from these lists. Writing is the administrator's alone (specification section 3),
/// and there is no delete: master data is deactivated so history keeps resolving.
/// </summary>
[ApiController]
[Authorize]
public abstract class MasterDataControllerBase<TDto, TUpsert>(IMasterListService<TDto, TUpsert> service)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await service.GetAllAsync(includeInactive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Administrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] TUpsert request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await service.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.Administrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] TUpsert request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await service.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPut("{id:int}/active")]
    [Authorize(Roles = RoleNames.Administrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetActive(
        int id,
        [FromBody] SetActiveRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await service.SetActiveAsync(id, request.IsActive, cancellationToken));
    }

    /// <summary>
    /// Removes a row nothing references — a typo, a test. A referenced row is
    /// refused with a message naming what uses it (specification section 4).
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Administrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToResponse(result);
    }
}
