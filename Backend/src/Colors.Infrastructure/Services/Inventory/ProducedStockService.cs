using Colors.Application.Common.Models;
using Colors.Application.Features.Inventory;
using Colors.Domain.Common;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Inventory;

/// <summary>
/// Produced stock — rolls, bags and pallets in one list (specification sections 8 to 10).
///
/// Three tables, one answer. The storekeeper holding a label does not know or care which
/// of the three he has, and the barcode table already treats them the same way, so this
/// reads all three and puts them in one shape.
///
/// Each kind keeps its own status words rather than being flattened into a shared set.
/// A roll that <c>Needs test</c> and a bag that is <c>Assigned</c> are genuinely
/// different situations, and blurring them into "in use" would lose what the man needs
/// to know.
/// </summary>
public class ProducedStockService(ColorsDbContext db) : IProducedStockService
{
    private const int MaxRows = 500;

    public async Task<IReadOnlyList<ProducedStockItemDto>> GetAsync(
        string? kind = null,
        string? status = null,
        string? search = null,
        bool availableOnly = false,
        CancellationToken cancellationToken = default)
    {
        var wanted = Trimmed(kind);
        var items = new List<ProducedStockItemDto>();

        if (Wants(wanted, "Roll"))
        {
            items.AddRange(await RollsAsync(cancellationToken));
        }

        if (Wants(wanted, "Bag"))
        {
            items.AddRange(await BagsAsync(cancellationToken));
        }

        if (Wants(wanted, "Pallet"))
        {
            items.AddRange(await PalletsAsync(cancellationToken));
        }

        var wantedStatus = Trimmed(status);
        if (wantedStatus is not null)
        {
            items = items
                .Where(i => i.Status.Equals(wantedStatus, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (availableOnly)
        {
            items = items.Where(i => i.IsAvailable).ToList();
        }

        var term = Trimmed(search);
        if (term is not null)
        {
            // One box for every field a man might have in front of him: the label, the
            // code written on the roll, the colour, or what it is.
            items = items
                .Where(i =>
                    Has(i.Barcode, term)
                    || Has(i.Code, term)
                    || Has(i.Description, term)
                    || Has(i.Whereabouts, term))
                .ToList();
        }

        return items
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .Take(MaxRows)
            .ToList();
    }

    public async Task<Result<BarcodeLabelDto>> GetLabelAsync(
        string barcode,
        CancellationToken cancellationToken = default)
    {
        var value = Trimmed(barcode)?.ToUpperInvariant();
        if (value is null)
        {
            return Result<BarcodeLabelDto>.Failure(
                ErrorCode.ValidationFailed, "Which label?");
        }

        var found = await db.Barcodes
            .FirstOrDefaultAsync(b => b.Value == value, cancellationToken);

        if (found is null)
        {
            return Result<BarcodeLabelDto>.Failure(
                ErrorCode.NotFound, $"No label in the system matches {value}.");
        }

        return found.ObjectType switch
        {
            BarcodeObjectType.Roll => await RollLabelAsync(found.Value, found.ObjectId, cancellationToken),
            BarcodeObjectType.Bag => await BagLabelAsync(found.Value, found.ObjectId, cancellationToken),
            BarcodeObjectType.Pallet => await PalletLabelAsync(found.Value, found.ObjectId, cancellationToken),
            _ => Result<BarcodeLabelDto>.Failure(
                ErrorCode.NotFound, "Nothing is known about that label."),
        };
    }

    public async Task<IReadOnlyList<BarcodeLabelDto>> GetLabelsAsync(
        IReadOnlyList<string> barcodes,
        CancellationToken cancellationToken = default)
    {
        var labels = new List<BarcodeLabelDto>();

        foreach (var barcode in barcodes.Take(MaxRows))
        {
            var label = await GetLabelAsync(barcode, cancellationToken);

            // One code nobody can resolve must not stop the other thirteen printing.
            if (label.IsSuccess && label.Value is not null)
            {
                labels.Add(label.Value);
            }
        }

        return labels;
    }

    // ---------- the three kinds ----------

    private async Task<List<ProducedStockItemDto>> RollsAsync(CancellationToken cancellationToken)
    {
        var rolls = await db.Rolls
            .Include(r => r.Batch)
            .Include(r => r.Color)
            .Include(r => r.RecipeVersion).ThenInclude(v => v.Family)
            .Include(r => r.TestReport)
            .OrderByDescending(r => r.ProducedAt)
            .Take(MaxRows)
            .ToListAsync(cancellationToken);

        var codes = await CodesAsync(BarcodeObjectType.Roll, rolls.Select(r => r.Id), cancellationToken);

        return rolls
            .Select(r => new ProducedStockItemDto(
                "Roll",
                r.Id,
                codes.GetValueOrDefault(r.Id, string.Empty),
                r.RollCode,
                $"{r.RecipeVersion.Family.Name} · {r.Color.Name}",
                Spaced(r.Status.ToString()),
                r.Status == RollStatus.Available,
                $"Batch {r.Batch.BatchNumber}",
                r.TestReport?.Weight,
                null,
                r.ProductionDate,
                r.ProducedAt))
            .ToList();
    }

    private async Task<List<ProducedStockItemDto>> BagsAsync(CancellationToken cancellationToken)
    {
        var bags = await db.ProducedBags
            .Include(b => b.Color)
            .Include(b => b.Product)
            .Include(b => b.ThermoProduction).ThenInclude(t => t.Roll)
            .Include(b => b.ThermoProduction).ThenInclude(t => t.ShiftLine)
                .ThenInclude(l => l.ShiftReport)
            .OrderByDescending(b => b.CreatedAt)
            .Take(MaxRows)
            .ToListAsync(cancellationToken);

        var codes = await CodesAsync(BarcodeObjectType.Bag, bags.Select(b => b.Id), cancellationToken);

        // Which pallet a bag sits on. Only the live assignments — a bag taken off is
        // back in the store, and saying otherwise is exactly the mistake to avoid.
        var bagIds = bags.Select(b => b.Id).ToList();
        var onPallets = await db.BagPalletAssignments
            .Where(a => bagIds.Contains(a.ProducedBagId) && a.ReversedAt == null)
            .Select(a => new { a.ProducedBagId, a.WoodenPallet.PalletNumber })
            .ToDictionaryAsync(a => a.ProducedBagId, a => a.PalletNumber, cancellationToken);

        return bags
            .Select(b => new ProducedStockItemDto(
                "Bag",
                b.Id,
                codes.GetValueOrDefault(b.Id, string.Empty),
                ProductCode.For(b.Product.IsAbsorbent, b.PieceCount, b.Color.Code),
                $"{b.Product.Name} · {b.Color.Name}",
                Spaced(b.Status.ToString()),
                b.Status == ProducedBagStatus.Available,
                // The roll stays on the row even once the bag is packed. Searching a
                // roll code has to find the bags it made — that is the traceability the
                // bag barcodes exist for, and it would be lost the moment the bag went
                // onto a pallet.
                onPallets.TryGetValue(b.Id, out var pallet)
                    ? $"Pallet {pallet} · from roll {b.ThermoProduction.Roll.RollCode}"
                    : $"From roll {b.ThermoProduction.Roll.RollCode}",
                b.Weight,
                b.PieceCount,
                b.ThermoProduction.ShiftLine.ShiftReport.ProductionDate,
                b.CreatedAt))
            .ToList();
    }

    private async Task<List<ProducedStockItemDto>> PalletsAsync(CancellationToken cancellationToken)
    {
        var pallets = await db.WoodenPallets
            .Include(p => p.Color)
            .Include(p => p.Product)
            .Include(p => p.ShiftLine).ThenInclude(l => l.ProductionLine)
            .Include(p => p.ShiftLine).ThenInclude(l => l.ShiftReport)
            .Include(p => p.Assignments).ThenInclude(a => a.ProducedBag)
            .OrderByDescending(p => p.CreatedAt)
            .Take(MaxRows)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var codes = await CodesAsync(BarcodeObjectType.Pallet, pallets.Select(p => p.Id), cancellationToken);

        return pallets
            .Select(p =>
            {
                var live = p.Assignments.Where(a => a.ReversedAt is null).ToList();

                return new ProducedStockItemDto(
                    "Pallet",
                    p.Id,
                    codes.GetValueOrDefault(p.Id, string.Empty),
                    $"Pallet {p.PalletNumber}",
                    p.Product is null
                        ? "Nothing on it yet"
                        : $"{p.Product.Name} · {p.Color?.Name}",
                    p.Status.ToString(),
                    p.Status is PalletStatus.Empty or PalletStatus.Opened,
                    $"{live.Count} bag{(live.Count == 1 ? "" : "s")} · {p.ShiftLine.ProductionLine.Name}",
                    live.Sum(a => a.ProducedBag.Weight),
                    live.Sum(a => a.ProducedBag.PieceCount),
                    p.ShiftLine.ShiftReport.ProductionDate,
                    p.CreatedAt);
            })
            .ToList();
    }

    // ---------- labels ----------

    private async Task<Result<BarcodeLabelDto>> RollLabelAsync(
        string barcode,
        int rollId,
        CancellationToken cancellationToken)
    {
        var roll = await db.Rolls
            .Include(r => r.Color)
            .Include(r => r.RecipeVersion).ThenInclude(v => v.Family)
            .Include(r => r.TestReport)
            .Include(r => r.Batch).ThenInclude(b => b.ShiftLine).ThenInclude(l => l.ShiftReport)
                .ThenInclude(s => s.Shift)
            .FirstOrDefaultAsync(r => r.Id == rollId, cancellationToken);

        return roll is null
            ? Gone(barcode)
            : Result<BarcodeLabelDto>.Success(new BarcodeLabelDto(
                barcode,
                "Roll",
                roll.RollCode,
                null,
                null,
                roll.RecipeVersion.Family.Name,
                roll.Color.Name,
                null,
                roll.TestReport?.Weight,
                roll.Batch.ShiftLine.ShiftReport.Shift.Name,
                roll.ProductionDate,
                roll.ProducedAt));
    }

    private async Task<Result<BarcodeLabelDto>> BagLabelAsync(
        string barcode,
        int bagId,
        CancellationToken cancellationToken)
    {
        var bag = await db.ProducedBags
            .Include(b => b.Color)
            .Include(b => b.Product)
            .Include(b => b.ThermoProduction).ThenInclude(t => t.Roll)
            .Include(b => b.ThermoProduction).ThenInclude(t => t.ShiftLine)
                .ThenInclude(l => l.ShiftReport).ThenInclude(s => s.Shift)
            .FirstOrDefaultAsync(b => b.Id == bagId, cancellationToken);

        if (bag is null)
        {
            return Gone(barcode);
        }

        var productCode = ProductCode.For(bag.Product.IsAbsorbent, bag.PieceCount, bag.Color.Code);

        return Result<BarcodeLabelDto>.Success(new BarcodeLabelDto(
            barcode,
            "Bag",
            productCode,
            // The factory already prints the roll number on every bag, and does its
            // traceability on paper with it. Keeping it makes the label familiar.
            bag.ThermoProduction.Roll.RollCode,
            productCode,
            bag.Product.Name,
            bag.Color.Name,
            bag.PieceCount,
            bag.Weight,
            // The thermo shift, which is not the shift inside the roll code — the two
            // are genuinely different, which is why the run is its own record.
            bag.ThermoProduction.ShiftLine.ShiftReport.Shift.Name,
            bag.ThermoProduction.ShiftLine.ShiftReport.ProductionDate,
            bag.CreatedAt));
    }

    private async Task<Result<BarcodeLabelDto>> PalletLabelAsync(
        string barcode,
        int palletId,
        CancellationToken cancellationToken)
    {
        var pallet = await db.WoodenPallets
            .Include(p => p.Color)
            .Include(p => p.Product)
            .Include(p => p.ShiftLine).ThenInclude(l => l.ShiftReport).ThenInclude(s => s.Shift)
            .Include(p => p.Assignments).ThenInclude(a => a.ProducedBag)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == palletId, cancellationToken);

        if (pallet is null)
        {
            return Gone(barcode);
        }

        var live = pallet.Assignments.Where(a => a.ReversedAt is null).ToList();

        return Result<BarcodeLabelDto>.Success(new BarcodeLabelDto(
            barcode,
            "Pallet",
            $"Pallet {pallet.PalletNumber}",
            null,
            null,
            pallet.Product?.Name,
            pallet.Color?.Name,
            live.Sum(a => a.ProducedBag.PieceCount),
            live.Sum(a => a.ProducedBag.Weight),
            pallet.ShiftLine.ShiftReport.Shift.Name,
            pallet.ShiftLine.ShiftReport.ProductionDate,
            pallet.CreatedAt));
    }

    // ---------- helpers ----------

    private async Task<Dictionary<int, string>> CodesAsync(
        BarcodeObjectType objectType,
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        return await db.Barcodes
            .Where(b => b.ObjectType == objectType && wanted.Contains(b.ObjectId) && b.IsActive)
            .ToDictionaryAsync(b => b.ObjectId, b => b.Value, cancellationToken);
    }

    private static bool Wants(string? asked, string kind) =>
        asked is null || asked.Equals(kind, StringComparison.OrdinalIgnoreCase);

    private static bool Has(string value, string term) =>
        value.Contains(term, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>NeedsTest</c> is how the code spells it. <c>Needs test</c> is how a person
    /// reads it.
    /// </summary>
    private static string Spaced(string status) =>
        string.Concat(status.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));

    private static Result<BarcodeLabelDto> Gone(string barcode) =>
        Result<BarcodeLabelDto>.Failure(
            ErrorCode.NotFound, $"{barcode} names something that is no longer here.");

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
