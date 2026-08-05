using Colors.Domain.Enums;

namespace Colors.Domain.Entities.Barcodes;

/// <summary>
/// One barcode, for one object (specification section 12).
///
/// A single table owns every barcode in the system rather than a column on each of
/// Rolls, ProducedBags and WoodenPallets. That buys three things a per-table column
/// cannot: uniqueness across the whole system enforced in one place, one lookup for
/// "scan anything and find it", and an immediate answer when a bag is scanned into a
/// pallet field — <i>that is a bag, not a pallet</i>.
///
/// The reference is deliberately polymorphic: <see cref="ObjectType"/> plus
/// <see cref="ObjectId"/>, with no foreign key. Three nullable foreign keys would let
/// a row point at two objects at once, or at none.
/// </summary>
public class Barcode
{
    public int Id { get; set; }

    /// <summary>
    /// What is printed and scanned — <c>R000123</c>, <c>B004501</c>, <c>P000087</c>.
    /// Unique across every object type, and never reused, not even after the object
    /// it named is scrapped.
    /// </summary>
    public required string Value { get; set; }

    public BarcodeObjectType ObjectType { get; set; }

    public int ObjectId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// False when a label is replaced — a torn label reprinted under a new value, say.
    /// The old row stays so an old label scanned months later is still recognised and
    /// can say what happened to it, rather than coming back as unknown.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
