namespace Colors.Domain.Enums;

/// <summary>
/// Whether a shift is still running. Specification section 2: production may only be
/// recorded against an Open report, and closing is what makes "did every shift get
/// its readings?" answerable.
/// </summary>
public enum ShiftReportStatus
{
    /// <summary>Running. Rolls, thermo runs and material tickets may be recorded.</summary>
    Open = 0,

    /// <summary>Finished. Nothing more may be posted to it without an administrator reopening it.</summary>
    Closed = 1,

    /// <summary>
    /// Reopened to fix its own record while another shift is running
    /// (specification section 2).
    ///
    /// Its electricity, times, downtime, workers and notes can all be corrected. What it
    /// will not take is anything <i>produced</i> — no batch, roll, thermo run, pallet or
    /// ticket — because those belong to the shift the men are actually standing in.
    ///
    /// This is what lets the one-open-shift rule stay strict without trapping a
    /// supervisor who noticed a missing meter reading an hour too late.
    /// </summary>
    Correcting = 2,
}
