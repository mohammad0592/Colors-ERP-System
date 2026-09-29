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
            new CreateRollRequest(ids.ShiftLineId, blackRecipeId, colourId, ids.NormalProductId, null, null),
            ids.UserId);

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
            new CreateRollRequest(ids.ShiftLineId, plainRecipeId, blackColourId, ids.NormalProductId, null, null),
            ids.UserId);

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
            new CreateRollRequest(ids.ShiftLineId, blackRecipeId, blackColourId, ids.NormalProductId, null, null),
            ids.UserId);

        Assert.True(roll.IsSuccess, roll.Message);
    }


    [Fact]
    public async Task A_roll_gets_a_code_a_serial_and_a_barcode()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRD1");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PRD1", ids.UserId);

        var roll = await NewService(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null), ids.UserId);

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
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null), ids.UserId);
        var second = await service.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null), ids.UserId);

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
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null), ids.UserId);

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
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null), ids.UserId);

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
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null), ids.UserId);

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
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null), ids.UserId);
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
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null), ids.UserId);

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
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null), ids.UserId);

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
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null), ids.UserId);
        await service.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null), ids.UserId);

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
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null), ids.UserId);

        Assert.False(roll.IsSuccess);
    }

    // ------------------------------------------- only this shift (19.6)
    //
    // The man at the machine is working now, and every roll from last month is noise.
    // The inventory screen is where the past belongs.

    [Fact]
    public async Task The_line_screen_shows_only_the_open_shift()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "SCOPE1");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "SCOPE1", ids.UserId);
        var service = NewService(db);

        var made = await service.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null), ids.UserId);
        Assert.True(made.IsSuccess, made.Message);

        // While the shift is open the roll is on the line screen.
        var thisShift = await service.GetRollsAsync(currentShiftOnly: true);
        Assert.Contains(thisShift, r => r.Id == made.Value!.Id);

        // The shift ends. The roll is history now, and history is inventory's job.
        var report = await db.ShiftReports.FirstAsync(r => r.Id == ids.ShiftReportId);
        report.Status = ShiftReportStatus.Closed;
        await db.SaveChangesAsync();

        var afterClose = await service.GetRollsAsync(currentShiftOnly: true);
        Assert.DoesNotContain(afterClose, r => r.Id == made.Value!.Id);

        // But it is still there when nobody asked for the line screen's view.
        var everything = await service.GetRollsAsync();
        Assert.Contains(everything, r => r.Id == made.Value!.Id);
    }

    [Fact]
    public async Task A_roll_waiting_to_be_measured_is_never_hidden_by_the_shift()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "SCOPE2");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "SCOPE2", ids.UserId);
        var service = NewService(db);

        var made = await service.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null), ids.UserId);
        Assert.True(made.IsSuccess, made.Message);

        var report = await db.ShiftReports.FirstAsync(r => r.Id == ids.ShiftReportId);
        report.Status = ShiftReportStatus.Closed;
        await db.SaveChangesAsync();

        // This is the one that matters. A roll made at the end of a shift is measured on
        // the next one -- that is ordinary. If the waiting list were scoped to the open
        // shift the roll would vanish from the only screen that can measure it, sit at
        // NeedsTest for ever, and be refused by the thermo with nobody able to see why.
        var waiting = await service.GetRollsAsync(needsTestOnly: true);

        Assert.Contains(waiting, r => r.Id == made.Value!.Id);
    }

    // ------------------------------------------ made for a product (19.1)
    //
    // A roll says what it was made for. The thickness range judges it and never stops it,
    // because the first rolls of a run come out wrong while the machine is set, and they
    // are still rolls for this product.

    /// <summary>Four readings with the same value, so the average is exactly that.</summary>
    private static SaveRollTestRequest Measured(decimal thickness) =>
        new(95m, 1200m, 9m, thickness, thickness, thickness, thickness, null);

    [Fact]
    public async Task A_roll_must_say_what_it_was_made_for()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PROD1");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PROD1", ids.UserId);

        var roll = await NewService(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null, null),
            ids.UserId);

        Assert.False(roll.IsSuccess);
        Assert.Equal("roll.chooseProduct", roll.MessageCode);
    }

    [Fact]
    public async Task A_product_and_a_recipe_must_agree_on_absorbency()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PROD2");

        // A normal recipe, and an absorbent product. Caught at the mixer, rather than as a
        // puzzle at the thermo about why the bags came out wrong.
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PROD2", ids.UserId);

        var roll = await NewService(db).CreateRollAsync(
            new CreateRollRequest(
                ids.ShiftLineId, recipeId, colourId, ids.AbsorbentProductId, null, null),
            ids.UserId);

        Assert.False(roll.IsSuccess);
        Assert.Equal("roll.productAbsorbentRecipeNot", roll.MessageCode);
    }

    [Fact]
    public async Task The_roll_carries_the_product_it_was_made_for()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PROD3");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PROD3", ids.UserId);

        var roll = await NewService(db).CreateRollAsync(
            new CreateRollRequest(
                ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null),
            ids.UserId);

        Assert.True(roll.IsSuccess, roll.Message);
        Assert.Equal(ids.NormalProductId, roll.Value!.ProductId);
        Assert.Equal("Big Plate PROD3", roll.Value.ProductName);
    }

    [Theory]
    [InlineData(2.9, true)]
    [InlineData(3.5, false)]
    public async Task Measuring_a_roll_judges_it_against_its_product(
        decimal thickness,
        bool expected)
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, expected ? "PROD4a" : "PROD4b");
        var (colourId, recipeId) = await RecipeAndColourAsync(
            db, expected ? "PROD4a" : "PROD4b", ids.UserId);

        var product = await db.Products.FirstAsync(p => p.Id == ids.NormalProductId);
        product.MinThickness = 2.8m;
        product.MaxThickness = 3.0m;
        await db.SaveChangesAsync();

        var service = NewService(db);
        var roll = await service.CreateRollAsync(
            new CreateRollRequest(
                ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null),
            ids.UserId);

        var measured = await service.SaveTestReportAsync(
            roll.Value!.Id, Measured(thickness), ids.UserId);

        Assert.True(measured.IsSuccess, measured.Message);
        Assert.Equal(expected, measured.Value!.TestReport!.ThicknessInSpec);
    }

    [Fact]
    public async Task An_out_of_spec_roll_is_still_released_to_the_thermo()
    {
        // The whole point of a verdict rather than a gate. A setup roll that came out at
        // 4 mm is still a plate roll, still used, and the record simply says what it was.
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PROD5");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PROD5", ids.UserId);

        var product = await db.Products.FirstAsync(p => p.Id == ids.NormalProductId);
        product.MinThickness = 2.8m;
        product.MaxThickness = 3.0m;
        await db.SaveChangesAsync();

        var service = NewService(db);
        var roll = await service.CreateRollAsync(
            new CreateRollRequest(
                ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null),
            ids.UserId);

        var measured = await service.SaveTestReportAsync(roll.Value!.Id, Measured(4.0m), ids.UserId);

        Assert.True(measured.IsSuccess, measured.Message);
        Assert.False(measured.Value!.TestReport!.ThicknessInSpec);
        Assert.Equal(RollStatus.Available.ToString(), measured.Value.Status);
    }

    [Fact]
    public async Task A_product_with_no_range_gives_no_verdict()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PROD6");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PROD6", ids.UserId);
        var service = NewService(db);

        var roll = await service.CreateRollAsync(
            new CreateRollRequest(
                ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null),
            ids.UserId);

        var measured = await service.SaveTestReportAsync(roll.Value!.Id, Measured(9.0m), ids.UserId);

        // Not "failed". Nobody has said what this product should be yet.
        Assert.Null(measured.Value!.TestReport!.ThicknessInSpec);
    }

    [Fact]
    public async Task Changing_a_range_later_does_not_rewrite_old_verdicts()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PROD7");
        var (colourId, recipeId) = await RecipeAndColourAsync(db, "PROD7", ids.UserId);

        var product = await db.Products.FirstAsync(p => p.Id == ids.NormalProductId);
        product.MinThickness = 2.8m;
        product.MaxThickness = 3.0m;
        await db.SaveChangesAsync();

        var service = NewService(db);
        var roll = await service.CreateRollAsync(
            new CreateRollRequest(
                ids.ShiftLineId, recipeId, colourId, ids.NormalProductId, null, null),
            ids.UserId);
        await service.SaveTestReportAsync(roll.Value!.Id, Measured(2.9m), ids.UserId);

        // A year later the factory decides plates should be 3.0 to 3.2. The roll measured
        // at 2.9 was in spec by the rule that held when it was made, and a report read in
        // March must not say something different in May.
        product.MinThickness = 3.0m;
        product.MaxThickness = 3.2m;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reread = await NewService(db).GetRollAsync(roll.Value.Id);

        Assert.True(reread.Value!.TestReport!.ThicknessInSpec);
    }
}
