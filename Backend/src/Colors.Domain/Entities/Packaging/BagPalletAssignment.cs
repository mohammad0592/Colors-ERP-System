using Colors.Domain.Entities.Production;

namespace Colors.Domain.Entities.Packaging;

/// <summary>
/// One bag put on one pallet (specification section 10).
///
/// The most repeated manual action in the factory, so who did it is recorded on every
/// row. Without a barcode on each bag none of this is possible: the real form shows
/// pallets of 15 built from rolls of 12, 9 and 14, so a pallet holds bags from more than
/// one roll and nothing else could say which roll a bag came from.
///
/// <b>Rows are never deleted.</b> A wrong scan is undone by filling in the three
/// reversal columns, which sends the bag back to <c>Available</c> and leaves both events
/// in the history.
/// </summary>
public class BagPalletAssignment
{
    public int Id { get; set; }

    public int ProducedBagId { get; set; }

    public ProducedBag ProducedBag { get; set; } = null!;

    public int WoodenPalletId { get; set; }

    public WoodenPallet WoodenPallet { get; set; } = null!;

    public int AssignedByUserId { get; set; }

    public DateTimeOffset AssignedAt { get; set; }

    // ---- the undo, when a bag went on the wrong pallet ---------------------
    // All three move together: a reversal without a reason is not a correction, it is
    // a second mistake.

    public int? ReversedByUserId { get; set; }

    public DateTimeOffset? ReversedAt { get; set; }

    public string? ReversalReason { get; set; }

    /// <summary>True while this row still puts the bag on the pallet.</summary>
    public bool IsActive => ReversedAt is null;
}
