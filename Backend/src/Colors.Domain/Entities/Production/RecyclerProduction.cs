using Colors.Domain.Entities.Shifts;

namespace Colors.Domain.Entities.Production;

/// <summary>
/// What the recycler did in one line of one shift (specification section 11).
///
/// Scrap is collected off the floor and weighed, ground, and the output weighed again.
/// Written once, at the end of the shift — a unique index on <see cref="ShiftLineId"/>
/// sees to that, because a second record would add the same output to the store twice.
/// </summary>
public class RecyclerProduction
{
    public int Id { get; set; }

    /// <summary>The recycling line's part of the shift this belongs to.</summary>
    public int ShiftLineId { get; set; }

    public ShiftLine ShiftLine { get; set; } = null!;

    /// <summary>Scrap collected and weighed in, in kilograms.</summary>
    public decimal ScrapWeight { get; set; }

    /// <summary>
    /// Recycled material weighed out, in kilograms. This is what goes back into the
    /// store.
    ///
    /// It may be <b>more</b> than <see cref="ScrapWeight"/>: the recycler grinds what is
    /// in front of it, and that can include a pile left from an earlier shift.
    /// </summary>
    public decimal RecycledMaterialWeight { get; set; }

    public int RecordedByUserId { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Worked out, never stored (specification section 11): every input is on this row
    /// and frozen the moment it is written.
    ///
    /// Null where no scrap was weighed, because a share of nothing is not zero — it is
    /// not a number at all, and showing 0% would read as a perfect shift.
    ///
    /// Negative where more came out than went in, which says this shift ground scrap it
    /// did not collect.
    /// </summary>
    public decimal? LossPercentage =>
        ScrapWeight == 0
            ? null
            : (ScrapWeight - RecycledMaterialWeight) / ScrapWeight * 100m;
}
