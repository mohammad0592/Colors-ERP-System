using Colors.Domain.Enums;

namespace Colors.Domain.Entities.System;

/// <summary>
/// One line in the audit log (specification section 15).
///
/// <b>What it is for.</b> Most of what the factory does already records who did it — a
/// roll names the man who made it, a ticket names who issued it. This table exists for
/// the two kinds of thing that leave no such trace:
///
/// <list type="bullet">
/// <item>
/// <b>Decisions and corrections</b> — a recipe changed, a shift reopened, a bag taken
/// back off a pallet, a stock figure adjusted, somebody's roles changed. These change
/// what the records mean, and the record itself only shows the result, never that it was
/// changed or by whom.
/// </item>
/// <item>
/// <b>Refusals.</b> A rejected scan changes nothing anywhere, so without this line it
/// never happened. But a man scanning wrong bags all evening is exactly what a
/// supervisor wants to see.
/// </item>
/// </list>
///
/// <b>Routine production is deliberately not here.</b> Auditing every roll and every bag
/// would copy what those tables already say, and bury the handful of lines that matter
/// under a thousand that do not.
///
/// Nothing in this table is ever edited or deleted. A log that can be tidied is not a log.
/// </summary>
public class AuditEntry
{
    public int Id { get; set; }

    /// <summary>
    /// Who did it. Null only where nobody was signed in — a background job, or a failed
    /// login, where the whole point is that we do not know who it was.
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// The shift it happened during, where one was open. Lets a supervisor read the log
    /// for his own shift rather than the whole month.
    /// </summary>
    public int? ShiftReportId { get; set; }

    /// <summary>What was attempted, in words: "Reversed", "Recipe version created".</summary>
    public required string Action { get; set; }

    /// <summary>The kind of thing it happened to, e.g. <c>BagPalletAssignment</c>.</summary>
    public required string ObjectType { get; set; }

    /// <summary>Its key, where the thing existed. Null on a refusal that created nothing.</summary>
    public int? ObjectId { get; set; }

    public AuditResult Result { get; set; }

    /// <summary>
    /// What changed, or why it was refused — the refusal message the man saw on screen.
    /// </summary>
    public string? Details { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
