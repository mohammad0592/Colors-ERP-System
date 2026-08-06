using Colors.Application.Common.Models;
using Colors.Application.Features.Packaging;
using Colors.Domain.Common;
using Colors.Domain.Constants;
using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Packaging;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Packaging;

/// <summary>
/// What packaging a shift used (specification section 10).
///
/// Three of the materials are not typed at all: what the shift produced already says how
/// many were used, and the operator entering them by hand is how the 2 July form ended
/// up saying 6.1 large bags where 61 were used, and 4.14 small where 122 were.
///
/// The weights stay typed, because the factory already weighs them — and that is what
/// makes the check free. Counted against weighed, with no extra work for anybody.
/// </summary>
public class PackagingService(
    ColorsDbContext db,
    StockLedger ledger,
    TimeProvider timeProvider) : IPackagingService
{
    public async Task<IReadOnlyList<PackagingConsumptionDto>> GetAllAsync(
        int? shiftReportId = null,
        CancellationToken cancellationToken = default)
    {
        var records = await Query()
            .Where(c => shiftReportId == null || c.ShiftLine.ShiftReportId == shiftReportId)
            .OrderByDescending(c => c.RecordedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var names = await UserNamesAsync(records.Select(c => c.RecordedByUserId), cancellationToken);
        var stock = await StockAsync(
            records.SelectMany(c => c.Lines).Select(l => l.MaterialId), cancellationToken);

        return records.Select(c => ToDto(c, names, stock)).ToList();
    }

    public async Task<Result<PackagingDraftDto>> GetDraftAsync(
        int shiftLineId,
        CancellationToken cancellationToken = default)
    {
        var shiftLine = await db.ShiftLines
            .Include(l => l.ProductionLine)
            .Include(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .FirstOrDefaultAsync(l => l.Id == shiftLineId, cancellationToken);

        if (shiftLine is null)
        {
            return Result<PackagingDraftDto>.Failure(
                ErrorCode.NotFound, "This line of the shift does not exist.");
        }

        var existing = await Query()
            .FirstOrDefaultAsync(c => c.ShiftLineId == shiftLineId, cancellationToken);

        var counts = await CountsAsync(shiftLineId, cancellationToken);
        var materials = await PackagingMaterialsAsync(cancellationToken);
        var stock = await StockAsync(materials.Select(m => m.Id), cancellationToken);

        // A line already recorded shows what was saved; the rest show the count and an
        // empty weight box.
        var saved = existing?.Lines.ToDictionary(l => l.MaterialId) ?? [];

        var lines = materials
            .Select(m =>
            {
                var quantity = saved.TryGetValue(m.Id, out var line)
                    ? line.Quantity
                    : counts.For(m.CountedAs);

                return ToLineDto(
                    m,
                    quantity,
                    saved.TryGetValue(m.Id, out var l) ? l.Weight : null,
                    m.CountedAs != CountedPackaging.None,
                    stock);
            })
            .ToList();

        return Result<PackagingDraftDto>.Success(new PackagingDraftDto(
            shiftLineId,
            shiftLine.ProductionLine.Name,
            shiftLine.ShiftReport.Shift.Name,
            shiftLine.ShiftReport.ProductionDate,
            counts.BagsProduced,
            counts.PalletsCompleted,
            existing is not null,
            lines));
    }

    public async Task<Result<PackagingConsumptionDto>> SaveAsync(
        SavePackagingRequest request,
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

        // Packaging is used where the bags come off (specification section 4).
        if (!shiftLine.ProductionLine.FormsBags)
        {
            return Invalid(
                $"{shiftLine.ProductionLine.Name} does not pack anything. Choose the thermo line.");
        }

        if (!ShiftWork.AcceptsWork(shiftLine.ShiftReport.Status))
        {
            return Invalid(ShiftWork.RefusalFor(shiftLine.ShiftReport));
        }

        if (await db.PackagingConsumptions
                .AnyAsync(c => c.ShiftLineId == request.ShiftLineId, cancellationToken))
        {
            return Invalid(
                "Packaging has already been recorded for this line. It is written once, "
                + "at the end of the shift.");
        }

        var materials = await PackagingMaterialsAsync(cancellationToken);
        var byId = materials.ToDictionary(m => m.Id);

        var typed = request.Lines
            .Where(l => byId.ContainsKey(l.MaterialId))
            .GroupBy(l => l.MaterialId)
            .ToDictionary(g => g.Key, g => g.First());

        var unknown = request.Lines.Where(l => !byId.ContainsKey(l.MaterialId)).ToList();
        if (unknown.Count > 0)
        {
            return Invalid("Every line must name an active packaging material.");
        }

        if (request.Lines.Any(l => l.Quantity < 0))
        {
            return Invalid("A quantity cannot be less than nothing.");
        }

        if (request.Lines.Any(l => l.Weight is <= 0))
        {
            return Invalid("A weight of zero is not a weighing. Leave it empty instead.");
        }

        // The counted three are worked out here, not believed from the screen. A tablet
        // can be old, or somebody can post straight to the endpoint.
        var counts = await CountsAsync(request.ShiftLineId, cancellationToken);

        var lines = materials
            .Select(m =>
            {
                var isCounted = m.CountedAs != CountedPackaging.None;
                typed.TryGetValue(m.Id, out var given);

                return new PackagingConsumptionLine
                {
                    MaterialId = m.Id,
                    Quantity = isCounted ? counts.For(m.CountedAs) : given?.Quantity ?? 0m,
                    Weight = given?.Weight,
                    WasCounted = isCounted,
                };
            })
            // A material nobody used and nobody weighed did not take part in this shift.
            .Where(l => l.Quantity > 0 || l.Weight is not null)
            .ToList();

        if (lines.Count == 0)
        {
            return Invalid(
                "Nothing to record. The shift produced no bags and no packaging was typed in.");
        }

        // The record and the stock it moves are one act. Half a record posted — three
        // materials out of six because the fourth was short — would leave the store
        // wrong and the shift's figures unreadable.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var consumption = new PackagingConsumption
        {
            ShiftLineId = shiftLine.Id,
            RecordedByUserId = userId,
            RecordedAt = timeProvider.GetUtcNow(),
            Notes = Trimmed(request.Notes),
            Lines = lines,
        };

        db.PackagingConsumptions.Add(consumption);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var line in lines.Where(l => l.Quantity > 0))
        {
            var material = byId[line.MaterialId];

            // Its own movement type, never Issue: packaging must not land in the
            // material-waste figures, which only mean anything for what the mixer took.
            var posted = await ledger.PostAsync(
                line.MaterialId,
                MovementTypeNames.PackagingConsumption,
                line.Quantity,
                userId,
                $"Packaging used on {shiftLine.ProductionLine.Name}, shift "
                + $"{shiftLine.ShiftReport.Shift.Name} {shiftLine.ShiftReport.ProductionDate:dd/MM/yyyy}",
                null,
                shiftLine.ShiftReportId,
                cancellationToken);

            if (!posted.IsSuccess)
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();

                return Invalid($"{material.Name}: {posted.Message}");
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return await LoadAsync(consumption.Id, cancellationToken);
    }

    // ---------- what the shift produced ----------

    /// <summary>
    /// The three figures nobody types, read off what the shift actually made.
    ///
    /// The bag counts come from the <b>product</b>, so a shift that switched mould
    /// halfway is still counted correctly — the plates take a large bag and two small,
    /// the meal boxes one small and no large, and each bag knows which it is.
    /// </summary>
    private async Task<ProducedCounts> CountsAsync(
        int shiftLineId,
        CancellationToken cancellationToken)
    {
        var bags = await db.ProducedBags
            .Where(b => b.ThermoProduction.ShiftLineId == shiftLineId)
            .Select(b => new { b.Product.LargeBagsPerBag, b.Product.SmallBagsPerBag })
            .ToListAsync(cancellationToken);

        // A pallet counts when it is finished — an open one is still being built and its
        // wood has not been used up yet.
        var pallets = await db.WoodenPallets
            .CountAsync(p => p.ShiftLineId == shiftLineId && p.CompletedAt != null, cancellationToken);

        return new ProducedCounts(
            bags.Count,
            bags.Sum(b => b.LargeBagsPerBag),
            bags.Sum(b => b.SmallBagsPerBag),
            pallets);
    }

    private sealed record ProducedCounts(
        int BagsProduced,
        int LargeBags,
        int SmallBags,
        int PalletsCompleted)
    {
        public decimal For(CountedPackaging counted) => counted switch
        {
            CountedPackaging.LargeBag => LargeBags,
            CountedPackaging.SmallBag => SmallBags,
            CountedPackaging.WoodenPallet => PalletsCompleted,
            _ => 0m,
        };
    }

    // ---------- helpers ----------

    /// <summary>Active materials in a category that does not go out on a ticket.</summary>
    private async Task<List<Material>> PackagingMaterialsAsync(CancellationToken cancellationToken) =>
        await db.Materials
            .Include(m => m.Category)
            .Include(m => m.BaseUnit)
            .Where(m => m.IsActive && !m.Category.IssuedOnTickets)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);

    private async Task<Dictionary<int, decimal>> StockAsync(
        IEnumerable<int> materialIds,
        CancellationToken cancellationToken)
    {
        var wanted = materialIds.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        return await db.MaterialInventory
            .Where(i => wanted.Contains(i.MaterialId))
            .ToDictionaryAsync(i => i.MaterialId, i => i.CurrentQuantity, cancellationToken);
    }

    private static PackagingLineDto ToLineDto(
        Material material,
        decimal quantity,
        decimal? weight,
        bool isCounted,
        Dictionary<int, decimal> stock)
    {
        var expected = material.UnitWeight is null
            ? (decimal?)null
            : Math.Round(quantity * material.UnitWeight.Value, 3);

        return new PackagingLineDto(
            material.Id,
            material.Code,
            material.Name,
            material.BaseUnit.Symbol,
            material.CountedAs.ToString(),
            isCounted,
            quantity,
            weight,
            expected,
            expected is null || weight is null ? null : Math.Round(weight.Value - expected.Value, 3),
            stock.GetValueOrDefault(material.Id, 0m));
    }

    private IQueryable<PackagingConsumption> Query() =>
        db.PackagingConsumptions
            .Include(c => c.ShiftLine).ThenInclude(l => l.ProductionLine)
            .Include(c => c.ShiftLine).ThenInclude(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .Include(c => c.Lines).ThenInclude(l => l.Material).ThenInclude(m => m.BaseUnit)
            .AsSplitQuery();

    private static PackagingConsumptionDto ToDto(
        PackagingConsumption c,
        Dictionary<int, string> names,
        Dictionary<int, decimal> stock) =>
        new(
            c.Id,
            c.ShiftLineId,
            c.ShiftLine.ProductionLine.Name,
            c.ShiftLine.ShiftReport.Shift.Name,
            c.ShiftLine.ShiftReport.ProductionDate,
            names.GetValueOrDefault(c.RecordedByUserId, "—"),
            c.RecordedAt,
            c.Notes,
            c.Lines
                .OrderBy(l => l.Material.Name)
                .Select(l => ToLineDto(l.Material, l.Quantity, l.Weight, l.WasCounted, stock))
                .ToList());

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

    private async Task<Result<PackagingConsumptionDto>> LoadAsync(
        int id,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var record = await Query().FirstAsync(c => c.Id == id, cancellationToken);
        var names = await UserNamesAsync([record.RecordedByUserId], cancellationToken);
        var stock = await StockAsync(record.Lines.Select(l => l.MaterialId), cancellationToken);

        return Result<PackagingConsumptionDto>.Success(ToDto(record, names, stock));
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<PackagingConsumptionDto> Invalid(string message) =>
        Result<PackagingConsumptionDto>.Failure(ErrorCode.ValidationFailed, message);
}
