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

    /// <summary>Also an event. Nothing sets it yet — there is no dispatch phase.</summary>
    public DateTimeOffset? ShippedAt { get; set; }

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
