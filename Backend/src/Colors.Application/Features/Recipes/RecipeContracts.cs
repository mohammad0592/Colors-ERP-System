using Colors.Application.Common.Models;

namespace Colors.Application.Features.Recipes;

/// <summary>Shapes crossing the API for recipes. Specification section 5.</summary>

public sealed record RecipeFamilyDto(
    int Id,
    string Name,
    int ProductTypeId,
    string ProductTypeName,
    bool UsesRecycle,
    bool IsAbsorbent,
    string? Description,
    bool IsActive,
    // The number of the version in production, when the family has one.
    int? CurrentRecipeNumber,
    int VersionCount);

public sealed record SaveRecipeFamilyRequest(
    string Name,
    int ProductTypeId,
    bool UsesRecycle,
    bool IsAbsorbent,
    string? Description);

public sealed record RecipeIngredientDto(
    int MaterialId,
    string MaterialCode,
    string MaterialName,
    bool IsBaseResin,
    decimal TargetPercentage,
    decimal MinPercentage,
    decimal MaxPercentage);

public sealed record SaveRecipeIngredientRequest(
    int MaterialId,
    bool IsBaseResin,
    decimal TargetPercentage,
    decimal MinPercentage,
    decimal MaxPercentage);

/// <summary>A version in a list — enough for the table, without its ingredients.</summary>
public sealed record RecipeVersionSummaryDto(
    int Id,
    int RecipeNumber,
    int RecipeFamilyId,
    string FamilyName,
    int VersionNumber,
    string Status,
    bool IsEditable,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    string? Notes,
    int IngredientCount);

/// <summary>A version with its full formula.</summary>
public sealed record RecipeVersionDto(
    int Id,
    int RecipeNumber,
    int RecipeFamilyId,
    string FamilyName,
    int VersionNumber,
    string Status,
    bool IsEditable,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    string? Notes,
    IReadOnlyList<RecipeIngredientDto> Ingredients);

/// <summary>
/// Creates a draft. The recipe number and version number are given by the system,
/// so two people writing recipes at once cannot collide.
/// </summary>
public sealed record CreateRecipeVersionRequest(
    int RecipeFamilyId,
    string? Notes,
    IReadOnlyList<SaveRecipeIngredientRequest> Ingredients);

/// <summary>Edits a draft. Frozen versions refuse this.</summary>
public sealed record UpdateRecipeVersionRequest(
    string? Notes,
    IReadOnlyList<SaveRecipeIngredientRequest> Ingredients);

/// <summary>
/// Copies any version into a new draft — the "try a small change" path the factory
/// uses constantly (specification section 5).
/// </summary>
public sealed record CopyRecipeVersionRequest(string? Notes);

/// <summary>
/// Recipes: the families and their versions.
///
/// Declared here, implemented in Infrastructure — this layer must not know how the
/// data is stored (specification section 0.1).
/// </summary>
public interface IRecipeService
{
    Task<IReadOnlyList<RecipeFamilyDto>> GetFamiliesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<Result<RecipeFamilyDto>> CreateFamilyAsync(
        SaveRecipeFamilyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RecipeFamilyDto>> UpdateFamilyAsync(
        int id,
        SaveRecipeFamilyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RecipeFamilyDto>> SetFamilyActiveAsync(
        int id,
        bool isActive,
        CancellationToken cancellationToken = default);

    /// <summary>Every version, newest first, optionally narrowed to one family.</summary>
    Task<IReadOnlyList<RecipeVersionSummaryDto>> GetVersionsAsync(
        int? familyId = null,
        CancellationToken cancellationToken = default);

    Task<Result<RecipeVersionDto>> GetVersionAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<RecipeVersionDto>> CreateVersionAsync(
        CreateRecipeVersionRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<Result<RecipeVersionDto>> UpdateVersionAsync(
        int id,
        UpdateRecipeVersionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Copies a version into a new draft, ingredients and all.</summary>
    Task<Result<RecipeVersionDto>> CopyVersionAsync(
        int id,
        CopyRecipeVersionRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a draft into production. The family's previous Current version becomes
    /// Archived in the same transaction, and this one is frozen for ever.
    /// </summary>
    Task<Result<RecipeVersionDto>> PromoteVersionAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>Discards a draft. Only drafts — nothing in production is ever removed.</summary>
    Task<Result<bool>> DeleteDraftAsync(
        int id,
        CancellationToken cancellationToken = default);
}
