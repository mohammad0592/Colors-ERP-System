using Colors.Application.Features.Production;
using Colors.Application.Features.ShiftReports;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Production;
using Colors.Infrastructure.Services.ShiftReports;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// The roll numbers start again at 01 when a shift is opened as the extruder's starting
/// shift, and at no other time (specification section 19.16).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class StartingShiftTests(DatabaseFixture fixture)
{
    private static ShiftReportService Shifts(ColorsDbContext db) =>
        new(db, TimeProvider.System, NullLogger<ShiftReportService>.Instance);

    private static ProductionService Production(ColorsDbContext db) =>
        new(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

    /// <summary>
    /// The factory's shift closed and a second shift ready to open after it, with the
    /// production lines the factory has.
    /// </summary>
    private static async Task<(FactoryData.Ids Ids, int NextShiftId, DateOnly Day, int Extruder, int Thermo)>
        ReadyForNextShiftAsync(ColorsDbContext db, string suffix)
    {
        var ids = await FactoryData.CreateAsync(db, suffix);
        var next = new Domain.Entities.MasterData.Shift
        {
            Name = $"Next {suffix}",
            StartTime = new TimeOnly(16, 0),
            EndTime = new TimeOnly(0, 0),
        };
        db.Shifts.Add(next);
        await db.SaveChangesAsync();

        var extruder = await db.ShiftLines.Where(l => l.Id == ids.ShiftLineId).Select(l => l.ProductionLineId).FirstAsync();
        var thermo = await db.ShiftLines.Where(l => l.Id == ids.ThermoShiftLineId).Select(l => l.ProductionLineId).FirstAsync();
        var day = await db.ShiftReports.Where(r => r.Id == ids.ShiftReportId).Select(r => r.ProductionDate).FirstAsync();

        var closed = await Shifts(db).CloseAsync(ids.ShiftReportId, ids.UserId);
        Assert.True(closed.IsSuccess, closed.Message);

        return (ids, next.Id, day, extruder, thermo);
    }

    /// <summary>
    /// The suite shares one database and one counter. Put it well past where it was, so no
    /// later test is handed a roll code an earlier one already used this minute.
    /// </summary>
    private static async Task RestoreAsync(ColorsDbContext db, int before)
    {
        db.ChangeTracker.Clear();
        var counter = await db.RollNumberCounters.SingleAsync(c => c.Id == 1);
        counter.NextNumber = before + 1000;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Opening_the_extruders_starting_shift_starts_the_roll_numbers_at_01()
    {
        await using var db = fixture.CreateContext();
        var before = (await Production(db).GetRollNumberAsync()).NextNumber;

        try
        {
            var (ids, nextShiftId, day, extruder, thermo) = await ReadyForNextShiftAsync(db, "STS1");

            var opened = await Shifts(db).OpenAsync(
                new OpenShiftReportRequest(day, nextShiftId, ids.UserId, [extruder, thermo], ExtruderStartingShift: true),
                ids.UserId);

            Assert.True(opened.IsSuccess, opened.Message);
            // Marked on the extruder's line only — it is the extruder's count.
            Assert.True(opened.Value!.Lines.Single(l => l.ProductionLineId == extruder).IsStartingShift);
            Assert.False(opened.Value.Lines.Single(l => l.ProductionLineId == thermo).IsStartingShift);

            var number = await Production(db).GetRollNumberAsync();
            Assert.Equal(1, number.NextNumber);
            Assert.NotNull(number.LastResetAt);
            Assert.NotNull(number.LastResetByName);

            // The first roll of the shift is 01.
            var family = new RecipeFamily
            {
                Name = "Family STS1",
                Code = "N",
                Versions =
                [
                    new RecipeVersion
                    {
                        RecipeNumber = TestSequences.NextRecipeNumber(),
                        VersionNumber = 1,
                        Status = RecipeVersionStatus.Current,
                        CreatedByUserId = ids.UserId,
                        CreatedAt = DateTimeOffset.UtcNow,
                    },
                ],
            };
            db.RecipeFamilies.Add(family);
            await db.SaveChangesAsync();
            var colour = await TestSequences.ColourAsync(db);
            var extruderLine = opened.Value.Lines.Single(l => l.ProductionLineId == extruder).Id;

            var roll = await Production(db).CreateRollAsync(
                new CreateRollRequest(extruderLine, family.Versions[0].Id, colour.Id, ids.NormalProductId, null, null),
                ids.UserId);

            Assert.True(roll.IsSuccess, roll.Message);
            Assert.Equal(1, roll.Value!.RollNumber);
            Assert.StartsWith("01N", roll.Value.RollCode, StringComparison.Ordinal);
        }
        finally
        {
            await RestoreAsync(db, before);
        }
    }

    [Fact]
    public async Task An_ordinary_shift_carries_on_counting()
    {
        await using var db = fixture.CreateContext();
        var (ids, nextShiftId, day, extruder, _) = await ReadyForNextShiftAsync(db, "STS2");
        var before = (await Production(db).GetRollNumberAsync()).NextNumber;

        // Not ticked: the default, and most shifts.
        var opened = await Shifts(db).OpenAsync(
            new OpenShiftReportRequest(day, nextShiftId, ids.UserId, [extruder]),
            ids.UserId);

        Assert.True(opened.IsSuccess, opened.Message);
        Assert.False(opened.Value!.Lines.Single().IsStartingShift);
        Assert.Equal(before, (await Production(db).GetRollNumberAsync()).NextNumber);
    }

    [Fact]
    public async Task A_starting_shift_without_the_extruder_is_refused_and_resets_nothing()
    {
        await using var db = fixture.CreateContext();
        var (ids, nextShiftId, day, _, thermo) = await ReadyForNextShiftAsync(db, "STS3");
        var before = (await Production(db).GetRollNumberAsync()).NextNumber;

        var opened = await Shifts(db).OpenAsync(
            new OpenShiftReportRequest(day, nextShiftId, ids.UserId, [thermo], ExtruderStartingShift: true),
            ids.UserId);

        Assert.Equal("shift.startingNeedsExtruder", opened.MessageCode);
        Assert.Equal(before, (await Production(db).GetRollNumberAsync()).NextNumber);
    }
}
