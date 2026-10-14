namespace Colors.Domain.Entities.Production;

/// <summary>
/// What was tested as the roll left the extruder (specification section 8): the sample
/// plate and the four thickness readings. The weight and length used to be here too, and
/// moved to the roll itself in section 19.12.
///
/// Written by the Extruder Test Person — today the same man as the operator, holding
/// both roles. Separate from the roll because it is a different event, by a different
/// role, at a different moment: the roll exists first, the measurements come after.
///
/// Filled in a reading at a time and saved as it goes (specification section 19.15): the
/// absorbency alone takes ten minutes in water, and nothing typed before that should wait
/// on it. Every reading is therefore optional here, and <see cref="IsComplete"/> says when
/// the test is done.
/// </summary>
public class RollTestReport
{
    /// <summary>How long the absorbency sample stays in the water before it is weighed again.</summary>
    public static readonly TimeSpan SoakTime = TimeSpan.FromMinutes(10);

    public int Id { get; set; }

    public int RollId { get; set; }

    public Roll Roll { get; set; } = null!;

    /// <summary>
    /// Grams, from a sample plate pressed from this roll. Measured again after
    /// forming, on the thermo side — a gap between the two points at a forming problem.
    /// </summary>
    public decimal? PlateWeight { get; set; }

    // Four readings across the roll, named by position rather than 1..4 because that
    // is what the gauge and the Roll Log app show the man taking them. Null until taken.

    public decimal? ThicknessRs { get; set; }

    public decimal? ThicknessRm { get; set; }

    public decimal? ThicknessLm { get; set; }

    public decimal? ThicknessLs { get; set; }

    /// <summary>
    /// The mean of the four, once all four are in. Calculated, never stored — a fifth
    /// column could only ever disagree with the readings it comes from.
    /// </summary>
    public decimal? AverageThickness =>
        ThicknessRs is { } rs && ThicknessRm is { } rm && ThicknessLm is { } lm && ThicknessLs is { } ls
            ? Math.Round((rs + rm + lm + ls) / 4m, 3)
            : null;

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

    // ---- absorbency, absorbent rolls only (specification section 19.14) ---------------
    // A piece of the roll is weighed dry, left in water for ten minutes, and weighed
    // again. Both weights are kept, in grams; the percentage is worked out from them.

    /// <summary>The sample's weight before the water. Null on a roll that is not absorbent.</summary>
    public decimal? AbsorbencyDryWeight { get; set; }

    /// <summary>The same sample after ten minutes in water.</summary>
    public decimal? AbsorbencyWetWeight { get; set; }

    /// <summary>
    /// When the dry weight was first saved — the moment the piece went into the water. The
    /// wet weight is taken <see cref="SoakTime"/> after it, and the screens count down to
    /// it from here, so a tablet put down and picked up again still knows how long is left.
    /// </summary>
    public DateTimeOffset? AbsorbencyDryWeighedAt { get; set; }

    /// <summary>
    /// The water it took up, as a share of its dry weight:
    /// (wet − dry) ÷ dry × 100. Calculated, never stored — the two weights are frozen on
    /// this row, so the percentage can never disagree with them.
    /// </summary>
    public decimal? AbsorbencyPercentage =>
        AbsorbencyDryWeight is > 0 && AbsorbencyWetWeight is not null
            ? Math.Round((AbsorbencyWetWeight.Value - AbsorbencyDryWeight.Value) / AbsorbencyDryWeight.Value * 100m, 2)
            : null;

    /// <summary>
    /// Every reading the roll asks for is in: the plate, the four thicknesses, and on an
    /// absorbent roll both absorbency weights. Until then the roll stays at Needs Test.
    /// </summary>
    public bool IsComplete(bool isAbsorbent) =>
        PlateWeight is not null
        && AverageThickness is not null
        && (!isAbsorbent || AbsorbencyWetWeight is not null);

    /// <summary>Whoever saved it last, and when.</summary>
    public int TestedByUserId { get; set; }

    public DateTimeOffset TestedAt { get; set; }

    public string? Notes { get; set; }
}
