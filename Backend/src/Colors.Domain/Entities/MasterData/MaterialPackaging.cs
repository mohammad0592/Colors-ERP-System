namespace Colors.Domain.Entities.MasterData;

/// <summary>
/// One pack size a material arrives in: GPPS comes as a bag of 25 kg and a pallet of
/// 750 kg. The storekeeper receives "1 pallet" and the system converts to the base
/// unit (specification section 4). A material may have several rows, so the same
/// material in two pack sizes needs no code change.
/// </summary>
public class MaterialPackaging
{
    public int Id { get; set; }

    public int MaterialId { get; set; }

    /// <summary>The pack: bag, pallet, piece.</summary>
    public int UnitId { get; set; }

    public Unit Unit { get; set; } = null!;

    /// <summary>How much of the base unit one pack holds — 25 for a 25 kg bag.</summary>
    public decimal QuantityInBaseUnit { get; set; }

    /// <summary>Pre-selected on the receiving screen.</summary>
    public bool IsDefaultReceiving { get; set; }
}
