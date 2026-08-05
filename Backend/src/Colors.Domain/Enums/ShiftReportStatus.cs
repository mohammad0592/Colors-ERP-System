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
}
