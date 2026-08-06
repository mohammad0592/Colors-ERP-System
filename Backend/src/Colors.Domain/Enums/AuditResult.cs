namespace Colors.Domain.Enums;

/// <summary>
/// Whether the thing somebody tried actually happened (specification section 15).
/// </summary>
public enum AuditResult
{
    /// <summary>It happened, and the record changed.</summary>
    Success = 1,

    /// <summary>
    /// The system refused it. Nothing changed anywhere else, which is exactly why it has
    /// to be written down here — a man scanning wrong bags all evening leaves no other
    /// trace at all.
    /// </summary>
    Rejected = 2,
}
