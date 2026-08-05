using Colors.Domain.Common;

namespace Colors.Domain.Entities.MasterData;

/// <summary>Groups materials for filtering: Raw Material, Packaging Material, Consumable.</summary>
public class MaterialCategory : MasterEntity
{
    /// <summary>
    /// Whether material in this category leaves the store on an issue ticket
    /// (specification sections 4 and 7). True for raw material only.
    ///
    /// The two kinds leave in completely different ways. Resin and additives go out
    /// weighed, against a ticket, and the leftover is weighed back in — that
    /// subtraction is the factory's waste control. Packaging goes to the bench, never
    /// comes back, and is not weighed at all: the system works out the bags and
    /// pallets from what was produced (section 11).
    ///
    /// So packaging on a ticket would be counted twice and would ask somebody to weigh
    /// things that are counted in pieces.
    /// </summary>
    public bool IssuedOnTickets { get; set; }
}
