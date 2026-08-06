using Colors.Application.Common.Models;
using Colors.Application.Features.Reports;
using Colors.Domain.Common;
using Colors.Domain.Enums;
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
                ErrorCode.NotFound, "This shift does not exist.");
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
                ErrorCode.NotFound, "This shift does not exist.");
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
}
