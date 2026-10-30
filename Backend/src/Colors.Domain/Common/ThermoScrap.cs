using Colors.Domain.Entities.Production;

namespace Colors.Domain.Common;

/// <summary>
/// What a forming run threw away (specification section 9).
///
/// The roll went in whole and came out as plates plus skeleton. What the plates do not
/// weigh is scrap, and it goes to the recycler:
///
/// <code>scrap = roll weight − (plates × plate weight)</code>
///
/// <b>Calculated, never stored.</b> Every input is frozen the moment the run is counted
/// — the roll's weight on its own test report, the piece count and the plate weight on
/// the thermo's — so there is nothing here that a later edit to master data could
/// rewrite (specification section 0.1).
///
/// <b>Not the recycler's loss.</b> That one is scrap lost over scrap ground, and answers
/// a question about the grinder. This is a question about the forming machine, and it is
/// true whether or not anything is ever recycled — which is why it lives here and shows
/// on the thermo's own screens (specification section 11).
///
/// The formula lives in one place so the screen, the shift total and the reports cannot
/// drift into three slightly different answers.
/// </summary>
public static class ThermoScrap
{
    /// <summary>
    /// Kilograms thrown away, or null where the run cannot say: an unweighed roll, a run
    /// that has not been counted yet, or one that left its roll unfinished. Never zero for
    /// those — zero would read as a run that wasted nothing.
    ///
    /// A roll worked more than once (section 19.23) is weighed once, so its scrap is said
    /// once: on the run that finishes it, as the roll's weight less the plates of every
    /// run — <paramref name="earlierPlatesKg"/> being those of the unfinished runs before.
    /// Null there while one of those is not counted yet.
    /// </summary>
    public static decimal? For(ThermoProduction run, decimal? earlierPlatesKg)
    {
        var rollWeight = run.Roll.Weight;
        var counted = run.TestReport;

        if (run.LeftUnfinished || rollWeight is null || counted is null || earlierPlatesKg is null)
        {
            return null;
        }

        return Math.Round(
            Between(rollWeight.Value, counted.PieceCount, counted.PieceWeight) - earlierPlatesKg.Value,
            3);
    }

    /// <summary>
    /// The same sum from three loose numbers, for callers reading a projection rather
    /// than whole entities — the reports, mostly.
    ///
    /// It exists so the formula is written once. A report that repeated it would be free
    /// to disagree with the screen the operator read it off, and the two would drift
    /// apart the first time either was touched.
    /// </summary>
    public static decimal Between(decimal rollWeight, int pieceCount, decimal pieceWeight) =>
        // The plate is weighed in grams, the roll in kilograms.
        Math.Round(rollWeight - (pieceCount * pieceWeight / 1000m), 3);
}
