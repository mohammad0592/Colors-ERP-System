using Colors.Domain.Entities.Shifts;
using Colors.Domain.Enums;

namespace Colors.Domain.Entities.Inventory;

/// <summary>
/// Material leaving the store for a line (specification section 7).
///
/// This is the heart of the waste control, and it exists because of one sentence from
/// the owner: <i>"the workers are not careful about the material."</i> Material out is
/// weighed, leftover back is weighed, and the difference is what was really used —
/// not what somebody remembers using.
/// </summary>
public class MaterialIssueTicket
{
    public int Id { get; set; }

    /// <summary>The number printed on the paper ticket the worker carries.</summary>
    public int TicketNumber { get; set; }

    /// <summary>
    /// The line the material is going to, during which shift. One foreign key answers
    /// which line, which shift and which day.
    /// </summary>
    public int ShiftLineId { get; set; }

    public ShiftLine ShiftLine { get; set; } = null!;

    // BatchId arrives in phase 8, when batches exist. The ticket names the batch and
    // not the recipe: the batch is enough (batch -> rolls -> recipe) and storing the
    // recipe here as well would let the two disagree.

    /// <summary>The inventory manager who issued it.</summary>
    public int IssuedByUserId { get; set; }

    public IssueTicketStatus Status { get; set; } = IssueTicketStatus.Open;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public int? ClosedByUserId { get; set; }

    public string? Notes { get; set; }

    public List<MaterialIssueTicketLine> Lines { get; set; } = [];
}
