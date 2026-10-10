using Colors.Domain.Common;

namespace Colors.Domain.Entities.Recipes;

/// <summary>
/// One of the factory's main recipes — Normal, Absorbent, Lunch Box (specification
/// section 5). A roll is told apart from another by its main recipe and its colour;
/// the exact percentages live in the versions.
/// </summary>
public class RecipeFamily : MasterEntity
{
    /// <summary>
    /// The family's short form inside a roll code — <c>N</c> for Normal, <c>Abs</c>
    /// for Absorbent, <c>LN</c> for Lunch Box (specification section 8).
    ///
    /// Not a fixed length, so a future family of any length needs no code change. Its
    /// own column rather than a rule over the name, because a rename must never
    /// silently rewrite what the codes on the factory floor mean.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// True for the Absorbent family. Copied onto every bag, because a pallet may only
    /// hold one type and the check must not walk five joins on each barcode scan.
    /// Never matched on the family's name — names must not drive logic.
    /// </summary>
    public bool IsAbsorbent { get; set; }

    public string? Description { get; set; }

    public List<RecipeVersion> Versions { get; set; } = [];
}
