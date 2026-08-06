using Colors.Application.Common.Models;

namespace Colors.Application.Features.Inventory;

/// <summary>
/// Everything the factory has made, in one list. Specification sections 8 to 10.
///
/// Rolls, bags and pallets are three different things, but the storekeeper asking
/// "where is this?" does not care which — he has a label in his hand. So they are
/// answered together, in one shape, and the barcode is the thing they share.
/// </summary>
public sealed record ProducedStockItemDto(
    // Roll · Bag · Pallet.
    string Kind,
    int Id,
    string Barcode,
    // What a person reads: the roll code, the product code, or the pallet number.
    string Code,
    string Description,
    string Status,
    // Whether it is still usable, whichever kind it is.
    bool IsAvailable,
    // Where it is now — its batch, the pallet it sits on, the line that built it.
    string Whereabouts,
    decimal? Weight,
    // A roll only. Bags and pallets are counted, not measured by length.
    decimal? Length,
    int? PieceCount,
    DateOnly ProductionDate,
    DateTimeOffset CreatedAt);

/// <summary>Everything printed on one label (specification section 12).</summary>
public sealed record BarcodeLabelDto(
    string Barcode,
    string Kind,
    // The line a man reads and can type when the label is torn.
    string HeadlineCode,
    // The roll a bag came from. The factory already prints this today.
    string? RollCode,
    // AB500B — the kind of bag, not the bag. Text only, never the barcode.
    string? ProductCode,
    string? ProductName,
    string? ColorName,
    int? PieceCount,
    decimal? Weight,
    decimal? Length,
    string? ShiftName,
    DateOnly ProductionDate,
    DateTimeOffset CreatedAt);

/// <summary>
/// Produced stock and the labels that go on it.
///
/// Declared here, implemented in Infrastructure.
/// </summary>
public interface IProducedStockService
{
    /// <summary>
    /// <paramref name="kind"/> is Roll, Bag or Pallet; null means all three.
    /// <paramref name="search"/> matches a barcode, a roll code, a colour or a product.
    /// </summary>
    Task<IReadOnlyList<ProducedStockItemDto>> GetAsync(
        string? kind = null,
        string? status = null,
        string? search = null,
        bool availableOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>What goes on the label for one barcode.</summary>
    Task<Result<BarcodeLabelDto>> GetLabelAsync(
        string barcode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The labels for a whole run at once — one thermo form makes a dozen or more bags,
    /// and they are printed together as one job.
    ///
    /// Returned in the order asked for, so the labels come off the printer in the order
    /// the bags were made. A barcode that resolves to nothing is skipped rather than
    /// failing the sheet: one bad code must not stop the other thirteen printing.
    /// </summary>
    Task<IReadOnlyList<BarcodeLabelDto>> GetLabelsAsync(
        IReadOnlyList<string> barcodes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Asked for as a body rather than a query string: a run can make a couple of hundred
/// bags, and that many codes in a URL is a length limit waiting to be hit.
/// </summary>
public sealed record LabelSheetRequest(IReadOnlyList<string> Barcodes);
