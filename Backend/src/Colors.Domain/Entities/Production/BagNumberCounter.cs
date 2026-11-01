namespace Colors.Domain.Entities.Production;

/// <summary>
/// The number the next bag of the day gets — the digits after the B in every bag code
/// (specification section 19.25).
///
/// One row per day, in the factory's own calendar. The count starts again at 1 every
/// day at midnight, where shift B ends, so a new day is a new row rather than a reset
/// somebody has to remember. The old rows stay: they cost nothing and say how many bags
/// each day made.
/// </summary>
public class BagNumberCounter
{
    /// <summary>The day, as the factory's clock tells it.</summary>
    public DateOnly Day { get; set; }

    /// <summary>The number the next bag that day will be given.</summary>
    public int NextNumber { get; set; } = 1;
}
