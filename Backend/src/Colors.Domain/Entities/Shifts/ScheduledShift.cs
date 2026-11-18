using Colors.Domain.Entities.MasterData;

namespace Colors.Domain.Entities.Shifts;

/// <summary>
/// A shift planned the day before: which shift, who supervises, which lines run and who
/// operates each (specification section 19.30).
///
/// It is not a shift yet. Nothing can be recorded against it, and the reports never see
/// it. At its start time the system opens the real <see cref="ShiftReport"/> from it, and
/// from then on that is the shift — this row only remembers that it was planned, and
/// which shift it became.
/// </summary>
public class ScheduledShift
{
    public int Id { get; set; }

    /// <summary>The day it is for. Only ever tomorrow, when it is first saved.</summary>
    public DateOnly ProductionDate { get; set; }

    public int ShiftId { get; set; }

    public Shift Shift { get; set; } = null!;

    public int? SupervisorUserId { get; set; }

    /// <summary>
    /// Open it as the extruder's starting shift, putting the roll numbers back to 01
    /// (specification section 19.16). Off unless the supervisor ticks it.
    /// </summary>
    public bool ExtruderStartingShift { get; set; }

    /// <summary>The lines that will run, each with its operator.</summary>
    public List<ScheduledShiftLine> Lines { get; set; } = [];

    public int ScheduledByUserId { get; set; }

    public DateTimeOffset ScheduledAt { get; set; }

    public int? ChangedByUserId { get; set; }

    public DateTimeOffset? ChangedAt { get; set; }

    /// <summary>The shift it became, once it opened. Null while it is still a plan.</summary>
    public int? ShiftReportId { get; set; }

    public ShiftReport? ShiftReport { get; set; }

    /// <summary>
    /// Why it never opened, when it did not — the system was off for the whole shift, or
    /// the shift it opened was discarded. Set once, and the plan is finished with: without
    /// it the next check would try to open the shift again.
    /// </summary>
    public string? NotOpenedReason { get; set; }
}

/// <summary>One line planned to run on a scheduled shift, and the man who will run it.</summary>
public class ScheduledShiftLine
{
    public int Id { get; set; }

    public int ScheduledShiftId { get; set; }

    public ScheduledShift ScheduledShift { get; set; } = null!;

    public int ProductionLineId { get; set; }

    public ProductionLine ProductionLine { get; set; } = null!;

    /// <summary>
    /// The line's operator. May be left open and named on the day, the same as on a shift
    /// opened by hand.
    /// </summary>
    public int? OperatorUserId { get; set; }
}
