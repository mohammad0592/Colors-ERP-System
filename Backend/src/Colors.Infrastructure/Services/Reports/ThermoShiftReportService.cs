using System.Globalization;
using Colors.Application.Common.Models;
using Colors.Application.Features.Reports;
using Colors.Domain.Common;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Reports;

/// <summary>
/// The thermoforming line's report for one shift — the paper form the factory filled in
/// by hand, read from what the system already holds (specification section 13).
///
/// Nothing is typed again here. The rolls are the thermo runs of the shift's forming
/// line, the pallets those built on it, the packaging what was recorded at its end, and
/// the times, crew and notes what the line's operator entered for it.
/// </summary>
public class ThermoShiftReportService(ColorsDbContext db) : IThermoShiftReportService
{
    public async Task<Result<ThermoShiftReportDto>> GetAsync(
        int shiftReportId,
        CancellationToken cancellationToken = default)
    {
        var report = await db.ShiftReports
            .Include(r => r.Shift)
            .Include(r => r.Lines).ThenInclude(l => l.ProductionLine)
            .Include(r => r.Lines).ThenInclude(l => l.Workers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == shiftReportId, cancellationToken);

        if (report is null)
        {
            return Result<ThermoShiftReportDto>.Failure(
                ErrorCode.NotFound, "This shift does not exist.", "shift.notFound");
        }

        // The forming line, by what it does rather than what it is called.
        var line = report.Lines.FirstOrDefault(l => l.ProductionLine.FormsBags);
        if (line is null)
        {
            return Result<ThermoShiftReportDto>.Failure(
                ErrorCode.NotFound,
                "The thermoforming line did not run on this shift.",
                "reports.noThermoLine");
        }

        var runs = await db.ThermoProductions
            .Where(p => p.ShiftLineId == line.Id)
            .Include(p => p.Roll).ThenInclude(r => r.Color)
            .Include(p => p.Roll).ThenInclude(r => r.TestReport)
            .Include(p => p.Roll).ThenInclude(r => r.Batch).ThenInclude(b => b.ShiftLine)
                .ThenInclude(l => l.ShiftReport).ThenInclude(s => s.Shift)
            .Include(p => p.Roll).ThenInclude(r => r.RecipeVersion).ThenInclude(v => v.Family)
            .Include(p => p.Roll).ThenInclude(r => r.RecipeVersion).ThenInclude(v => v.Ingredients)
                .ThenInclude(i => i.Material)
            .Include(p => p.TestReport).ThenInclude(t => t!.Product)
            .Include(p => p.Roll).ThenInclude(r => r.Product)
            .AsSplitQuery()
            .OrderBy(p => p.StartedAt)
            .ToListAsync(cancellationToken);

        var rolls = runs.Select(ToRollRow).ToList();
        var counted = runs.Where(p => p.TestReport is not null && p.Roll.Weight is not null).ToList();

        // One line per product, as the form's yield block has. Only counted rolls: a roll
        // still in the machine has a weight but no plates yet, and counting it would read
        // as the whole roll lost.
        var products = counted
            .GroupBy(p => p.TestReport!.ProductId)
            .Select(g =>
            {
                var product = g.First().TestReport!.Product;
                var rollWeight = g.Sum(p => p.Roll.Weight!.Value);
                var pieces = g.Sum(p => p.TestReport!.PieceCount);
                var productWeight = Math.Round(
                    g.Sum(p => p.TestReport!.PieceCount * p.TestReport.PieceWeight / 1000m), 3);
                var scrap = Math.Round(rollWeight - productWeight, 3);
                var codes = g
                    .Select(p => ProductCode.For(product.IsAbsorbent, product.PiecesPerBag, p.Roll.Color.Code))
                    .Distinct()
                    .Order();

                return new ThermoReportProductDto(
                    product.Name,
                    string.Join(" / ", codes),
                    rollWeight,
                    pieces,
                    productWeight,
                    scrap,
                    rollWeight == 0 ? null : Math.Round(scrap / rollWeight * 100m, 1));
            })
            .OrderBy(p => p.ProductName)
            .ToList();

        var pallets = await PalletsAsync(line.Id, cancellationToken);
        var (recorded, packaging) = await PackagingAsync(line.Id, cancellationToken);

        var userIds = line.Workers.Select(w => w.UserId)
            .Concat(line.OperatorUserId is null ? [] : [line.OperatorUserId.Value])
            .Distinct()
            .ToList();
        var people = await db.Set<ApplicationUser>()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FullName, u.EmployeeNumber }, cancellationToken);

        var operatorPerson = line.OperatorUserId is null
            ? null
            : people.GetValueOrDefault(line.OperatorUserId.Value);

        string NameOf(int id) => people.GetValueOrDefault(id)?.FullName ?? "—";

        return Result<ThermoShiftReportDto>.Success(new ThermoShiftReportDto(
            report.Id,
            report.ProductionDate,
            report.Shift.Name,
            report.Status.ToString(),
            line.ProductionLine.Name,
            operatorPerson?.FullName,
            operatorPerson?.EmployeeNumber,
            report.ElectricityStartMeter,
            report.ElectricityEndMeter,
            report.ElectricityUsed,
            line.CycleTimeSeconds,
            line.FeedDistanceMm,
            line.MachineSpeed,
            Format(line.ProductionStartTime),
            Format(line.ProductionEndTime),
            line.DowntimeHours,
            line.ActualProductionHours,
            line.Workers.Where(w => !w.IsTrainee).Select(w => NameOf(w.UserId)).Order().ToList(),
            line.Workers.Where(w => w.IsTrainee).Select(w => NameOf(w.UserId)).Order().ToList(),
            line.Notes,
            rolls,
            // Every roll that went in, as the form's own total adds them; the yield block
            // below takes only the counted ones.
            runs.Sum(p => p.Roll.Weight ?? 0m),
            counted.Sum(p => p.TestReport!.BagCount),
            counted.Sum(p => p.TestReport!.PieceCount),
            products,
            pallets,
            recorded,
            packaging));
    }

    private static ThermoReportRollDto ToRollRow(Domain.Entities.Production.ThermoProduction run)
    {
        var roll = run.Roll;
        var count = run.TestReport;
        var product = count?.Product ?? roll.Product;

        // The recycled share of the mix, read off the material's flag rather than its
        // name. Most recipes have none, and then the column says so.
        var recycled = roll.RecipeVersion.Ingredients
            .Where(i => i.Material.IsRecycledOutput)
            .Sum(i => (decimal?)i.TargetPercentage);

        return new ThermoReportRollDto(
            roll.RollCode,
            roll.Color.Name,
            roll.Weight,
            roll.Length,
            roll.TestReport?.AverageThickness,
            run.TotalTimeMinutes,
            product?.Name,
            product?.PiecesPerBag,
            count?.BagCount,
            count?.PieceCount,
            count?.PieceWeight,
            count?.BagWeight,
            roll.RecipeVersion.Family.IsAbsorbent,
            count is null || !roll.RecipeVersion.Family.IsAbsorbent ? null : count.AbsorbentPercentage,
            recycled is null or 0 ? null : recycled,
            roll.Batch.ShiftLine.ShiftReport.Shift.Name,
            ThermoScrap.For(run));
    }

    /// <summary>The pallets built on the line this shift, cancelled ones left out.</summary>
    private async Task<IReadOnlyList<ThermoReportPalletDto>> PalletsAsync(
        int shiftLineId,
        CancellationToken cancellationToken)
    {
        var pallets = await db.WoodenPallets
            .Where(p => p.ShiftLineId == shiftLineId && p.CancelledAt == null)
            .Include(p => p.Product)
            .Include(p => p.Color)
            .Include(p => p.Assignments).ThenInclude(a => a.ProducedBag)
                .ThenInclude(b => b.ThermoProduction).ThenInclude(t => t.Roll)
            .AsSplitQuery()
            .OrderBy(p => p.PalletNumber)
            .ToListAsync(cancellationToken);

        return pallets
            .Select(p =>
            {
                var bags = p.Assignments.Where(a => a.IsActive).ToList();
                var perBag = p.Product?.PiecesPerBag;

                return new ThermoReportPalletDto(
                    p.PalletNumber,
                    p.Status.ToString(),
                    bags.Count,
                    perBag,
                    perBag is null ? null : bags.Sum(a => a.ProducedBag.PieceCount),
                    p.Product?.Name,
                    p.Color?.Name,
                    bags.Select(a => a.ProducedBag.ThermoProduction.Roll.RollCode)
                        .Distinct()
                        .Order()
                        .ToList());
            })
            .ToList();
    }

    /// <summary>What was recorded as used at the end of the shift, largest first.</summary>
    private async Task<(bool Recorded, IReadOnlyList<ThermoReportPackagingDto> Lines)> PackagingAsync(
        int shiftLineId,
        CancellationToken cancellationToken)
    {
        var consumption = await db.PackagingConsumptions
            .Include(c => c.Lines).ThenInclude(l => l.Material).ThenInclude(m => m.BaseUnit)
            .FirstOrDefaultAsync(c => c.ShiftLineId == shiftLineId, cancellationToken);

        if (consumption is null)
        {
            return (false, []);
        }

        return (true, consumption.Lines
            .Where(l => l.Quantity > 0)
            .OrderBy(l => l.Material.Name)
            .Select(l => new ThermoReportPackagingDto(
                l.Material.Name,
                l.Material.BaseUnit.Symbol,
                l.Quantity,
                l.Weight))
            .ToList());
    }

    private static string? Format(TimeOnly? time) =>
        time?.ToString("HH:mm", CultureInfo.InvariantCulture);
}
