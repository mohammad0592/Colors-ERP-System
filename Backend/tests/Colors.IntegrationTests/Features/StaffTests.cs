using Colors.Application.Features.ShiftReports;
using Colors.Application.Features.Staff;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Reports;
using Colors.Infrastructure.Services.ShiftReports;
using Colors.Infrastructure.Services.Staff;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// Staff who work the floor without an account, recorded on the shifts they work beside
/// the system's users (specification section 19.19).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class StaffTests(DatabaseFixture fixture)
{
    private static ShiftReportService Shifts(ColorsDbContext db) =>
        new(db, TimeProvider.System, NullLogger<ShiftReportService>.Instance);

    private static StaffService Staff(ColorsDbContext db) => new(db, TimeProvider.System);

    private static readonly ShiftActor Supervisor = new(0, IsManager: true);

    private static UpdateShiftLineRequest LineWith(params SaveShiftWorkerRequest[] workers) =>
        new("08:00", "16:00", null, null, null, null, workers);

    private static SaveShiftWorkerRequest StaffMember(int staffId, bool trainee = false) =>
        new(null, [], trainee, staffId);

    [Fact]
    public async Task A_staff_member_without_an_account_works_a_shift_beside_a_user()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "STF1");
        var added = await Staff(db).CreateAsync(new SaveStaffMemberRequest("Khaled STF1", null), ids.UserId);
        Assert.True(added.IsSuccess, added.Message);

        var saved = await Shifts(db).UpdateLineAsync(
            ids.ShiftReportId,
            ids.ShiftLineId,
            LineWith(new SaveShiftWorkerRequest(ids.UserId, [], false), StaffMember(added.Value!.Id)),
            Supervisor);

        Assert.True(saved.IsSuccess, saved.Message);
        var workers = saved.Value!.Lines.Single(l => l.Id == ids.ShiftLineId).Workers;
        Assert.Equal(2, workers.Count);
        var staff = workers.Single(w => w.StaffMemberId == added.Value.Id);
        Assert.Null(staff.UserId);
        Assert.Equal("Khaled STF1", staff.FullName);

        // And the extruder's report names him with the rest of the shift's staff.
        var report = await new ExtruderShiftReportService(db).GetAsync(ids.ShiftReportId);
        Assert.Contains("Khaled STF1", report.Value!.Workers);
    }

    [Fact]
    public async Task A_row_is_a_user_or_a_staff_member_never_both_or_neither()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "STF2");
        var added = await Staff(db).CreateAsync(new SaveStaffMemberRequest("Samir STF2", null), ids.UserId);

        var both = await Shifts(db).UpdateLineAsync(
            ids.ShiftReportId, ids.ShiftLineId,
            LineWith(new SaveShiftWorkerRequest(ids.UserId, [], false, added.Value!.Id)), Supervisor);
        var neither = await Shifts(db).UpdateLineAsync(
            ids.ShiftReportId, ids.ShiftLineId,
            LineWith(new SaveShiftWorkerRequest(null, [], false)), Supervisor);
        var twice = await Shifts(db).UpdateLineAsync(
            ids.ShiftReportId, ids.ShiftLineId,
            LineWith(StaffMember(added.Value.Id), StaffMember(added.Value.Id)), Supervisor);

        Assert.False(both.IsSuccess);
        Assert.False(neither.IsSuccess);
        Assert.False(twice.IsSuccess);
    }

    [Fact]
    public async Task Somebody_who_has_left_is_not_put_on_a_new_shift()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "STF3");
        var added = await Staff(db).CreateAsync(new SaveStaffMemberRequest("Fadi STF3", null), ids.UserId);
        await Staff(db).UpdateAsync(added.Value!.Id, new SaveStaffMemberRequest("Fadi STF3", null, IsActive: false));

        var saved = await Shifts(db).UpdateLineAsync(
            ids.ShiftReportId, ids.ShiftLineId, LineWith(StaffMember(added.Value.Id)), Supervisor);

        Assert.False(saved.IsSuccess);
        // Still listed for the shifts he did work, when asked for everyone.
        Assert.Contains(await Staff(db).GetAllAsync(includeInactive: true), s => s.Id == added.Value.Id);
        Assert.DoesNotContain(await Staff(db).GetAllAsync(), s => s.Id == added.Value.Id);
    }

    [Fact]
    public async Task A_number_names_one_person()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "STF4");
        var userNumber = await db.Users.Where(u => u.Id == ids.UserId).Select(u => u.EmployeeNumber).FirstAsync();

        var first = await Staff(db).CreateAsync(new SaveStaffMemberRequest("Omar STF4", "S-STF4"), ids.UserId);
        var again = await Staff(db).CreateAsync(new SaveStaffMemberRequest("Other STF4", "S-STF4"), ids.UserId);
        var aUser = await Staff(db).CreateAsync(new SaveStaffMemberRequest("Copy STF4", userNumber), ids.UserId);
        var nameless = await Staff(db).CreateAsync(new SaveStaffMemberRequest("  ", null), ids.UserId);

        Assert.True(first.IsSuccess, first.Message);
        Assert.Equal("staff.numberTaken", again.MessageCode);
        // A system user is already on the list to choose from.
        Assert.Equal("staff.numberIsUser", aUser.MessageCode);
        Assert.Equal("staff.nameRequired", nameless.MessageCode);
    }
}
