namespace Colors.Domain.Entities.Production;

/// <summary>
/// What was measured as the roll left the extruder (specification section 8).
///
/// Written by the Extruder Test Person — today the same man as the operator, holding
/// both roles. Separate from the roll because it is a different event, by a different
/// role, at a different moment: the roll exists first, the measurements come after.
/// </summary>
public class RollTestReport
{
    public int Id { get; set; }

    public int RollId { get; set; }

    public Roll Roll { get; set; } = null!;

    /// <summary>Kilograms.</summary>
    public decimal Weight { get; set; }

    public decimal Length { get; set; }

    /// <summary>
    /// Grams, from a sample plate pressed from this roll. Measured again after
    /// forming, on the thermo side — a gap between the two points at a forming problem.
    /// </summary>
    public decimal PlateWeight { get; set; }

    // Four readings across the roll, named by position rather than 1..4 because that
    // is what the gauge and the Roll Log app show the man taking them.

    public decimal ThicknessRs { get; set; }

    public decimal ThicknessRm { get; set; }

    public decimal ThicknessLm { get; set; }

    public decimal ThicknessLs { get; set; }

    /// <summary>
    /// The mean of the four. Calculated, never stored — a fifth column could only ever
    /// disagree with the readings it comes from.
    /// </summary>
    public decimal AverageThickness =>
        Math.Round((ThicknessRs + ThicknessRm + ThicknessLm + ThicknessLs) / 4m, 3);

    /// <summary>
    /// Whether the average came out inside the product's range, decided when the readings
    /// were saved (specification section 19.1). Null where the product had no range.
    ///
    /// <b>Stored, not worked out.</b> The readings are frozen on this row, but the range is
    /// master data and will be edited — the factory has not even measured it yet. Worked
    /// out afresh, every roll made last year would change its verdict the day somebody
    /// corrected a range, and a report read in March would say something different in May.
    /// This is the calculated-or-stored test in section 0.1, failing in the same way
    /// PieceCount does.
    /// </summary>
    public bool? ThicknessInSpec { get; set; }

    public int TestedByUserId { get; set; }

    public DateTimeOffset TestedAt { get; set; }

    public string? Notes { get; set; }
}
