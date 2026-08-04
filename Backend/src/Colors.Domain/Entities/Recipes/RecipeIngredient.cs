using Colors.Domain.Entities.MasterData;

namespace Colors.Domain.Entities.Recipes;

/// <summary>
/// One material in one recipe version, with the range the supervisor allows.
///
/// The percentages are parts per hundred resin, confirmed by the factory's own
/// worked example: GPPS at 100 with talc at 1 and nucleating at 1.5–2 on top
/// (specification section 5). So the base resin rows total 100 and the additives
/// are measured against them — the whole list does not add up to 100.
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

    /// <summary>What the operator aims for.</summary>
    public decimal TargetPercentage { get; set; }

    public decimal MinPercentage { get; set; }

    public decimal MaxPercentage { get; set; }
}
