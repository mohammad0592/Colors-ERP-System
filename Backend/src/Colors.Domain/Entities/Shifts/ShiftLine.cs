using Colors.Domain.Entities.MasterData;

namespace Colors.Domain.Entities.Shifts;

/// <summary>
/// One production line's part of a shift (specification section 2).
///
/// This is what production points at: a single foreign key that answers *which line,
/// which shift, which day* at once. The extruder and the thermo rarely run identical
/// hours, so the times and the downtime live here rather than on the shift.
/// </summary>
public class ShiftLine
{
    public int Id { get; set; }

    public int ShiftReportId { get; set; }

    public ShiftReport ShiftReport { get; set; } = null!;

    public int ProductionLineId { get; set; }

    public ProductionLine ProductionLine { get; set; } = null!;

    // ---- what the paper form records --------------------------------------

    /// <summary>When production actually started, which may differ from the shift's nominal start.</summary>
    public TimeOnly? ProductionStartTime { get; set; }

    public TimeOnly? ProductionEndTime { get; set; }

    /// <summary>Hours this line stood still. Subtracted from its running time.</summary>
    public decimal? DowntimeHours { get; set; }

    /// <summary>
    /// Which template is bolted into the machine this shift. Forming line only.
    ///
    /// Changing a mould is heavy work, so one is mounted at the start and runs all
    /// shift. It may still be swapped while the shift is open — that costs nothing,
    /// because every run stores the product it actually made, so history is fixed at
    /// the moment it happened.
    /// </summary>
    public int? MouldId { get; set; }

    public Mould? Mould { get; set; }

    // No electricity here: the factory has one meter for the whole building, so the
    // reading belongs to the shift (specification section 2).

    // ---- machine settings, thermo only ------------------------------------
    // Units from the real form: cycles per hour, millimetres, seconds. Guarded by
    // ProductionLine.RecordsMachineSettings.

    /// <summary>Forming speed, cycles per hour — 580 on the July form.</summary>
    public int? MachineSpeed { get; set; }

    /// <summary>Feed distance in millimetres — 1220 on the July form.</summary>
    public int? FeedDistanceMm { get; set; }

    /// <summary>Seconds per forming cycle — 8 on the July form.</summary>
    public decimal? CycleTimeSeconds { get; set; }

    /// <summary>Who worked this line. A man works the extruder or the thermo, not "the shift".</summary>
    public List<ShiftWorker> Workers { get; set; } = [];

    /// <summary>
    /// Hours actually producing: end minus start, less downtime. Also calculated.
    /// The July form recorded 08:00 to 16:00 with no downtime, and wrote 8.
    /// </summary>
    public decimal? ActualProductionHours
    {
        get
        {
            if (ProductionStartTime is null || ProductionEndTime is null)
            {
                return null;
            }

            var start = ProductionStartTime.Value.ToTimeSpan();
            var end = ProductionEndTime.Value.ToTimeSpan();

            // 00:00 as an end time means midnight at the end of the day, as shift B
            // uses it — otherwise 16:00 to 00:00 would come out as minus sixteen hours.
            if (end <= start)
            {
                end += TimeSpan.FromHours(24);
            }

            return (decimal)(end - start).TotalHours - (DowntimeHours ?? 0);
        }
    }
}
