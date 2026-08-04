using Colors.Domain.Common;

namespace Colors.Domain.Entities.MasterData;

/// <summary>
/// A working shift: A 08:00-16:00, B 16:00-00:00, C 00:00-08:00. No shift crosses
/// midnight, so the date of a shift report is never ambiguous (specification
/// section 2).
/// </summary>
public class Shift : MasterEntity
{
    public TimeOnly StartTime { get; set; }

    /// <summary>00:00 means midnight at the end of the day, as shift B uses it.</summary>
    public TimeOnly EndTime { get; set; }
}
