using Colors.Domain.Enums;

namespace Colors.Domain.Entities.Recipes;

/// <summary>
/// One exact formula. The factory says "recipe 8" and means a version, not a family
/// (specification section 5) — so the number it says out loud is stored here.
///
/// Frozen once it leaves Draft: rolls reference a version, and years later the
/// question "what exactly made this pallet?" must still have the same answer.
/// </summary>
public class RecipeVersion
{
    public int Id { get; set; }

    /// <summary>
    /// The number the whole factory uses — "recipe 8". Unique across every family,
    /// never reused, and shown wherever an operator picks a recipe.
    /// </summary>
    public int RecipeNumber { get; set; }

    public int RecipeFamilyId { get; set; }

    public RecipeFamily Family { get; set; } = null!;

    /// <summary>Which revision of its family this is: 1, 2, 3…</summary>
    public int VersionNumber { get; set; }

    public RecipeVersionStatus Status { get; set; } = RecipeVersionStatus.Draft;

    public int CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>What changed and why — the supervisor's note to his future self.</summary>
    public string? Notes { get; set; }

    public List<RecipeIngredient> Ingredients { get; set; } = [];

    /// <summary>A version may only be edited while it is still a draft.</summary>
    public bool IsEditable => Status == RecipeVersionStatus.Draft;
}
