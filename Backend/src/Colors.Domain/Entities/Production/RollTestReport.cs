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

    public int TestedByUserId { get; set; }

    public DateTimeOffset TestedAt { get; set; }

    public string? Notes { get; set; }
}
