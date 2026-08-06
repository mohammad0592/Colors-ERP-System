using Colors.Application.Features.Production;
using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Production;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// Line 1 — batches, rolls and their measurements (specification section 8).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ProductionTests(DatabaseFixture fixture)
{
    private static ProductionService NewService(ColorsDbContext db) =>
        new(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

    /// <summary>A colour and a recipe in production, which every roll needs.</summary>
    private static async Task<(int ColorId, int RecipeVersionId)> RecipeAndColourAsync(
        ColorsDbContext db,
        string suffix,
        int authorUserId,
        bool absorbent = false)
    {
        var colour = await TestSequences.ColourAsync(db);

        var productType = await db.ProductTypes.FirstOrDefaultAsync()
                          ?? new ProductType { Name = $"Plate {suffix}" };
        if (productType.Id == 0)
        {
            db.ProductTypes.Add(productType);
        }

        await db.SaveChangesAsync();

        var family = new RecipeFamily
        {
            Name = $"Family {suffix}",
            Code = absorbent ? "Abs" : "N",
            ProductTypeId = productType.Id,
            IsAbsorbent = absorbent,
            Versions =
            [
                new RecipeVersion
                {
                    RecipeNumber = TestSequences.NextRecipeNumber(),
                    VersionNumber = 1,
                    Status = RecipeVersionStatus.Current,
                    CreatedByUserId = authorUserId,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
            ],
        };

        db.RecipeFamilies.Add(family);
        await db.SaveChangesAsync();

        return (colour.Id, family.Versions[0].Id);
    }

    /// <summary>
    /// A Black family — one that replaces 35% of its GPPS with recycle — and the black
    /// colour it must be made in (specification section 5).
    /// </summary>
    private static async Task<(int BlackColorId, int BlackRecipeId)> BlackRecipeAsync(
        ColorsDbContext db,
        string suffix,
        int authorUserId)
    {
        var black = await TestSequences.BlackColourAsync(db);
        var productType = await db.ProductTypes.FirstAsync();

        var family = new RecipeFamily
        {
            Name = $"Family Black {suffix}",
            Code = "N",
            ProductTypeId = productType.Id,
            UsesRecycle = true,
            BlackOnly = true,
            Versions =
            [
                new RecipeVersion
                {
                    RecipeNumber = TestSequences.NextRecipeNumber(),
                    VersionNumber = 1,
                    Status = RecipeVersionStatus.Current,
                    CreatedByUserId = authorUserId,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
            ],
        };

        db.RecipeFamilies.Add(family);
        await db.SaveChangesAsync();

        return (black.Id, family.Versions[0].Id);
    }

    [Fact]
    public async Task A_black_recipe_cannot_be_made_in_another_colour()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "BLK1");
        var (colourId, _) = await RecipeAndColourAsync(db, "BLK1", ids.UserId);
        var (_, blackRecipeId) = await BlackRecipeAsync(db, "BLK1", ids.UserId);

        // A third of the polymer is recycled material, which is dark. No amount of
        // white colouring hides it, so this roll cannot exist.
        var roll = await NewService(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, blackRecipeId, colourId, null, null), ids.UserId);

        Assert.False(roll.IsSuccess);
        Assert.Contains("only be made in black", roll.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_except_black_recipe_cannot_be_made_in_black()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "BLK2");
        var (_, plainRecipeId) = await RecipeAndColourAsync(db, "BLK2", ids.UserId);
        var (blackColourId, _) = await BlackRecipeAsync(db, "BLK2", ids.UserId);

        // The other direction, and the factory's own policy: black is made on the
        // recipe that uses recycle, which is the whole reason that recipe exists.
        var roll = await NewService(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, plainRecipeId, blackColourId, null, null), ids.UserId);

        Assert.False(roll.IsSuccess);
        Assert.Contains("cannot be made in", roll.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_black_recipe_in_black_is_allowed()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "BLK3");
        var (blackColourId, blackRecipeId) = await BlackRecipeAsync(db, "BLK3", ids.UserId);

        var roll = await NewService(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, blackRecipeId, blackColourId, null, null), ids.UserId);

        Assert.True(roll.IsSuccess, roll.Message);
    }


    [Fact]
    public async Task A_roll_gets_a_code_a_serial_and_a_barcode()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRD1");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PRD1", ids.UserId);

        var roll = await NewService(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);

        Assert.True(roll.IsSuccess, roll.Message);
        Assert.Equal(1, roll.Value!.DailySerial);
        Assert.NotEmpty(roll.Value.RollCode);

        // The label and the roll are one act — a roll nobody can scan is no use on the
        // floor, and a label naming nothing is worse.
        Assert.StartsWith("R", roll.Value.Barcode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_serial_counts_up_within_a_day()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRD2");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PRD2", ids.UserId);
        var service = NewService(db);

        var first = await service.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);
        var second = await service.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);

        Assert.Equal(1, first.Value!.DailySerial);
        Assert.Equal(2, second.Value!.DailySerial);
        Assert.NotEqual(first.Value.RollCode, second.Value.RollCode);
    }

    [Fact]
    public async Task A_new_roll_needs_testing_before_it_can_be_used()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRD3");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PRD3", ids.UserId);

        var roll = await NewService(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);

        Assert.Equal(RollStatus.NeedsTest.ToString(), roll.Value!.Status);
        Assert.True(roll.Value.NeedsTest);
    }

    [Fact]
    public async Task Saving_the_measurements_makes_the_roll_available()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRD4");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PRD4", ids.UserId);
        var service = NewService(db);

        var roll = await service.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);

        var tested = await service.SaveTestReportAsync(
            roll.Value!.Id,
            new SaveRollTestRequest(95.5m, 1200m, 9m, 1.2m, 1.25m, 1.3m, 1.25m, null),
            ids.UserId);

        Assert.True(tested.IsSuccess, tested.Message);
        Assert.Equal(RollStatus.Available.ToString(), tested.Value!.Status);

        // The mean of the four readings, worked out rather than stored.
        Assert.Equal(1.25m, tested.Value.TestReport!.AverageThickness);
    }

    [Fact]
    public async Task A_roll_weighing_350_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRD5");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PRD5", ids.UserId);
        var service = NewService(db);

        var roll = await service.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);

        // The real Roll Log export has one: the operator typed the length into the
        // weight box. On paper it was wrong for ever; here he is still at the machine.
        var tested = await service.SaveTestReportAsync(
            roll.Value!.Id,
            new SaveRollTestRequest(350m, 1200m, 9m, 1.2m, 1.25m, 1.3m, 1.25m, null),
            ids.UserId);

        Assert.False(tested.IsSuccess);
        Assert.Contains("weight box", tested.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_roll_is_measured_once()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRD6");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PRD6", ids.UserId);
        var service = NewService(db);

        var roll = await service.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);
        var request = new SaveRollTestRequest(95m, 1200m, 9m, 1.2m, 1.2m, 1.2m, 1.2m, null);

        await service.SaveTestReportAsync(roll.Value!.Id, request, ids.UserId);
        var again = await service.SaveTestReportAsync(roll.Value.Id, request, ids.UserId);

        Assert.False(again.IsSuccess);
    }



    [Fact]
    public async Task A_draft_recipe_cannot_make_a_roll()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRD9");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PRD9", ids.UserId);

        var version = await db.RecipeVersions.FirstAsync(v => v.Id == recipeId);
        version.Status = RecipeVersionStatus.Draft;
        await db.SaveChangesAsync();

        // A draft may still change, so a roll made to it could never be reproduced.
        var roll = await NewService(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);

        Assert.False(roll.IsSuccess);
        Assert.Contains("draft", roll.Message!, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task The_database_refuses_two_rolls_with_the_same_serial_on_a_day()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRD11");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PRD11", ids.UserId);

        var roll = await NewService(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);

        var saved = await db.Rolls.FirstAsync(r => r.Id == roll.Value!.Id);

        db.Rolls.Add(new Domain.Entities.Production.Roll
        {
            ProductionDate = saved.ProductionDate,
            DailySerial = saved.DailySerial,
            RollCode = saved.RollCode + "X",
            BatchId = saved.BatchId,
            RecipeVersionId = recipeId,
            ColorId = colourId,
            ProducedByUserId = ids.UserId,
            ProducedAt = DateTimeOffset.UtcNow,
        });

        // Two tablets logging a roll in the same moment must not both be handed 13.
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_batch_reports_the_weight_of_the_rolls_that_have_been_measured()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRD12");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PRD12", ids.UserId);
        var service = NewService(db);

        var first = await service.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);
        await service.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);

        await service.SaveTestReportAsync(
            first.Value!.Id,
            new SaveRollTestRequest(96m, 1200m, 9m, 1.2m, 1.2m, 1.2m, 1.2m, null),
            ids.UserId);

        var batches = await service.GetBatchesAsync(ids.ShiftReportId);
        var batch = batches.Single();

        // Two rolls, one measured — the kilograms out that the waste report will set
        // against the kilograms issued.
        Assert.Equal(2, batch.RollCount);
        Assert.Equal(96m, batch.TotalRollWeight);
    }
    [Fact]
    public async Task A_roll_cannot_be_logged_to_a_closed_shift()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRD13");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PRD13", ids.UserId);

        var report = await db.ShiftReports.FirstAsync(r => r.Id == ids.ShiftReportId);
        report.Status = ShiftReportStatus.Closed;
        await db.SaveChangesAsync();

        // All material goes back to the store at shift end, so a roll made against a
        // finished shift could never be true — and the mix it would open with it.
        var roll = await NewService(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);

        Assert.False(roll.IsSuccess);
    }
}
