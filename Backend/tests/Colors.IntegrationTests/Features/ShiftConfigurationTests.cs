using Colors.Application.Features.ShiftReports;
using Colors.Domain.Constants;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.ShiftReports;
using Colors.IntegrationTests.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// Who runs each line on a shift, who may fill it in, and the meter they share
/// (specification section 2).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ShiftConfigurationTests(DatabaseFixture fixture)
{
    private static ShiftReportService NewService(ColorsDbContext db) =>
        new(db, TimeProvider.System, NullLogger<ShiftReportService>.Instance);

    private static readonly ShiftActor Supervisor = new(0, IsManager: true);

    private static UpdateShiftLineRequest Line(string? notes = null) =>
        new("08:00", "16:00", null, null, null, null, [], notes);

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
    public async Task The_operator_of_a_line_must_hold_the_job_that_runs_it()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "CFG1");
        var thermoMan = await PersonWithJobAsync(db, "CFG1T", RoleNames.ThermoOperator);
        var extruderMan = await PersonWithJobAsync(db, "CFG1E", RoleNames.ExtruderOperator);
        var service = NewService(db);

        // The extruder is run by an extruder operator. Read off what the line does, not
        // what it is called.
        var wrong = await service.SetLineOperatorAsync(
            ids.ShiftReportId, ids.ShiftLineId, new SetLineOperatorRequest(thermoMan));
        var right = await service.SetLineOperatorAsync(
            ids.ShiftReportId, ids.ShiftLineId, new SetLineOperatorRequest(extruderMan));

        Assert.False(wrong.IsSuccess);
        Assert.Equal("shift.operatorLacksRole", wrong.MessageCode);
        Assert.True(right.IsSuccess, right.Message);

        var line = right.Value!.Lines.Single(l => l.Id == ids.ShiftLineId);
        Assert.Equal(extruderMan, line.OperatorUserId);
        Assert.Equal(RoleNames.ExtruderOperator, line.OperatorRole);
        Assert.Equal("Operator CFG1E", line.OperatorName);
    }

    [Fact]
    public async Task Only_the_lines_own_operator_may_fill_it_in()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "CFG2");
        var assigned = await PersonWithJobAsync(db, "CFG2A", RoleNames.ExtruderOperator);
        var colleague = await PersonWithJobAsync(db, "CFG2B", RoleNames.ExtruderOperator);
        var service = NewService(db);
        await service.SetLineOperatorAsync(
            ids.ShiftReportId, ids.ShiftLineId, new SetLineOperatorRequest(assigned));

        // Same job, but not the man put on this line tonight.
        var refused = await service.UpdateLineAsync(
            ids.ShiftReportId, ids.ShiftLineId, Line(), new ShiftActor(colleague, false));
        var allowed = await service.UpdateLineAsync(
            ids.ShiftReportId, ids.ShiftLineId, Line("Screw cleaned at 10"), new ShiftActor(assigned, false));

        Assert.False(refused.IsSuccess);
        Assert.Equal("shift.notLineOperator", refused.MessageCode);
        Assert.True(allowed.IsSuccess, allowed.Message);
        Assert.Equal(
            "Screw cleaned at 10",
            allowed.Value!.Lines.Single(l => l.Id == ids.ShiftLineId).Notes);
    }

    [Fact]
    public async Task The_supervisor_may_fill_in_any_line()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "CFG3");

        var saved = await NewService(db).UpdateLineAsync(
            ids.ShiftReportId, ids.ThermoShiftLineId, Line(), Supervisor);

        Assert.True(saved.IsSuccess, saved.Message);
    }

    [Fact]
    public async Task Any_operator_on_the_shift_may_enter_the_meter_and_is_named_against_it()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "CFG4");
        var extruderMan = await PersonWithJobAsync(db, "CFG4E", RoleNames.ExtruderOperator);
        var thermoMan = await PersonWithJobAsync(db, "CFG4T", RoleNames.ThermoOperator);
        var stranger = await PersonWithJobAsync(db, "CFG4S", null);
        var service = NewService(db);
        await service.SetLineOperatorAsync(
            ids.ShiftReportId, ids.ShiftLineId, new SetLineOperatorRequest(extruderMan));
        await service.SetLineOperatorAsync(
            ids.ShiftReportId, ids.ThermoShiftLineId, new SetLineOperatorRequest(thermoMan));

        var refused = await service.RecordElectricityAsync(
            ids.ShiftReportId, new RecordElectricityRequest(1000m, null), new ShiftActor(stranger, false));
        Assert.False(refused.IsSuccess);
        Assert.Equal("shift.notShiftOperator", refused.MessageCode);

        var first = await service.RecordElectricityAsync(
            ids.ShiftReportId, new RecordElectricityRequest(1000m, null), new ShiftActor(extruderMan, false));
        Assert.True(first.IsSuccess, first.Message);
        Assert.Equal("Operator CFG4E", first.Value!.ElectricityStartRecordedBy);
        Assert.NotNull(first.Value.ElectricityStartRecordedAt);

        // The thermo man sees it entered and adds the end reading. The start reading he
        // sent back unchanged keeps its author.
        var second = await service.RecordElectricityAsync(
            ids.ShiftReportId, new RecordElectricityRequest(1000m, 1450m), new ShiftActor(thermoMan, false));
        Assert.True(second.IsSuccess, second.Message);
        Assert.Equal("Operator CFG4E", second.Value!.ElectricityStartRecordedBy);
        Assert.Equal("Operator CFG4T", second.Value.ElectricityEndRecordedBy);
        Assert.Equal(450m, second.Value.ElectricityUsed);

        // And may correct one, which then names him.
        var corrected = await service.RecordElectricityAsync(
            ids.ShiftReportId, new RecordElectricityRequest(1010m, 1450m), new ShiftActor(thermoMan, false));
        Assert.Equal("Operator CFG4T", corrected.Value!.ElectricityStartRecordedBy);
    }
}
