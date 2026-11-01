namespace Colors.Domain.Enums;

/// <summary>
/// Where a packed bag is in its life (specification section 9).
///
/// Shorter than <see cref="RollStatus"/> because a bag has a shorter life: it is made,
/// it goes on a pallet, and that is all. There is no "needs test" — the measuring
/// happened to the run that produced it, not to the bag.
/// </summary>
public enum ProducedBagStatus
{
    /// <summary>Made and waiting. May be scanned onto a pallet.</summary>
    Available = 1,

    /// <summary>On a pallet. It cannot be put on a second one.</summary>
    Assigned = 2,

    /// <summary>
    /// Torn, dirty or badly formed — it goes to the recycler, not onto a pallet
    /// (specification section 19.27). Its packaging was used, so it still counts there.
    /// </summary>
    Defective = 3,

    /// <summary>
    /// Never existed: made by a count typed wrong, and taken away when the count was
    /// corrected (section 19.27). Its label no longer scans.
    /// </summary>
    Cancelled = 4,
}
