using Colors.Domain.Common;

namespace Colors.Domain.Entities.MasterData;

/// <summary>
/// Something the factory makes (specification section 4).
///
/// A product is what a given mould produces from a given kind of material. The two
/// plate moulds each make a normal and an absorbent product; the meal box and clamshell
/// moulds make one each. Nobody chooses a product on screen: the mould comes from the
/// shift, the absorbency comes from the roll's recipe, and those two together are the
/// unique key here.
///
/// Size lives in the name — "Big Plate", "Small Meal Box" — rather than in a separate
/// list, because a clamshell has no size and would have had to carry a meaningless one.
/// </summary>
public class Product : MasterEntity
{
    public int MouldId { get; set; }

    public Mould Mould { get; set; } = null!;

    /// <summary>Plate · Meal Box · Clamshell. For grouping in reports.</summary>
    public int ProductTypeId { get; set; }

    public ProductType ProductType { get; set; } = null!;

    /// <summary>
    /// The NOR/ABS distinction, decided by what was mixed into the roll rather than by
    /// the mould. A flag, never matched on the name.
    /// </summary>
    public bool IsAbsorbent { get; set; }

    /// <summary>
    /// Pieces in one packed bag — 500 for a plate, 250 for a meal box or clamshell.
    /// The multiplier behind <c>PieceCount = BagCount × PiecesPerBag</c>, read from
    /// here so it is never written into the code.
    /// </summary>
    public int PiecesPerBag { get; set; }

    /// <summary>
    /// Small bags consumed making one packed bag. Two for plates, whose big bag holds
    /// two small ones inside; one for a meal box or clamshell, packed in the small bag
    /// directly.
    /// </summary>
    public int SmallBagsPerBag { get; set; } = 1;

    /// <summary>
    /// Large bags consumed making one packed bag — one for a plate, none for a meal box
    /// or clamshell, which go in the small bag directly (specification section 10).
    ///
    /// Its own column rather than a reading of <see cref="SmallBagsPerBag"/>. Two smalls
    /// does mean a big bag holding them today, but that is a guess about why a number is
    /// what it is: a product needing two large bags, or one small inside a large, would
    /// break it silently. Two columns state the two facts.
    /// </summary>
    public int LargeBagsPerBag { get; set; }

    /// <summary>Bags that complete a pallet — 15 for plates, about 21 for the rest.</summary>
    public int BagsPerPallet { get; set; }
}
