using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Shifts;
using Colors.Domain.Enums;

namespace Colors.Domain.Entities.Packaging;

/// <summary>
/// A pallet of bags (specification section 10).
///
/// An empty pallet has no colour and no product. The first bag scanned onto it sets
/// both, and every later bag must match — the rule the factory gave in its own words:
/// <i>"when the pallet is empty, the pallet will take the first scanned bag's
/// characteristics."</i>
///
/// <b>Nothing here counts.</b> Bag count and piece count are read off the assignments,
/// because a pallet holds a couple of dozen at most. Neither is the status stored — see
/// <see cref="Status"/>.
/// </summary>
public class WoodenPallet
{
    public int Id { get; set; }

    /// <summary>The number the factory says out loud. Never reused.</summary>
    public int PalletNumber { get; set; }

    /// <summary>The forming line's part of the shift this pallet was built on.</summary>
    public int ShiftLineId { get; set; }

    public ShiftLine ShiftLine { get; set; } = null!;

    /// <summary>Null until the first bag is scanned, then fixed.</summary>
    public int? ColorId { get; set; }

    public Color? Color { get; set; }

    /// <summary>
    /// Null until the first bag is scanned. Once set it also decides how many bags
    /// complete the pallet, through the product's own <c>BagsPerPallet</c> — 15 for
    /// plates, about 21 for the rest. Never a number in the code.
    /// </summary>
    public int? ProductId { get; set; }

    public Product? Product { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set when the last bag the product allows goes on. An event, so a date.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Also an event: the pallet left the factory. Only a finished pallet can, which the
    /// database itself enforces — <c>ck_pallets_dates_in_order</c> refuses a shipping
    /// date without a completion date, or one that comes before it.
    /// </summary>
    public DateTimeOffset? ShippedAt { get; set; }

    /// <summary>Who released it. Travels with <see cref="ShippedAt"/>, never alone.</summary>
    public int? ShippedByUserId { get; set; }

    /// <summary>
    /// Set when a shipping was undone, and cleared again the moment the pallet ships for
    /// real. So these three describe a pallet that is <i>back in the factory</i> after a
    /// wrong scan — never one standing shipped.
    ///
    /// The longer history is not lost by clearing them: a pallet is one of the types the
    /// audit interceptor watches, so every one of these changes is already a line in the
    /// log with its old and new value (specification section 15).
    /// </summary>
    public DateTimeOffset? ShippingReversedAt { get; set; }

    public int? ShippingReversedByUserId { get; set; }

    /// <summary>Required to undo a shipping, exactly as a reversal needs one.</summary>
    public string? ShippingReversalReason { get; set; }

    /// <summary>
    /// Set when a pallet started by mistake is cancelled. Only ever on an empty one:
    /// once a bag is on it the wood is under the bags and the pallet is real.
    /// </summary>
    public DateTimeOffset? CancelledAt { get; set; }

    public int? CancelledByUserId { get; set; }

    /// <summary>Required to cancel, exactly as a reversal needs one.</summary>
    public string? CancellationReason { get; set; }

    public string? Notes { get; set; }

    public List<BagPalletAssignment> Assignments { get; set; } = [];

    /// <summary>
    /// Worked out, never stored (specification section 10).
    ///
    /// Shipping and completing are events, so they are dates. Empty and Opened are not
    /// events at all — they are only <i>does this pallet have bags on it</i>, which the
    /// assignments already answer. A stored status would be a second copy of that, free
    /// to drift into saying <c>Opened</c> about a pallet holding nothing.
    /// </summary>
    public PalletStatus Status
    {
        get
        {
            // First, because it is the one state that ends the pallet's life without it
            // ever having been a pallet of anything.
            if (CancelledAt is not null)
            {
                return PalletStatus.Cancelled;
            }

            if (ShippedAt is not null)
            {
                return PalletStatus.Shipped;
            }

            if (CompletedAt is not null)
            {
                return PalletStatus.Completed;
            }

            return Assignments.Any(a => a.ReversedAt is null)
                ? PalletStatus.Opened
                : PalletStatus.Empty;
        }
    }
}
