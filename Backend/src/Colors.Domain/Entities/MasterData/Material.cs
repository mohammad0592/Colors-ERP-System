using Colors.Domain.Common;
using Colors.Domain.Enums;

namespace Colors.Domain.Entities.MasterData;

/// <summary>
/// A physical material the factory buys and consumes. Identified by its code, never
/// by its name (specification section 4).
/// </summary>
public class Material : MasterEntity
{
    /// <summary>Unique, e.g. MAT0001. The stable identity; names may be rewritten.</summary>
    public string Code { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public MaterialCategory Category { get; set; } = null!;

    /// <summary>
    /// The unit stock is held in — usually kg. Every quantity for this material is
    /// stored in this unit only; packagings convert on the way in.
    /// </summary>
    public int BaseUnitId { get; set; }

    public Unit BaseUnit { get; set; } = null!;

    /// <summary>Below this, the stock report shows the material as low.</summary>
    public decimal MinQuantity { get; set; }

    /// <summary>
    /// Weight of one piece in the base unit, when known — a large bag is 0.085 kg.
    /// Lets the system cross-check weighed packaging against counted packaging
    /// (specification section 10).
    /// </summary>
    public decimal? UnitWeight { get; set; }

    /// <summary>
    /// Which packaging material the system counts this row as, if any
    /// (specification section 10).
    ///
    /// Three of them do not need typing: what the shift produced already says how many
    /// were used. Left empty, the material is typed by hand like tape and shrink — so
    /// the list is not a fixed set of six, it is however many the factory keeps.
    /// </summary>
    public CountedPackaging CountedAs { get; set; }

    /// <summary>
    /// True for the one material the recycler makes (specification section 11).
    ///
    /// Only one row may carry it — a partial unique index sees to that — because the
    /// recycler adds its output to exactly one pile. A flag rather than a search for a
    /// material named "Recycled Material": renaming that row would send a shift's output
    /// nowhere, with no error to notice.
    /// </summary>
    public bool IsRecycledOutput { get; set; }

    /// <summary>Reserved for a future version; v1 barcodes rolls, bags and pallets only.</summary>
    public bool BarcodeTracked { get; set; }

    public string? Notes { get; set; }

    public List<MaterialPackaging> Packagings { get; set; } = [];
}
