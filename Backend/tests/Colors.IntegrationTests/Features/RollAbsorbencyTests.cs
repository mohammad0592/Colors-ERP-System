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
/// water (specification section 19.14).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RollAbsorbencyTests(DatabaseFixture fixture)
{
    private static ProductionService NewService(ColorsDbContext db) =>
        new(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

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

        var roll = await NewService(db).CreateRollAsync(
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

    [Fact]
    public async Task The_percentage_is_the_water_taken_up_as_a_share_of_the_dry_weight()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ABT1");
        var roll = await RollAsync(db, ids, "ABT1", absorbent: true);

        // 10 g dry, 13.5 g after ten minutes in water: it took up 3.5 g, which is 35%.
        var tested = await NewService(db).SaveTestReportAsync(roll.Id, Test(10m, 13.5m), ids.UserId);

        Assert.True(tested.IsSuccess, tested.Message);
        Assert.Equal(35m, tested.Value!.TestReport!.AbsorbencyPercentage);
        Assert.Equal(10m, tested.Value.TestReport.AbsorbencyDryWeight);
        Assert.Equal(13.5m, tested.Value.TestReport.AbsorbencyWetWeight);
    }

    [Fact]
    public async Task An_absorbent_roll_cannot_be_tested_without_it()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ABT2");
        var roll = await RollAsync(db, ids, "ABT2", absorbent: true);

        var tested = await NewService(db).SaveTestReportAsync(roll.Id, Test(null, null), ids.UserId);

        Assert.Equal("roll.absorbencyMissing", tested.MessageCode);
    }

    [Fact]
    public async Task A_roll_that_is_not_absorbent_takes_no_absorbency()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ABT3");
        var roll = await RollAsync(db, ids, "ABT3", absorbent: false);
        var service = NewService(db);

        var refused = await service.SaveTestReportAsync(roll.Id, Test(10m, 12m), ids.UserId);
        var tested = await service.SaveTestReportAsync(roll.Id, Test(null, null), ids.UserId);

        Assert.Equal("roll.notAbsorbent", refused.MessageCode);
        Assert.True(tested.IsSuccess, tested.Message);
        Assert.Null(tested.Value!.TestReport!.AbsorbencyPercentage);
    }

    [Fact]
    public async Task Weights_the_wrong_way_round_are_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ABT4");
        var roll = await RollAsync(db, ids, "ABT4", absorbent: true);

        // Lighter after the water: the two boxes were filled the wrong way round.
        var tested = await NewService(db).SaveTestReportAsync(roll.Id, Test(13.5m, 10m), ids.UserId);

        Assert.Equal("roll.absorbencyBackwards", tested.MessageCode);
    }
}
