using Colors.Application.Features.ShiftReports;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.ShiftReports;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// One shift at a time (specification section 2).
///
/// The factory cannot work shift A and shift B at once — the men on A go home before
/// the men on B arrive. With two open there is nothing to say which shift a roll belongs
/// to, and once it is recorded against the wrong one the figures for both are wrong.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class SingleOpenShiftTests(DatabaseFixture fixture)
{
    private static ShiftReportService NewService(ColorsDbContext db) =>
        new(db, TimeProvider.System, NullLogger<ShiftReportService>.Instance);

    /// <summary>A second shift on the same day, which the factory would run after the first.</summary>
    private static async Task<int> SecondShiftAsync(ColorsDbContext db, string suffix)
    {
        var shift = new Domain.Entities.MasterData.Shift
        {
            Name = $"Next {suffix}",
            StartTime = new TimeOnly(16, 0),
            EndTime = new TimeOnly(0, 0),
        };

        db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        return shift.Id;
    }

    [Fact]
    public async Task A_second_shift_cannot_be_opened_while_one_is_running()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "OPEN1");
        var nextShiftId = await SecondShiftAsync(db, "OPEN1");

        var line = await db.ShiftLines
            .Where(l => l.Id == ids.ShiftLineId)
            .Select(l => l.ProductionLineId)
            .FirstAsync();

        var report = await db.ShiftReports.AsNoTracking().FirstAsync(r => r.Id == ids.ShiftReportId);

        var second = await NewService(db).OpenAsync(
            new OpenShiftReportRequest(report.ProductionDate, nextShiftId, ids.UserId, [line]),
            ids.UserId);

        Assert.False(second.IsSuccess);

        // The message has to name the one to close, or the supervisor is stuck.
        Assert.Contains("still open", second.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one shift at a time", second.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Closing_the_first_lets_the_next_one_start()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "OPEN2");
        var nextShiftId = await SecondShiftAsync(db, "OPEN2");
        var service = NewService(db);

        var line = await db.ShiftLines
            .Where(l => l.Id == ids.ShiftLineId)
            .Select(l => l.ProductionLineId)
            .FirstAsync();

        var report = await db.ShiftReports.AsNoTracking().FirstAsync(r => r.Id == ids.ShiftReportId);

        var closed = await service.CloseAsync(ids.ShiftReportId, ids.UserId);
        Assert.True(closed.IsSuccess, closed.Message);

        // Close A, then open B. That is the whole working day.
        var second = await service.OpenAsync(
            new OpenShiftReportRequest(report.ProductionDate, nextShiftId, ids.UserId, [line]),
            ids.UserId);

        Assert.True(second.IsSuccess, second.Message);
    }

    [Fact]
    public async Task An_old_shift_can_be_reopened_to_fix_it_while_another_runs()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "OPEN3");
        var nextShiftId = await SecondShiftAsync(db, "OPEN3");
        var service = NewService(db);

        var line = await db.ShiftLines
            .Where(l => l.Id == ids.ShiftLineId)
            .Select(l => l.ProductionLineId)
            .FirstAsync();

        var report = await db.ShiftReports.AsNoTracking().FirstAsync(r => r.Id == ids.ShiftReportId);

        await service.CloseAsync(ids.ShiftReportId, ids.UserId);
        await service.OpenAsync(
            new OpenShiftReportRequest(report.ProductionDate, nextShiftId, ids.UserId, [line]),
            ids.UserId);

        // The supervisor closed A, B started, and only then did he notice A's meter
        // reading was missing. He cannot close B — B is really running — so refusing
        // here would mean that reading could never be fixed at all.
        var reopened = await service.ReopenAsync(
            ids.ShiftReportId,
            new ReopenShiftReportRequest("The electricity reading was missed"),
            ids.UserId);

        Assert.True(reopened.IsSuccess, reopened.Message);

        // Being fixed, not being worked — and its record is open to change.
        Assert.Equal(ShiftReportStatus.Correcting.ToString(), reopened.Value!.Status);
        Assert.False(reopened.Value.IsOpen);
        Assert.True(reopened.Value.CanEdit);

        // The reason and the circumstance both stay on the record.
        Assert.Contains("electricity reading", reopened.Value.Notes!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("while shift", reopened.Value.Notes!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_shift_being_fixed_takes_no_production()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "OPEN5");
        var nextShiftId = await SecondShiftAsync(db, "OPEN5");
        var service = NewService(db);

        var line = await db.ShiftLines
            .Where(l => l.Id == ids.ShiftLineId)
            .Select(l => l.ProductionLineId)
            .FirstAsync();

        var report = await db.ShiftReports.AsNoTracking().FirstAsync(r => r.Id == ids.ShiftReportId);

        await service.CloseAsync(ids.ShiftReportId, ids.UserId);
        await service.OpenAsync(
            new OpenShiftReportRequest(report.ProductionDate, nextShiftId, ids.UserId, [line]),
            ids.UserId);
        await service.ReopenAsync(
            ids.ShiftReportId,
            new ReopenShiftReportRequest("Downtime was written down wrong"),
            ids.UserId);

        // This is the whole reason Correcting exists rather than a second Open shift.
        var production = new Colors.Infrastructure.Services.Production.ProductionService(
            db,
            new Colors.Infrastructure.Services.Barcodes.BarcodeService(db, TimeProvider.System),
            TimeProvider.System);

        var roll = await production.CreateRollAsync(
            new Colors.Application.Features.Production.CreateRollRequest(
                ids.ShiftLineId, 0, 0, null, null),
            ids.UserId);

        Assert.False(roll.IsSuccess);

        // And it must not claim the shift is closed, because it plainly is not.
        Assert.Contains("fix its record", roll.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("is closed", roll.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reopening_with_nothing_else_running_opens_it_properly()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "OPEN6");
        var service = NewService(db);

        await service.CloseAsync(ids.ShiftReportId, ids.UserId);

        // Closed by mistake while it was still running — work carries on.
        var reopened = await service.ReopenAsync(
            ids.ShiftReportId,
            new ReopenShiftReportRequest("Closed by mistake, the shift is still running"),
            ids.UserId);

        Assert.True(reopened.IsSuccess, reopened.Message);
        Assert.Equal(ShiftReportStatus.Open.ToString(), reopened.Value!.Status);
        Assert.True(reopened.Value.IsOpen);
    }

    [Fact]
    public async Task The_database_refuses_a_second_open_shift()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "OPEN4");

        var closed = await db.ShiftReports
            .Where(r => r.Id != ids.ShiftReportId && r.Status == ShiftReportStatus.Closed)
            .Select(r => r.Id)
            .FirstAsync();

        // Straight to the table, past every check in the service. Two supervisors on two
        // tablets can open a shift in the same moment, so the index is the only real
        // guard — and it indexes a constant, so every open row collides with every other.
        var secondOpen = () => db.Database.ExecuteSqlAsync(
            $"""UPDATE "ShiftReports" SET "Status" = 'Open' WHERE "Id" = {closed}""");

        await Assert.ThrowsAsync<Npgsql.PostgresException>(secondOpen);
    }
}
