using Colors.Application.Features.Production;
using Colors.Application.Features.Thermo;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Production;
using Colors.Infrastructure.Services.ShiftReports;
using Colors.Infrastructure.Services.Thermo;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// What has to be finished before a shift can close, and what the close finishes for
/// itself (specification sections 2 and 8).
///
/// The mix is the second kind: nobody opens a batch and nobody closes one. It is the
/// extruder's part of this shift, so the shift ending is the mix ending.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ShiftCloseTests(DatabaseFixture fixture)
{
    private static ShiftReportService Shifts(ColorsDbContext db) =>
        new(db, TimeProvider.System, NullLogger<ShiftReportService>.Instance);

    private static ProductionService Production(ColorsDbContext db) =>
        new(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

    private static ThermoService Thermo(ColorsDbContext db) =>
        new(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

    /// <summary>A recipe and colour that agree, which every roll needs.</summary>
    private static async Task<(int ColorId, int RecipeVersionId)> RecipeAsync(
        ColorsDbContext db,
        string suffix,
        int authorUserId)
    {
        var colour = await TestSequences.ColourAsync(db);
        var productType = await db.ProductTypes.FirstAsync();

        var family = new RecipeFamily
        {
            Name = $"Family {suffix}",
            Code = "N",
            ProductTypeId = productType.Id,
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

    [Fact]
    public async Task A_shift_cannot_close_while_a_roll_is_in_the_thermo()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "CLOSE5");
        var (colourId, recipeId) = await RecipeAsync(db, "CLOSE5", ids.UserId);

        var roll = await Production(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);

        await Production(db).SaveTestReportAsync(
            roll.Value!.Id,
            new SaveRollTestRequest(95m, 1200m, 9m, 1.2m, 1.2m, 1.2m, 1.2m, null),
            ids.UserId);

        var run = await Thermo(db).StartRunAsync(
            new StartThermoRunRequest(null, roll.Value.Id, ids.ThermoShiftLineId, null, null),
            ids.UserId);
        Assert.True(run.IsSuccess, run.Message);

        // It is physically in the machine: the run has no end time and made no bags.
        var closed = await Shifts(db).CloseAsync(ids.ShiftReportId, ids.UserId);

        Assert.False(closed.IsSuccess);
        Assert.Contains(roll.Value.RollCode, closed.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("still in the thermo", closed.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Taking_the_roll_out_lets_the_shift_close()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "CLOSE6");
        var (colourId, recipeId) = await RecipeAsync(db, "CLOSE6", ids.UserId);

        var roll = await Production(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);

        await Production(db).SaveTestReportAsync(
            roll.Value!.Id,
            new SaveRollTestRequest(95m, 1200m, 9m, 1.2m, 1.2m, 1.2m, 1.2m, null),
            ids.UserId);

        var run = await Thermo(db).StartRunAsync(
            new StartThermoRunRequest(null, roll.Value.Id, ids.ThermoShiftLineId, null, null),
            ids.UserId);

        await Thermo(db).FinishRunAsync(
            run.Value!.Id, new FinishThermoRunRequest(run.Value.StartedAt.AddMinutes(40)));

        // Counting what it made can still be done afterwards, so an uncounted run does
        // not block the close — only a roll still inside the machine does.
        var closed = await Shifts(db).CloseAsync(ids.ShiftReportId, ids.UserId);

        Assert.True(closed.IsSuccess, closed.Message);
    }
    [Fact]
    public async Task The_first_roll_opens_the_mix_and_nobody_has_to()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "CLOSE7");
        var (colourId, recipeId) = await RecipeAsync(db, "CLOSE7", ids.UserId);

        Assert.Empty(await Production(db).GetBatchesAsync(ids.ShiftReportId));

        var first = await Production(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);
        Assert.True(first.IsSuccess, first.Message);

        var second = await Production(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);

        // One mix per shift is now a fact the data enforces, not one the factory
        // reports: the second roll joins the first roll's batch because there is no
        // second one to create.
        var batches = await Production(db).GetBatchesAsync(ids.ShiftReportId);
        Assert.Single(batches);
        Assert.Equal(2, batches[0].RollCount);
        Assert.Equal(first.Value!.BatchId, second.Value!.BatchId);
    }

    [Fact]
    public async Task Closing_the_shift_finishes_its_mix()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "CLOSE8");
        var (colourId, recipeId) = await RecipeAsync(db, "CLOSE8", ids.UserId);

        await Production(db).CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipeId, colourId, null, null), ids.UserId);

        Assert.Single(await Production(db).GetBatchesAsync(ids.ShiftReportId, openOnly: true));

        var closed = await Shifts(db).CloseAsync(ids.ShiftReportId, ids.UserId);
        Assert.True(closed.IsSuccess, closed.Message);

        // All material goes back to the store at shift end, so the mix ends with it.
        Assert.Empty(await Production(db).GetBatchesAsync(ids.ShiftReportId, openOnly: true));
    }

    [Fact]
    public async Task A_roll_cannot_be_logged_against_a_line_that_does_not_mix()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "CLOSE9");
        var (colourId, recipeId) = await RecipeAsync(db, "CLOSE9", ids.UserId);

        var roll = await Production(db).CreateRollAsync(
            new CreateRollRequest(ids.ThermoShiftLineId, recipeId, colourId, null, null),
            ids.UserId);

        Assert.False(roll.IsSuccess);
        Assert.Contains("does not make rolls", roll.Message!, StringComparison.OrdinalIgnoreCase);
    }
}
