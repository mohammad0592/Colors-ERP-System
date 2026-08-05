using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Shifts;

namespace Colors.Domain.Entities.Inventory;

/// <summary>
/// One change to the store — the ledger behind every balance (specification section 6).
///
/// Nobody edits a quantity by hand. A stock count that disagrees with the system is
/// itself a movement, with a reason, so the history explains how a number became what
/// it is.
/// </summary>
public class MaterialInventoryMovement
{
    public int Id { get; set; }

    public int MaterialId { get; set; }

    public Material Material { get; set; } = null!;

    public int MovementTypeId { get; set; }

    /// <summary>Carries the direction, so this row never needs a sign.</summary>
    public MovementType MovementType { get; set; } = null!;

    /// <summary>Always positive, always in the material's base unit.</summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// The shift it happened during, where there was one.
    ///
    /// Nullable on purpose: a delivery can arrive at the gate before the morning shift
    /// is opened, and refusing to book it in until somebody opens a shift would send
    /// the storekeeper looking for a supervisor instead of doing his job.
    /// </summary>
    public int? ShiftReportId { get; set; }

    public ShiftReport? ShiftReport { get; set; }

    /// <summary>
    /// The ticket that caused this, where one did.
    ///
    /// Naming the cause is what lets you go both ways: from a stock change to its
    /// reason, and from a ticket to every kilogram it moved. The recycler run and the
    /// packaging record join it in phases 11 and 12.
    /// </summary>
    public int? IssueTicketId { get; set; }

    public int UserId { get; set; }

    public DateTimeOffset MovementDate { get; set; }

    /// <summary>Required for an adjustment: a correction with no reason is a mystery.</summary>
    public string? Notes { get; set; }
}
