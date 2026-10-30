using Colors.Domain.Entities.Shifts;

namespace Colors.Domain.Entities.Production;

/// <summary>
/// One roll put through the thermo (specification section 9). One roll goes in whole —
/// a roll is never split. Usually one of these per roll; more when a roll was taken out
/// before it was finished and went in again later (section 19.23), but only ever one run
/// that finishes it.
///
/// <b>Why this exists when the roll already has a status.</b> A roll made on 18 July by
/// Ali may be used on 2 August by Omar. The roll's own columns already hold
/// <i>18 July, shift A, Ali</i> — that is its birth at the extruder. <i>2 August,
/// shift B, Omar, in at 09:10, out at 10:00</i> is a different event with nowhere else
/// to live. A status cannot carry it: <c>Processed</c> says the roll was used, not when
/// or by whom, and when a status changes its old value is gone.
/// </summary>
public class ThermoProduction
{
    public int Id { get; set; }

    /// <summary>One run finishes a roll; the runs before it, if any, were left unfinished.</summary>
    public int RollId { get; set; }

    public Roll Roll { get; set; } = null!;

    /// <summary>
    /// The thermo's part of a shift: which line, which shift, which day. Its line must
    /// form bags. What is being made comes from the roll, not the line.
    /// </summary>
    public int ShiftLineId { get; set; }

    public ShiftLine ShiftLine { get; set; } = null!;

    public int OperatorUserId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Null while the roll is still in the machine.</summary>
    public DateTimeOffset? FinishedAt { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Why the roll came out before it was finished (specification section 19.23). Null
    /// for a run that finished its roll. Required when it is set: a roll back in stock
    /// half used needs the reason with it.
    /// </summary>
    public string? UnfinishedReason { get; set; }

    /// <summary>The roll came out of this run unfinished and went back to stock.</summary>
    public bool LeftUnfinished => UnfinishedReason is not null;

    public ThermoTestReport? TestReport { get; set; }

    public List<ProducedBag> Bags { get; set; } = [];

    /// <summary>
    /// الزمن الكلي on the paper form — the total time in the machine. Calculated from
    /// the two timestamps on this row, both of which come from a scan and neither of
    /// which can ever change. A stored copy could only disagree with them
    /// (specification section 0.1).
    /// </summary>
    public int? TotalTimeMinutes => FinishedAt is null
        ? null
        : (int)Math.Round((FinishedAt.Value - StartedAt).TotalMinutes);
}
