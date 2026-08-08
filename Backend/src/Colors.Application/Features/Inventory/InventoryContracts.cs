using Colors.Application.Common.Models;

namespace Colors.Application.Features.Inventory;

// Shapes crossing the API for the store. Specification section 6.

/// <summary>
/// One material's balance, always in its base unit. <c>IsBelowMinimum</c> is worked out
/// on the server so the screen and the reorder report can never disagree about what
/// counts as low.
/// </summary>
public sealed record MaterialStockDto(
    int MaterialId,
    string Code,
    string Name,
    string CategoryName,
    // True only for raw material. The issue screen offers these and nothing else —
    // packaging is counted from production, not carried out on a ticket.
    bool IssuedOnTickets,
    string BaseUnitName,
    string BaseUnitSymbol,
    decimal CurrentQuantity,
    decimal MinQuantity,
    bool IsBelowMinimum,
    DateTimeOffset? LastUpdated);

/// <summary>A line of the ledger — what moved, which way, who did it and why.</summary>
public sealed record InventoryMovementDto(
    int Id,
    int MaterialId,
    string MaterialCode,
    string MaterialName,
    string MovementTypeName,
    // +1 in, −1 out. The screen shows the sign; the quantity itself never carries one.
    int Direction,
    decimal Quantity,
    string BaseUnitSymbol,
    string UserName,
    DateTimeOffset MovementDate,
    string? Notes);

/// <summary>
/// Booking a delivery in. The storekeeper picks the material, picks the unit it
/// arrived in — pallet, bag or kilogram — and types the number he counted.
/// </summary>
public sealed record ReceiveMaterialRequest(
    int MaterialId,
    // The unit as delivered. The system converts to the base unit.
    int UnitId,
    decimal Quantity,
    string? Notes);

/// <summary>
/// Correcting a balance after a stock count. Always needs a reason: a correction
/// nobody explained is a mystery a month later.
/// </summary>
public sealed record AdjustStockRequest(
    int MaterialId,
    // What the count actually found, in the base unit.
    decimal CountedQuantity,
    string Reason);

/// <summary>What one unit of a material is worth in its base unit, for the receive screen.</summary>
public sealed record ReceivingUnitDto(
    int UnitId,
    string UnitName,
    string UnitSymbol,
    decimal QuantityInBaseUnit,
    bool IsDefault);

/// <summary>The store. Declared here, implemented in Infrastructure (section 0.1).</summary>
public interface IInventoryService
{
    /// <summary>Every material with its balance, including those never yet received.</summary>
    Task<IReadOnlyList<MaterialStockDto>> GetStockAsync(
        bool belowMinimumOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>The units a material may be received in, largest first.</summary>
    Task<Result<IReadOnlyList<ReceivingUnitDto>>> GetReceivingUnitsAsync(
        int materialId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryMovementDto>> GetMovementsAsync(
        int? materialId = null,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<Result<MaterialStockDto>> ReceiveAsync(
        ReceiveMaterialRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Supervisor and administrator only — it overrides what the ledger says.</summary>
    Task<Result<MaterialStockDto>> AdjustAsync(
        AdjustStockRequest request,
        int userId,
        CancellationToken cancellationToken = default);
}
