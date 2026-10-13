namespace Colors.Domain.Entities.Production;

/// <summary>
/// The number the next roll gets — the leading digits of every roll code
/// (specification section 8).
///
/// One row for the whole factory. It counts on across shifts and days and goes back to 1
/// only when a supervisor presses Reset: the factory restarts the count when it chooses,
/// not when the calendar turns. A row rather than "the largest number so far plus one",
/// because after a reset the largest number so far is no longer the one to follow.
/// </summary>
public class RollNumberCounter
{
    /// <summary>Always 1. There is one counter.</summary>
    public int Id { get; set; }

    /// <summary>The number the next roll will be given.</summary>
    public int NextNumber { get; set; } = 1;

    /// <summary>When it was last put back to 1, and by whom. Null before the first reset.</summary>
    public DateTimeOffset? LastResetAt { get; set; }

    public int? LastResetByUserId { get; set; }
}
