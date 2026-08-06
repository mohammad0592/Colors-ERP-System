namespace Colors.Domain.Enums;

/// <summary>
/// Which packaging material the system counts a row as (specification section 10).
///
/// Three of the factory's packaging materials do not need to be typed at all, because
/// what was produced already says how many were used. This is how the system knows
/// which material row is which.
///
/// A setting rather than a check on the material's name. Rename "Large Bags" and a name
/// rule would stop counting silently — no error, just a number quietly going to zero on
/// every shift report from then on, which nobody notices in a bag count.
/// </summary>
public enum CountedPackaging
{
    /// <summary>Typed by hand, like tape and shrink. Used by length and by feel.</summary>
    None = 0,

    /// <summary>One per produced bag, for products packed in one — the plates.</summary>
    LargeBag = 1,

    /// <summary>The product's own <c>SmallBagsPerBag</c> per produced bag.</summary>
    SmallBag = 2,

    /// <summary>One per pallet completed on the shift.</summary>
    WoodenPallet = 3,
}
