using Colors.Domain.Entities.MasterData;

namespace Colors.Domain.Entities.Recipes;

/// <summary>
/// One material in one recipe version, at one percentage.
///
/// The percentages are parts per hundred resin, confirmed by the factory's own
/// worked example: GPPS at 100 with talc at 1 and nucleating at 1.8 on top
/// (specification section 5). So the base resin rows total 100 and the additives
/// are measured against them — the whole list does not add up to 100.
///
/// There is one number per material, not a range. The factory works to a figure, and
/// a minimum and maximum beside it were boxes nobody filled in with anything but the
/// same number three times.
/// </summary>
public class RecipeIngredient
{
    public int Id { get; set; }

    public int RecipeVersionId { get; set; }

    public int MaterialId { get; set; }

    public Material Material { get; set; } = null!;

    /// <summary>
    /// True for GPPS and Recycle — the polymer that forms the 100% base. False for
    /// everything added on top of it. Without this flag a validator would try to make
    /// the whole list total 100 and reject every real recipe.
    /// </summary>
    public bool IsBaseResin { get; set; }

    /// <summary>The percentage of this material in the mix.</summary>
    public decimal TargetPercentage { get; set; }
}
