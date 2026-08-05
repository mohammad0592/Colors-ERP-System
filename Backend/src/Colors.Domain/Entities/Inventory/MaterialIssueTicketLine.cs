using Colors.Domain.Entities.MasterData;

namespace Colors.Domain.Entities.Inventory;

/// <summary>
/// One material on a ticket (specification section 7).
///
/// Both quantities are <b>weighed</b>, which is what makes the numbers real rather
/// than remembered.
/// </summary>
public class MaterialIssueTicketLine
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    public int MaterialId { get; set; }

    public Material Material { get; set; } = null!;

    /// <summary>What left the store, in the material's base unit.</summary>
    public decimal IssuedQuantity { get; set; }

    /// <summary>
    /// What came back, weighed at the end of the shift. Zero until the leftover is
    /// returned, which is the normal state of an open ticket.
    /// </summary>
    public decimal ReturnedQuantity { get; set; }

    /// <summary>
    /// What was actually used. Calculated, never stored — it is derivable from the
    /// two weighings, and a third column could only ever disagree with them.
    /// </summary>
    public decimal NetUsed => IssuedQuantity - ReturnedQuantity;
}
