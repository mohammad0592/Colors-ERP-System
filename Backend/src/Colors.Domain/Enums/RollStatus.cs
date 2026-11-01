namespace Colors.Domain.Enums;

/// <summary>
/// Where a roll is in its life (specification section 8).
///
/// <see cref="NeedsTest"/> is not a quality gate. The measurements are never compared
/// against limits and no roll is rejected for its numbers — the status only makes sure
/// the measurement was <i>taken</i>, because once the roll is formed into plates there
/// is nothing left to measure.
/// </summary>
public enum RollStatus
{
    /// <summary>Just off the extruder. Cannot go to the thermo until it is measured.</summary>
    NeedsTest = 1,

    /// <summary>Measured, in stock. May sit here for weeks — rolls are used to order, not in order.</summary>
    Available = 2,

    InThermo = 3,

    Processed = 4,

    Scrapped = 5,

    /// <summary>
    /// Taken out of the thermo before it was finished, for a reason somebody gave
    /// (specification section 19.23). Back in stock, and may go into the machine again —
    /// so one roll can have several runs, and is counted once.
    /// </summary>
    NotCompleted = 6,

    /// <summary>
    /// Logged by mistake — a roll that never came off the extruder, or was logged twice
    /// (specification section 19.27). Kept, with who cancelled it and why, so its number
    /// is never handed out again; left out of every report as if it had not been made.
    /// </summary>
    Cancelled = 7,
}
