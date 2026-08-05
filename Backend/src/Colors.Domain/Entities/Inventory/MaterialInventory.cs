using Colors.Domain.Entities.MasterData;

namespace Colors.Domain.Entities.Inventory;

/// <summary>
/// What is in the store, one row per material, always in that material's base unit
/// (specification section 6).
///
/// Rolls, bags and pallets are deliberately not here. Each is a unique object with its
/// own status, and counting them is a query — storing them twice would mean two places
/// to update and two numbers free to disagree.
/// </summary>
public class MaterialInventory
{
    /// <summary>The material is the key. One material, one balance, no room for two.</summary>
    public int MaterialId { get; set; }

    public Material Material { get; set; } = null!;

    /// <summary>
    /// A deliberate cached total of the movements, kept because the stock screen reads
    /// it constantly. It is only ever written in the same transaction as the movement
    /// that changed it, so the two cannot drift apart.
    /// </summary>
    public decimal CurrentQuantity { get; set; }

    public DateTimeOffset LastUpdated { get; set; }
}
