using System.Globalization;
using Colors.Application.Common.Models;
using Colors.Application.Features.Reports;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Reports;

/// <summary>
/// The extruder's report for one shift — every roll made on it, with everything recorded
/// against each (specification section 13.1).
///
/// Nothing is typed again here. The rolls are the ones logged on the shift's extruder line,
/// whatever has happened to them since; the times, crew and notes are what the line's
/// operator entered for it.
/// </summary>
public class ExtruderShiftReportService(ColorsDbContext db) : IExtruderShiftReportService
{
    public async Task<Result<ExtruderShiftReportDto>> GetAsync(
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
            return Result<ExtruderShiftReportDto>.Failure(
                ErrorCode.NotFound, "This shift does not exist.", "shift.notFound");
        }

        // The extruder, by what it does rather than what it is called.
        var line = report.Lines.FirstOrDefault(l => l.ProductionLine.MakesRolls);
        if (line is null)
        {
            return Result<ExtruderShiftReportDto>.Failure(
                ErrorCode.NotFound,
                "The extruder did not run on this shift.",
                "reports.noExtruderLine");
        }

        var rolls = await db.Rolls
            .Where(r => r.Batch.ShiftLineId == line.Id)
            .Include(r => r.Batch)
            .Include(r => r.RecipeVersion).ThenInclude(v => v.Family)
            .Include(r => r.Color)
            .Include(r => r.Product)
            .Include(r => r.TestReport)
            .AsSplitQuery()
            .OrderBy(r => r.ProducedAt)
            .ToListAsync(cancellationToken);

        // Where each went: the thermo shift that formed it. Usually a later shift — rolls
        // wait in stock — so it is looked up, not assumed.
        var rollIds = rolls.Select(r => r.Id).ToList();
        var formed = (await db.ThermoProductions
                .Where(p => rollIds.Contains(p.RollId))
                .Select(p => new
                {
                    p.RollId,
                    p.StartedAt,
                    p.ShiftLine.ShiftReport.ProductionDate,
                    ShiftName = p.ShiftLine.ShiftReport.Shift.Name,
                })
                .ToListAsync(cancellationToken))
            .GroupBy(p => p.RollId)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.StartedAt).First());

        var userIds = rolls
            .SelectMany(r => new int?[] { r.ProducedByUserId, r.MeasuredByUserId, r.TestReport?.TestedByUserId })
            .Concat(line.Workers.Select(w => (int?)w.UserId))
            .Append(line.OperatorUserId)
            .OfType<int>()
            .Distinct()
            .ToList();
        var people = await db.Set<ApplicationUser>()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FullName, u.EmployeeNumber }, cancellationToken);

        string NameOf(int id) => people.GetValueOrDefault(id)?.FullName ?? "—";
        string? NameOrNull(int? id) => id is null ? null : NameOf(id.Value);

        var rows = rolls
            .Select(r =>
            {
                var test = r.TestReport;
                var went = formed.GetValueOrDefault(r.Id);
                return new ExtruderReportRollDto(
                    r.RollNumber,
                    r.RollCode,
                    r.ProducedAt,
                    r.Batch.BatchNumber,
                    r.RecipeVersion.RecipeNumber,
                    r.RecipeVersion.Family.Name,
                    r.RecipeVersion.Family.IsAbsorbent,
                    r.Product?.Name,
                    r.Color.Name,
                    r.Status.ToString(),
                    r.Weight,
                    r.Length,
                    test?.PlateWeight,
                    test?.ThicknessRs,
                    test?.ThicknessRm,
                    test?.ThicknessLm,
                    test?.ThicknessLs,
                    test?.AverageThickness,
                    r.Product?.MinThickness,
                    r.Product?.MaxThickness,
                    test?.ThicknessInSpec,
                    test?.AbsorbencyDryWeight,
                    test?.AbsorbencyWetWeight,
                    test?.AbsorbencyPercentage,
                    test?.IsComplete(r.RecipeVersion.Family.IsAbsorbent) == true,
                    NameOf(r.ProducedByUserId),
                    NameOrNull(r.MeasuredByUserId),
                    NameOrNull(test?.TestedByUserId),
                    went?.ProductionDate,
                    went?.ShiftName,
                    r.Notes,
                    test?.Notes);
            })
            .ToList();

        var products = rows
            .GroupBy(r => r.ProductName ?? "—")
            .Select(g =>
            {
                var tested = g.Where(r => r.AverageThickness is not null).ToList();
                return new ExtruderReportProductDto(
                    g.Key,
                    g.Count(),
                    g.Sum(r => r.Weight ?? 0m),
                    g.Sum(r => r.Length ?? 0m),
                    tested.Count == 0 ? null : Math.Round(tested.Average(r => r.AverageThickness!.Value), 3),
                    g.Count(r => r.ThicknessInSpec == false));
            })
            .OrderBy(p => p.ProductName)
            .ToList();

        var operatorPerson = line.OperatorUserId is null
            ? null
            : people.GetValueOrDefault(line.OperatorUserId.Value);

        return Result<ExtruderShiftReportDto>.Success(new ExtruderShiftReportDto(
            report.Id,
            report.ProductionDate,
            report.Shift.Name,
            report.Status.ToString(),
            line.ProductionLine.Name,
            operatorPerson?.FullName,
            operatorPerson?.EmployeeNumber,
            Format(line.ProductionStartTime),
            Format(line.ProductionEndTime),
            line.DowntimeHours,
            line.ActualProductionHours,
            line.Workers.Where(w => !w.IsTrainee).Select(w => NameOf(w.UserId)).Order().ToList(),
            line.Workers.Where(w => w.IsTrainee).Select(w => NameOf(w.UserId)).Order().ToList(),
            line.Notes,
            rolls.Select(r => r.Batch.BatchNumber).Distinct().Order().ToList(),
            rows,
            rows.Count,
            rows.Count(r => r.Weight is not null),
            rows.Count(r => r.TestComplete),
            rolls.Count(r => r.Status is not (RollStatus.NeedsTest or RollStatus.Scrapped)),
            rows.Count(r => r.ThicknessInSpec == false),
            rows.Sum(r => r.Weight ?? 0m),
            rows.Sum(r => r.Length ?? 0m),
            products));
    }

    private static string? Format(TimeOnly? time) =>
        time?.ToString("HH:mm", CultureInfo.InvariantCulture);
}
