using Colors.Domain.Common;
using Colors.Domain.Entities.MasterData;

namespace Colors.Domain.Entities.Recipes;

/// <summary>
/// One of the factory's four formulas — Normal, Normal Black, ABS, ABS Black
/// (specification section 5). The family says what kind of product it makes; the
/// exact percentages live in its versions.
/// </summary>
public class RecipeFamily : MasterEntity
{
    /// <summary>
    /// The family's short form inside a roll code — <c>N</c> for Normal, <c>Abs</c>
    /// for Absorbent (specification section 8).
    ///
    /// Not a fixed length, so a future family of any length needs no code change. Its
    /// own column rather than a rule over the name, because a rename must never
    /// silently rewrite what the codes on the factory floor mean.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    public int ProductTypeId { get; set; }

    public ProductType ProductType { get; set; } = null!;

    /// <summary>True for the Black families, which replace 35% of the GPPS with recycle.</summary>
    public bool UsesRecycle { get; set; }

    /// <summary>
    /// True for the ABS families. Copied onto every bag, because a pallet may only
    /// hold one type and the check must not walk five joins on each barcode scan.
    /// Never matched on the family's name — names must not drive logic.
    /// </summary>
    public bool IsAbsorbent { get; set; }

    public string? Description { get; set; }

    public List<RecipeVersion> Versions { get; set; } = [];
}
