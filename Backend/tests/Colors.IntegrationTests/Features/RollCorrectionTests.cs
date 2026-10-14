using Colors.Application.Features.Production;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Production;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// A roll's code and out time: suggested when it is logged, typed over when the roll was
/// labelled by hand, and corrected afterwards while its shift is open (section 8).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RollCorrectionTests(DatabaseFixture fixture)
{
    private static ProductionService NewService(ColorsDbContext db) =>
        new(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

    private static async Task<(int ColorId, int RecipeId)> RecipeAndColourAsync(
        ColorsDbContext db,
        FactoryData.Ids ids,
        string suffix)
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

        return (colour.Id, family.Versions[0].Id);
    }

    /// <summary>A correction of the code and time only, the rest as the roll has it.</summary>
    private static UpdateRollRequest Unchanged(RollDto roll, string? code, DateTimeOffset producedAt) =>
        new(code, producedAt, roll.RecipeVersionId, roll.ColorId, roll.ProductId, roll.Notes);

    private static CreateRollRequest Roll(FactoryData.Ids ids, int recipeId, int colourId, string? code = null, DateTimeOffset? outAt = null) =>
        new(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, outAt, null, code);

    [Fact]
    public async Task The_suggested_code_is_the_one_a_roll_gets_when_none_is_typed()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RCC1");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, ids, "RCC1");
        var service = NewService(db);

        // The same moment for both, as the tablet would send it.
        var outAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var suggested = await service.SuggestRollCodeAsync(ids.ShiftLineId, recipeId, colourId, outAt);
        var roll = await service.CreateRollAsync(Roll(ids, recipeId, colourId, outAt: outAt), ids.UserId);

        Assert.True(roll.IsSuccess, roll.Message);
        Assert.Equal(suggested.Value, roll.Value!.RollCode);
    }

    [Fact]
    public async Task A_typed_code_and_out_time_are_kept()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RCC2");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, ids, "RCC2");
        var outAt = DateTimeOffset.UtcNow.AddMinutes(-40);

        // Labelled by hand before it was logged: the code on the roll is the code.
        var roll = await NewService(db).CreateRollAsync(
            Roll(ids, recipeId, colourId, "  HAND-RCC2  ", outAt), ids.UserId);

        Assert.True(roll.IsSuccess, roll.Message);
        Assert.Equal("HAND-RCC2", roll.Value!.RollCode);
        Assert.Equal(outAt.ToUnixTimeSeconds(), roll.Value.ProducedAt.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Two_rolls_may_never_share_a_code()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RCC3");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, ids, "RCC3");
        var service = NewService(db);

        var first = await service.CreateRollAsync(Roll(ids, recipeId, colourId, "DUP-RCC3"), ids.UserId);
        var second = await service.CreateRollAsync(Roll(ids, recipeId, colourId, "DUP-RCC3"), ids.UserId);
        var other = await service.CreateRollAsync(Roll(ids, recipeId, colourId), ids.UserId);
        var renamed = await service.UpdateRollAsync(
            other.Value!.Id, Unchanged(other.Value, "DUP-RCC3", other.Value.ProducedAt));

        Assert.True(first.IsSuccess, first.Message);
        Assert.Equal("roll.codeTaken", second.MessageCode);
        Assert.Equal("roll.codeTaken", renamed.MessageCode);
    }

    [Fact]
    public async Task A_logged_roll_can_be_corrected_and_its_barcode_follows_the_code()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RCC4");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, ids, "RCC4");
        var service = NewService(db);
        var roll = (await service.CreateRollAsync(Roll(ids, recipeId, colourId), ids.UserId)).Value!;
        var earlier = roll.ProducedAt.AddMinutes(-25);

        var corrected = await service.UpdateRollAsync(roll.Id, Unchanged(roll, "FIX-RCC4", earlier));

        Assert.True(corrected.IsSuccess, corrected.Message);
        Assert.Equal("FIX-RCC4", corrected.Value!.RollCode);
        Assert.Equal(earlier.ToUnixTimeSeconds(), corrected.Value.ProducedAt.ToUnixTimeSeconds());
        // One code: the barcode is the code, so it changes with it and a new label is
        // printed (section 19.12).
        Assert.Equal("FIX-RCC4", corrected.Value.Barcode);
    }

    [Fact]
    public async Task A_roll_cannot_come_out_in_the_future()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RCC5");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, ids, "RCC5");

        var roll = await NewService(db).CreateRollAsync(
            Roll(ids, recipeId, colourId, outAt: DateTimeOffset.UtcNow.AddHours(2)), ids.UserId);

        Assert.Equal("roll.outAtInFuture", roll.MessageCode);
    }

    [Fact]
    public async Task A_roll_of_a_closed_shift_is_not_changed()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RCC6");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, ids, "RCC6");
        var service = NewService(db);
        var roll = (await service.CreateRollAsync(Roll(ids, recipeId, colourId), ids.UserId)).Value!;

        var report = await db.ShiftReports.SingleAsync(r => r.Id == ids.ShiftReportId);
        report.Status = ShiftReportStatus.Closed;
        await db.SaveChangesAsync();

        var corrected = await service.UpdateRollAsync(roll.Id, Unchanged(roll, "LATE-RCC6", roll.ProducedAt));

        Assert.Equal("roll.shiftClosed", corrected.MessageCode);
    }
}
