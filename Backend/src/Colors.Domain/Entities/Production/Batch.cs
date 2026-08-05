using Colors.Domain.Entities.Shifts;

namespace Colors.Domain.Entities.Production;

/// <summary>
/// One mix (specification section 8).
///
/// The batch is the smallest unit that knows its materials: one mix makes fifteen to
/// seventeen rolls, so nothing smaller can say what went into it. A batch never
/// crosses a shift, because all material goes back to the store at shift end.
///
/// <b>There is deliberately no recipe here.</b> It is not yet certain whether one
/// batch always uses one recipe, and a required column would force the operator to
/// enter something false. The recipe lives on the roll, where it is certain — and if
/// the factory later confirms one recipe per batch, a nullable column can be added
/// with no rebuild (specification section 18, question 5).
/// </summary>
public class Batch
{
    public int Id { get; set; }

    /// <summary>The number the factory says out loud — "batch 47".</summary>
    public int BatchNumber { get; set; }

    /// <summary>The extruder's part of a shift: which line, which shift, which day.</summary>
    public int ShiftLineId { get; set; }

    public ShiftLine ShiftLine { get; set; } = null!;

    public int CreatedByUserId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Null while the mix is still being drawn from.</summary>
    public DateTimeOffset? FinishedAt { get; set; }

    public string? Notes { get; set; }

    public List<Roll> Rolls { get; set; } = [];
}
