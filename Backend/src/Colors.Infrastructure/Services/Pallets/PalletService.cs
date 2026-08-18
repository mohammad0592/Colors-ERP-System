using Colors.Application.Common.Models;
using Colors.Application.Features.Barcodes;
using Colors.Application.Features.Pallets;
using Colors.Domain.Constants;
using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Packaging;
using Colors.Domain.Entities.Production;
using Colors.Domain.Common;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Pallets;

/// <summary>
/// Pallets — specification section 10.
///
/// One rule carries this phase, and the factory gave it in its own words: <i>"when the
/// pallet is empty, the pallet will take the first scanned bag's characteristics."</i>
/// After that every bag must match, and the number that fills the pallet is the
/// product's own — never a number in the code.
/// </summary>
public class PalletService(
    ColorsDbContext db,
    IBarcodeService barcodes,
    StockLedger ledger,
    TimeProvider timeProvider) : IPalletService
{
    public async Task<IReadOnlyList<PalletSummaryDto>> GetPalletsAsync(
        int? shiftLineId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        var pallets = await PalletQuery()
            .Where(p => shiftLineId == null || p.ShiftLineId == shiftLineId)
            .Where(p => !openOnly
                        || (p.CompletedAt == null && p.ShippedAt == null && p.CancelledAt == null))
            .OrderByDescending(p => p.PalletNumber)
            .Take(300)
            .ToListAsync(cancellationToken);

        var names = await UserNamesAsync(pallets.Select(p => p.CreatedByUserId), cancellationToken);
        var codes = await BarcodesForAsync(
            BarcodeObjectType.Pallet, pallets.Select(p => p.Id), cancellationToken);

        return pallets.Select(p => ToSummary(p, names, codes)).ToList();
    }

    public async Task<Result<PalletDto>> GetPalletAsync(
        int palletId,
        CancellationToken cancellationToken = default)
    {
        var pallet = await PalletQuery().FirstOrDefaultAsync(p => p.Id == palletId, cancellationToken);

        return pallet is null
            ? PalletNotFound()
            : Result<PalletDto>.Success(await ToDtoAsync(pallet, cancellationToken));
    }

    public async Task<IReadOnlyList<AvailableBagDto>> GetAvailableBagsAsync(
        int? palletId = null,
        CancellationToken cancellationToken = default)
    {
        // An open pallet only accepts its own colour and product, so offering anything
        // else would be offering a refusal.
        var pallet = palletId is null
            ? null
            : await db.WoodenPallets.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == palletId, cancellationToken);

        var bags = await db.ProducedBags
            .Include(b => b.Color)
            .Include(b => b.Product)
            .Include(b => b.ThermoProduction).ThenInclude(t => t.Roll)
            .Where(b => b.Status == ProducedBagStatus.Available)
            .Where(b => pallet == null || pallet.ColorId == null
                        || (b.ColorId == pallet.ColorId && b.ProductId == pallet.ProductId))
            // Oldest first: bags sit while pallets are built, and the oldest should go.
            .OrderBy(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

        var codes = await BarcodesForAsync(
            BarcodeObjectType.Bag, bags.Select(b => b.Id), cancellationToken);

        return bags
            .Select(b => new AvailableBagDto(
                b.Id,
                codes.GetValueOrDefault(b.Id, string.Empty),
                b.ThermoProduction.Roll.RollCode,
                b.ColorId,
                b.Color.Name,
                b.ProductId,
                b.Product.Name,
                b.Weight,
                b.PieceCount,
                b.CreatedAt))
            .ToList();
    }

    public async Task<Result<PalletDto>> StartPalletAsync(
        StartPalletRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var shiftLine = await db.ShiftLines
            .Include(l => l.ProductionLine)
            .Include(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .FirstOrDefaultAsync(l => l.Id == request.ShiftLineId, cancellationToken);

        if (shiftLine is null)
        {
            return Invalid("Choose a line of an open shift.");
        }

        // Pallets are built where the bags come off (specification section 4).
        if (!shiftLine.ProductionLine.FormsBags)
        {
            return Invalid(
                $"{shiftLine.ProductionLine.Name} does not make bags, so nothing is packed "
                + "there. Choose the thermo line.");
        }

        if (!ShiftWork.AcceptsWork(shiftLine.ShiftReport.Status))
        {
            return Invalid(ShiftWork.RefusalFor(shiftLine.ShiftReport));
        }

        // The pallet, its label and the wood it is built on are one act, exactly as a
        // roll and its bag are.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var pallet = new WoodenPallet
        {
            PalletNumber = await NextNumberAsync(
                ColorsDbContext.PalletNumberSequence, cancellationToken),
            ShiftLineId = shiftLine.Id,
            CreatedByUserId = userId,
            CreatedAt = timeProvider.GetUtcNow(),
            Notes = Trimmed(request.Notes),
        };

        db.WoodenPallets.Add(pallet);
        await db.SaveChangesAsync(cancellationToken);

        var barcode = await barcodes.IssueAsync(
            BarcodeObjectType.Pallet, pallet.Id, cancellationToken);

        if (!barcode.IsSuccess)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return Invalid(barcode.Message ?? "The pallet's label could not be printed.");
        }

        // The wood leaves the store now, not at the end of the shift. That is when the
        // operator picks it up, and it is what makes the store's figure true all day
        // instead of only after a shift closes (specification section 10).
        //
        // It is also the whole guard. The ledger refuses to go below nothing, so a
        // factory out of wooden pallets cannot start one — no separate check needed,
        // and no way for the two to disagree.
        var wood = await WoodenPalletMaterialAsync(cancellationToken);

        if (wood is not null)
        {
            var taken = await ledger.PostAsync(
                wood.Id,
                MovementTypeNames.PackagingConsumption,
                1m,
                userId,
                $"Pallet {pallet.PalletNumber} started on {shiftLine.ProductionLine.Name}, "
                + $"shift {shiftLine.ShiftReport.Shift.Name} "
                + $"{shiftLine.ShiftReport.ProductionDate:dd/MM/yyyy}",
                null,
                shiftLine.ShiftReportId,
                cancellationToken);

            if (!taken.IsSuccess)
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                return Invalid(taken.Message ?? "There is no wooden pallet to build on.");
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return await LoadAsync(pallet.Id, cancellationToken);
    }

    public async Task<Result<PalletDto>> CancelPalletAsync(
        int palletId,
        CancelPalletRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var reason = Trimmed(request.Reason);
        if (reason is null)
        {
            return Invalid("Say why the pallet is being cancelled.");
        }

        var pallet = await PalletQuery().FirstOrDefaultAsync(p => p.Id == palletId, cancellationToken);
        if (pallet is null)
        {
            return PalletNotFound();
        }

        if (pallet.CancelledAt is not null)
        {
            return Invalid($"Pallet {pallet.PalletNumber} has already been cancelled.");
        }

        if (pallet.ShippedAt is not null || pallet.CompletedAt is not null)
        {
            return Invalid(
                $"Pallet {pallet.PalletNumber} is finished. Take its bags off first if it "
                + "was built by mistake.");
        }

        // The wood only comes back if nothing was stacked on it. Once a bag is on the
        // pallet the wood is under the bags, and taking the bags off is the way back.
        if (pallet.Assignments.Any(a => a.ReversedAt is null))
        {
            return Invalid(
                $"Pallet {pallet.PalletNumber} has bags on it. Take them off before "
                + "cancelling it.");
        }

        var shiftLine = await db.ShiftLines
            .Include(l => l.ProductionLine)
            .Include(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .FirstAsync(l => l.Id == pallet.ShiftLineId, cancellationToken);

        if (!ShiftWork.AcceptsWork(shiftLine.ShiftReport.Status))
        {
            return Invalid(ShiftWork.RefusalFor(shiftLine.ShiftReport));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        pallet.CancelledAt = timeProvider.GetUtcNow();
        pallet.CancelledByUserId = userId;
        pallet.CancellationReason = reason;

        await db.SaveChangesAsync(cancellationToken);

        var wood = await WoodenPalletMaterialAsync(cancellationToken);

        if (wood is not null)
        {
            var returned = await ledger.PostAsync(
                wood.Id,
                MovementTypeNames.Return,
                1m,
                userId,
                $"Pallet {pallet.PalletNumber} cancelled: {reason}",
                null,
                shiftLine.ShiftReportId,
                cancellationToken);

            if (!returned.IsSuccess)
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                return Invalid(returned.Message ?? "The wooden pallet could not go back.");
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return await LoadAsync(pallet.Id, cancellationToken);
    }

    /// <summary>
    /// The material a pallet is built on, or null where the factory has not said which
    /// one it is. Only one material can be it — a unique index sees to that.
    /// </summary>
    private Task<Material?> WoodenPalletMaterialAsync(CancellationToken cancellationToken) =>
        db.Materials.FirstOrDefaultAsync(
            m => m.CountedAs == CountedPackaging.WoodenPallet && m.IsActive, cancellationToken);

    public async Task<Result<PalletDto>> ScanBagAsync(
        int palletId,
        ScanBagRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var pallet = await PalletQuery().FirstOrDefaultAsync(p => p.Id == palletId, cancellationToken);
        if (pallet is null)
        {
            return PalletNotFound();
        }

        if (pallet.CancelledAt is not null)
        {
            return Invalid(
                $"Pallet {pallet.PalletNumber} was cancelled and its wooden pallet went "
                + "back to the store. Start a new one.");
        }

        if (pallet.ShippedAt is not null)
        {
            return Invalid($"Pallet {pallet.PalletNumber} has already been shipped.");
        }

        if (pallet.CompletedAt is not null)
        {
            return Invalid(
                $"Pallet {pallet.PalletNumber} is full. Start a new one for the next bag.");
        }

        var found = await FindBagAsync(request, cancellationToken);
        if (!found.IsSuccess)
        {
            return Invalid(found.Message ?? "Scan the bag.");
        }

        var bag = found.Value!;

        if (bag.Status != ProducedBagStatus.Available)
        {
            // Naming the pallet it is already on saves a walk down the line.
            return Invalid(await BagRefusalAsync(bag, cancellationToken));
        }

        // The rule the factory gave: an empty pallet takes the first bag's colour and
        // product; after that every bag must match both.
        if (pallet.ColorId is null)
        {
            pallet.ColorId = bag.ColorId;
            pallet.ProductId = bag.ProductId;
        }
        else if (pallet.ColorId != bag.ColorId || pallet.ProductId != bag.ProductId)
        {
            return Invalid(
                $"Pallet {pallet.PalletNumber} is {pallet.Color?.Name} "
                + $"{pallet.Product?.Name}. This bag is {bag.Color.Name} {bag.Product.Name}, "
                + "so it belongs on a different pallet.");
        }

        var now = timeProvider.GetUtcNow();

        // Counted before the new row is tracked. EF fixes up the pallet's own
        // collection the moment the assignment is added, so counting afterwards counts
        // this bag twice and fills the pallet one bag early.
        var onIt = pallet.Assignments.Count(a => a.ReversedAt is null) + 1;

        db.BagPalletAssignments.Add(new BagPalletAssignment
        {
            ProducedBagId = bag.Id,
            WoodenPalletId = pallet.Id,
            AssignedByUserId = userId,
            AssignedAt = now,
        });

        bag.Status = ProducedBagStatus.Assigned;

        // Capacity comes from the product this pallet took off its first bag — 15 for
        // plates, about 21 for the rest. Never a number in the code.
        var capacity = await CapacityAsync(pallet.ProductId, cancellationToken);

        if (capacity is not null && onIt >= capacity)
        {
            pallet.CompletedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(palletId, cancellationToken);
    }

    public async Task<Result<PalletDto>> ReverseAssignmentAsync(
        int assignmentId,
        ReverseAssignmentRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var reason = Trimmed(request.Reason);
        if (reason is null)
        {
            return Invalid("Say why the bag is coming off. It stays in the history either way.");
        }

        var assignment = await db.BagPalletAssignments
            .Include(a => a.ProducedBag)
            .Include(a => a.WoodenPallet)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);

        if (assignment is null)
        {
            return Result<PalletDto>.Failure(ErrorCode.NotFound, "This scan does not exist.");
        }

        if (assignment.ReversedAt is not null)
        {
            return Invalid("This bag has already been taken off.");
        }

        if (assignment.WoodenPallet.ShippedAt is not null)
        {
            return Invalid(
                $"Pallet {assignment.WoodenPallet.PalletNumber} has left the factory. "
                + "A bag cannot be taken off it now.");
        }

        assignment.ReversedByUserId = userId;
        assignment.ReversedAt = timeProvider.GetUtcNow();
        assignment.ReversalReason = reason;

        // Back in the store, ready for the right pallet. The partial unique index lets
        // it be scanned again precisely because this row is now reversed.
        assignment.ProducedBag.Status = ProducedBagStatus.Available;

        // A pallet that was full is not full any more.
        assignment.WoodenPallet.CompletedAt = null;

        await db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(assignment.WoodenPalletId, cancellationToken);
    }

    // ---------- helpers ----------

    public async Task<IReadOnlyList<PalletSummaryDto>> GetPalletsInStockAsync(
        CancellationToken cancellationToken = default)
    {
        var pallets = await PalletQuery()
            .Where(p => p.CompletedAt != null
                        && p.ShippedAt == null
                        && p.CancelledAt == null)
            // Oldest first. A pallet that has stood in the factory since March should go
            // before one finished this morning, and the man loading the lorry has no
            // other way of knowing which is which.
            .OrderBy(p => p.CompletedAt)
            .ToListAsync(cancellationToken);

        var names = await UserNamesAsync(pallets.Select(p => p.CreatedByUserId), cancellationToken);
        var codes = await BarcodesForAsync(
            BarcodeObjectType.Pallet, pallets.Select(p => p.Id), cancellationToken);

        return pallets.Select(p => ToSummary(p, names, codes)).ToList();
    }

    public async Task<Result<PalletDto>> ShipPalletAsync(
        ShipPalletRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var found = await FindPalletAsync(request, cancellationToken);
        if (!found.IsSuccess || found.Value is null)
        {
            return Result<PalletDto>.Failure(
                found.ErrorCode, found.Message ?? "This pallet does not exist.");
        }

        var pallet = found.Value;

        if (pallet.CancelledAt is not null)
        {
            return Invalid($"Pallet {pallet.PalletNumber} was cancelled. It never held anything.");
        }

        if (pallet.ShippedAt is not null)
        {
            return Invalid($"Pallet {pallet.PalletNumber} has already gone.");
        }

        if (pallet.CompletedAt is null)
        {
            // The database would refuse this anyway - ck_pallets_dates_in_order will not
            // take a shipping date without a completion date. Said here in words the man
            // on the floor can act on, rather than as a constraint violation.
            return Invalid(
                $"Pallet {pallet.PalletNumber} is not finished yet. Only a full pallet leaves.");
        }

        // No open shift is required, and that is deliberate. Cancelling is shift work -
        // it happens at the machine, to a pallet being built. A pallet may stand finished
        // for weeks and leave on a Friday, so tying dispatch to an open shift would stop
        // the lorry for a reason the factory would not recognise.
        pallet.ShippedAt = timeProvider.GetUtcNow();
        pallet.ShippedByUserId = userId;

        // A pallet that ships for real is no longer a pallet that came back, so the
        // record of the earlier mistake goes. The audit log still has it.
        pallet.ShippingReversedAt = null;
        pallet.ShippingReversedByUserId = null;
        pallet.ShippingReversalReason = null;

        await db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(pallet.Id, cancellationToken);
    }

    public async Task<Result<PalletDto>> ReverseShipmentAsync(
        int palletId,
        ReverseShipmentRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var reason = Trimmed(request.Reason);
        if (reason is null)
        {
            return Invalid("Say why the pallet is coming back.");
        }

        var pallet = await PalletQuery().FirstOrDefaultAsync(p => p.Id == palletId, cancellationToken);
        if (pallet is null)
        {
            return PalletNotFound();
        }

        if (pallet.ShippedAt is null)
        {
            return Invalid($"Pallet {pallet.PalletNumber} has not been shipped.");
        }

        pallet.ShippedAt = null;
        pallet.ShippedByUserId = null;
        pallet.ShippingReversedAt = timeProvider.GetUtcNow();
        pallet.ShippingReversedByUserId = userId;
        pallet.ShippingReversalReason = reason;

        await db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(pallet.Id, cancellationToken);
    }

    /// <summary>
    /// A pallet by its label, or by its id for the office. The same shape as finding a
    /// bag, and for the same reason: the floor scans.
    /// </summary>
    private async Task<Result<WoodenPallet>> FindPalletAsync(
        ShipPalletRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.PalletBarcode))
        {
            var scan = await barcodes.LookupAsync(
                request.PalletBarcode.Trim(), BarcodeObjectType.Pallet, cancellationToken);

            // A bag's label comes back successful, saying it is a bag. Checked here, or a
            // bag label whose id happened to match a pallet would ship the wrong thing.
            if (!scan.IsSuccess
                || scan.Value is null
                || !scan.Value.Found
                || scan.Value.ObjectType != BarcodeObjectType.Pallet.ToString())
            {
                return Result<WoodenPallet>.Failure(
                    ErrorCode.ValidationFailed,
                    scan.Value?.Message ?? scan.Message ?? "That label is not one of ours.");
            }

            var scanned = await PalletQuery()
                .FirstOrDefaultAsync(p => p.Id == scan.Value.ObjectId, cancellationToken);

            return scanned is null
                ? Result<WoodenPallet>.Failure(
                    ErrorCode.NotFound, "That label names a pallet that is no longer here.")
                : Result<WoodenPallet>.Success(scanned);
        }

        if (request.PalletId is null)
        {
            return Result<WoodenPallet>.Failure(ErrorCode.ValidationFailed, "Scan the pallet.");
        }

        var picked = await PalletQuery()
            .FirstOrDefaultAsync(p => p.Id == request.PalletId, cancellationToken);

        return picked is null
            ? Result<WoodenPallet>.Failure(ErrorCode.NotFound, "This pallet does not exist.")
            : Result<WoodenPallet>.Success(picked);
    }

    /// <summary>
    /// The scan comes first, because that is what the floor does. An id is accepted too,
    /// for the office picking off the list.
    /// </summary>
    private async Task<Result<ProducedBag>> FindBagAsync(
        ScanBagRequest request,
        CancellationToken cancellationToken)
    {
        var bags = db.ProducedBags
            .Include(b => b.Color)
            .Include(b => b.Product);

        if (!string.IsNullOrWhiteSpace(request.BagBarcode))
        {
            // Asking for a bag by name is what turns "that is a pallet, not a bag" into
            // an answer instead of a failed search.
            var scan = await barcodes.LookupAsync(
                request.BagBarcode.Trim(), BarcodeObjectType.Bag, cancellationToken);

            // A wrong kind of label comes back *successful*, carrying "that is a pallet,
            // not a bag". The type has to be checked here — without it, a pallet label
            // whose id happens to match a bag's would quietly pack the wrong thing.
            if (!scan.IsSuccess
                || scan.Value is null
                || !scan.Value.Found
                || scan.Value.ObjectType != BarcodeObjectType.Bag.ToString())
            {
                return Result<ProducedBag>.Failure(
                    ErrorCode.ValidationFailed,
                    scan.Value?.Message ?? scan.Message ?? "That label is not one of ours.");
            }

            var scanned = await bags
                .FirstOrDefaultAsync(b => b.Id == scan.Value.ObjectId, cancellationToken);

            return scanned is null
                ? Result<ProducedBag>.Failure(
                    ErrorCode.NotFound, "That label names a bag that is no longer here.")
                : Result<ProducedBag>.Success(scanned);
        }

        if (request.ProducedBagId is null)
        {
            return Result<ProducedBag>.Failure(ErrorCode.ValidationFailed, "Scan the bag.");
        }

        var picked = await bags
            .FirstOrDefaultAsync(b => b.Id == request.ProducedBagId, cancellationToken);

        return picked is null
            ? Result<ProducedBag>.Failure(ErrorCode.NotFound, "This bag does not exist.")
            : Result<ProducedBag>.Success(picked);
    }

    private async Task<string> BagRefusalAsync(ProducedBag bag, CancellationToken cancellationToken)
    {
        if (bag.Status != ProducedBagStatus.Assigned)
        {
            return BagRefusal(bag);
        }

        var pallet = await db.BagPalletAssignments
            .Where(a => a.ProducedBagId == bag.Id && a.ReversedAt == null)
            .Select(a => (int?)a.WoodenPallet.PalletNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return pallet is null
            ? BagRefusal(bag)
            : $"This bag is already on pallet {pallet}.";
    }

    private static string BagRefusal(ProducedBag bag) => bag.Status switch
    {
        ProducedBagStatus.Assigned => "This bag is already on a pallet.",
        ProducedBagStatus.Defective => "This bag was marked defective.",
        _ => "This bag cannot go on a pallet.",
    };

    private async Task<int?> CapacityAsync(int? productId, CancellationToken cancellationToken)
    {
        if (productId is null)
        {
            return null;
        }

        var capacity = await db.Products
            .Where(p => p.Id == productId)
            .Select(p => (int?)p.BagsPerPallet)
            .FirstOrDefaultAsync(cancellationToken);

        // A product with no figure set would otherwise complete the pallet on its first
        // bag. Better to leave it open and let a person close it.
        return capacity is > 0 ? capacity : null;
    }

    private async Task<int> NextNumberAsync(string sequence, CancellationToken cancellationToken)
    {
        var next = await db.Database
            .SqlQuery<int>($"SELECT nextval({sequence})::int AS \"Value\"")
            .ToListAsync(cancellationToken);

        return next[0];
    }

    private IQueryable<WoodenPallet> PalletQuery() =>
        db.WoodenPallets
            .Include(p => p.Color)
            .Include(p => p.Product)
            .Include(p => p.ShiftLine).ThenInclude(l => l.ProductionLine)
            .Include(p => p.ShiftLine).ThenInclude(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .Include(p => p.Assignments).ThenInclude(a => a.ProducedBag)
                .ThenInclude(b => b.ThermoProduction).ThenInclude(t => t.Roll)
            .AsSplitQuery();

    private PalletSummaryDto ToSummary(
        WoodenPallet pallet,
        Dictionary<int, string> names,
        Dictionary<int, string> codes)
    {
        var live = pallet.Assignments.Where(a => a.ReversedAt is null).ToList();

        return new PalletSummaryDto(
            pallet.Id,
            pallet.PalletNumber,
            codes.GetValueOrDefault(pallet.Id, string.Empty),
            pallet.ShiftLineId,
            pallet.ShiftLine.ProductionLine.Name,
            pallet.ShiftLine.ShiftReport.Shift.Name,
            pallet.ShiftLine.ShiftReport.ProductionDate,
            pallet.ColorId,
            pallet.Color?.Name,
            pallet.ProductId,
            pallet.Product?.Name,
            pallet.Status.ToString(),
            pallet.Status is PalletStatus.Empty or PalletStatus.Opened,
            live.Count,
            live.Sum(a => a.ProducedBag.PieceCount),
            live.Sum(a => a.ProducedBag.Weight),
            pallet.Product?.BagsPerPallet is > 0 ? pallet.Product.BagsPerPallet : null,
            names.GetValueOrDefault(pallet.CreatedByUserId, "—"),
            pallet.CreatedAt,
            pallet.CompletedAt,
            pallet.ShippedAt);
    }

    private async Task<PalletDto> ToDtoAsync(
        WoodenPallet pallet,
        CancellationToken cancellationToken)
    {
        var userIds = new List<int> { pallet.CreatedByUserId };
        if (pallet.CancelledByUserId is not null)
        {
            userIds.Add(pallet.CancelledByUserId.Value);
        }

        if (pallet.ShippedByUserId is not null)
        {
            userIds.Add(pallet.ShippedByUserId.Value);
        }

        if (pallet.ShippingReversedByUserId is not null)
        {
            userIds.Add(pallet.ShippingReversedByUserId.Value);
        }

        userIds.AddRange(pallet.Assignments.Select(a => a.AssignedByUserId));
        userIds.AddRange(pallet.Assignments
            .Where(a => a.ReversedByUserId is not null)
            .Select(a => a.ReversedByUserId!.Value));

        var names = await UserNamesAsync(userIds, cancellationToken);
        var palletCodes = await BarcodesForAsync(
            BarcodeObjectType.Pallet, [pallet.Id], cancellationToken);
        var bagCodes = await BarcodesForAsync(
            BarcodeObjectType.Bag,
            pallet.Assignments.Select(a => a.ProducedBagId),
            cancellationToken);

        var summary = ToSummary(pallet, names, palletCodes);

        return new PalletDto(
            summary.Id,
            summary.PalletNumber,
            summary.Barcode,
            summary.ShiftLineId,
            summary.ProductionLineName,
            summary.ShiftName,
            summary.ProductionDate,
            summary.ColorId,
            summary.ColorName,
            summary.ProductId,
            summary.ProductName,
            summary.Status,
            summary.IsOpen,
            summary.BagCount,
            summary.PieceCount,
            summary.Weight,
            summary.Capacity,
            summary.CreatedByName,
            summary.CreatedAt,
            summary.CompletedAt,
            pallet.ShippedAt,
            pallet.ShippedByUserId is null
                ? null
                : names.GetValueOrDefault(pallet.ShippedByUserId.Value, "—"),
            pallet.ShippingReversedAt,
            pallet.ShippingReversedByUserId is null
                ? null
                : names.GetValueOrDefault(pallet.ShippingReversedByUserId.Value, "—"),
            pallet.ShippingReversalReason,
            pallet.CancelledAt,
            pallet.CancelledByUserId is null
                ? null
                : names.GetValueOrDefault(pallet.CancelledByUserId.Value, "—"),
            pallet.CancellationReason,
            pallet.Notes,
            pallet.Assignments
                // Newest first: the scan just made is the one being checked.
                .OrderByDescending(a => a.AssignedAt)
                .ThenByDescending(a => a.Id)
                .Select(a => new PalletBagDto(
                    a.Id,
                    a.ProducedBagId,
                    bagCodes.GetValueOrDefault(a.ProducedBagId, string.Empty),
                    a.ProducedBag.ThermoProduction.Roll.RollCode,
                    a.ProducedBag.Weight,
                    a.ProducedBag.PieceCount,
                    names.GetValueOrDefault(a.AssignedByUserId, "—"),
                    a.AssignedAt,
                    a.ReversedAt is null,
                    a.ReversedByUserId is null
                        ? null
                        : names.GetValueOrDefault(a.ReversedByUserId.Value, "—"),
                    a.ReversedAt,
                    a.ReversalReason))
                .ToList());
    }

    private async Task<Dictionary<int, string>> UserNamesAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        return await db.Set<ApplicationUser>()
            .Where(u => wanted.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }

    private async Task<Dictionary<int, string>> BarcodesForAsync(
        BarcodeObjectType objectType,
        IEnumerable<int> objectIds,
        CancellationToken cancellationToken)
    {
        var wanted = objectIds.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        return await db.Barcodes
            .Where(b => b.ObjectType == objectType && wanted.Contains(b.ObjectId) && b.IsActive)
            .ToDictionaryAsync(b => b.ObjectId, b => b.Value, cancellationToken);
    }

    private async Task<Result<PalletDto>> LoadAsync(int id, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var pallet = await PalletQuery().FirstAsync(p => p.Id == id, cancellationToken);

        return Result<PalletDto>.Success(await ToDtoAsync(pallet, cancellationToken));
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<PalletDto> Invalid(string message) =>
        Result<PalletDto>.Failure(ErrorCode.ValidationFailed, message);

    private static Result<PalletDto> PalletNotFound() =>
        Result<PalletDto>.Failure(ErrorCode.NotFound, "This pallet does not exist.");
}
