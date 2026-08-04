using System.Security.Claims;
using Colors.Application.Features.Recipes;
using Colors.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// Recipes — the four families and every version of them.
///
/// Reading is open to any signed-in worker, because the extruder operator picks a
/// recipe to start a batch. Writing belongs to the Administrator and the Supervisor:
/// specification section 3 gives recipe authoring to both, since the supervisor is
/// the one who actually adjusts percentages.
/// </summary>
[ApiController]
[Route("api/recipes")]
[Authorize]
public class RecipesController(IRecipeService recipes) : ApiControllerBase
{
    private const string CanEdit = $"{RoleNames.Administrator},{RoleNames.Supervisor}";

    // ---------- families ----------

    [HttpGet("families")]
    public async Task<IActionResult> GetFamilies(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await recipes.GetFamiliesAsync(includeInactive, cancellationToken));
    }

    [HttpPost("families")]
    [Authorize(Roles = CanEdit)]
    public async Task<IActionResult> CreateFamily(
        [FromBody] SaveRecipeFamilyRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await recipes.CreateFamilyAsync(request, cancellationToken));
    }

    [HttpPut("families/{id:int}")]
    [Authorize(Roles = CanEdit)]
    public async Task<IActionResult> UpdateFamily(
        int id,
        [FromBody] SaveRecipeFamilyRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await recipes.UpdateFamilyAsync(id, request, cancellationToken));
    }

    [HttpPut("families/{id:int}/active")]
    [Authorize(Roles = CanEdit)]
    public async Task<IActionResult> SetFamilyActive(
        int id,
        [FromBody] Application.Features.MasterData.SetActiveRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await recipes.SetFamilyActiveAsync(id, request.IsActive, cancellationToken));
    }

    // ---------- versions ----------

    [HttpGet("versions")]
    public async Task<IActionResult> GetVersions(
        [FromQuery] int? familyId = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await recipes.GetVersionsAsync(familyId, cancellationToken));
    }

    [HttpGet("versions/{id:int}")]
    public async Task<IActionResult> GetVersion(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await recipes.GetVersionAsync(id, cancellationToken));
    }

    [HttpPost("versions")]
    [Authorize(Roles = CanEdit)]
    public async Task<IActionResult> CreateVersion(
        [FromBody] CreateRecipeVersionRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await recipes.CreateVersionAsync(request, CurrentUserId(), cancellationToken));
    }

    [HttpPut("versions/{id:int}")]
    [Authorize(Roles = CanEdit)]
    public async Task<IActionResult> UpdateVersion(
        int id,
        [FromBody] UpdateRecipeVersionRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await recipes.UpdateVersionAsync(id, request, cancellationToken));
    }

    /// <summary>Copies a recipe into a new draft — the "try a small change" path.</summary>
    [HttpPost("versions/{id:int}/copy")]
    [Authorize(Roles = CanEdit)]
    public async Task<IActionResult> CopyVersion(
        int id,
        [FromBody] CopyRecipeVersionRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(await recipes.CopyVersionAsync(id, request, CurrentUserId(), cancellationToken));
    }

    /// <summary>Puts a draft into production and retires the family's previous one.</summary>
    [HttpPost("versions/{id:int}/promote")]
    [Authorize(Roles = CanEdit)]
    public async Task<IActionResult> PromoteVersion(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await recipes.PromoteVersionAsync(id, cancellationToken));
    }

    [HttpDelete("versions/{id:int}")]
    [Authorize(Roles = CanEdit)]
    public async Task<IActionResult> DeleteDraft(int id, CancellationToken cancellationToken)
    {
        var result = await recipes.DeleteDraftAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToResponse(result);
    }

    /// <summary>
    /// Who is writing. Taken from the token, never from the request body — otherwise
    /// a caller could claim someone else wrote a recipe.
    /// </summary>
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
