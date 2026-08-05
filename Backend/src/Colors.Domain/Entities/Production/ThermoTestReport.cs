using Colors.Domain.Entities.MasterData;

namespace Colors.Domain.Entities.Production;

/// <summary>
/// What was measured after the roll was formed (specification section 9).
///
/// Written by the Thermo Test Person, from the factory's own form. Separate from the
/// run for the same reason the roll's test is separate from the roll: different role,
/// different moment.
///
/// <b>Saving this creates the bags.</b> The bag count lives here, and it does not exist
/// until the end of the run — which is exactly when the factory counts them.
///
/// <b>No duplication.</b> Roll weight, length and thickness are on the paper form but
/// are not here: they already exist in <see cref="RollTestReport"/>. The screen shows
/// them read-only, in the position the operator is used to.
/// </summary>
public class ThermoTestReport
{
    public int Id { get; set; }

    public int ThermoProductionId { get; set; }

    public ThermoProduction ThermoProduction { get; set; } = null!;

    /// <summary>
    /// حجم الصنف — what was made. Never chosen on screen: the mould comes from the
    /// shift and the absorbency from the roll's recipe, and those two are the unique
    /// key on <see cref="MasterData.Product"/>. Stored here because a mould may be
    /// swapped later in the shift, so history must be fixed at the moment it happened.
    /// </summary>
    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    /// <summary>عدد الأكياس المنتجة من الرول — the one count the man enters.</summary>
    public int BagCount { get; set; }

    /// <summary>
    /// عدد الصحون المنتجة من الرول — plates, boxes or clamshells.
    /// <c>BagCount × product.PiecesPerBag</c>, worked out at save and then frozen.
    ///
    /// The one derived number this design stores. <c>PiecesPerBag</c> is master data
    /// and master data gets edited, so a live formula would let a change made next year
    /// rewrite what last year produced (specification section 0.1). Never typed, so it
    /// still cannot be mistyped.
    /// </summary>
    public int PieceCount { get; set; }

    /// <summary>وزن الصحن المنتج، غرام — measured again after forming. A gap against the roll's sample points at a forming problem.</summary>
    public decimal PieceWeight { get; set; }

    /// <summary>وزن الكيس المنتج الواحد، كغم.</summary>
    public decimal BagWeight { get; set; }

    /// <summary>
    /// نسبة الإمتصاص للصحن المنتج % — ABS recipes only. Zero for Normal, and a value
    /// above zero on a Normal roll is refused: absorbency comes from what was mixed,
    /// so a Normal roll cannot have absorbed anything.
    /// </summary>
    public decimal AbsorbentPercentage { get; set; }

    public int TestedByUserId { get; set; }

    public DateTimeOffset TestedAt { get; set; }

    public string? Notes { get; set; }
}
