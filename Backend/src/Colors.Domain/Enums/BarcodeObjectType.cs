namespace Colors.Domain.Enums;

/// <summary>
/// The three things v1 barcodes (specification section 12). Raw materials are
/// explicitly not among them.
///
/// The type is stored beside the id because the reference is polymorphic — it is what
/// lets a scan answer <i>"that is a bag, not a pallet"</i> instead of failing a search.
/// </summary>
public enum BarcodeObjectType
{
    Roll = 1,
    Bag = 2,
    Pallet = 3,
}
