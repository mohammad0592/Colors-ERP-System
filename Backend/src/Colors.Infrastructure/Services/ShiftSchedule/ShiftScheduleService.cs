using System.Globalization;
using Colors.Application.Common.Models;
using Colors.Application.Features.ShiftSchedule;
using Colors.Domain.Common;
using Colors.Domain.Entities.Inventory;
using Colors.Domain.Entities.Production;
using Colors.Domain.Entities.Shifts;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.ShiftReports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Colors.Infrastructure.Services.ShiftSchedule;

/// <summary>
/// Tomorrow's shifts, planned the day before and opened and closed by the system at their
/// times (specification section 19.30).
///
/// The plan is made a day ahead so nobody is deciding at 07:55 who runs the extruder.
/// From the plan, the shift opens itself at its start with its supervisor, lines and
/// operators already set, and closes itself at its end. The supervisor can still open a
/// shift by hand when one was not planned — the schedule is the normal way, not the only
/// one.
/// </summary>
public class ShiftScheduleService(
    ColorsDbContext db,
    FactoryClock clock,
    TimeProvider timeProvider,
    ILogger<ShiftScheduleService> logger) : IShiftScheduleService
{
    public async Task<ShiftScheduleDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var today = clock.Today;
        var tomorrow = today.AddDays(1);

        var plans = await Query()
            .Where(s => s.ProductionDate >= today && s.ProductionDate <= tomorrow)
            .ToListAsync(cancellationToken);

        var lines = await db.ProductionLines
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);

        return new ShiftScheduleDto(
            today,
            tomorrow,
            await ToDtosAsync(plans, cancellationToken),
            lines
                .Select(l => new ScheduleLineOptionDto(
                    l.Id,
                    l.Name,
                    l.MakesRolls,
                    ShiftReportService.OperatorRoleFor(l)))
                .ToList());
    }

    public async Task<Result<ScheduledShiftDto>> ScheduleAsync(
        ScheduleShiftRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        // Tomorrow and no further. Planned further ahead, a plan outlives the week it was
        // made for — the man named is on leave by then, and nobody looks at it again
        // until it opens with the wrong crew.
        var tomorrow = clock.Today.AddDays(1);
        if (request.ProductionDate != tomorrow)
        {
            return Invalid(
                $"Shifts are planned for tomorrow only, {tomorrow:dd/MM/yyyy}.",
                "shiftSchedule.tomorrowOnly",
                tomorrow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        }

        var shift = await db.Shifts.FirstOrDefaultAsync(
            s => s.Id == request.ShiftId && s.IsActive,
            cancellationToken);

        if (shift is null)
        {
            return Invalid("Choose an active shift.", "shift.chooseActiveShift");
        }

        if (await db.ScheduledShifts.AnyAsync(
                s => s.ProductionDate == request.ProductionDate && s.ShiftId == request.ShiftId,
                cancellationToken))
        {
            return Invalid(
                $"Shift {shift.Name} is already planned for that day. Change that plan instead.",
                "shiftSchedule.alreadyPlanned",
                shift.Name);
        }

        // Opened by hand ahead of time — which the Shifts screen allows a day early. The
        // plan would only find it already there.
        if (await db.ShiftReports.AnyAsync(
                r => r.ProductionDate == request.ProductionDate && r.ShiftId == request.ShiftId,
                cancellationToken))
        {
            return Invalid(
                $"Shift {shift.Name} on that day has already been opened.",
                "shiftSchedule.alreadyOpened",
                shift.Name);
        }

        var refusal = await ValidateAsync(
            request.SupervisorUserId,
            request.Lines,
            request.ExtruderStartingShift,
            cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var plan = new ScheduledShift
        {
            ProductionDate = request.ProductionDate,
            ShiftId = request.ShiftId,
            SupervisorUserId = request.SupervisorUserId,
            ExtruderStartingShift = request.ExtruderStartingShift,
            ScheduledByUserId = userId,
            ScheduledAt = timeProvider.GetUtcNow(),
            Lines = Distinct(request.Lines)
                .Select(l => new ScheduledShiftLine
                {
                    ProductionLineId = l.ProductionLineId,
                    OperatorUserId = l.OperatorUserId,
                })
                .ToList(),
        };

        db.ScheduledShifts.Add(plan);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two supervisors planning the same shift in the same moment. The unique
            // index lets one through; the other hears why.
            db.ChangeTracker.Clear();
            return Invalid(
                $"Shift {shift.Name} is already planned for that day. Change that plan instead.",
                "shiftSchedule.alreadyPlanned",
                shift.Name);
        }

        return Result<ScheduledShiftDto>.Success(await LoadAsync(plan.Id, cancellationToken));
    }

    public async Task<Result<ScheduledShiftDto>> UpdateAsync(
        int id,
        UpdateScheduledShiftRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var plan = await Query().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (plan is null)
        {
            return NotFound();
        }

        var locked = Unchangeable(plan);
        if (locked is not null)
        {
            return Invalid(locked.Value.Message, locked.Value.Code, plan.Shift.Name);
        }

        var refusal = await ValidateAsync(
            request.SupervisorUserId,
            request.Lines,
            request.ExtruderStartingShift,
            cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        plan.SupervisorUserId = request.SupervisorUserId;
        plan.ExtruderStartingShift = request.ExtruderStartingShift;
        plan.ChangedByUserId = userId;
        plan.ChangedAt = timeProvider.GetUtcNow();

        // Changed in place rather than cleared and added back: a line kept on the plan is
        // the same row with a new operator, and the unique index never sees it twice.
        var wanted = Distinct(request.Lines).ToDictionary(l => l.ProductionLineId);

        foreach (var line in plan.Lines.ToList())
        {
            if (wanted.Remove(line.ProductionLineId, out var keep))
            {
                line.OperatorUserId = keep.OperatorUserId;
            }
            else
            {
                plan.Lines.Remove(line);
                db.ScheduledShiftLines.Remove(line);
            }
        }

        foreach (var added in wanted.Values)
        {
            plan.Lines.Add(new ScheduledShiftLine
            {
                ProductionLineId = added.ProductionLineId,
                OperatorUserId = added.OperatorUserId,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result<ScheduledShiftDto>.Success(await LoadAsync(plan.Id, cancellationToken));
    }

    public async Task<Result<bool>> CancelAsync(int id, CancellationToken cancellationToken = default)
    {
        var plan = await Query().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (plan is null)
        {
            return Result<bool>.Failure(
                ErrorCode.NotFound,
                "This planned shift does not exist.",
                "shiftSchedule.notFound");
        }

        var locked = Unchangeable(plan);
        if (locked is not null)
        {
            return Result<bool>.Failure(
                ErrorCode.ValidationFailed,
                locked.Value.Message,
                locked.Value.Code,
                plan.Shift.Name);
        }

        db.ScheduledShifts.Remove(plan);
        await db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }

    public async Task<ScheduleRunResult> RunDueAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.Now;
        var today = clock.Today;
        var opened = new List<int>();
        var closed = new List<int>();
        var notOpened = new List<int>();

        // Plans whose day has come. Only today's and earlier can be due: every shift
        // starts on its own date.
        var waiting = await db.ScheduledShifts
            .Include(s => s.Shift)
            .Include(s => s.Lines).ThenInclude(l => l.ProductionLine)
            .Where(s => s.ShiftReportId == null
                        && s.NotOpenedReason == null
                        && s.ProductionDate <= today)
            .ToListAsync(cancellationToken);

        foreach (var plan in waiting.OrderBy(p => ShiftHours.StartOf(p.ProductionDate, p.Shift)))
        {
            var start = ShiftHours.StartOf(plan.ProductionDate, plan.Shift);
            var end = ShiftHours.EndOf(plan.ProductionDate, plan.Shift);

            if (start > now)
            {
                continue;
            }

            // The whole shift went by with the system off. Opening it now would put
            // tonight's work on a shift that ended hours ago.
            if (end <= now)
            {
                plan.NotOpenedReason =
                    $"The system was not running between {start:HH:mm} and {end:HH:mm}, so it never opened.";
                await db.SaveChangesAsync(cancellationToken);
                notOpened.Add(plan.Id);

                logger.LogWarning(
                    "Planned shift {Shift} on {Date} was never opened: the system was not running during it.",
                    plan.Shift.Name,
                    plan.ProductionDate);
                continue;
            }

            var reportId = await OpenAsync(plan, now, cancellationToken);
            if (reportId is not null)
            {
                opened.Add(reportId.Value);
            }
        }

        // Scheduled shifts whose time is up and that no next shift has taken over.
        var running = await db.ShiftReports
            .Include(r => r.Shift)
            .Include(r => r.Lines)
            .Where(r => r.Status == ShiftReportStatus.Open
                        && db.ScheduledShifts.Any(s => s.ShiftReportId == r.Id))
            .ToListAsync(cancellationToken);

        foreach (var report in running)
        {
            if (ShiftHours.EndOf(report.ProductionDate, report.Shift) > now)
            {
                continue;
            }

            if (await CloseIfClearAsync(report, now, cancellationToken))
            {
                closed.Add(report.Id);
            }
        }

        return new ScheduleRunResult(opened, closed, notOpened);
    }

    // ---------- opening and closing ----------

    /// <summary>
    /// Opens the shift a plan describes. Whatever shift is still running is closed first —
    /// the factory works one at a time — and anything still open on it carries over to
    /// this one: a roll in the thermo, a ticket not yet weighed back. That is the factory's
    /// choice: the shift ends on time, and the work goes on under the men who are now
    /// there to finish it (section 19.30).
    /// </summary>
    private async Task<int?> OpenAsync(ScheduledShift plan, DateTime now, CancellationToken cancellationToken)
    {
        // Opened by hand already, before its time came. The plan simply becomes it.
        var existing = await db.ShiftReports.FirstOrDefaultAsync(
            r => r.ProductionDate == plan.ProductionDate && r.ShiftId == plan.ShiftId,
            cancellationToken);

        if (existing is not null)
        {
            plan.ShiftReportId = existing.Id;
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var at = timeProvider.GetUtcNow();
            var clockTime = now.ToString("HH:mm", CultureInfo.InvariantCulture);

            var previous = await db.ShiftReports
                .Include(r => r.Shift)
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.Status == ShiftReportStatus.Open, cancellationToken);

            var runs = new List<ThermoProduction>();
            var tickets = new List<MaterialIssueTicket>();
            var lineOf = new Dictionary<int, int>();

            if (previous is not null)
            {
                lineOf = previous.Lines.ToDictionary(l => l.Id, l => l.ProductionLineId);
                var lineIds = lineOf.Keys.ToList();

                runs = await db.ThermoProductions
                    .Include(p => p.Roll)
                    .Where(p => lineIds.Contains(p.ShiftLineId) && p.FinishedAt == null)
                    .ToListAsync(cancellationToken);

                tickets = await db.MaterialIssueTickets
                    .Where(t => lineIds.Contains(t.ShiftLineId) && t.Status == IssueTicketStatus.Open)
                    .ToListAsync(cancellationToken);

                await FinishBatchesAsync(lineIds, at, cancellationToken);

                previous.Status = ShiftReportStatus.Closed;
                previous.ClosedAt = at;
                previous.ClosedByUserId = null;
                previous.Notes = WithNote(
                    previous.Notes,
                    $"Closed by the schedule at {clockTime}, when shift {plan.Shift.Name} started."
                    + CarriedNote(runs, tickets, $" Carried over to shift {plan.Shift.Name}: "));

                // Saved before the new shift is added: the database allows one open shift,
                // and this one has to be closed before the next can be open.
                await db.SaveChangesAsync(cancellationToken);
            }

            var extruder = plan.Lines.FirstOrDefault(l => l.ProductionLine.MakesRolls);

            var report = new ShiftReport
            {
                ProductionDate = plan.ProductionDate,
                ShiftId = plan.ShiftId,
                Status = ShiftReportStatus.Open,
                SupervisorUserId = plan.SupervisorUserId,
                // The man who planned it. The shift opened because of his plan, and the
                // record has to name a person rather than "the system".
                OpenedByUserId = plan.ScheduledByUserId,
                OpenedAt = at,
                Notes = $"Opened by the schedule at {clockTime}."
                        + CarriedNote(
                            runs,
                            tickets,
                            previous is null ? "" : $" Carried over from shift {previous.Shift.Name}: "),
                Lines = plan.Lines
                    .Select(l => new ShiftLine
                    {
                        ProductionLineId = l.ProductionLineId,
                        OperatorUserId = l.OperatorUserId,
                        IsStartingShift = plan.ExtruderStartingShift && l.ProductionLineId == extruder?.ProductionLineId,
                    })
                    .ToList(),
            };

            // A roll in the thermo means the thermo is running, whatever the plan said.
            // The work needs a line on this shift to land on.
            var needed = runs.Select(r => lineOf[r.ShiftLineId])
                .Concat(tickets.Select(t => lineOf[t.ShiftLineId]))
                .Distinct()
                .Where(lineId => report.Lines.All(l => l.ProductionLineId != lineId));

            foreach (var lineId in needed)
            {
                report.Lines.Add(new ShiftLine { ProductionLineId = lineId });
            }

            db.ShiftReports.Add(report);

            // The extruder's starting shift: the roll numbers start again at 01 (section
            // 19.16), saved with the shift so the two cannot come apart.
            if (plan.ExtruderStartingShift && extruder is not null)
            {
                var counter = await db.RollNumberCounters.FirstOrDefaultAsync(c => c.Id == 1, cancellationToken);
                if (counter is null)
                {
                    counter = new RollNumberCounter { Id = 1 };
                    db.RollNumberCounters.Add(counter);
                }

                counter.NextNumber = 1;
                counter.LastResetAt = at;
                counter.LastResetByUserId = plan.ScheduledByUserId;
            }

            await db.SaveChangesAsync(cancellationToken);

            foreach (var run in runs)
            {
                run.ShiftLineId = LineFor(report, lineOf[run.ShiftLineId]);
            }

            foreach (var ticket in tickets)
            {
                ticket.ShiftLineId = LineFor(report, lineOf[ticket.ShiftLineId]);
            }

            plan.ShiftReportId = report.Id;

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Shift {Shift} on {Date} opened by the schedule{Previous}.",
                plan.Shift.Name,
                plan.ProductionDate,
                previous is null ? "" : $", closing shift {previous.Shift.Name} on {previous.ProductionDate:dd/MM/yyyy}");

            return report.Id;
        }
        catch (DbUpdateException exception)
        {
            // Nothing half-done is kept: the old shift stays open and the plan waits. The
            // next check tries again, so a passing fault costs half a minute.
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();

            logger.LogError(
                exception,
                "Planned shift {Shift} on {Date} could not be opened. It will be tried again.",
                plan.Shift.Name,
                plan.ProductionDate);

            return null;
        }
    }

    /// <summary>
    /// Closes a scheduled shift at its end, when nothing is left open on it. With a roll in
    /// the thermo or a ticket still out and no next shift to carry them, it stays open —
    /// closing would leave that work on a closed shift, where it could never be finished.
    /// It closes on a later check, once they are done, or when the next shift opens and
    /// takes them over.
    /// </summary>
    private async Task<bool> CloseIfClearAsync(ShiftReport report, DateTime now, CancellationToken cancellationToken)
    {
        var lineIds = report.Lines.Select(l => l.Id).ToList();

        var stillOpen =
            await db.MaterialIssueTickets.AnyAsync(
                t => lineIds.Contains(t.ShiftLineId) && t.Status == IssueTicketStatus.Open,
                cancellationToken)
            || await db.ThermoProductions.AnyAsync(
                p => lineIds.Contains(p.ShiftLineId) && p.FinishedAt == null,
                cancellationToken);

        if (stillOpen)
        {
            return false;
        }

        var at = timeProvider.GetUtcNow();

        await FinishBatchesAsync(lineIds, at, cancellationToken);

        report.Status = ShiftReportStatus.Closed;
        report.ClosedAt = at;
        report.ClosedByUserId = null;
        report.Notes = WithNote(
            report.Notes,
            $"Closed by the schedule at {now.ToString("HH:mm", CultureInfo.InvariantCulture)}.");

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Shift {Shift} on {Date} closed by the schedule.",
            report.Shift.Name,
            report.ProductionDate);

        return true;
    }

    /// <summary>
    /// The mix ends with the shift, as it does when a shift is closed by hand: all material
    /// goes back to the store at shift end (specification section 8).
    /// </summary>
    private async Task FinishBatchesAsync(List<int> lineIds, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var batches = await db.Batches
            .Where(b => lineIds.Contains(b.ShiftLineId) && b.FinishedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var batch in batches)
        {
            batch.FinishedAt = at;
        }
    }

    private static int LineFor(ShiftReport report, int productionLineId) =>
        report.Lines.First(l => l.ProductionLineId == productionLineId).Id;

    /// <summary>"roll 01…-BLUE in the thermo; ticket 12" — what moved, for both shifts' notes.</summary>
    private static string CarriedNote(
        IReadOnlyCollection<ThermoProduction> runs,
        IReadOnlyCollection<MaterialIssueTicket> tickets,
        string lead)
    {
        if (runs.Count == 0 && tickets.Count == 0)
        {
            return "";
        }

        var parts = new List<string>();

        if (runs.Count > 0)
        {
            parts.Add(
                $"{(runs.Count == 1 ? "roll" : "rolls")} "
                + $"{string.Join(", ", runs.Select(r => r.Roll.RollCode).Order())} in the thermo");
        }

        if (tickets.Count > 0)
        {
            parts.Add(
                $"{(tickets.Count == 1 ? "ticket" : "tickets")} "
                + string.Join(", ", tickets.Select(t => t.TicketNumber).Order()));
        }

        return lead + string.Join("; ", parts) + ".";
    }

    /// <summary>
    /// The schedule's own line added to a shift's notes. The supervisor's words come first
    /// and are never cut: if both will not fit, the schedule's line is the one left out —
    /// the log still has it.
    /// </summary>
    private static string? WithNote(string? notes, string note)
    {
        const int limit = 1000;

        if (string.IsNullOrWhiteSpace(notes))
        {
            return note.Length <= limit ? note : note[..limit];
        }

        var combined = $"{notes}\n{note}";
        return combined.Length <= limit ? combined : notes;
    }

    // ---------- rules ----------

    /// <summary>
    /// A plan can be changed until its shift starts. After that it is a shift, and a shift
    /// is changed on the Shifts screen, where every change is the shift's own.
    /// </summary>
    private (string Message, string Code)? Unchangeable(ScheduledShift plan)
    {
        if (plan.ShiftReportId is not null)
        {
            return (
                $"Shift {plan.Shift.Name} has already opened. Change it on the Shifts screen.",
                "shiftSchedule.alreadyOpenedChange");
        }

        if (plan.NotOpenedReason is not null || clock.Now >= ShiftHours.StartOf(plan.ProductionDate, plan.Shift))
        {
            return (
                $"Shift {plan.Shift.Name} has already started, so its plan can no longer be changed.",
                "shiftSchedule.alreadyStarted");
        }

        return null;
    }

    private async Task<Result<ScheduledShiftDto>?> ValidateAsync(
        int? supervisorUserId,
        IReadOnlyList<ScheduleShiftLineRequest> requested,
        bool extruderStartingShift,
        CancellationToken cancellationToken)
    {
        var lines = Distinct(requested).ToList();
        if (lines.Count == 0)
        {
            return Invalid("Choose at least one line that will run this shift.", "shiftSchedule.chooseALine");
        }

        var lineIds = lines.Select(l => l.ProductionLineId).ToList();
        var chosen = await db.ProductionLines
            .Where(l => lineIds.Contains(l.Id) && l.IsActive)
            .ToListAsync(cancellationToken);

        if (chosen.Count != lineIds.Count)
        {
            return Invalid("Every line must be an active production line.", "shift.activeLine");
        }

        if (extruderStartingShift && !chosen.Any(l => l.MakesRolls))
        {
            return Invalid(
                "A starting shift starts the extruder's roll numbers again, so the extruder must be one of the lines.",
                "shift.startingNeedsExtruder");
        }

        if (supervisorUserId is not null
            && !await db.Set<ApplicationUser>().AnyAsync(
                u => u.Id == supervisorUserId && u.IsActive,
                cancellationToken))
        {
            return Invalid("Choose an active person as the supervisor.", "shiftSchedule.chooseActiveSupervisor");
        }

        // The same rule as naming an operator on an open shift: the extruder's operator
        // must hold the extruder operator's job, read off the line's flags (section 2).
        foreach (var line in lines.Where(l => l.OperatorUserId is not null))
        {
            var productionLine = chosen.First(l => l.Id == line.ProductionLineId);

            var person = await db.Set<ApplicationUser>()
                .Where(u => u.Id == line.OperatorUserId && u.IsActive)
                .Select(u => new { u.Id, u.FullName })
                .FirstOrDefaultAsync(cancellationToken);

            if (person is null)
            {
                return Invalid("Choose an active person as the operator.", "shift.chooseActiveOperator");
            }

            var role = ShiftReportService.OperatorRoleFor(productionLine);
            if (role is null)
            {
                continue;
            }

            var holdsIt = await db.UserRoles
                .Join(db.Set<ApplicationRole>(), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                .AnyAsync(x => x.UserId == person.Id && x.Name == role, cancellationToken);

            if (!holdsIt)
            {
                return Invalid(
                    $"{person.FullName} does not hold the job that runs {productionLine.Name}. "
                    + "Give it to them in Users first.",
                    "shift.operatorLacksRole",
                    person.FullName,
                    productionLine.Name);
            }
        }

        return null;
    }

    /// <summary>One entry a line. A line sent twice keeps the last operator given for it.</summary>
    private static IEnumerable<ScheduleShiftLineRequest> Distinct(IReadOnlyList<ScheduleShiftLineRequest> lines) =>
        lines.GroupBy(l => l.ProductionLineId).Select(g => g.Last());

    // ---------- reading ----------

    private IQueryable<ScheduledShift> Query() =>
        db.ScheduledShifts
            .Include(s => s.Shift)
            .Include(s => s.ShiftReport)
            .Include(s => s.Lines).ThenInclude(l => l.ProductionLine);

    private async Task<ScheduledShiftDto> LoadAsync(int id, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var plan = await Query().FirstAsync(s => s.Id == id, cancellationToken);
        return (await ToDtosAsync([plan], cancellationToken))[0];
    }

    private async Task<List<ScheduledShiftDto>> ToDtosAsync(
        IReadOnlyList<ScheduledShift> plans,
        CancellationToken cancellationToken)
    {
        var userIds = plans
            .SelectMany(p => new[] { p.SupervisorUserId, p.ScheduledByUserId, p.ChangedByUserId }
                .Concat(p.Lines.Select(l => l.OperatorUserId)))
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var names = await db.Set<ApplicationUser>()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        string? NameOf(int? userId) => userId is null ? null : names.GetValueOrDefault(userId.Value);

        var now = clock.Now;

        return plans
            .OrderBy(p => p.ProductionDate)
            .ThenBy(p => p.Shift.StartTime)
            .Select(p =>
            {
                var start = ShiftHours.StartOf(p.ProductionDate, p.Shift);
                var end = ShiftHours.EndOf(p.ProductionDate, p.Shift);

                var status = p.NotOpenedReason is not null ? "NotOpened"
                    : p.ShiftReport is null ? "Planned"
                    : p.ShiftReport.Status != ShiftReportStatus.Open ? "Finished"
                    : now >= end ? "Overrunning"
                    : "Running";

                return new ScheduledShiftDto(
                    p.Id,
                    p.ProductionDate,
                    p.ShiftId,
                    p.Shift.Name,
                    p.Shift.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                    p.Shift.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                    p.SupervisorUserId,
                    NameOf(p.SupervisorUserId),
                    p.ExtruderStartingShift,
                    p.Lines
                        .OrderBy(l => l.ProductionLine.Name)
                        .Select(l => new ScheduledShiftLineDto(
                            l.ProductionLineId,
                            l.ProductionLine.Name,
                            ShiftReportService.OperatorRoleFor(l.ProductionLine),
                            l.OperatorUserId,
                            NameOf(l.OperatorUserId)))
                        .ToList(),
                    status,
                    status == "Planned" && now < start,
                    p.ShiftReportId,
                    p.NotOpenedReason,
                    NameOf(p.ScheduledByUserId) ?? "",
                    p.ScheduledAt,
                    NameOf(p.ChangedByUserId),
                    p.ChangedAt);
            })
            .ToList();
    }

    private static Result<ScheduledShiftDto> NotFound() =>
        Result<ScheduledShiftDto>.Failure(
            ErrorCode.NotFound,
            "This planned shift does not exist.",
            "shiftSchedule.notFound");

    private static Result<ScheduledShiftDto> Invalid(string message, string code, params string[] args) =>
        Result<ScheduledShiftDto>.Failure(ErrorCode.ValidationFailed, message, code, args);
}
