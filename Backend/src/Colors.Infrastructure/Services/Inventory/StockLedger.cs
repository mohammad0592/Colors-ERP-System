using Colors.Application.Common.Models;
using Colors.Domain.Entities.Inventory;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Inventory;

/// <summary>
/// The single door every stock movement goes through (specification section 6).
///
/// Extracted from the inventory service the moment a second caller appeared — issue
/// tickets move stock too, and two copies of "lock the balance, check it, write both
/// rows" is exactly how one of them ends up subtly different.
///
/// <b>Transactions.</b> A lone movement gets its own. A caller already inside one — a
/// ticket issuing six materials at once — has its transaction joined instead, so
/// either every line of that ticket moves or none does.
/// </summary>
public class StockLedger(ColorsDbContext db, TimeProvider timeProvider)
{
    /// <summary>
    /// Posts one movement and updates the balance, both or neither.
    ///
    /// Takes a row lock on the balance first, so a second tablet asking the same
    /// question waits rather than reading a number about to change. A check in
    /// application code alone cannot promise this: two tablets can pass it in the same
    /// millisecond.
    /// </summary>
    public async Task<Result<decimal>> PostAsync(
        int materialId,
        string movementTypeName,
        decimal quantity,
        int userId,
        string note,
        int? issueTicketId = null,
        int? shiftReportId = null,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            return Result<decimal>.Failure(ErrorCode.ValidationFailed, "A movement must be more than nothing.");
        }

        var movementType = await db.MovementTypes
            .FirstOrDefaultAsync(t => t.Name == movementTypeName, cancellationToken);

        if (movementType is null)
        {
            return Result<decimal>.Failure(
                ErrorCode.ValidationFailed,
                $"The movement type '{movementTypeName}' is missing from master data.");
        }

        var owned = db.Database.CurrentTransaction is null;
        var transaction = owned
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            // SELECT ... FOR UPDATE. Creates the row when this material has never moved,
            // so the next movement has something to lock.
            var balance = await db.MaterialInventory
                .FromSql($"""SELECT * FROM "MaterialInventory" WHERE "MaterialId" = {materialId} FOR UPDATE""")
                .FirstOrDefaultAsync(cancellationToken);

            if (balance is null)
            {
                balance = new MaterialInventory
                {
                    MaterialId = materialId,
                    CurrentQuantity = 0m,
                    LastUpdated = timeProvider.GetUtcNow(),
                };
                db.MaterialInventory.Add(balance);
            }

            var after = balance.CurrentQuantity + (quantity * movementType.Direction);

            if (after < 0)
            {
                var material = await db.Materials
                    .Include(m => m.BaseUnit)
                    .FirstAsync(m => m.Id == materialId, cancellationToken);

                var message =
                    $"There is not enough {material.Name}. The store holds "
                    + $"{balance.CurrentQuantity:0.###} {material.BaseUnit.Symbol} and this would take "
                    + $"{quantity:0.###}.";

                if (owned)
                {
                    await transaction!.RollbackAsync(cancellationToken);
                    db.ChangeTracker.Clear();
                }

                return Result<decimal>.Failure(ErrorCode.ValidationFailed, message);
            }

            balance.CurrentQuantity = after;
            balance.LastUpdated = timeProvider.GetUtcNow();

            db.MaterialInventoryMovements.Add(new MaterialInventoryMovement
            {
                MaterialId = materialId,
                MovementTypeId = movementType.Id,
                Quantity = quantity,
                ShiftReportId = shiftReportId ?? await CurrentShiftIdAsync(cancellationToken),
                IssueTicketId = issueTicketId,
                UserId = userId,
                MovementDate = timeProvider.GetUtcNow(),
                Notes = note,
            });

            await db.SaveChangesAsync(cancellationToken);

            if (owned)
            {
                await transaction!.CommitAsync(cancellationToken);
            }

            return Result<decimal>.Success(after);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// The open shift to hang a movement on, if there is one.
    ///
    /// Deliveries do arrive before anyone opens a shift, and the storekeeper must not
    /// be sent looking for a supervisor before he can book them in — so this is
    /// allowed to find nothing.
    /// </summary>
    public async Task<int?> CurrentShiftIdAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        return await db.ShiftReports
            .Where(r => r.Status == Domain.Enums.ShiftReportStatus.Open
                        && r.ProductionDate >= today.AddDays(-1))
            .OrderByDescending(r => r.ProductionDate)
            .ThenByDescending(r => r.OpenedAt)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
