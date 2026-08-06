using Colors.Application.Common.Models;

namespace Colors.Application.Features.Pallets;

/// <summary>Shapes crossing the API for pallets. Specification section 10.</summary>

public sealed record PalletSummaryDto(
    int Id,
    int PalletNumber,
    string Barcode,
    int ShiftLineId,
    string ProductionLineName,
    string ShiftName,
    DateOnly ProductionDate,
    // Both null until the first bag is scanned.
    int? ColorId,
    string? ColorName,
    int? ProductId,
    string? ProductName,
    // Worked out from two dates and the bags on it, never stored.
    string Status,
    bool IsOpen,
    // COUNT and SUM over the assignments — a pallet holds a couple of dozen at most.
    int BagCount,
    int PieceCount,
    decimal Weight,
    // From the product this pallet took off its first bag. Null while it is empty.
    int? Capacity,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record PalletBagDto(
    int AssignmentId,
    int ProducedBagId,
    string Barcode,
    string RollCode,
    decimal Weight,
    int PieceCount,
    string AssignedByName,
    DateTimeOffset AssignedAt,
    // A reversed row stays on the pallet's history and says who undid it and why.
    bool IsActive,
    string? ReversedByName,
    DateTimeOffset? ReversedAt,
    string? ReversalReason);

public sealed record PalletDto(
    int Id,
    int PalletNumber,
    string Barcode,
    int ShiftLineId,
    string ProductionLineName,
    string ShiftName,
    DateOnly ProductionDate,
    int? ColorId,
    string? ColorName,
    int? ProductId,
    string? ProductName,
    string Status,
    bool IsOpen,
    int BagCount,
    int PieceCount,
    decimal Weight,
    int? Capacity,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ShippedAt,
    // Set only on a cancelled pallet. Its wooden pallet went back to the store.
    DateTimeOffset? CancelledAt,
    string? CancelledByName,
    string? CancellationReason,
    string? Notes,
    IReadOnlyList<PalletBagDto> Bags);

/// <summary>A bag waiting to go on a pallet.</summary>
public sealed record AvailableBagDto(
    int Id,
    string Barcode,
    string RollCode,
    int ColorId,
    string ColorName,
    int ProductId,
    string ProductName,
    decimal Weight,
    int PieceCount,
    DateTimeOffset CreatedAt);

/// <summary>Starts an empty pallet. It has no colour and no product until a bag lands on it.</summary>
public sealed record StartPalletRequest(int ShiftLineId, string? Notes);

/// <summary>
/// Puts one bag on one pallet.
///
/// The operator scans; the id is there for the office. Nothing else is given — the
/// colour and the product come from the bag itself.
/// </summary>
public sealed record ScanBagRequest(string? BagBarcode, int? ProducedBagId);

/// <summary>
/// Undoes a wrong scan. The reason is required: a reversal without one is not a
/// correction, and the row is kept for ever either way.
/// </summary>
public sealed record ReverseAssignmentRequest(string Reason);

/// <summary>
/// Cancels a pallet started by mistake and sends its wooden pallet back to the store.
/// Only an empty one, and the reason is required for the same reason a reversal needs
/// one.
/// </summary>
public sealed record CancelPalletRequest(string Reason);

/// <summary>
/// Pallets (specification section 10).
///
/// Declared here, implemented in Infrastructure.
/// </summary>
public interface IPalletService
{
    Task<IReadOnlyList<PalletSummaryDto>> GetPalletsAsync(
        int? shiftLineId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default);

    Task<Result<PalletDto>> GetPalletAsync(
        int palletId,
        CancellationToken cancellationToken = default);

    /// <summary>Bags that have been made and not yet put on a pallet.</summary>
    Task<IReadOnlyList<AvailableBagDto>> GetAvailableBagsAsync(
        int? palletId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an empty pallet and takes its wooden pallet out of the store. The store
    /// never goes below nothing, so a factory with no wood cannot start one.
    /// </summary>
    Task<Result<PalletDto>> StartPalletAsync(
        StartPalletRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Sends the wooden pallet back. Refused once a bag is on it.</summary>
    Task<Result<PalletDto>> CancelPalletAsync(
        int palletId,
        CancelPalletRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The first bag sets the pallet's colour and product; every later bag must match
    /// both. Reaching the product's own bags-per-pallet completes it.
    /// </summary>
    Task<Result<PalletDto>> ScanBagAsync(
        int palletId,
        ScanBagRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Sends the bag back to the store. The assignment row stays, marked.</summary>
    Task<Result<PalletDto>> ReverseAssignmentAsync(
        int assignmentId,
        ReverseAssignmentRequest request,
        int userId,
        CancellationToken cancellationToken = default);
}
