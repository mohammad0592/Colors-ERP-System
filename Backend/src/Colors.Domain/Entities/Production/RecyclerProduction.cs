using Colors.Domain.Entities.Shifts;

namespace Colors.Domain.Entities.Production;

/// <summary>
/// What the recycler produced in one line of one shift (specification section 11).
///
/// <b>One number, because one number is all the factory can measure.</b> Scrap sits in
/// two silos, one big and one small, and is drawn out to be ground — there is no moment
/// when a shift's scrap is on a scale. So the weight going in is not recorded, and
/// nothing that depended on it exists either: no loss percentage, and no comparison
/// against what the thermo calculated.
///
/// Written once, at the end of the shift — a unique index on <see cref="ShiftLineId"/>
/// sees to that, because a second record would add the same output to the store twice.
/// </summary>
public class RecyclerProduction
{
    public int Id { get; set; }

    /// <summary>The recycling line's part of the shift this belongs to.</summary>
    public int ShiftLineId { get; set; }

    public ShiftLine ShiftLine { get; set; } = null!;

    /// <summary>
    /// Recycled material weighed out, in kilograms. This is what goes back into the
    /// store, and it is the whole record.
    /// </summary>
    public decimal RecycledMaterialWeight { get; set; }

    public int RecordedByUserId { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public string? Notes { get; set; }
}
