namespace Colors.Domain.Entities.Packaging;

/// <summary>
/// The number the next pallet finished that day gets — the two digits after the P in
/// every pallet code (specification section 19.26).
///
/// One row per day, as for the bags (<c>BagNumberCounter</c>): the count starts again at 1
/// at midnight, where shift B ends, because a new day is a new row.
/// </summary>
public class PalletNumberCounter
{
    /// <summary>The day, as the factory's clock tells it.</summary>
    public DateOnly Day { get; set; }

    /// <summary>The number the next pallet finished that day will be given.</summary>
    public int NextNumber { get; set; } = 1;
}
