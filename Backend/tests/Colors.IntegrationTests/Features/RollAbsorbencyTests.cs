using Colors.Application.Features.Production;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Production;
using Colors.IntegrationTests.Common;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// The absorbency test on absorbent rolls: a piece weighed dry, then after ten minutes in
/// water (specification sections 19.14 and 19.15).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RollAbsorbencyTests(DatabaseFixture fixture)
{
    private static ProductionService NewService(ColorsDbContext db, TimeProvider clock) =>
        new(db, new BarcodeService(db, clock), clock);

    private static async Task<RollDto> RollAsync(ColorsDbContext db, FactoryData.Ids ids, string suffix, bool absorbent)
    {
        var colour = await TestSequences.ColourAsync(db);
        var family = new RecipeFamily
        {
            Name = $"Family {suffix}",
            Code = absorbent ? "Abs" : "N",
            IsAbsorbent = absorbent,
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

        var roll = await NewService(db, TimeProvider.System).CreateRollAsync(
            new CreateRollRequest(
                ids.ShiftLineId,
                family.Versions[0].Id,
                colour.Id,
                absorbent ? ids.AbsorbentProductId : ids.NormalProductId,
                null,
                null),
            ids.UserId);
        Assert.True(roll.IsSuccess, roll.Message);
        return roll.Value!;
    }

    private static SaveRollTestRequest Test(decimal? dry, decimal? wet) =>
        new(9m, 1.2m, 1.2m, 1.2m, 1.2m, null, dry, wet);

    /// <summary>
    /// A clock starting now, so a roll logged a moment ago is not in its future — in whole
    /// microseconds, which is all PostgreSQL keeps, so a time read back equals the one set.
    /// </summary>
    private static TestClock Clock()
    {
        var now = DateTimeOffset.UtcNow;
        return new TestClock(now.AddTicks(-(now.Ticks % 10)));
    }

    [Fact]
    public async Task The_percentage_is_the_water_taken_up_as_a_share_of_the_dry_weight()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ABT1");
        var roll = await RollAsync(db, ids, "ABT1", absorbent: true);
        var clock = Clock();
        var service = NewService(db, clock);

        // Weighed dry and into the water: the screens count down to when it comes out.
        var soaking = await service.SaveTestReportAsync(roll.Id, Test(10m, null), ids.UserId);
        Assert.True(soaking.IsSuccess, soaking.Message);
        Assert.Equal(clock.Now.AddMinutes(10), soaking.Value!.TestReport!.AbsorbencyWetDueAt);
        Assert.True(soaking.Value.NeedsTest);

        // 10 g dry, 13.5 g after ten minutes in water: it took up 3.5 g, which is 35%.
        clock.Advance(TimeSpan.FromMinutes(10));
        var tested = await service.SaveTestReportAsync(roll.Id, Test(10m, 13.5m), ids.UserId);

        Assert.True(tested.IsSuccess, tested.Message);
        Assert.Equal(35m, tested.Value!.TestReport!.AbsorbencyPercentage);
        Assert.Equal(10m, tested.Value.TestReport.AbsorbencyDryWeight);
        Assert.Equal(13.5m, tested.Value.TestReport.AbsorbencyWetWeight);
        Assert.Null(tested.Value.TestReport.AbsorbencyWetDueAt);
        Assert.False(tested.Value.NeedsTest);
    }

    [Fact]
    public async Task The_wet_weight_waits_ten_minutes_after_the_dry_one_is_saved()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ABT2");
        var roll = await RollAsync(db, ids, "ABT2", absorbent: true);
        var clock = Clock();
        var service = NewService(db, clock);

        // Both at once: the dry weight was never saved, so nothing started the clock.
        var together = await service.SaveTestReportAsync(roll.Id, Test(10m, 13.5m), ids.UserId);
        Assert.Equal("roll.absorbencyDryFirst", together.MessageCode);

        await service.SaveTestReportAsync(roll.Id, Test(10m, null), ids.UserId);

        // Correcting the dry weight is the same piece, already in the water: the clock
        // does not start again.
        clock.Advance(TimeSpan.FromMinutes(4));
        await service.SaveTestReportAsync(roll.Id, Test(10.2m, null), ids.UserId);

        clock.Advance(TimeSpan.FromMinutes(2));
        var early = await service.SaveTestReportAsync(roll.Id, Test(10.2m, 13.5m), ids.UserId);
        Assert.Equal("roll.absorbencySoaking", early.MessageCode);
        Assert.Equal(["4"], early.MessageArgs);

        clock.Advance(TimeSpan.FromMinutes(4));
        var onTime = await service.SaveTestReportAsync(roll.Id, Test(10.2m, 13.5m), ids.UserId);
        Assert.True(onTime.IsSuccess, onTime.Message);
    }

    [Fact]
    public async Task A_roll_that_is_not_absorbent_takes_no_absorbency()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ABT3");
        var roll = await RollAsync(db, ids, "ABT3", absorbent: false);
        var service = NewService(db, Clock());

        var refused = await service.SaveTestReportAsync(roll.Id, Test(10m, null), ids.UserId);
        var tested = await service.SaveTestReportAsync(roll.Id, Test(null, null), ids.UserId);

        Assert.Equal("roll.notAbsorbent", refused.MessageCode);
        Assert.True(tested.IsSuccess, tested.Message);
        Assert.Null(tested.Value!.TestReport!.AbsorbencyPercentage);
        // Its test asks for no absorbency, so the plate and four readings finish it.
        Assert.False(tested.Value.NeedsTest);
    }

    [Fact]
    public async Task Weights_the_wrong_way_round_are_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ABT4");
        var roll = await RollAsync(db, ids, "ABT4", absorbent: true);
        var clock = Clock();
        var service = NewService(db, clock);

        await service.SaveTestReportAsync(roll.Id, Test(13.5m, null), ids.UserId);
        clock.Advance(TimeSpan.FromMinutes(11));

        // Lighter after the water: the two boxes were filled the wrong way round.
        var tested = await service.SaveTestReportAsync(roll.Id, Test(13.5m, 10m), ids.UserId);

        Assert.Equal("roll.absorbencyBackwards", tested.MessageCode);
    }

    [Fact]
    public async Task An_absorbent_roll_is_not_ready_until_its_absorbency_is_in()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ABT5");
        var roll = await RollAsync(db, ids, "ABT5", absorbent: true);
        var clock = Clock();
        var service = NewService(db, clock);

        await service.MeasureRollAsync(roll.Id, new MeasureRollRequest(95m, 1200m), ids.UserId);
        var soaking = await service.SaveTestReportAsync(roll.Id, Test(10m, null), ids.UserId);
        Assert.Equal(RollStatus.NeedsTest.ToString(), soaking.Value!.Status);

        clock.Advance(TimeSpan.FromMinutes(10));
        var done = await service.SaveTestReportAsync(roll.Id, Test(10m, 12m), ids.UserId);
        Assert.Equal(RollStatus.Available.ToString(), done.Value!.Status);
    }
}
