using Colors.Application.Features.ShiftReports;
using Colors.Domain.Constants;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Services.ShiftReports;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// The jobs a worker did on a shift (specification section 2).
///
/// A list rather than a single choice, because that is how the factory runs: the same
/// man usually runs the extruder and takes its measurements, and the thermo operator
/// also builds the pallets.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ShiftWorkerRoleTests(DatabaseFixture fixture)
{
    private static ShiftReportService NewService(Infrastructure.Persistence.ColorsDbContext db) =>
        new(db, TimeProvider.System, NullLogger<ShiftReportService>.Instance);

    private static async Task<(int Operator, int TestPerson)> ExtruderRolesAsync(
        Infrastructure.Persistence.ColorsDbContext db)
    {
        async Task<int> Ensure(string name)
        {
            var role = await db.Set<ApplicationRole>().FirstOrDefaultAsync(r => r.Name == name);
            if (role is null)
            {
                role = new ApplicationRole { Name = name, Description = name };
                db.Add(role);
                await db.SaveChangesAsync();
            }
            return role.Id;
        }

        return (await Ensure(RoleNames.ExtruderOperator), await Ensure(RoleNames.ExtruderTestPerson));
    }

    private static UpdateShiftLineRequest LineWith(params SaveShiftWorkerRequest[] workers) =>
        new(null, "08:00", "16:00", null, null, null, null, workers);

    [Fact]
    public async Task One_man_may_hold_two_jobs_on_the_same_shift()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ROLE1");
        var (op, test) = await ExtruderRolesAsync(db);

        // He runs the extruder and takes its measurements. Forcing one would make the
        // record say he ran the machine and say nothing about the testing.
        var saved = await NewService(db).UpdateLineAsync(
            ids.ShiftReportId,
            ids.ShiftLineId,
            LineWith(new SaveShiftWorkerRequest(ids.UserId, [op, test], false)));

        Assert.True(saved.IsSuccess, saved.Message);

        var worker = saved.Value!.Lines.Single().Workers.Single();
        Assert.Equal(2, worker.RoleInShiftIds.Count);
        Assert.Contains(op, worker.RoleInShiftIds);
        Assert.Contains(test, worker.RoleInShiftIds);
        Assert.Contains(RoleNames.ExtruderOperator, worker.RoleInShiftNames);
        Assert.Contains(RoleNames.ExtruderTestPerson, worker.RoleInShiftNames);
    }

    [Fact]
    public async Task The_same_job_twice_for_one_man_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ROLE2");
        var (op, _) = await ExtruderRolesAsync(db);

        var saved = await NewService(db).UpdateLineAsync(
            ids.ShiftReportId,
            ids.ShiftLineId,
            LineWith(new SaveShiftWorkerRequest(ids.UserId, [op, op], false)));

        Assert.False(saved.IsSuccess);
    }

    [Fact]
    public async Task Recording_no_job_at_all_is_still_allowed()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ROLE3");

        // A shift where nobody wrote the jobs down is ordinary. Refusing it would only
        // teach people to tick a box at random.
        var saved = await NewService(db).UpdateLineAsync(
            ids.ShiftReportId,
            ids.ShiftLineId,
            LineWith(new SaveShiftWorkerRequest(ids.UserId, [], true)));

        Assert.True(saved.IsSuccess, saved.Message);

        var worker = saved.Value!.Lines.Single().Workers.Single();
        Assert.Empty(worker.RoleInShiftIds);
        Assert.True(worker.IsTrainee);
    }

    [Fact]
    public async Task A_job_that_does_not_exist_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ROLE4");

        var saved = await NewService(db).UpdateLineAsync(
            ids.ShiftReportId,
            ids.ShiftLineId,
            LineWith(new SaveShiftWorkerRequest(ids.UserId, [999999], false)));

        Assert.False(saved.IsSuccess);
    }

    [Fact]
    public async Task Changing_the_jobs_replaces_them_rather_than_adding()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ROLE5");
        var (op, test) = await ExtruderRolesAsync(db);
        var service = NewService(db);

        await service.UpdateLineAsync(ids.ShiftReportId, ids.ShiftLineId,
            LineWith(new SaveShiftWorkerRequest(ids.UserId, [op, test], false)));

        var second = await service.UpdateLineAsync(ids.ShiftReportId, ids.ShiftLineId,
            LineWith(new SaveShiftWorkerRequest(ids.UserId, [test], false)));

        var worker = second.Value!.Lines.Single().Workers.Single();
        Assert.Single(worker.RoleInShiftIds);
        Assert.Equal(test, worker.RoleInShiftIds[0]);

        // The rows that went are really gone, not orphaned behind the worker. Scoped
        // to this shift — the tests share one database.
        await using var fresh = fixture.CreateContext();
        var rows = await fresh.ShiftWorkerRoles
            .CountAsync(r => fresh.ShiftWorkers
                .Where(w => w.ShiftLineId == ids.ShiftLineId)
                .Select(w => w.Id)
                .Contains(r.ShiftWorkerId));

        Assert.Equal(1, rows);
    }

    [Fact]
    public async Task IsTrainee_stays_one_fact_however_many_jobs_he_did()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ROLE6");
        var (op, test) = await ExtruderRolesAsync(db);

        var saved = await NewService(db).UpdateLineAsync(
            ids.ShiftReportId,
            ids.ShiftLineId,
            LineWith(new SaveShiftWorkerRequest(ids.UserId, [op, test], true)));

        // Two jobs, one worker row — so being a trainee cannot end up true for one job
        // and false for the other.
        await using var fresh = fixture.CreateContext();
        var workers = await fresh.ShiftWorkers.Where(w => w.ShiftLineId == ids.ShiftLineId).ToListAsync();

        Assert.Single(workers);
        Assert.True(workers[0].IsTrainee);
        Assert.True(saved.IsSuccess);
    }
}
