using Colors.Application.Common.Models;
using Colors.Application.Features.Reports;
using Colors.Domain.Common;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Reports;

/// <summary>
/// Reports (specification section 13).
///
/// Every figure here is <b>read</b> from records that already exist. Nothing is stored,
/// so a report cannot disagree with the data underneath it — it <i>is</i> the data,
/// arranged.
/// </summary>
public class ReportsService(ColorsDbContext db) : IReportsService
{
    public async Task<Result<MaterialWasteReportDto>> GetMaterialWasteAsync(
        int shiftReportId,
        CancellationToken cancellationToken = default)
    {
        var report = await db.ShiftReports
            .Include(r => r.Shift)
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == shiftReportId, cancellationToken);

        if (report is null)
        {
            return Result<MaterialWasteReportDto>.Failure(
                ErrorCode.NotFound, "This shift does not exist.", "shift.notFound");
        }

        var lineIds = report.Lines.Select(l => l.Id).ToList();

        // What actually left the store for this shift, and what came back. Net used is
        // the pair, not two separate stories — a ticket opened and closed is one act.
        var ticketIds = await db.MaterialIssueTickets
            .Where(t => lineIds.Contains(t.ShiftLineId))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var used = await db.MaterialIssueTicketLines
            .Where(l => ticketIds.Contains(l.TicketId))
            .Include(l => l.Material).ThenInclude(m => m.BaseUnit)
            .GroupBy(l => l.MaterialId)
            .Select(g => new
            {
                MaterialId = g.Key,
                Material = g.First().Material,
                Issued = g.Sum(l => l.IssuedQuantity),
                Returned = g.Sum(l => l.ReturnedQuantity),
            })
            .ToListAsync(cancellationToken);

        // The rolls this shift made, and the recipes they were made to. One mix a shift
        // means one recipe in practice, but the data is asked rather than assumed.
        var rolls = await db.Rolls
            .Where(r => lineIds.Contains(r.Batch.ShiftLineId))
            .Select(r => new
            {
                r.RecipeVersionId,
                r.RecipeVersion.RecipeNumber,
                r.RecipeVersion.VersionNumber,
                FamilyName = r.RecipeVersion.Family.Name,
                Weight = r.TestReport == null ? (decimal?)null : r.TestReport.Weight,
            })
            .ToListAsync(cancellationToken);

        var recipes = rolls.Select(r => r.RecipeVersionId).Distinct().ToList();
        var single = recipes.Count == 1 ? rolls[0] : null;

        // The requirement only means something when every roll came from one recipe.
        // Two recipes in a shift and there is no single set of percentages to hold the
        // materials against, so the column is left empty rather than filled with an
        // average nobody asked for.
        var ingredients = single is null
            ? []
            : await db.RecipeIngredients
                .Where(i => i.RecipeVersionId == single.RecipeVersionId)
                .Include(i => i.Material).ThenInclude(m => m.BaseUnit)
                .ToListAsync(cancellationToken);

        var byMaterial = ingredients.ToDictionary(i => i.MaterialId);

        // The 100% the percentages are shares of: the polymer that actually went in.
        // Read off the recipe's own IsBaseResin flag, never off a material's name
        // (specification section 5).
        var resinUsed = used
            .Where(u => byMaterial.TryGetValue(u.MaterialId, out var i) && i.IsBaseResin)
            .Sum(u => u.Issued - u.Returned);

        // Every material that took part: issued to the shift, required by the recipe, or
        // both. A material the recipe asks for and nobody issued is exactly the kind of
        // hole this report exists to show.
        var materialIds = used.Select(u => u.MaterialId)
            .Union(ingredients.Select(i => i.MaterialId))
            .ToList();

        var lines = new List<MaterialWasteLineDto>();

        foreach (var materialId in materialIds)
        {
            var actual = used.FirstOrDefault(u => u.MaterialId == materialId);
            byMaterial.TryGetValue(materialId, out var ingredient);

            var material = actual?.Material ?? ingredient!.Material;
            var issued = actual?.Issued ?? 0m;
            var returned = actual?.Returned ?? 0m;
            var net = issued - returned;

            decimal? required = ingredient is null || resinUsed == 0
                ? null
                : Math.Round(resinUsed * ingredient.TargetPercentage / 100m, 3);

            decimal? difference = required is null
                ? null
                : Math.Round(net - required.Value, 3);

            // The share actually used, against the range the supervisor set. Outside it
            // is worth seeing even when the kilograms look small.
            var outsideRange = ingredient is not null && resinUsed > 0
                && (net / resinUsed * 100m < ingredient.MinPercentage
                    || net / resinUsed * 100m > ingredient.MaxPercentage);

            lines.Add(new MaterialWasteLineDto(
                materialId,
                material.Code,
                material.Name,
                material.BaseUnit.Symbol,
                ingredient?.IsBaseResin ?? false,
                issued,
                returned,
                net,
                ingredient?.TargetPercentage,
                required,
                difference,
                required is null or 0
                    ? null
                    : Math.Round(difference!.Value / required.Value * 100m, 2),
                outsideRange));
        }

        return Result<MaterialWasteReportDto>.Success(new MaterialWasteReportDto(
            report.Id,
            report.ProductionDate,
            report.Shift.Name,
            report.Status.ToString(),
            single?.RecipeNumber,
            single?.FamilyName,
            single?.VersionNumber,
            recipes.Count,
            resinUsed,
            rolls.Count,
            rolls.Sum(r => r.Weight ?? 0m),
            // Base resin first, then the additives in the order they are used, so the
            // report reads the way the recipe does.
            lines
                .OrderByDescending(l => l.IsBaseResin)
                .ThenByDescending(l => l.TargetPercentage ?? -1)
                .ThenBy(l => l.MaterialName)
                .ToList()));
    }

    public async Task<Result<ConsumptionReportDto>> GetConsumptionAsync(
        DateOnly from,
        DateOnly to,
        ConsumptionGrouping grouping,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            return Result<ConsumptionReportDto>.Failure(
                ErrorCode.ValidationFailed, "The last day cannot be before the first.", "reports.badDateRange");
        }

        // One row per shift first, whichever grouping was asked for: a recipe's usage is
        // its shifts added together, and a shift is the only thing material is ever
        // issued to (specification section 13).
        var shifts = await db.ShiftReports
            .Where(r => r.ProductionDate >= from && r.ProductionDate <= to)
            .Select(r => new
            {
                r.Id,
                r.ProductionDate,
                ShiftName = r.Shift.Name,
                LineIds = r.Lines.Select(l => l.Id).ToList(),
            })
            .ToListAsync(cancellationToken);

        if (shifts.Count == 0)
        {
            return Result<ConsumptionReportDto>.Success(new ConsumptionReportDto(
                from, to, grouping.ToString(), [], 0));
        }

        var allLineIds = shifts.SelectMany(s => s.LineIds).ToList();

        var used = await db.MaterialIssueTickets
            .Where(t => allLineIds.Contains(t.ShiftLineId))
            .SelectMany(t => t.Lines, (t, l) => new UsedLine(
                t.ShiftLineId,
                l.MaterialId,
                l.Material.Code,
                l.Material.Name,
                l.Material.BaseUnit.Symbol,
                l.IssuedQuantity,
                l.ReturnedQuantity))
            .ToListAsync(cancellationToken);

        var rolls = await db.Rolls
            .Where(r => allLineIds.Contains(r.Batch.ShiftLineId))
            .Select(r => new
            {
                ShiftLineId = r.Batch.ShiftLineId,
                r.RecipeVersionId,
                r.RecipeVersion.RecipeNumber,
                FamilyName = r.RecipeVersion.Family.Name,
                Weight = r.TestReport == null ? (decimal?)null : r.TestReport.Weight,
            })
            .ToListAsync(cancellationToken);

        // Everything a shift did, gathered once and then arranged whichever way was
        // asked for.
        var perShift = shifts.Select(s =>
        {
            var lines = s.LineIds;
            var shiftRolls = rolls.Where(r => lines.Contains(r.ShiftLineId)).ToList();
            var recipes = shiftRolls.Select(r => r.RecipeVersionId).Distinct().ToList();

            return new
            {
                s.Id,
                s.ProductionDate,
                s.ShiftName,
                Used = used.Where(u => lines.Contains(u.ShiftLineId)).ToList(),
                Rolls = shiftRolls.Count,
                RollWeight = shiftRolls.Sum(r => r.Weight ?? 0m),
                RecipeCount = recipes.Count,
                RecipeNumber = recipes.Count == 1 ? shiftRolls[0].RecipeNumber : (int?)null,
                FamilyName = recipes.Count == 1 ? shiftRolls[0].FamilyName : null,
            };
        }).ToList();

        List<ConsumptionGroupDto> groups;
        var mixed = 0;

        if (grouping == ConsumptionGrouping.Recipe)
        {
            // A shift that switched recipe cannot say which of them its material went
            // into. Left out, and counted so the reader knows what is missing.
            mixed = perShift.Count(s => s.RecipeCount > 1);

            groups = perShift
                .Where(s => s.RecipeNumber is not null)
                .GroupBy(s => new { s.RecipeNumber, s.FamilyName })
                .Select(g => new ConsumptionGroupDto(
                    $"Recipe {g.Key.RecipeNumber} — {g.Key.FamilyName}",
                    null,
                    null,
                    null,
                    g.Key.RecipeNumber,
                    g.Key.FamilyName,
                    g.Count(),
                    g.Sum(s => s.Rolls),
                    g.Sum(s => s.RollWeight),
                    g.SelectMany(s => s.Used).Sum(u => u.Issued - u.Returned),
                    Materials(
                        g.SelectMany(s => s.Used),
                        g.Sum(s => s.RollWeight))))
                .OrderBy(g => g.RecipeNumber)
                .ToList();
        }
        else
        {
            groups = perShift
                .OrderByDescending(s => s.ProductionDate)
                .ThenBy(s => s.ShiftName)
                .Select(s => new ConsumptionGroupDto(
                    $"{s.ProductionDate:dd/MM/yyyy} — shift {s.ShiftName}",
                    s.Id,
                    s.ProductionDate,
                    s.ShiftName,
                    s.RecipeNumber,
                    s.FamilyName,
                    1,
                    s.Rolls,
                    s.RollWeight,
                    s.Used.Sum(u => u.Issued - u.Returned),
                    Materials(s.Used, s.RollWeight)))
                .ToList();
        }

        // A row that consumed nothing and made nothing is noise on a range report.
        groups = groups.Where(g => g.Materials.Count > 0 || g.RollsProduced > 0).ToList();

        return Result<ConsumptionReportDto>.Success(new ConsumptionReportDto(
            from, to, grouping.ToString(), groups, mixed));

        // The per-material rows, with usage per kilogram of roll so a long shift and a
        // short one can be read against each other.
        static IReadOnlyList<ConsumptionMaterialDto> Materials(
            IEnumerable<UsedLine> lines,
            decimal rollWeight) =>
            lines
                .GroupBy(l => new { l.MaterialId, l.Code, l.Name, l.UnitSymbol })
                .Select(g =>
                {
                    var net = g.Sum(l => l.Issued - l.Returned);

                    return new ConsumptionMaterialDto(
                        g.Key.MaterialId,
                        g.Key.Code,
                        g.Key.Name,
                        g.Key.UnitSymbol,
                        g.Sum(l => l.Issued),
                        g.Sum(l => l.Returned),
                        net,
                        rollWeight == 0 ? null : Math.Round(net / rollWeight, 4));
                })
                .OrderByDescending(m => m.NetUsed)
                .ToList();
    }

    public async Task<Result<PalletProductionReportDto>> GetPalletProductionAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            return Result<PalletProductionReportDto>.Failure(
                ErrorCode.ValidationFailed, "The last day cannot be before the first.", "reports.badDateRange");
        }

        // A pallet belongs to the shift it was started on, which is where its wood came
        // from (specification section 10).
        var pallets = await db.WoodenPallets
            .Where(p => p.ShiftLine.ShiftReport.ProductionDate >= from
                        && p.ShiftLine.ShiftReport.ProductionDate <= to)
            .Select(p => new
            {
                p.Id,
                p.ProductId,
                ProductName = p.Product == null ? null : p.Product.Name,
                BagsPerPallet = p.Product == null ? 0 : p.Product.BagsPerPallet,
                Completed = p.CompletedAt != null,
                Cancelled = p.CancelledAt != null,
                Bags = p.Assignments.Count(a => a.ReversedAt == null),
                Pieces = p.Assignments
                    .Where(a => a.ReversedAt == null)
                    .Sum(a => (int?)a.ProducedBag.PieceCount) ?? 0,
                Weight = p.Assignments
                    .Where(a => a.ReversedAt == null)
                    .Sum(a => (decimal?)a.ProducedBag.Weight) ?? 0m,
            })
            .ToListAsync(cancellationToken);

        // Only finished pallets are counted under a product. A pallet still being filled
        // could still change what it holds, and one given up on held nothing at all.
        var products = pallets
            .Where(p => p.Completed && !p.Cancelled && p.ProductId is not null)
            .GroupBy(p => new { p.ProductId, p.ProductName, p.BagsPerPallet })
            .Select(g => new PalletProductLineDto(
                g.Key.ProductId!.Value,
                g.Key.ProductName!,
                g.Count(),
                g.Sum(p => p.Bags),
                g.Sum(p => p.Pieces),
                Math.Round(g.Sum(p => p.Weight), 3),
                g.Key.BagsPerPallet))
            .OrderByDescending(p => p.PalletsCompleted)
            .ThenBy(p => p.ProductName)
            .ToList();

        return Result<PalletProductionReportDto>.Success(new PalletProductionReportDto(
            from,
            to,
            pallets.Count,
            pallets.Count(p => p.Completed && !p.Cancelled),
            pallets.Count(p => p.Cancelled),
            pallets.Count(p => !p.Completed && !p.Cancelled),
            products));
    }

    public async Task<Result<RecycledMaterialReportDto>> GetRecycledMaterialAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            return Result<RecycledMaterialReportDto>.Failure(
                ErrorCode.ValidationFailed, "The last day cannot be before the first.", "reports.badDateRange");
        }

        // Which material the recycler makes is a flag on the row, never its name
        // (specification section 11).
        var material = await db.Materials
            .Where(m => m.IsRecycledOutput)
            .Select(m => new { m.Id, m.Name })
            .FirstOrDefaultAsync(cancellationToken);

        var made = await db.RecyclerProductions
            .Where(r => r.ShiftLine.ShiftReport.ProductionDate >= from
                        && r.ShiftLine.ShiftReport.ProductionDate <= to)
            .OrderByDescending(r => r.ShiftLine.ShiftReport.ProductionDate)
            .Select(r => new
            {
                r.ShiftLine.ShiftReportId,
                r.ShiftLine.ShiftReport.ProductionDate,
                ShiftName = r.ShiftLine.ShiftReport.Shift.Name,
                LineName = r.ShiftLine.ProductionLine.Name,
                r.RecycledMaterialWeight,
                r.RecordedByUserId,
                r.Notes,
            })
            .ToListAsync(cancellationToken);

        var names = await UserNamesAsync(made.Select(m => m.RecordedByUserId), cancellationToken);

        // The other half of the question: how much the mixer took back out. Only the
        // black recipes use it, so this is what the pile is for.
        var consumed = material is null
            ? 0m
            : await db.MaterialIssueTickets
                .Where(t => t.ShiftLine.ShiftReport.ProductionDate >= from
                            && t.ShiftLine.ShiftReport.ProductionDate <= to)
                .SelectMany(t => t.Lines)
                .Where(l => l.MaterialId == material.Id)
                .SumAsync(l => (decimal?)(l.IssuedQuantity - l.ReturnedQuantity), cancellationToken)
                ?? 0m;

        var inStock = material is null
            ? 0m
            : await db.MaterialInventory
                .Where(i => i.MaterialId == material.Id)
                .Select(i => i.CurrentQuantity)
                .FirstOrDefaultAsync(cancellationToken);

        var produced = made.Sum(m => m.RecycledMaterialWeight);

        return Result<RecycledMaterialReportDto>.Success(new RecycledMaterialReportDto(
            from,
            to,
            material?.Name,
            produced,
            consumed,
            Math.Round(produced - consumed, 3),
            inStock,
            made.Select(m => new RecycledShiftLineDto(
                m.ShiftReportId,
                m.ProductionDate,
                m.ShiftName,
                m.LineName,
                m.RecycledMaterialWeight,
                names.GetValueOrDefault(m.RecordedByUserId, "—"),
                m.Notes)).ToList()));
    }

    public async Task<Result<ShiftSummaryReportDto>> GetShiftSummaryAsync(
        int shiftReportId,
        CancellationToken cancellationToken = default)
    {
        var report = await db.ShiftReports
            .Include(r => r.Shift)
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == shiftReportId, cancellationToken);

        if (report is null)
        {
            return Result<ShiftSummaryReportDto>.Failure(
                ErrorCode.NotFound, "This shift does not exist.", "shift.notFound");
        }

        var lineIds = report.Lines.Select(l => l.Id).ToList();

        var supervisor = report.SupervisorUserId is null
            ? null
            : await db.Set<ApplicationUser>()
                .Where(u => u.Id == report.SupervisorUserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);

        // What the extruder made.
        var made = await db.Rolls
            .Where(r => lineIds.Contains(r.Batch.ShiftLineId))
            .Select(r => r.TestReport == null ? (decimal?)null : r.TestReport.Weight)
            .ToListAsync(cancellationToken);

        // What the thermo formed, one row per counted run. Each carries its own plate
        // weight — the whole point of the summary, because a single shared figure would
        // quietly rewrite the loss (specification section 13).
        var runs = await db.ThermoTestReports
            .Where(t => lineIds.Contains(t.ThermoProduction.ShiftLineId))
            .Select(t => new
            {
                t.ProductId,
                ProductName = t.Product.Name,
                t.BagCount,
                t.PieceCount,
                t.PieceWeight,
                RollWeight = db.RollTestReports
                    .Where(rt => rt.RollId == t.ThermoProduction.RollId)
                    .Select(rt => (decimal?)rt.Weight)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var products = runs
            .GroupBy(r => new { r.ProductId, r.ProductName })
            .Select(g =>
            {
                var rollWeight = g.Sum(r => r.RollWeight ?? 0m);

                // One shared formula with the thermo's own screen, so the summary can
                // never quietly disagree with the row the operator read it off.
                var loss = g.Sum(r =>
                    ThermoScrap.Between(r.RollWeight ?? 0m, r.PieceCount, r.PieceWeight));

                return new ShiftProductLineDto(
                    g.Key.ProductId,
                    g.Key.ProductName,
                    g.Count(),
                    rollWeight,
                    g.Sum(r => r.BagCount),
                    g.Sum(r => r.PieceCount),
                    Math.Round(rollWeight - loss, 3),
                    Math.Round(loss, 3),
                    rollWeight == 0 ? null : Math.Round(loss / rollWeight * 100m, 2));
            })
            .OrderBy(p => p.ProductName)
            .ToList();

        var totalRollWeight = products.Sum(p => p.RollWeightUsed);
        var totalProductWeight = products.Sum(p => p.ProductWeight);

        var pallets = await db.WoodenPallets
            .Where(p => lineIds.Contains(p.ShiftLineId) && p.CancelledAt == null)
            .Select(p => p.CompletedAt)
            .ToListAsync(cancellationToken);

        var recycled = await db.RecyclerProductions
            .Where(r => lineIds.Contains(r.ShiftLineId))
            .SumAsync(r => (decimal?)r.RecycledMaterialWeight, cancellationToken) ?? 0m;

        return Result<ShiftSummaryReportDto>.Success(new ShiftSummaryReportDto(
            report.Id,
            report.ProductionDate,
            report.Shift.Name,
            report.Status.ToString(),
            supervisor,
            report.ElectricityUsed,
            made.Count,
            made.Sum(w => w ?? 0m),
            products.Sum(p => p.RollsUsed),
            totalRollWeight,
            products.Sum(p => p.BagCount),
            products.Sum(p => p.PieceCount),
            totalProductWeight,
            Math.Round(totalRollWeight - totalProductWeight, 3),
            totalRollWeight == 0
                ? null
                : Math.Round((totalRollWeight - totalProductWeight) / totalRollWeight * 100m, 2),
            pallets.Count,
            pallets.Count(c => c is not null),
            recycled,
            products));
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

    /// <summary>
    /// One issue-ticket line flattened onto its shift line, which is the only thing
    /// material is ever issued to. Named rather than anonymous so the grouping below
    /// stays readable and typed.
    /// </summary>
    private sealed record UsedLine(
        int ShiftLineId,
        int MaterialId,
        string Code,
        string Name,
        string UnitSymbol,
        decimal Issued,
        decimal Returned);
}
