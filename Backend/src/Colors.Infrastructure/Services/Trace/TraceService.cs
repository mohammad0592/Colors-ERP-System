using Colors.Application.Common.Models;
using Colors.Application.Features.Trace;
using Colors.Domain.Common;
using Colors.Domain.Entities.Packaging;
using Colors.Domain.Entities.Production;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.MasterData;
using Colors.Infrastructure.Services.Thermo;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Trace;

/// <summary>
/// Where one thing came from, and what it became (specification section 13).
///
/// Nothing here is stored. Every link it follows is already a foreign key — a bag knows
/// its run, a run knows its roll, a roll knows its recipe and its mix — so this walks
/// what exists rather than keeping a second copy that could disagree.
///
/// It says everything that is recorded about what was scanned: every reading, every note,
/// who did each thing and when, and anything put right afterwards. A man tracing a label
/// is usually chasing a complaint, and the one fact left off is the one he needed.
/// </summary>
public class TraceService(ColorsDbContext db) : ITraceService
{
    public async Task<Result<TraceDto>> GetAsync(
        string barcode,
        CancellationToken cancellationToken = default)
    {
        // Cleaned as every lookup cleans: capitals, no spaces (specification section 19.12).
        var value = barcode is null ? null : RollCode.AsBarcode(barcode);
        if (string.IsNullOrEmpty(value))
        {
            return Invalid("Scan a label, or type the code printed under it.", "trace.scanOrType");
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
                .Where(r => r.RollCode.ToUpper().Replace(" ", "") == value)
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (rollId is null)
            {
                return Result<TraceDto>.Failure(
                    ErrorCode.NotFound,
                    $"Nothing in the system matches {value} — not a label, and not a roll code.", "trace.noMatch", value);
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
            _ => Result<TraceDto>.Failure(ErrorCode.NotFound, "Nothing is known about that label.", "stock.unknownLabel"),
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

        var runs = await RunQuery()
            .Where(p => p.RollId == rollId)
            .OrderBy(p => p.StartedAt)
            .ToListAsync(cancellationToken);

        var names = await UserNamesAsync(
            [.. RollUserIds(roll), .. runs.SelectMany(RunUserIds)], cancellationToken);

        return Result<TraceDto>.Success(new TraceDto(
            barcode,
            "Roll",
            roll.RollCode,
            await MixAsync(roll, cancellationToken),
            RollDto(roll, barcode, names),
            null,
            null,
            null,
            // Forwards: everything this roll became.
            await BagsOfRollAsync(rollId, cancellationToken),
            await RunDtosAsync(runs, names, cancellationToken),
            []));
    }

    private async Task<Result<TraceDto>> FromBagAsync(
        string barcode,
        int bagId,
        CancellationToken cancellationToken)
    {
        var bag = await BagQuery().FirstOrDefaultAsync(b => b.Id == bagId, cancellationToken);
        if (bag is null)
        {
            return Gone(barcode);
        }

        var run = await RunQuery().FirstAsync(p => p.Id == bag.ThermoProductionId, cancellationToken);
        var roll = await RollQuery().FirstAsync(r => r.Id == run.RollId, cancellationToken);

        // Every pallet it has been on, the ones it came off included.
        var moves = await db.BagPalletAssignments
            .Include(a => a.WoodenPallet)
            .Where(a => a.ProducedBagId == bagId)
            .OrderBy(a => a.AssignedAt)
            .ToListAsync(cancellationToken);
        var palletCodes = await BarcodesForAsync(
            BarcodeObjectType.Pallet, moves.Select(a => a.WoodenPalletId).Distinct().ToList(), cancellationToken);

        var rollCodes = await BarcodesForAsync(BarcodeObjectType.Roll, [roll.Id], cancellationToken);
        var names = await UserNamesAsync(
            [
                .. RollUserIds(roll),
                .. RunUserIds(run),
                .. BagUserIds(bag),
                .. moves.Select(a => a.AssignedByUserId),
                .. moves.Where(a => a.ReversedByUserId is not null).Select(a => a.ReversedByUserId!.Value),
            ],
            cancellationToken);

        var onNow = moves.FirstOrDefault(a => a.ReversedAt is null);
        var codes = await ProductCodeBook.LoadAsync(db, cancellationToken);

        return Result<TraceDto>.Success(new TraceDto(
            barcode,
            "Bag",
            bag.Product.Name,
            await MixAsync(roll, cancellationToken),
            RollDto(roll, rollCodes.GetValueOrDefault(roll.Id, string.Empty), names),
            (await RunDtosAsync([run], names, cancellationToken))[0],
            BagDto(
                bag,
                barcode,
                codes,
                names,
                onNow?.WoodenPallet.PalletNumber,
                onNow is null ? null : palletCodes.GetValueOrDefault(onNow.WoodenPalletId)),
            onNow is null ? null : await PalletDtoAsync(onNow.WoodenPalletId, cancellationToken),
            [],
            [],
            moves
                .Select(a => new TracePalletMoveDto(
                    a.WoodenPallet.PalletNumber,
                    palletCodes.GetValueOrDefault(a.WoodenPalletId),
                    a.AssignedAt,
                    names.GetValueOrDefault(a.AssignedByUserId, "—"),
                    a.ReversedAt,
                    Name(names, a.ReversedByUserId),
                    a.ReversalReason))
                .ToList()));
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
            await ToPalletDtoAsync(pallet, barcode, cancellationToken),
            // The bags on it, each naming its own roll. A pallet of fifteen built from
            // rolls of twelve and nine reads as exactly that, which is what the bag
            // barcodes were for (specification section 10).
            await BagsOnPalletAsync(bagIds, cancellationToken),
            [],
            []));
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

    private static TraceRollDto RollDto(Roll roll, string barcode, Dictionary<int, string> names)
    {
        var test = roll.TestReport;

        return new(
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
            roll.Weight,
            roll.Length,
            test?.PlateWeight,
            test?.AverageThickness,
            roll.Product?.Name,
            roll.Notes,
            Name(names, roll.MeasuredByUserId),
            roll.MeasuredAt,
            test is null
                ? null
                : new TraceRollTestDto(
                    names.GetValueOrDefault(test.TestedByUserId, "—"),
                    test.TestedAt,
                    test.PlateWeight,
                    test.ThicknessRs,
                    test.ThicknessRm,
                    test.ThicknessLm,
                    test.ThicknessLs,
                    test.AverageThickness,
                    test.ThicknessInSpec,
                    test.AbsorbencyDryWeight,
                    test.AbsorbencyWetWeight,
                    test.AbsorbencyPercentage,
                    test.Notes),
            roll.RemovedAt,
            Name(names, roll.RemovedByUserId),
            roll.RemovalReason);
    }

    private async Task<List<TraceThermoDto>> RunDtosAsync(
        List<ThermoProduction> runs,
        Dictionary<int, string> names,
        CancellationToken cancellationToken)
    {
        // What earlier unfinished runs of the same roll made, which the finishing run's
        // scrap is worked out against (section 19.23).
        var earlier = await UnfinishedRuns.ForRollsAsync(
            db, runs.Select(r => r.RollId), cancellationToken);

        return runs
            .Select(run =>
            {
                var counted = run.TestReport;
                return new TraceThermoDto(
                    run.Id,
                    run.ShiftLine.ShiftReport.Shift.Name,
                    run.ShiftLine.ShiftReport.ProductionDate,
                    names.GetValueOrDefault(run.OperatorUserId, "—"),
                    run.StartedAt,
                    run.FinishedAt,
                    run.TotalTimeMinutes,
                    counted?.Product.Name ?? run.Roll.Product?.Name,
                    counted?.BagCount,
                    counted?.PieceCount,
                    counted?.PieceWeight,
                    counted?.BagWeight,
                    counted?.AbsorbentPercentage,
                    run.ShiftLine.ProductionLine.Name,
                    run.Notes,
                    run.UnfinishedReason,
                    counted is null ? null : names.GetValueOrDefault(counted.TestedByUserId, "—"),
                    counted?.TestedAt,
                    counted?.Notes,
                    counted?.CorrectedAt,
                    Name(names, counted?.CorrectedByUserId),
                    counted?.CorrectionReason,
                    ThermoScrap.For(run, earlier[run.RollId].PlatesKg));
            })
            .ToList();
    }

    private static TraceBagDto BagDto(
        ProducedBag bag,
        string barcode,
        ProductCodeBook codes,
        Dictionary<int, string> names,
        int? palletNumber,
        string? palletBarcode) =>
        new(
            bag.Id,
            barcode,
            bag.ThermoProduction.Roll.RollCode,
            bag.Product.Name,
            bag.Color.Name,
            bag.Weight,
            bag.PieceCount,
            Spaced(bag.Status.ToString()),
            palletNumber,
            codes.For(bag.ProductId, bag.ColorId, bag.Product.IsAbsorbent, bag.PieceCount, bag.Color.Code),
            bag.CreatedAt,
            bag.Notes,
            bag.RemovedAt,
            Name(names, bag.RemovedByUserId),
            bag.RemovalReason,
            palletBarcode);

    private async Task<List<TraceBagDto>> BagsOfRollAsync(
        int rollId,
        CancellationToken cancellationToken)
    {
        var bags = await BagQuery()
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

        var bags = await BagQuery()
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
        var bagCodes = await BarcodesForAsync(BarcodeObjectType.Bag, ids, cancellationToken);
        var pallets = await PalletsOfBagsAsync(ids, cancellationToken);
        var palletCodes = await BarcodesForAsync(
            BarcodeObjectType.Pallet, pallets.Values.Select(p => p.Id).Distinct().ToList(), cancellationToken);
        var names = await UserNamesAsync([.. bags.SelectMany(BagUserIds)], cancellationToken);
        var codes = await ProductCodeBook.LoadAsync(db, cancellationToken);

        return bags
            .Select(b =>
            {
                var on = pallets.TryGetValue(b.Id, out var pallet) ? pallet : null;
                return BagDto(
                    b,
                    bagCodes.GetValueOrDefault(b.Id, string.Empty),
                    codes,
                    names,
                    on?.Number,
                    on is null ? null : palletCodes.GetValueOrDefault(on.Id));
            })
            .ToList();
    }

    private async Task<TracePalletDto?> PalletDtoAsync(
        int palletId,
        CancellationToken cancellationToken)
    {
        var pallet = await PalletQuery().FirstOrDefaultAsync(p => p.Id == palletId, cancellationToken);
        if (pallet is null)
        {
            return null;
        }

        var codes = await BarcodesForAsync(BarcodeObjectType.Pallet, [pallet.Id], cancellationToken);
        return await ToPalletDtoAsync(pallet, codes.GetValueOrDefault(pallet.Id, string.Empty), cancellationToken);
    }

    private async Task<TracePalletDto> ToPalletDtoAsync(
        WoodenPallet pallet,
        string barcode,
        CancellationToken cancellationToken)
    {
        var live = pallet.Assignments.Where(a => a.ReversedAt is null).ToList();

        var names = await UserNamesAsync(
            new int?[]
            {
                pallet.CreatedByUserId,
                pallet.ClosedByUserId,
                pallet.ShippedByUserId,
                pallet.ShippingReversedByUserId,
                pallet.CancelledByUserId,
            }
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToList(),
            cancellationToken);

        // P in front of the code of the bags on it (section 19.26). The pallet took its
        // product and colour off its first bag, so they are the bags'.
        string? generalCode = null;
        if (pallet.Product is not null && pallet.Color is not null)
        {
            var book = await ProductCodeBook.LoadAsync(db, cancellationToken);
            generalCode = PalletCode.General(book.For(
                pallet.Product.Id,
                pallet.Color.Id,
                pallet.Product.IsAbsorbent,
                pallet.Product.PiecesPerBag,
                pallet.Color.Code));
        }

        return new TracePalletDto(
            pallet.Id,
            pallet.PalletNumber,
            barcode,
            pallet.Product?.Name,
            pallet.Color?.Name,
            Spaced(pallet.Status.ToString()),
            live.Count,
            pallet.Product?.BagsPerPallet is > 0 ? pallet.Product.BagsPerPallet : null,
            live.Sum(a => a.ProducedBag.PieceCount),
            live.Sum(a => a.ProducedBag.Weight),
            pallet.ShiftLine.ShiftReport.Shift.Name,
            pallet.ShiftLine.ShiftReport.ProductionDate,
            pallet.CreatedAt,
            pallet.CompletedAt,
            generalCode,
            names.GetValueOrDefault(pallet.CreatedByUserId, "—"),
            pallet.ClosedAt,
            Name(names, pallet.ClosedByUserId),
            pallet.ShippedAt,
            Name(names, pallet.ShippedByUserId),
            pallet.ShippingReversedAt,
            Name(names, pallet.ShippingReversedByUserId),
            pallet.ShippingReversalReason,
            pallet.CancelledAt,
            Name(names, pallet.CancelledByUserId),
            pallet.CancellationReason,
            pallet.Notes);
    }

    // ---------- helpers ----------

    private IQueryable<Roll> RollQuery() =>
        db.Rolls
            .Include(r => r.Color)
            .Include(r => r.Product)
            .Include(r => r.RecipeVersion).ThenInclude(v => v.Family)
            .Include(r => r.TestReport)
            .Include(r => r.Batch).ThenInclude(b => b.ShiftLine).ThenInclude(l => l.ProductionLine)
            .Include(r => r.Batch).ThenInclude(b => b.ShiftLine)
                .ThenInclude(l => l.ShiftReport).ThenInclude(s => s.Shift)
            .AsSplitQuery();

    /// <summary>A run with what its scrap and its count need: the roll's weight and the count.</summary>
    private IQueryable<ThermoProduction> RunQuery() =>
        db.ThermoProductions
            .Include(p => p.Roll).ThenInclude(r => r.Product)
            .Include(p => p.TestReport).ThenInclude(t => t!.Product)
            .Include(p => p.ShiftLine).ThenInclude(l => l.ProductionLine)
            .Include(p => p.ShiftLine).ThenInclude(l => l.ShiftReport).ThenInclude(s => s.Shift)
            .AsSplitQuery();

    private IQueryable<ProducedBag> BagQuery() =>
        db.ProducedBags
            .Include(b => b.Color)
            .Include(b => b.Product)
            .Include(b => b.ThermoProduction).ThenInclude(t => t.Roll);

    private IQueryable<WoodenPallet> PalletQuery() =>
        db.WoodenPallets
            .Include(p => p.Color)
            .Include(p => p.Product)
            .Include(p => p.ShiftLine).ThenInclude(l => l.ShiftReport).ThenInclude(s => s.Shift)
            .Include(p => p.Assignments).ThenInclude(a => a.ProducedBag)
            .AsSplitQuery();

    private static IEnumerable<int> RollUserIds(Roll roll) =>
        new int?[] { roll.ProducedByUserId, roll.MeasuredByUserId, roll.RemovedByUserId, roll.TestReport?.TestedByUserId }
            .Where(id => id is not null)
            .Select(id => id!.Value);

    private static IEnumerable<int> RunUserIds(ThermoProduction run) =>
        new int?[] { run.OperatorUserId, run.TestReport?.TestedByUserId, run.TestReport?.CorrectedByUserId }
            .Where(id => id is not null)
            .Select(id => id!.Value);

    private static IEnumerable<int> BagUserIds(ProducedBag bag) =>
        bag.RemovedByUserId is { } id ? [id] : [];

    private sealed record PalletRef(int Id, int Number);

    /// <summary>Which pallet each bag sits on. Live assignments only — a bag taken off is back in the store.</summary>
    private async Task<Dictionary<int, PalletRef>> PalletsOfBagsAsync(
        IReadOnlyList<int> bagIds,
        CancellationToken cancellationToken)
    {
        if (bagIds.Count == 0)
        {
            return [];
        }

        return await db.BagPalletAssignments
            .Where(a => bagIds.Contains(a.ProducedBagId) && a.ReversedAt == null)
            .Select(a => new { a.ProducedBagId, a.WoodenPalletId, a.WoodenPallet.PalletNumber })
            .ToDictionaryAsync(
                a => a.ProducedBagId,
                a => new PalletRef(a.WoodenPalletId, a.PalletNumber),
                cancellationToken);
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

        // A code switched off still names its thing here: a bag cancelled by a count
        // correction no longer scans anywhere else, but a trace of its roll must still say
        // which bag it was. A live code wins where there are both.
        var rows = await db.Barcodes
            .Where(b => b.ObjectType == objectType && objectIds.Contains(b.ObjectId))
            .Select(b => new { b.ObjectId, b.Value, b.IsActive })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(b => b.ObjectId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.IsActive).First().Value);
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

    /// <summary>The name of whoever did a thing that may not have been done.</summary>
    private static string? Name(Dictionary<int, string> names, int? id) =>
        id is null ? null : names.GetValueOrDefault(id.Value, "—");

    /// <summary><c>NeedsTest</c> is how the code spells it; <c>Needs test</c> is how a person reads it.</summary>
    private static string Spaced(string status) =>
        string.Concat(status.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));

    private static Result<TraceDto> Gone(string barcode) =>
        Result<TraceDto>.Failure(
            ErrorCode.NotFound, $"{barcode} names something that is no longer here.", "stock.gone", barcode);

    /// <summary>The same refusal, named so the screens can say it in Arabic.</summary>
    private static Result<TraceDto> Invalid(string message, string code, params string[] args) =>
        Result<TraceDto>.Failure(ErrorCode.ValidationFailed, message, code, args);
}
