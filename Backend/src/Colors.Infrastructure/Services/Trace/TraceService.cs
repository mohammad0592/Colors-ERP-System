using Colors.Application.Common.Models;
using Colors.Application.Features.Trace;
using Colors.Domain.Entities.Packaging;
using Colors.Domain.Entities.Production;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Trace;

/// <summary>
/// Where one thing came from, and what it became (specification section 13).
///
/// Nothing here is stored. Every link it follows is already a foreign key — a bag knows
/// its run, a run knows its roll, a roll knows its recipe and its mix — so this walks
/// what exists rather than keeping a second copy that could disagree.
/// </summary>
public class TraceService(ColorsDbContext db) : ITraceService
{
    public async Task<Result<TraceDto>> GetAsync(
        string barcode,
        CancellationToken cancellationToken = default)
    {
        var value = barcode?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(value))
        {
            return Invalid("Scan a label, or type the code printed under it.");
        }

        var found = await db.Barcodes
            .FirstOrDefaultAsync(b => b.Value == value, cancellationToken);

        if (found is null)
        {
            // Not a barcode. It may still be a roll code, which is the line printed
            // large on the label and on every bag the roll made — so it is what a man
            // reads out and types when the barcode is torn or he is holding a paper
            // form rather than the roll.
            var rollId = await db.Rolls
                .Where(r => r.RollCode.ToUpper() == value)
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (rollId is null)
            {
                return Result<TraceDto>.Failure(
                    ErrorCode.NotFound,
                    $"Nothing in the system matches {value} — not a label, and not a roll code.");
            }

            var rollBarcode = await db.Barcodes
                .Where(b => b.ObjectType == BarcodeObjectType.Roll
                            && b.ObjectId == rollId.Value
                            && b.IsActive)
                .Select(b => b.Value)
                .FirstOrDefaultAsync(cancellationToken);

            return await FromRollAsync(rollBarcode ?? value, rollId.Value, cancellationToken);
        }

        return found.ObjectType switch
        {
            BarcodeObjectType.Roll => await FromRollAsync(found.Value, found.ObjectId, cancellationToken),
            BarcodeObjectType.Bag => await FromBagAsync(found.Value, found.ObjectId, cancellationToken),
            BarcodeObjectType.Pallet => await FromPalletAsync(found.Value, found.ObjectId, cancellationToken),
            _ => Result<TraceDto>.Failure(ErrorCode.NotFound, "Nothing is known about that label."),
        };
    }

    // ---------- the three starting points ----------

    private async Task<Result<TraceDto>> FromRollAsync(
        string barcode,
        int rollId,
        CancellationToken cancellationToken)
    {
        var roll = await RollQuery().FirstOrDefaultAsync(r => r.Id == rollId, cancellationToken);
        if (roll is null)
        {
            return Gone(barcode);
        }

        return Result<TraceDto>.Success(new TraceDto(
            barcode,
            "Roll",
            roll.RollCode,
            await MixAsync(roll, cancellationToken),
            await RollDtoAsync(roll, barcode, cancellationToken),
            null,
            null,
            null,
            // Forwards: everything this roll became.
            await BagsOfRollAsync(rollId, cancellationToken)));
    }

    private async Task<Result<TraceDto>> FromBagAsync(
        string barcode,
        int bagId,
        CancellationToken cancellationToken)
    {
        var bag = await db.ProducedBags
            .Include(b => b.Color)
            .Include(b => b.Product)
            .Include(b => b.ThermoProduction).ThenInclude(t => t.TestReport).ThenInclude(r => r!.Product)
            .Include(b => b.ThermoProduction).ThenInclude(t => t.ShiftLine)
                .ThenInclude(l => l.Mould)
            .Include(b => b.ThermoProduction).ThenInclude(t => t.ShiftLine)
                .ThenInclude(l => l.ShiftReport).ThenInclude(s => s.Shift)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == bagId, cancellationToken);

        if (bag is null)
        {
            return Gone(barcode);
        }

        var roll = await RollQuery()
            .FirstAsync(r => r.Id == bag.ThermoProduction.RollId, cancellationToken);

        var rollCodes = await BarcodesForAsync(BarcodeObjectType.Roll, [roll.Id], cancellationToken);
        var pallets = await PalletsOfBagsAsync([bag.Id], cancellationToken);
        var names = await UserNamesAsync(
            [roll.ProducedByUserId, bag.ThermoProduction.OperatorUserId], cancellationToken);

        var run = bag.ThermoProduction;

        return Result<TraceDto>.Success(new TraceDto(
            barcode,
            "Bag",
            bag.Product.Name,
            await MixAsync(roll, cancellationToken),
            RollDto(roll, rollCodes.GetValueOrDefault(roll.Id, string.Empty), names),
            new TraceThermoDto(
                run.Id,
                run.ShiftLine.ShiftReport.Shift.Name,
                run.ShiftLine.ShiftReport.ProductionDate,
                names.GetValueOrDefault(run.OperatorUserId, "—"),
                run.StartedAt,
                run.FinishedAt,
                run.TotalTimeMinutes,
                run.ShiftLine.Mould?.Name,
                run.TestReport?.Product.Name,
                run.TestReport?.BagCount,
                run.TestReport?.PieceCount,
                run.TestReport?.PieceWeight,
                run.TestReport?.BagWeight,
                run.TestReport?.AbsorbentPercentage),
            new TraceBagDto(
                bag.Id,
                barcode,
                roll.RollCode,
                bag.Product.Name,
                bag.Color.Name,
                bag.Weight,
                bag.PieceCount,
                Spaced(bag.Status.ToString()),
                pallets.TryGetValue(bag.Id, out var number) ? number : null),
            pallets.TryGetValue(bag.Id, out var palletNumber)
                ? await PalletDtoAsync(palletNumber, cancellationToken)
                : null,
            []));
    }

    private async Task<Result<TraceDto>> FromPalletAsync(
        string barcode,
        int palletId,
        CancellationToken cancellationToken)
    {
        var pallet = await PalletQuery().FirstOrDefaultAsync(p => p.Id == palletId, cancellationToken);
        if (pallet is null)
        {
            return Gone(barcode);
        }

        var bagIds = pallet.Assignments
            .Where(a => a.ReversedAt is null)
            .Select(a => a.ProducedBagId)
            .ToList();

        return Result<TraceDto>.Success(new TraceDto(
            barcode,
            "Pallet",
            $"Pallet {pallet.PalletNumber}",
            null,
            null,
            null,
            null,
            ToPalletDto(pallet, barcode),
            // The bags on it, each naming its own roll. A pallet of fifteen built from
            // rolls of twelve and nine reads as exactly that, which is what the bag
            // barcodes were for (specification section 10).
            await BagsOnPalletAsync(bagIds, cancellationToken)));
    }

    // ---------- the links ----------

    /// <summary>
    /// The mix, and the materials issued to the shift that ran it.
    ///
    /// Issued to the <i>shift line</i>, not to the mix, because the ticket never gained
    /// the BatchId section 7 describes. With the mixer filled once a shift that is the
    /// same set of materials — but the flag travels with the answer so the screen can
    /// say which sentence is true.
    /// </summary>
    private async Task<TraceMixDto> MixAsync(Roll roll, CancellationToken cancellationToken)
    {
        // A line has no navigation back to its ticket, so the tickets are read and
        // their lines come with them.
        var tickets = await db.MaterialIssueTickets
            .Include(t => t.Lines).ThenInclude(l => l.Material).ThenInclude(m => m.BaseUnit)
            .Where(t => t.ShiftLineId == roll.Batch.ShiftLineId)
            .OrderBy(t => t.TicketNumber)
            .ToListAsync(cancellationToken);

        return new TraceMixDto(
            roll.Batch.BatchNumber,
            roll.Batch.ShiftLine.ShiftReport.Shift.Name,
            roll.Batch.ShiftLine.ShiftReport.ProductionDate,
            roll.Batch.ShiftLine.ProductionLine.Name,
            tickets
                .SelectMany(t => t.Lines
                    .OrderBy(l => l.Material.Name)
                    .Select(l => new TraceMaterialDto(
                        t.TicketNumber,
                        l.Material.Name,
                        l.IssuedQuantity,
                        l.ReturnedQuantity,
                        l.NetUsed,
                        l.Material.BaseUnit.Symbol)))
                .ToList(),
            IssuedToShiftNotMix: true);
    }

    private async Task<TraceRollDto> RollDtoAsync(
        Roll roll,
        string barcode,
        CancellationToken cancellationToken)
    {
        var names = await UserNamesAsync([roll.ProducedByUserId], cancellationToken);
        return RollDto(roll, barcode, names);
    }

    private static TraceRollDto RollDto(Roll roll, string barcode, Dictionary<int, string> names) =>
        new(
            roll.Id,
            roll.RollCode,
            barcode,
            roll.RecipeVersion.RecipeNumber,
            roll.RecipeVersion.Family.Name,
            roll.Color.Name,
            roll.Batch.ShiftLine.ShiftReport.Shift.Name,
            roll.ProductionDate,
            names.GetValueOrDefault(roll.ProducedByUserId, "—"),
            roll.ProducedAt,
            Spaced(roll.Status.ToString()),
            roll.TestReport?.Weight,
            roll.TestReport?.Length,
            roll.TestReport?.PlateWeight,
            roll.TestReport?.AverageThickness);

    private async Task<List<TraceBagDto>> BagsOfRollAsync(
        int rollId,
        CancellationToken cancellationToken)
    {
        var bags = await db.ProducedBags
            .Include(b => b.Color)
            .Include(b => b.Product)
            .Include(b => b.ThermoProduction).ThenInclude(t => t.Roll)
            .Where(b => b.ThermoProduction.RollId == rollId)
            .OrderBy(b => b.Id)
            .ToListAsync(cancellationToken);

        return await ToBagDtosAsync(bags, cancellationToken);
    }

    private async Task<List<TraceBagDto>> BagsOnPalletAsync(
        List<int> bagIds,
        CancellationToken cancellationToken)
    {
        if (bagIds.Count == 0)
        {
            return [];
        }

        var bags = await db.ProducedBags
            .Include(b => b.Color)
            .Include(b => b.Product)
            .Include(b => b.ThermoProduction).ThenInclude(t => t.Roll)
            .Where(b => bagIds.Contains(b.Id))
            .OrderBy(b => b.Id)
            .ToListAsync(cancellationToken);

        return await ToBagDtosAsync(bags, cancellationToken);
    }

    private async Task<List<TraceBagDto>> ToBagDtosAsync(
        List<ProducedBag> bags,
        CancellationToken cancellationToken)
    {
        var ids = bags.Select(b => b.Id).ToList();
        var codes = await BarcodesForAsync(BarcodeObjectType.Bag, ids, cancellationToken);
        var pallets = await PalletsOfBagsAsync(ids, cancellationToken);

        return bags
            .Select(b => new TraceBagDto(
                b.Id,
                codes.GetValueOrDefault(b.Id, string.Empty),
                b.ThermoProduction.Roll.RollCode,
                b.Product.Name,
                b.Color.Name,
                b.Weight,
                b.PieceCount,
                Spaced(b.Status.ToString()),
                pallets.TryGetValue(b.Id, out var number) ? number : null))
            .ToList();
    }

    private async Task<TracePalletDto?> PalletDtoAsync(
        int palletNumber,
        CancellationToken cancellationToken)
    {
        var pallet = await PalletQuery()
            .FirstOrDefaultAsync(p => p.PalletNumber == palletNumber, cancellationToken);

        if (pallet is null)
        {
            return null;
        }

        var codes = await BarcodesForAsync(BarcodeObjectType.Pallet, [pallet.Id], cancellationToken);
        return ToPalletDto(pallet, codes.GetValueOrDefault(pallet.Id, string.Empty));
    }

    private static TracePalletDto ToPalletDto(WoodenPallet pallet, string barcode)
    {
        var live = pallet.Assignments.Where(a => a.ReversedAt is null).ToList();

        return new TracePalletDto(
            pallet.Id,
            pallet.PalletNumber,
            barcode,
            pallet.Product?.Name,
            pallet.Color?.Name,
            pallet.Status.ToString(),
            live.Count,
            pallet.Product?.BagsPerPallet is > 0 ? pallet.Product.BagsPerPallet : null,
            live.Sum(a => a.ProducedBag.PieceCount),
            live.Sum(a => a.ProducedBag.Weight),
            pallet.ShiftLine.ShiftReport.Shift.Name,
            pallet.ShiftLine.ShiftReport.ProductionDate,
            pallet.CreatedAt,
            pallet.CompletedAt);
    }

    // ---------- helpers ----------

    private IQueryable<Roll> RollQuery() =>
        db.Rolls
            .Include(r => r.Color)
            .Include(r => r.RecipeVersion).ThenInclude(v => v.Family)
            .Include(r => r.TestReport)
            .Include(r => r.Batch).ThenInclude(b => b.ShiftLine).ThenInclude(l => l.ProductionLine)
            .Include(r => r.Batch).ThenInclude(b => b.ShiftLine)
                .ThenInclude(l => l.ShiftReport).ThenInclude(s => s.Shift)
            .AsSplitQuery();

    private IQueryable<WoodenPallet> PalletQuery() =>
        db.WoodenPallets
            .Include(p => p.Color)
            .Include(p => p.Product)
            .Include(p => p.ShiftLine).ThenInclude(l => l.ShiftReport).ThenInclude(s => s.Shift)
            .Include(p => p.Assignments).ThenInclude(a => a.ProducedBag)
            .AsSplitQuery();

    /// <summary>Which pallet each bag sits on. Live assignments only — a bag taken off is back in the store.</summary>
    private async Task<Dictionary<int, int>> PalletsOfBagsAsync(
        IReadOnlyList<int> bagIds,
        CancellationToken cancellationToken)
    {
        if (bagIds.Count == 0)
        {
            return [];
        }

        return await db.BagPalletAssignments
            .Where(a => bagIds.Contains(a.ProducedBagId) && a.ReversedAt == null)
            .Select(a => new { a.ProducedBagId, a.WoodenPallet.PalletNumber })
            .ToDictionaryAsync(a => a.ProducedBagId, a => a.PalletNumber, cancellationToken);
    }

    private async Task<Dictionary<int, string>> BarcodesForAsync(
        BarcodeObjectType objectType,
        IReadOnlyList<int> objectIds,
        CancellationToken cancellationToken)
    {
        if (objectIds.Count == 0)
        {
            return [];
        }

        return await db.Barcodes
            .Where(b => b.ObjectType == objectType && objectIds.Contains(b.ObjectId) && b.IsActive)
            .ToDictionaryAsync(b => b.ObjectId, b => b.Value, cancellationToken);
    }

    private async Task<Dictionary<int, string>> UserNamesAsync(
        IReadOnlyList<int> ids,
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

    /// <summary><c>NeedsTest</c> is how the code spells it; <c>Needs test</c> is how a person reads it.</summary>
    private static string Spaced(string status) =>
        string.Concat(status.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));

    private static Result<TraceDto> Gone(string barcode) =>
        Result<TraceDto>.Failure(
            ErrorCode.NotFound, $"{barcode} names something that is no longer here.");

    private static Result<TraceDto> Invalid(string message) =>
        Result<TraceDto>.Failure(ErrorCode.ValidationFailed, message);
}
