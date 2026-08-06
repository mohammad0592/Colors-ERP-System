using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Shifts;

namespace Colors.Domain.Entities.Packaging;

/// <summary>
/// What packaging a shift used (specification section 10).
///
/// Recorded once, at the end, by the Packaging Operator — one per line per shift. It is
/// a header with a line per material rather than a column per material, so adding a new
/// packaging material is a row in Master Data and not a migration.
/// </summary>
public class PackagingConsumption
{
    public int Id { get; set; }

    /// <summary>The line's part of the shift. One consumption each, at shift end.</summary>
    public int ShiftLineId { get; set; }

    public ShiftLine ShiftLine { get; set; } = null!;

    public int RecordedByUserId { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public string? Notes { get; set; }

    public List<PackagingConsumptionLine> Lines { get; set; } = [];
}

/// <summary>
/// One packaging material on one shift's record.
///
/// The factory writes down <b>both a count and a weight</b> — العدد and الوزن — so both
/// are here. Some materials have both, some only one: tape and wooden pallets are
/// counted and not weighed.
/// </summary>
public class PackagingConsumptionLine
{
    public int Id { get; set; }

    public int ConsumptionId { get; set; }

    public PackagingConsumption Consumption { get; set; } = null!;

    public int MaterialId { get; set; }

    public Material Material { get; set; } = null!;

    /// <summary>
    /// Individual units, not packs — a shift that used 61 large bags stores 61.
    ///
    /// Decimal even so, because tape and shrink are genuinely part-used: half a roll of
    /// tape is 0.5, and the real form records exactly that.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>Kilograms, where the factory weighs it. Null where it does not.</summary>
    public decimal? Weight { get; set; }

    /// <summary>
    /// True when the quantity came from what the shift produced rather than from a
    /// person, and then <b>frozen</b> at that value.
    ///
    /// Frozen for the same reason the piece count is: it comes from the product's
    /// bags-per-bag figures, which are master data and get edited. Recomputing it live
    /// would let a change made next year rewrite what last year consumed
    /// (specification section 0.1).
    /// </summary>
    public bool WasCounted { get; set; }

    /// <summary>
    /// What the count says this should have weighed, when the material has a unit
    /// weight. Calculated — its two inputs are on this row and on a material that
    /// cannot change what already happened.
    ///
    /// The gap against <see cref="Weight"/> is packaging wasted, torn or used
    /// elsewhere, and it costs nobody any extra work: the count comes from production
    /// and the weight is already on the paper form.
    /// </summary>
    public decimal? ExpectedWeight =>
        Material?.UnitWeight is null ? null : Math.Round(Quantity * Material.UnitWeight.Value, 3);
}
