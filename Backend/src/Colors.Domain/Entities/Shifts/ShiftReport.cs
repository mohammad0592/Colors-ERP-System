using Colors.Domain.Entities.MasterData;
using Colors.Domain.Enums;

namespace Colors.Domain.Entities.Shifts;

/// <summary>
/// One date, one shift, for the whole factory (specification section 2).
///
/// "Shift A on 4 August" is one thing to the people running it, not three. The lines
/// that actually ran hang underneath as <see cref="ShiftLine"/>s, because the times,
/// the meter and the machine belong to a line while the day belongs to the shift.
/// </summary>
public class ShiftReport
{
    public int Id { get; set; }

    /// <summary>
    /// The day the shift belongs to. A date, not a timestamp: no shift crosses
    /// midnight, so this is a plain business fact and never a moment in time.
    /// </summary>
    public DateOnly ProductionDate { get; set; }

    public int ShiftId { get; set; }

    public Shift Shift { get; set; } = null!;

    public ShiftReportStatus Status { get; set; } = ShiftReportStatus.Open;

    /// <summary>Who is answerable for this shift's figures.</summary>
    public int? SupervisorUserId { get; set; }

    // The factory has a single meter for the whole building, so the reading belongs to
    // the shift. Recording it per line would write the same meter down three times on
    // a day when all three ran, and any total would be triple the truth.

    public decimal? ElectricityStartMeter { get; set; }

    public decimal? ElectricityEndMeter { get; set; }

    public string? Notes { get; set; }

    public int OpenedByUserId { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    public int? ClosedByUserId { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>The lines that ran. A line that stood still is simply not here.</summary>
    public List<ShiftLine> Lines { get; set; } = [];

    /// <summary>
    /// Electricity used, end meter minus start. Calculated, never stored — the
    /// specification is explicit that a value derivable from two others is not kept
    /// as a third that can disagree with them.
    /// </summary>
    public decimal? ElectricityUsed =>
        ElectricityStartMeter is null || ElectricityEndMeter is null
            ? null
            : ElectricityEndMeter - ElectricityStartMeter;
}
