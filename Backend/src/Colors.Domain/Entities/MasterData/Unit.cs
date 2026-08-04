using Colors.Domain.Common;

namespace Colors.Domain.Entities.MasterData;

/// <summary>How a material is measured — kilogram, piece, bag, pallet, roll.</summary>
public class Unit : MasterEntity
{
    /// <summary>Short form shown after a number: "kg", "pcs".</summary>
    public string Symbol { get; set; } = string.Empty;
}
