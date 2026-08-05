namespace Colors.Domain.Enums;

/// <summary>
/// Where a pallet is in its life (specification section 10).
///
/// Never stored. Read off two dates and the bags actually on it — see
/// <see cref="Entities.Packaging.WoodenPallet.Status"/>.
/// </summary>
public enum PalletStatus
{
    /// <summary>Started, nothing on it. It has no colour and no product yet.</summary>
    Empty = 1,

    /// <summary>Holding bags, room for more. Its colour and product are now fixed.</summary>
    Opened = 2,

    /// <summary>Full — the product's own bags-per-pallet was reached.</summary>
    Completed = 3,

    Shipped = 4,
}
