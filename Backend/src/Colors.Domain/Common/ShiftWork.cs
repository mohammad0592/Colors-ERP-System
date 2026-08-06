using Colors.Domain.Entities.Shifts;
using Colors.Domain.Enums;

namespace Colors.Domain.Common;

/// <summary>
/// Whether work may be recorded against a shift, and what to say when it may not
/// (specification section 2).
///
/// One place, because five services ask the same question — batches, rolls, thermo runs,
/// pallets and issue tickets — and a shift that is being corrected is not the same
/// situation as one that is closed. Telling a man "this shift is closed" about a shift
/// he can plainly see is open would send him looking for a fault that is not there.
/// </summary>
public static class ShiftWork
{
    /// <summary>Only a running shift takes new work. A shift under correction does not.</summary>
    public static bool AcceptsWork(ShiftReportStatus status) => status == ShiftReportStatus.Open;

    /// <summary>The sentence to show when it does not. Needs the report's Shift loaded.</summary>
    public static string RefusalFor(ShiftReport report) => report.Status switch
    {
        ShiftReportStatus.Correcting =>
            $"Shift {report.Shift.Name} on {report.ProductionDate:dd/MM/yyyy} was reopened to "
            + "fix its record, not to work on. Record this against the shift that is running.",
        _ =>
            $"Shift {report.Shift.Name} on {report.ProductionDate:dd/MM/yyyy} is closed.",
    };
}
