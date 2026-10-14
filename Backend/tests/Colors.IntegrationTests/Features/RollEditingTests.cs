using Colors.Application.Features.Production;
using Colors.Domain.Common;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Production;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// Everything about a roll can be corrected, and its test is saved a reading at a time
/// (specification section 19.15).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RollEditingTests(DatabaseFixture fixture)
{
    private static ProductionService NewService(ColorsDbContext db) =>
        new(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

    private static async Task<RollDto> RollAsync(ColorsDbContext db, FactoryData.Ids ids, string suffix)
    {
        var colour = await TestSequences.ColourAsync(db);
        var family = new RecipeFamily
        {
            Name = $"Family {suffix}",
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

        var roll = await NewService(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, family.Versions[0].Id, colour.Id, ids.NormalProductId, null, null),
            ids.UserId);
        Assert.True(roll.IsSuccess, roll.Message);
        return roll.Value!;
    }

    private static UpdateRollRequest Edit(RollDto roll, int? colorId = null, int? productId = null, string? notes = null) =>
        new(null, roll.ProducedAt, roll.RecipeVersionId, colorId ?? roll.ColorId, productId ?? roll.ProductId, notes);

    [Fact]
    public async Task A_test_saved_part_way_keeps_the_roll_waiting_until_it_is_finished()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RED1");
        var roll = await RollAsync(db, ids, "RED1");
        var service = NewService(db);

        await service.MeasureRollAsync(roll.Id, new MeasureRollRequest(95m, 1200m), ids.UserId);

        // The plate first; the gauge comes later.
        var plate = await service.SaveTestReportAsync(
            roll.Id, new SaveRollTestRequest(9m, null, null, null, null, null), ids.UserId);
        Assert.True(plate.IsSuccess, plate.Message);
        Assert.Equal(RollStatus.NeedsTest.ToString(), plate.Value!.Status);
        Assert.True(plate.Value.NeedsTest);
        Assert.Null(plate.Value.TestReport!.AverageThickness);

        // Still on the list of rolls whose test is not finished.
        var waiting = await service.GetRollsAsync(needsTestOnly: true);
        Assert.Contains(waiting, r => r.Id == roll.Id);

        var finished = await service.SaveTestReportAsync(
            roll.Id, new SaveRollTestRequest(9m, 1.2m, 1.2m, 1.2m, 1.2m, null), ids.UserId);
        Assert.Equal(RollStatus.Available.ToString(), finished.Value!.Status);

        // Clearing a reading by mistake puts it back, rather than sending the thermo a
        // roll with a gap in its test.
        var cleared = await service.SaveTestReportAsync(
            roll.Id, new SaveRollTestRequest(9m, 1.2m, null, 1.2m, 1.2m, null), ids.UserId);
        Assert.Equal(RollStatus.NeedsTest.ToString(), cleared.Value!.Status);
    }

    [Fact]
    public async Task An_empty_test_or_a_zero_reading_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RED2");
        var roll = await RollAsync(db, ids, "RED2");
        var service = NewService(db);

        var empty = await service.SaveTestReportAsync(
            roll.Id, new SaveRollTestRequest(null, null, null, null, null, null), ids.UserId);
        var zero = await service.SaveTestReportAsync(
            roll.Id, new SaveRollTestRequest(9m, 0m, null, null, null, null), ids.UserId);

        Assert.Equal("roll.testEmpty", empty.MessageCode);
        Assert.Equal("roll.readingNotPositive", zero.MessageCode);
    }

    [Fact]
    public async Task A_new_colour_gives_the_roll_a_new_code_with_its_own_number()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RED3");
        var roll = await RollAsync(db, ids, "RED3");
        var blue = await TestSequences.ColourAsync(db);

        var edited = await NewService(db).UpdateRollAsync(roll.Id, Edit(roll, colorId: blue.Id, notes: "Was logged as the wrong colour"));

        Assert.True(edited.IsSuccess, edited.Message);
        var expected = RollCode.For(roll.RollNumber, "N", roll.ProducedAt, blue.Name);
        Assert.Equal(expected, edited.Value!.RollCode);
        Assert.Equal(RollCode.AsBarcode(expected), edited.Value.Barcode);
        Assert.Equal(blue.Id, edited.Value.ColorId);
        Assert.Equal("Was logged as the wrong colour", edited.Value.Notes);
    }

    [Fact]
    public async Task The_edit_screen_previews_the_code_with_the_rolls_own_number()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RED4");
        var roll = await RollAsync(db, ids, "RED4");
        var other = await TestSequences.ColourAsync(db);

        var preview = await NewService(db).SuggestRollCodeAsync(
            null, roll.RecipeVersionId, other.Id, roll.ProducedAt, forRollId: roll.Id);

        Assert.True(preview.IsSuccess, preview.Message);
        Assert.Equal(RollCode.For(roll.RollNumber, "N", roll.ProducedAt, other.Name), preview.Value);
    }

    [Fact]
    public async Task A_product_the_recipe_does_not_make_is_refused_on_an_edit_too()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RED5");
        var roll = await RollAsync(db, ids, "RED5");

        var edited = await NewService(db).UpdateRollAsync(roll.Id, Edit(roll, productId: ids.AbsorbentProductId));

        Assert.Equal("roll.productAbsorbentRecipeNot", edited.MessageCode);
    }

    [Fact]
    public async Task What_a_roll_is_made_of_is_not_changed_once_it_is_in_the_thermo()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RED6");
        var roll = await RollAsync(db, ids, "RED6");
        var other = await TestSequences.ColourAsync(db);

        var stored = await db.Rolls.SingleAsync(r => r.Id == roll.Id);
        stored.Status = RollStatus.InThermo;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = NewService(db);
        var recoloured = await service.UpdateRollAsync(roll.Id, Edit(roll, colorId: other.Id));
        var retested = await service.SaveTestReportAsync(
            roll.Id, new SaveRollTestRequest(9m, 1.2m, 1.2m, 1.2m, 1.2m, null), ids.UserId);

        Assert.Equal("roll.choicesAlreadyFormed", recoloured.MessageCode);
        Assert.Equal("roll.testAlreadyFormed", retested.MessageCode);
    }
}
