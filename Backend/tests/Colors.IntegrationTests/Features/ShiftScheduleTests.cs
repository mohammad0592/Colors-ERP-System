using Colors.Application.Features.ShiftSchedule;
using Colors.Application.Features.Thermo;
using Colors.Domain.Constants;
using Colors.Domain.Entities.Inventory;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.ShiftReports;
using Colors.Infrastructure.Services.ShiftSchedule;
using Colors.Infrastructure.Services.Thermo;
using Colors.IntegrationTests.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// Tomorrow's shifts, planned the day before and opened and closed by the system at their
/// times (specification section 19.30).
///
/// Each test plans a shift on a day of its own and moves a clock by hand across it, so
/// "08:00 tomorrow" is a line in the test rather than a wait.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ShiftScheduleTests(DatabaseFixture fixture)
{
    /// <summary>A factory with its shift still open, and a clock at noon the day before.</summary>
    private sealed record Setup(
        FactoryData.Ids Ids,
        int ShiftId,
        int ExtruderId,
        int ThermoId,
        DateOnly Day,
        TestClock Clock);

    private static async Task<Setup> SetupAsync(ColorsDbContext db, string suffix)
    {
        var ids = await FactoryData.CreateAsync(db, suffix);

        var lines = await db.ShiftLines
            .Where(l => l.ShiftReportId == ids.ShiftReportId)
            .ToDictionaryAsync(l => l.Id, l => l.ProductionLineId);

        var shiftId = await db.ShiftReports
            .Where(r => r.Id == ids.ShiftReportId)
            .Select(r => r.ShiftId)
            .FirstAsync();

        var day = TestSequences.NextProductionDate();

        // Kept in UTC and the factory told it is in UTC, so 08:00 in the test is 08:00
        // on the shift.
        var clock = new TestClock(new DateTimeOffset(day.AddDays(-1).ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));

        return new Setup(ids, shiftId, lines[ids.ShiftLineId], lines[ids.ThermoShiftLineId], day, clock);
    }

    private static ShiftScheduleService NewService(ColorsDbContext db, TestClock clock) =>
        new(db, new FactoryClock(clock, TimeZoneInfo.Utc), clock, NullLogger<ShiftScheduleService>.Instance);

    private static void At(Setup setup, int hour, int minute = 0, int dayOffset = 0) =>
        setup.Clock.Now = new DateTimeOffset(
            setup.Day.AddDays(dayOffset).ToDateTime(new TimeOnly(hour, minute)),
            TimeSpan.Zero);

    private static ScheduleShiftRequest Plan(Setup setup, params ScheduleShiftLineRequest[] lines) =>
        new(setup.Day, setup.ShiftId, setup.Ids.UserId, lines);

    /// <summary>A person holding one job — the way an operator is set up in Users.</summary>
    private static async Task<int> PersonWithJobAsync(ColorsDbContext db, string number, string? role)
    {
        var person = new ApplicationUser
        {
            UserName = number,
            EmployeeNumber = number,
            FullName = $"Operator {number}",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(person);
        await db.SaveChangesAsync();

        if (role is not null)
        {
            var found = await db.Set<ApplicationRole>().FirstOrDefaultAsync(r => r.Name == role);
            if (found is null)
            {
                found = new ApplicationRole { Name = role, Description = role };
                db.Add(found);
                await db.SaveChangesAsync();
            }

            db.UserRoles.Add(new IdentityUserRole<int> { UserId = person.Id, RoleId = found.Id });
            await db.SaveChangesAsync();
        }

        return person.Id;
    }

    [Fact]
    public async Task Only_tomorrow_can_be_planned_and_only_once()
    {
        await using var db = fixture.CreateContext();
        var setup = await SetupAsync(db, "SCH1");
        var service = NewService(db, setup.Clock);
        var extruder = new ScheduleShiftLineRequest(setup.ExtruderId, null);

        var today = await service.ScheduleAsync(Plan(setup, extruder) with { ProductionDate = setup.Day.AddDays(-1) }, setup.Ids.UserId);
        Assert.Equal("shiftSchedule.tomorrowOnly", today.MessageCode);

        var dayAfter = await service.ScheduleAsync(Plan(setup, extruder) with { ProductionDate = setup.Day.AddDays(1) }, setup.Ids.UserId);
        Assert.Equal("shiftSchedule.tomorrowOnly", dayAfter.MessageCode);

        var tomorrow = await service.ScheduleAsync(Plan(setup, extruder), setup.Ids.UserId);
        Assert.True(tomorrow.IsSuccess, tomorrow.Message);
        Assert.Equal("Planned", tomorrow.Value!.Status);
        Assert.True(tomorrow.Value.CanChange);

        var again = await service.ScheduleAsync(Plan(setup, extruder), setup.Ids.UserId);
        Assert.Equal("shiftSchedule.alreadyPlanned", again.MessageCode);
    }

    [Fact]
    public async Task A_line_operator_must_hold_the_job_that_runs_the_line()
    {
        await using var db = fixture.CreateContext();
        var setup = await SetupAsync(db, "SCH2");
        var service = NewService(db, setup.Clock);

        var storekeeper = await PersonWithJobAsync(db, "SCH2S", RoleNames.InventoryManager);
        var former = await PersonWithJobAsync(db, "SCH2T", RoleNames.ThermoOperator);

        var wrong = await service.ScheduleAsync(
            Plan(setup, new ScheduleShiftLineRequest(setup.ThermoId, storekeeper)),
            setup.Ids.UserId);
        Assert.Equal("shift.operatorLacksRole", wrong.MessageCode);

        var right = await service.ScheduleAsync(
            Plan(setup, new ScheduleShiftLineRequest(setup.ThermoId, former)),
            setup.Ids.UserId);
        Assert.True(right.IsSuccess, right.Message);
        Assert.Equal("Operator SCH2T", Assert.Single(right.Value!.Lines).OperatorName);
    }

    [Fact]
    public async Task A_planned_shift_opens_at_its_start_with_its_crew_and_closes_the_one_before()
    {
        await using var db = fixture.CreateContext();
        var setup = await SetupAsync(db, "SCH3");
        var service = NewService(db, setup.Clock);
        var former = await PersonWithJobAsync(db, "SCH3T", RoleNames.ThermoOperator);

        var plan = await service.ScheduleAsync(
            Plan(
                setup,
                new ScheduleShiftLineRequest(setup.ExtruderId, null),
                new ScheduleShiftLineRequest(setup.ThermoId, former)),
            setup.Ids.UserId);
        Assert.True(plan.IsSuccess, plan.Message);

        At(setup, 7, 59);
        Assert.Empty((await service.RunDueAsync()).OpenedShiftReportIds);

        At(setup, 8, 0);
        var run = await service.RunDueAsync();
        var openedId = Assert.Single(run.OpenedShiftReportIds);

        db.ChangeTracker.Clear();
        var opened = await db.ShiftReports.Include(r => r.Lines).FirstAsync(r => r.Id == openedId);
        Assert.Equal(ShiftReportStatus.Open, opened.Status);
        Assert.Equal(setup.Day, opened.ProductionDate);
        Assert.Equal(setup.Ids.UserId, opened.SupervisorUserId);
        Assert.Equal(2, opened.Lines.Count);
        Assert.Equal(former, opened.Lines.Single(l => l.ProductionLineId == setup.ThermoId).OperatorUserId);

        // One shift at a time: the one that was running ended as this one began.
        var before = await db.ShiftReports.FirstAsync(r => r.Id == setup.Ids.ShiftReportId);
        Assert.Equal(ShiftReportStatus.Closed, before.Status);
        Assert.Null(before.ClosedByUserId);

        // Asked again, it does nothing: each thing is done once.
        Assert.Empty((await service.RunDueAsync()).OpenedShiftReportIds);
        Assert.Equal("Running", (await service.GetAsync()).Shifts.Single(s => s.Id == plan.Value!.Id).Status);
    }

    [Fact]
    public async Task Work_still_open_when_the_next_shift_starts_carries_over_to_it()
    {
        await using var db = fixture.CreateContext();
        var setup = await SetupAsync(db, "SCH4");
        var service = NewService(db, setup.Clock);
        var thermo = new ThermoService(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

        // A roll in the thermo, and a ticket not yet weighed back, on the running shift.
        var roll = await ThermoTests.AvailableRollAsync(db, setup.Ids, "SCH4");
        var started = await thermo.StartRunAsync(
            new StartThermoRunRequest(roll.Barcode, null, setup.Ids.ThermoShiftLineId, null, null),
            setup.Ids.UserId);
        Assert.True(started.IsSuccess, started.Message);

        var ticket = new MaterialIssueTicket
        {
            TicketNumber = TestSequences.NextRecipeNumber(),
            ShiftLineId = setup.Ids.ShiftLineId,
            IssuedByUserId = setup.Ids.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.MaterialIssueTickets.Add(ticket);
        await db.SaveChangesAsync();

        // The plan has only the extruder. The roll in the thermo brings the thermo with it.
        var plan = await service.ScheduleAsync(
            Plan(setup, new ScheduleShiftLineRequest(setup.ExtruderId, null)),
            setup.Ids.UserId);
        Assert.True(plan.IsSuccess, plan.Message);

        At(setup, 8, 0);
        var openedId = Assert.Single((await service.RunDueAsync()).OpenedShiftReportIds);

        db.ChangeTracker.Clear();
        var opened = await db.ShiftReports.Include(r => r.Lines).FirstAsync(r => r.Id == openedId);
        var runLine = await db.ThermoProductions.Where(p => p.Id == started.Value!.Id).Select(p => p.ShiftLineId).FirstAsync();
        var ticketLine = await db.MaterialIssueTickets.Where(t => t.Id == ticket.Id).Select(t => t.ShiftLineId).FirstAsync();

        Assert.Equal(opened.Lines.Single(l => l.ProductionLineId == setup.ThermoId).Id, runLine);
        Assert.Equal(opened.Lines.Single(l => l.ProductionLineId == setup.ExtruderId).Id, ticketLine);
        Assert.Contains(roll.RollCode, opened.Notes);

        var before = await db.ShiftReports.FirstAsync(r => r.Id == setup.Ids.ShiftReportId);
        Assert.Equal(ShiftReportStatus.Closed, before.Status);
        Assert.Contains(roll.RollCode, before.Notes);

        // And the men on the new shift can finish it.
        var finished = await thermo.LeaveUnfinishedAsync(started.Value!.Id, new LeaveRunUnfinishedRequest("Test over"));
        Assert.True(finished.IsSuccess, finished.Message);

        var tidy = await db.MaterialIssueTickets.FirstAsync(t => t.Id == ticket.Id);
        tidy.Status = IssueTicketStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_scheduled_shift_closes_at_its_end_once_nothing_is_left_open_on_it()
    {
        await using var db = fixture.CreateContext();
        var setup = await SetupAsync(db, "SCH5");
        var service = NewService(db, setup.Clock);
        var thermo = new ThermoService(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

        // Made while the old shift is still running, which a roll needs.
        var roll = await ThermoTests.AvailableRollAsync(db, setup.Ids, "SCH5");

        var plan = await service.ScheduleAsync(
            Plan(setup, new ScheduleShiftLineRequest(setup.ThermoId, null)),
            setup.Ids.UserId);
        Assert.True(plan.IsSuccess, plan.Message);

        At(setup, 8, 0);
        var openedId = Assert.Single((await service.RunDueAsync()).OpenedShiftReportIds);
        var thermoLine = await db.ShiftLines
            .Where(l => l.ShiftReportId == openedId && l.ProductionLineId == setup.ThermoId)
            .Select(l => l.Id)
            .FirstAsync();

        var started = await thermo.StartRunAsync(
            new StartThermoRunRequest(roll.Barcode, null, thermoLine, null, null),
            setup.Ids.UserId);
        Assert.True(started.IsSuccess, started.Message);

        // 16:00, the roll still in the machine and no shift after to take it over: closing
        // would strand it on a closed shift, so it stays open and says so.
        At(setup, 16, 0);
        Assert.Empty((await service.RunDueAsync()).ClosedShiftReportIds);
        Assert.Equal("Overrunning", (await service.GetAsync()).Shifts.Single(s => s.Id == plan.Value!.Id).Status);

        var finished = await thermo.LeaveUnfinishedAsync(started.Value!.Id, new LeaveRunUnfinishedRequest("Test over"));
        Assert.True(finished.IsSuccess, finished.Message);

        At(setup, 16, 1);
        Assert.Equal(openedId, Assert.Single((await service.RunDueAsync()).ClosedShiftReportIds));

        db.ChangeTracker.Clear();
        var closed = await db.ShiftReports.FirstAsync(r => r.Id == openedId);
        Assert.Equal(ShiftReportStatus.Closed, closed.Status);
        Assert.Equal("Finished", (await service.GetAsync()).Shifts.Single(s => s.Id == plan.Value!.Id).Status);
    }

    [Fact]
    public async Task A_plan_can_be_changed_or_cancelled_until_its_shift_starts()
    {
        await using var db = fixture.CreateContext();
        var setup = await SetupAsync(db, "SCH6");
        var service = NewService(db, setup.Clock);

        var plan = await service.ScheduleAsync(
            Plan(setup, new ScheduleShiftLineRequest(setup.ExtruderId, null)),
            setup.Ids.UserId);
        Assert.True(plan.IsSuccess, plan.Message);

        var changed = await service.UpdateAsync(
            plan.Value!.Id,
            new UpdateScheduledShiftRequest(
                null,
                [new ScheduleShiftLineRequest(setup.ExtruderId, null), new ScheduleShiftLineRequest(setup.ThermoId, null)]),
            setup.Ids.UserId);
        Assert.True(changed.IsSuccess, changed.Message);
        Assert.Equal(2, changed.Value!.Lines.Count);
        Assert.Null(changed.Value.SupervisorUserId);
        Assert.NotNull(changed.Value.ChangedAt);

        At(setup, 8, 0);
        var late = await service.UpdateAsync(
            plan.Value.Id,
            new UpdateScheduledShiftRequest(null, [new ScheduleShiftLineRequest(setup.ExtruderId, null)]),
            setup.Ids.UserId);
        Assert.Equal("shiftSchedule.alreadyStarted", late.MessageCode);
        Assert.Equal("shiftSchedule.alreadyStarted", (await service.CancelAsync(plan.Value.Id)).MessageCode);

        Assert.Single((await service.RunDueAsync()).OpenedShiftReportIds);
        Assert.Equal("shiftSchedule.alreadyOpenedChange", (await service.CancelAsync(plan.Value.Id)).MessageCode);

        // Today, tomorrow's plan can still be cancelled.
        var nextDay = await service.ScheduleAsync(
            Plan(setup, new ScheduleShiftLineRequest(setup.ExtruderId, null)) with { ProductionDate = setup.Day.AddDays(1) },
            setup.Ids.UserId);
        Assert.True(nextDay.IsSuccess, nextDay.Message);
        Assert.True((await service.CancelAsync(nextDay.Value!.Id)).IsSuccess);
    }

    [Fact]
    public async Task A_shift_the_system_was_off_for_is_never_opened_late()
    {
        await using var db = fixture.CreateContext();
        var setup = await SetupAsync(db, "SCH7");
        var service = NewService(db, setup.Clock);

        var plan = await service.ScheduleAsync(
            Plan(setup, new ScheduleShiftLineRequest(setup.ExtruderId, null)),
            setup.Ids.UserId);
        Assert.True(plan.IsSuccess, plan.Message);

        // The first check after it is 16:30 — the whole of 08:00 to 16:00 went by unseen.
        At(setup, 16, 30);
        var run = await service.RunDueAsync();

        Assert.Empty(run.OpenedShiftReportIds);
        Assert.Contains(plan.Value!.Id, run.NotOpenedScheduleIds);
        Assert.False(await db.ShiftReports.AnyAsync(r => r.ProductionDate == setup.Day && r.ShiftId == setup.ShiftId));

        var shown = (await service.GetAsync()).Shifts.Single(s => s.Id == plan.Value.Id);
        Assert.Equal("NotOpened", shown.Status);
        Assert.NotNull(shown.NotOpenedReason);
    }

    [Fact]
    public async Task A_scheduled_shift_discarded_as_a_mistake_is_not_opened_again()
    {
        await using var db = fixture.CreateContext();
        var setup = await SetupAsync(db, "SCH8");
        var service = NewService(db, setup.Clock);

        var plan = await service.ScheduleAsync(
            Plan(setup, new ScheduleShiftLineRequest(setup.ExtruderId, null)),
            setup.Ids.UserId);
        Assert.True(plan.IsSuccess, plan.Message);

        At(setup, 8, 0);
        var openedId = Assert.Single((await service.RunDueAsync()).OpenedShiftReportIds);

        db.ChangeTracker.Clear();
        var shifts = new ShiftReportService(db, TimeProvider.System, NullLogger<ShiftReportService>.Instance);
        var discarded = await shifts.DeleteAsync(openedId);
        Assert.True(discarded.IsSuccess, discarded.Message);

        At(setup, 8, 1);
        Assert.Empty((await service.RunDueAsync()).OpenedShiftReportIds);
        Assert.Equal("NotOpened", (await service.GetAsync()).Shifts.Single(s => s.Id == plan.Value!.Id).Status);
    }
}
