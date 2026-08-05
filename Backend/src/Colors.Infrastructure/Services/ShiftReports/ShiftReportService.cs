using System.Globalization;
using Colors.Application.Common.Models;
using Colors.Application.Features.ShiftReports;
using Colors.Domain.Entities.Shifts;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Colors.Infrastructure.Services.ShiftReports;

/// <summary>
/// Shift reports. Specification section 2.
///
/// One shift per date, with the lines that ran hanging underneath. The rule that
/// matters: a closed shift accepts nothing further. That is what makes "did every
/// shift get its readings?" a question with an answer, and it is why reopening is an
/// administrator's decision with a reason attached.
/// </summary>
public class ShiftReportService(
    ColorsDbContext db,
    TimeProvider timeProvider,
    ILogger<ShiftReportService> logger) : IShiftReportService
{
    public async Task<IReadOnlyList<ShiftReportSummaryDto>> GetAllAsync(
        int? productionLineId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        var reports = await Query()
            .Where(r => productionLineId == null
                        || r.Lines.Any(l => l.ProductionLineId == productionLineId))
            .Where(r => !openOnly || r.Status == ShiftReportStatus.Open)
            .OrderByDescending(r => r.ProductionDate)
            .ThenBy(r => r.Shift.StartTime)
            .Take(200)
            .ToListAsync(cancellationToken);

        var names = await UserNamesAsync(
            reports.Where(r => r.SupervisorUserId is not null).Select(r => r.SupervisorUserId!.Value),
            cancellationToken);

        return reports
            .Select(r => new ShiftReportSummaryDto(
                r.Id,
                r.ProductionDate,
                r.ShiftId,
                r.Shift.Name,
                r.Status.ToString(),
                r.Status == ShiftReportStatus.Open,
                r.SupervisorUserId is null ? null : names.GetValueOrDefault(r.SupervisorUserId.Value),
                OrderedLines(r).Select(l => l.ProductionLine.Name).ToList(),
                r.Lines.Count,
                r.Lines.Sum(l => l.Workers.Count),
                r.ElectricityUsed,
                r.OpenedAt,
                r.ClosedAt))
            .ToList();
    }

    public async Task<Result<ShiftReportDto>> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var report = await Query().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return report is null
            ? NotFound()
            : Result<ShiftReportDto>.Success(await ToDtoAsync(report, cancellationToken));
    }

    public async Task<Result<ShiftReportDto>> OpenAsync(
        OpenShiftReportRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Shifts.AnyAsync(s => s.Id == request.ShiftId && s.IsActive, cancellationToken))
        {
            return Invalid("Choose an active shift.");
        }

        var lineIds = request.ProductionLineIds.Distinct().ToList();
        if (lineIds.Count == 0)
        {
            return Invalid("Choose at least one line that is running this shift.");
        }

        var activeLines = await db.ProductionLines
            .CountAsync(l => lineIds.Contains(l.Id) && l.IsActive, cancellationToken);

        if (activeLines != lineIds.Count)
        {
            return Invalid("Every line must be an active production line.");
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (request.ProductionDate > today.AddDays(1))
        {
            return Invalid("A shift cannot be opened more than a day ahead.");
        }

        // Opening the same shift twice would split one day's production across two
        // records, so the second attempt is refused rather than silently allowed.
        var existing = await Query().FirstOrDefaultAsync(
            r => r.ProductionDate == request.ProductionDate && r.ShiftId == request.ShiftId,
            cancellationToken);

        if (existing is not null)
        {
            return Invalid(
                $"Shift {existing.Shift.Name} on {existing.ProductionDate:dd/MM/yyyy} is already "
                + $"{existing.Status.ToString().ToLowerInvariant()}. Add a line to it instead of "
                + "opening a second shift.");
        }

        var report = new ShiftReport
        {
            ProductionDate = request.ProductionDate,
            ShiftId = request.ShiftId,
            Status = ShiftReportStatus.Open,
            SupervisorUserId = request.SupervisorUserId,
            OpenedByUserId = userId,
            OpenedAt = timeProvider.GetUtcNow(),
            Lines = lineIds.Select(lineId => new ShiftLine { ProductionLineId = lineId }).ToList(),
        };

        db.Set<ShiftReport>().Add(report);
        await db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(report.Id, cancellationToken);
    }

    public async Task<Result<ShiftReportDto>> UpdateAsync(
        int id,
        UpdateShiftReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var report = await Query().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        if (report.Status == ShiftReportStatus.Closed)
        {
            return ClosedShift();
        }

        if (request.ElectricityStartMeter is not null
            && request.ElectricityEndMeter is not null
            && request.ElectricityEndMeter < request.ElectricityStartMeter)
        {
            // A meter that rolled over or was replaced is the usual cause. It needs a
            // human decision, not a negative number quietly entering the reports.
            return Invalid(
                "The end meter is below the start meter. Check the readings — if the meter "
                + "rolled over or was replaced, record it in the shift's notes.");
        }

        report.SupervisorUserId = request.SupervisorUserId;
        report.ElectricityStartMeter = request.ElectricityStartMeter;
        report.ElectricityEndMeter = request.ElectricityEndMeter;
        report.Notes = Trimmed(request.Notes);

        await db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(id, cancellationToken);
    }

    public async Task<Result<ShiftReportDto>> AddLineAsync(
        int id,
        AddShiftLineRequest request,
        CancellationToken cancellationToken = default)
    {
        var report = await Query().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        if (report.Status == ShiftReportStatus.Closed)
        {
            return ClosedShift();
        }

        if (report.Lines.Any(l => l.ProductionLineId == request.ProductionLineId))
        {
            return Invalid("That line is already on this shift.");
        }

        if (!await db.ProductionLines.AnyAsync(
                l => l.Id == request.ProductionLineId && l.IsActive,
                cancellationToken))
        {
            return Invalid("Choose an active production line.");
        }

        report.Lines.Add(new ShiftLine { ProductionLineId = request.ProductionLineId });
        await db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(id, cancellationToken);
    }

    public async Task<Result<ShiftReportDto>> UpdateLineAsync(
        int id,
        int lineId,
        UpdateShiftLineRequest request,
        CancellationToken cancellationToken = default)
    {
        var report = await Query().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        if (report.Status == ShiftReportStatus.Closed)
        {
            return ClosedShift();
        }

        var line = report.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
        {
            return Result<ShiftReportDto>.Failure(
                ErrorCode.NotFound,
                "That line is not part of this shift.");
        }

        var start = ParseTime(request.ProductionStartTime);
        var end = ParseTime(request.ProductionEndTime);

        if (request.ProductionStartTime is not null && start is null)
        {
            return Invalid("The start time must be written as HH:mm, for example 08:00.");
        }

        if (request.ProductionEndTime is not null && end is null)
        {
            return Invalid("The end time must be written as HH:mm, for example 16:00.");
        }

        if (request.DowntimeHours is < 0)
        {
            return Invalid("Downtime cannot be negative.");
        }

        // The screen hides these boxes on a line that has no such settings; the server
        // refuses them too, so nothing can arrive by another route and sit in a column
        // that means nothing for that line.
        if (!line.ProductionLine.RecordsMachineSettings
            && (request.MachineSpeed is not null
                || request.FeedDistanceMm is not null
                || request.CycleTimeSeconds is not null))
        {
            return Invalid(
                $"{line.ProductionLine.Name} does not record machine settings. "
                + "Speed, feed distance and cycle time belong to the thermo line.");
        }

        // Downtime longer than the shift itself is a typo worth catching before it
        // reaches a report. Checked on a throwaway instance so nothing is mutated
        // until every rule has passed.
        var candidate = new ShiftLine
        {
            ProductionStartTime = start,
            ProductionEndTime = end,
            DowntimeHours = request.DowntimeHours,
        };

        if (candidate.ActualProductionHours is < 0)
        {
            return Invalid("Downtime is longer than the shift itself. Check the hours.");
        }

        var workerError = await ValidateWorkersAsync(request.Workers, cancellationToken);
        if (workerError is not null)
        {
            return Invalid(workerError);
        }

        line.ProductionStartTime = start;
        line.ProductionEndTime = end;
        line.DowntimeHours = request.DowntimeHours;
        line.MachineSpeed = request.MachineSpeed;
        line.FeedDistanceMm = request.FeedDistanceMm;
        line.CycleTimeSeconds = request.CycleTimeSeconds;

        // The request carries the whole crew, so the list is replaced rather than
        // reconciled — nothing else points at these rows.
        db.Set<ShiftWorker>().RemoveRange(line.Workers);
        line.Workers = request.Workers
            .Select(w => new ShiftWorker
            {
                UserId = w.UserId,
                RoleInShiftId = w.RoleInShiftId,
                IsTrainee = w.IsTrainee,
            })
            .ToList();

        await db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(id, cancellationToken);
    }

    public async Task<Result<ShiftReportDto>> RemoveLineAsync(
        int id,
        int lineId,
        CancellationToken cancellationToken = default)
    {
        var report = await Query().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        if (report.Status == ShiftReportStatus.Closed)
        {
            return ClosedShift();
        }

        var line = report.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
        {
            return Result<ShiftReportDto>.Failure(
                ErrorCode.NotFound,
                "That line is not part of this shift.");
        }

        if (report.Lines.Count == 1)
        {
            return Invalid(
                "A shift must have at least one line. Discard the whole shift instead.");
        }

        // Only a line nothing has been recorded against can go — one ticked by
        // mistake. Production records arrive in later phases and will be checked here
        // too; the database's restrict keys are the backstop until then.
        if (line.Workers.Count > 0)
        {
            return Invalid(
                $"{line.ProductionLine.Name} already has workers recorded on it. "
                + "Remove them first if the line did not run.");
        }

        db.Set<ShiftLine>().Remove(line);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Invalid(
                "Work has been recorded against this line, so it cannot be removed from the shift.");
        }

        return await LoadAsync(id, cancellationToken);
    }

    public async Task<Result<ShiftReportDto>> CloseAsync(
        int id,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var report = await Query().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        if (report.Status == ShiftReportStatus.Closed)
        {
            return Invalid("This shift is already closed.");
        }

        // Specification section 2 also requires every material issue ticket on every
        // line of the shift to be closed first. Tickets arrive in phase 7; this is
        // where that check belongs when they do.

        report.Status = ShiftReportStatus.Closed;
        report.ClosedByUserId = userId;
        report.ClosedAt = timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Shift {Shift} on {Date} ({Lines}) closed by user {UserId}.",
            report.Shift.Name,
            report.ProductionDate,
            string.Join(", ", OrderedLines(report).Select(l => l.ProductionLine.Name)),
            userId);

        return await LoadAsync(id, cancellationToken);
    }

    public async Task<Result<ShiftReportDto>> ReopenAsync(
        int id,
        ReopenShiftReportRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var report = await Query().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        if (report.Status == ShiftReportStatus.Open)
        {
            return Invalid("This shift is already open.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Invalid("Say why the shift is being reopened — it stays on the record.");
        }

        report.Status = ShiftReportStatus.Open;
        report.ClosedByUserId = null;
        report.ClosedAt = null;

        var reason = request.Reason.Trim();
        var stamp = timeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        report.Notes = string.IsNullOrWhiteSpace(report.Notes)
            ? $"[Reopened {stamp}] {reason}"
            : $"{report.Notes}\n[Reopened {stamp}] {reason}";

        await db.SaveChangesAsync(cancellationToken);

        // Reopening a closed shift changes figures somebody may already have read,
        // so it is worth finding in the log later.
        logger.LogWarning(
            "Shift {Shift} on {Date} REOPENED by user {UserId}: {Reason}",
            report.Shift.Name,
            report.ProductionDate,
            userId,
            reason);

        return await LoadAsync(id, cancellationToken);
    }

    public async Task<Result<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var report = await Query().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (report is null)
        {
            return Result<bool>.Failure(ErrorCode.NotFound, "This shift does not exist.");
        }

        if (report.Status == ShiftReportStatus.Closed)
        {
            return Result<bool>.Failure(
                ErrorCode.ValidationFailed,
                "A closed shift is part of the record and is kept for ever.");
        }

        // Only an empty shift can go — one opened on the wrong day.
        if (report.Lines.Any(l => l.Workers.Count > 0))
        {
            return Result<bool>.Failure(
                ErrorCode.ValidationFailed,
                "This shift already has workers recorded on it. Close it instead.");
        }

        db.Set<ShiftReport>().Remove(report);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The database's restrict foreign keys are the backstop for anything
            // recorded against the shift that this check does not know about yet.
            db.ChangeTracker.Clear();
            return Result<bool>.Failure(
                ErrorCode.ValidationFailed,
                "Work has been recorded against this shift, so it cannot be removed. Close it instead.");
        }

        return Result<bool>.Success(true);
    }

    // ---------- helpers ----------

    private IQueryable<ShiftReport> Query() =>
        db.Set<ShiftReport>()
            .Include(r => r.Shift)
            .Include(r => r.Lines).ThenInclude(l => l.ProductionLine)
            .Include(r => r.Lines).ThenInclude(l => l.Workers);

    /// <summary>Lines always read in the same order, so the screen never reshuffles.</summary>
    private static IEnumerable<ShiftLine> OrderedLines(ShiftReport report) =>
        report.Lines.OrderBy(l => l.ProductionLine.Name);

    private async Task<string?> ValidateWorkersAsync(
        IReadOnlyList<SaveShiftWorkerRequest> workers,
        CancellationToken cancellationToken)
    {
        if (workers.Select(w => w.UserId).Distinct().Count() != workers.Count)
        {
            return "Each person may be listed once on a line.";
        }

        if (workers.Count == 0)
        {
            return null;
        }

        var userIds = workers.Select(w => w.UserId).ToList();
        var active = await db.Set<ApplicationUser>()
            .CountAsync(u => userIds.Contains(u.Id) && u.IsActive, cancellationToken);

        if (active != userIds.Count)
        {
            return "Every person on the shift must be an active user.";
        }

        var roleIds = workers
            .Where(w => w.RoleInShiftId is not null)
            .Select(w => w.RoleInShiftId!.Value)
            .Distinct()
            .ToList();

        if (roleIds.Count == 0)
        {
            return null;
        }

        var knownRoles = await db.Set<ApplicationRole>()
            .CountAsync(r => roleIds.Contains(r.Id), cancellationToken);

        return knownRoles == roleIds.Count ? null : "Choose a real role for each worker.";
    }

    private static IEnumerable<int> UserIdsOf(ShiftReport report)
    {
        yield return report.OpenedByUserId;

        if (report.SupervisorUserId is not null)
        {
            yield return report.SupervisorUserId.Value;
        }

        if (report.ClosedByUserId is not null)
        {
            yield return report.ClosedByUserId.Value;
        }

        foreach (var worker in report.Lines.SelectMany(l => l.Workers))
        {
            yield return worker.UserId;
        }
    }

    /// <summary>
    /// Names for a set of user ids. Users live in Infrastructure and the domain refers
    /// to them by id alone, so names are fetched here rather than joined in the model.
    /// </summary>
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

    private async Task<Result<ShiftReportDto>> LoadAsync(int id, CancellationToken cancellationToken)
    {
        var saved = await Query().FirstAsync(r => r.Id == id, cancellationToken);
        return Result<ShiftReportDto>.Success(await ToDtoAsync(saved, cancellationToken));
    }

    private async Task<ShiftReportDto> ToDtoAsync(ShiftReport report, CancellationToken cancellationToken)
    {
        var names = await UserNamesAsync(UserIdsOf(report), cancellationToken);

        var workers = report.Lines.SelectMany(l => l.Workers).ToList();
        var workerIds = workers.Select(w => w.UserId).Distinct().ToList();

        var employeeNumbers = await db.Set<ApplicationUser>()
            .Where(u => workerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.EmployeeNumber, cancellationToken);

        var roleIds = workers
            .Where(w => w.RoleInShiftId is not null)
            .Select(w => w.RoleInShiftId!.Value)
            .Distinct()
            .ToList();

        var roleNames = await db.Set<ApplicationRole>()
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name ?? string.Empty, cancellationToken);

        return new ShiftReportDto(
            report.Id,
            report.ProductionDate,
            report.ShiftId,
            report.Shift.Name,
            report.Status.ToString(),
            report.Status == ShiftReportStatus.Open,
            report.SupervisorUserId,
            report.SupervisorUserId is null ? null : names.GetValueOrDefault(report.SupervisorUserId.Value),
            report.ElectricityStartMeter,
            report.ElectricityEndMeter,
            report.ElectricityUsed,
            report.Notes,
            names.GetValueOrDefault(report.OpenedByUserId, "—"),
            report.OpenedAt,
            report.ClosedByUserId is null ? null : names.GetValueOrDefault(report.ClosedByUserId.Value),
            report.ClosedAt,
            OrderedLines(report)
                .Select(line => new ShiftLineDto(
                    line.Id,
                    line.ProductionLineId,
                    line.ProductionLine.Name,
                    line.ProductionLine.RecordsMachineSettings,
                    Format(line.ProductionStartTime),
                    Format(line.ProductionEndTime),
                    line.DowntimeHours,
                    line.ActualProductionHours,
                    line.MachineSpeed,
                    line.FeedDistanceMm,
                    line.CycleTimeSeconds,
                    line.Workers
                        .Select(w => new ShiftWorkerDto(
                            w.UserId,
                            employeeNumbers.GetValueOrDefault(w.UserId, string.Empty),
                            names.GetValueOrDefault(w.UserId, "—"),
                            w.RoleInShiftId,
                            w.RoleInShiftId is null
                                ? null
                                : roleNames.GetValueOrDefault(w.RoleInShiftId.Value),
                            w.IsTrainee))
                        .OrderBy(w => w.IsTrainee)
                        .ThenBy(w => w.FullName)
                        .ToList()))
                .ToList());
    }

    private static string? Format(TimeOnly? time) =>
        time?.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static TimeOnly? ParseTime(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : TimeOnly.TryParseExact(value.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                ? time
                : null;

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<ShiftReportDto> NotFound() =>
        Result<ShiftReportDto>.Failure(ErrorCode.NotFound, "This shift does not exist.");

    private static Result<ShiftReportDto> Invalid(string message) =>
        Result<ShiftReportDto>.Failure(ErrorCode.ValidationFailed, message);

    private static Result<ShiftReportDto> ClosedShift() =>
        Invalid("This shift is closed. An administrator must reopen it before anything can change.");
}
