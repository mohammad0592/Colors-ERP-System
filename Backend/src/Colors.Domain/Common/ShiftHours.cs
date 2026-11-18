using Colors.Domain.Entities.MasterData;

namespace Colors.Domain.Common;

/// <summary>
/// When a shift on a given day starts and ends, on the factory's clock
/// (specification section 2).
///
/// One place, because the end needs care: shift B ends at 00:00, which is midnight at the
/// end of its day, not the start of it. Read naively, B would end eight hours before it
/// began, and a schedule would close it the moment it opened.
/// </summary>
public static class ShiftHours
{
    public static DateTime StartOf(DateOnly date, Shift shift) =>
        date.ToDateTime(shift.StartTime);

    public static DateTime EndOf(DateOnly date, Shift shift)
    {
        var end = date.ToDateTime(shift.EndTime);
        return end <= StartOf(date, shift) ? end.AddDays(1) : end;
    }
}
