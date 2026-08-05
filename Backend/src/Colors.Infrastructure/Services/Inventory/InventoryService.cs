using Colors.Application.Common.Models;
using Colors.Application.Features.Inventory;
using Colors.Domain.Constants;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Colors.Infrastructure.Services.Inventory;

/// <summary>
/// The store. Specification section 6.
///
/// One rule holds the whole thing up: <b>stock may never go negative</b>. Every
/// balance change goes through <see cref="MoveAsync"/>, which takes a row lock on the
/// balance, checks it, and writes the movement and the new total in one transaction.
/// A check in code alone would not do — two tablets can pass the same check in the
/// same millisecond.
/// </summary>
public class InventoryService(
    ColorsDbContext db,
    StockLedger ledger,
    ILogger<InventoryService> logger) : IInventoryService
{
    public async Task<IReadOnlyList<MaterialStockDto>> GetStockAsync(
        bool belowMinimumOnly = false,
        CancellationToken cancellationToken = default)
    {
        // A left join, so a material that has never been received still appears — at
        // zero, and below its minimum, which is exactly what the storekeeper needs to
        // see rather than a blank row missing from the list.
        var rows = await db.Materials
            .Where(m => m.IsActive)
            .Include(m => m.Category)
            .Include(m => m.BaseUnit)
            .Select(m => new
            {
                Material = m,
                Inventory = db.MaterialInventory.FirstOrDefault(i => i.MaterialId == m.Id),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new MaterialStockDto(
                r.Material.Id,
                r.Material.Code,
                r.Material.Name,
                r.Material.Category.Name,
                r.Material.Category.IssuedOnTickets,
                r.Material.BaseUnit.Name,
                r.Material.BaseUnit.Symbol,
                r.Inventory?.CurrentQuantity ?? 0m,
                r.Material.MinQuantity,
                (r.Inventory?.CurrentQuantity ?? 0m) < r.Material.MinQuantity,
                r.Inventory?.LastUpdated))
            .Where(dto => !belowMinimumOnly || dto.IsBelowMinimum)
            .OrderBy(dto => dto.Code)
            .ToList();
    }

    public async Task<Result<IReadOnlyList<ReceivingUnitDto>>> GetReceivingUnitsAsync(
        int materialId,
        CancellationToken cancellationToken = default)
    {
        var material = await db.Materials
            .Include(m => m.BaseUnit)
            .FirstOrDefaultAsync(m => m.Id == materialId, cancellationToken);

        if (material is null)
        {
            return Result<IReadOnlyList<ReceivingUnitDto>>.Failure(
                ErrorCode.NotFound,
                "This material does not exist.");
        }

        var packs = await db.MaterialPackagings
            .Include(p => p.Unit)
            .Where(p => p.MaterialId == materialId)
            .ToListAsync(cancellationToken);

        // The base unit is always offered, even with no pack sizes set up: material
        // does arrive loose, and the storekeeper must never be stuck.
        var units = new List<ReceivingUnitDto>
        {
            new(material.BaseUnitId, material.BaseUnit.Name, material.BaseUnit.Symbol, 1m,
                packs.All(p => !p.IsDefaultReceiving)),
        };

        units.AddRange(packs
            .Where(p => p.UnitId != material.BaseUnitId)
            .Select(p => new ReceivingUnitDto(
                p.UnitId,
                p.Unit.Name,
                p.Unit.Symbol,
                p.QuantityInBaseUnit,
                p.IsDefaultReceiving)));

        return Result<IReadOnlyList<ReceivingUnitDto>>.Success(
            units.OrderByDescending(u => u.QuantityInBaseUnit).ToList());
    }

    public async Task<IReadOnlyList<InventoryMovementDto>> GetMovementsAsync(
        int? materialId = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var movements = await db.MaterialInventoryMovements
            .Include(m => m.Material).ThenInclude(m => m.BaseUnit)
            .Include(m => m.MovementType)
            .Where(m => materialId == null || m.MaterialId == materialId)
            .OrderByDescending(m => m.MovementDate)
            .ThenByDescending(m => m.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

        var userIds = movements.Select(m => m.UserId).Distinct().ToList();
        var names = await db.Set<ApplicationUser>()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return movements
            .Select(m => new InventoryMovementDto(
                m.Id,
                m.MaterialId,
                m.Material.Code,
                m.Material.Name,
                m.MovementType.Name,
                m.MovementType.Direction,
                m.Quantity,
                m.Material.BaseUnit.Symbol,
                names.GetValueOrDefault(m.UserId, "—"),
                m.MovementDate,
                m.Notes))
            .ToList();
    }

    public async Task<Result<MaterialStockDto>> ReceiveAsync(
        ReceiveMaterialRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            return Invalid("Type how much arrived.");
        }

        var material = await db.Materials
            .Include(m => m.BaseUnit)
            .FirstOrDefaultAsync(m => m.Id == request.MaterialId && m.IsActive, cancellationToken);

        if (material is null)
        {
            return Invalid("Choose an active material.");
        }

        // How much of the base unit one delivered unit is worth. Receiving in the base
        // unit needs no pack row; anything else must have one, or the conversion would
        // be a guess.
        decimal perUnit;
        string unitName;

        if (request.UnitId == material.BaseUnitId)
        {
            perUnit = 1m;
            unitName = material.BaseUnit.Name;
        }
        else
        {
            var pack = await db.MaterialPackagings
                .Include(p => p.Unit)
                .FirstOrDefaultAsync(
                    p => p.MaterialId == material.Id && p.UnitId == request.UnitId,
                    cancellationToken);

            if (pack is null)
            {
                return Invalid(
                    $"{material.Name} has no pack size for that unit. "
                    + "Add one in Master Data, or receive it in its base unit.");
            }

            perUnit = pack.QuantityInBaseUnit;
            unitName = pack.Unit.Name;
        }

        var inBaseUnit = request.Quantity * perUnit;

        var note = string.IsNullOrWhiteSpace(request.Notes)
            ? $"Received {request.Quantity:0.###} {unitName}"
            : $"Received {request.Quantity:0.###} {unitName} — {request.Notes.Trim()}";

        return await MoveAsync(
            material.Id,
            MovementTypeNames.Receive,
            inBaseUnit,
            userId,
            note,
            cancellationToken);
    }

    public async Task<Result<MaterialStockDto>> AdjustAsync(
        AdjustStockRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Invalid("Say why the count differs — it stays on the record.");
        }

        if (request.CountedQuantity < 0)
        {
            return Invalid("A stock count cannot be less than nothing.");
        }

        var material = await db.Materials
            .FirstOrDefaultAsync(m => m.Id == request.MaterialId && m.IsActive, cancellationToken);

        if (material is null)
        {
            return Invalid("Choose an active material.");
        }

        var current = await db.MaterialInventory
            .Where(i => i.MaterialId == material.Id)
            .Select(i => (decimal?)i.CurrentQuantity)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

        var difference = request.CountedQuantity - current;

        if (difference == 0)
        {
            return Invalid("The count already matches what the system says. Nothing to correct.");
        }

        // Two movement types rather than a signed quantity, because finding less is a
        // different event from finding more and the reports read them differently.
        var typeName = difference > 0 ? MovementTypeNames.AdjustmentIn : MovementTypeNames.AdjustmentOut;

        var note =
            $"Counted {request.CountedQuantity:0.###}, system had {current:0.###} — {request.Reason.Trim()}";

        var result = await MoveAsync(
            material.Id,
            typeName,
            Math.Abs(difference),
            userId,
            note,
            cancellationToken);

        if (result.IsSuccess)
        {
            // An adjustment overrides the ledger, so it is worth finding later.
            logger.LogWarning(
                "Stock of {Material} adjusted from {Before} to {After} by user {UserId}: {Reason}",
                material.Name,
                current,
                request.CountedQuantity,
                userId,
                request.Reason.Trim());
        }

        return result;
    }

    /// <summary>
    /// Posts through the shared ledger, then returns the material's fresh row so the
    /// screen shows the balance the movement produced rather than asking again.
    /// </summary>
    private async Task<Result<MaterialStockDto>> MoveAsync(
        int materialId,
        string movementTypeName,
        decimal quantity,
        int userId,
        string note,
        CancellationToken cancellationToken)
    {
        var posted = await ledger.PostAsync(
            materialId,
            movementTypeName,
            quantity,
            userId,
            note,
            cancellationToken: cancellationToken);

        if (!posted.IsSuccess)
        {
            return Result<MaterialStockDto>.Failure(
                posted.ErrorCode,
                posted.Message ?? "The stock could not be moved.");
        }

        var stock = await GetStockAsync(false, cancellationToken);
        return Result<MaterialStockDto>.Success(stock.First(s => s.MaterialId == materialId));
    }

    private static Result<MaterialStockDto> Invalid(string message) =>
        Result<MaterialStockDto>.Failure(ErrorCode.ValidationFailed, message);
}
