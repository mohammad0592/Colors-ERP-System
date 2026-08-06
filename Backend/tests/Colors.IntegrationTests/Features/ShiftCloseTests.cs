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
/// What has to be finished before a shift can close (specification section 2).
///
/// Leaving a batch open across a close is a trap with no way out: rolls may only be
/// logged to an open shift, and a batch that produced no rolls may not be finished — so
/// an empty batch on a closed shift can neither take a roll nor be closed.
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
    public async Task A_shift_cannot_close_while_a_batch_is_running()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "CLOSE1");
        var (colourId, recipeId) = await RecipeAsync(db, "CLOSE1", ids.UserId);

        var batch = await Production(db).StartBatchAsync(
            new StartBatchRequest(ids.ShiftLineId, null), ids.UserId);

        await Production(db).CreateRollAsync(
            new CreateRollRequest(batch.Value!.Id, recipeId, colourId, null, null), ids.UserId);

        var closed = await Shifts(db).CloseAsync(ids.ShiftReportId, ids.UserId);

        Assert.False(closed.IsSuccess);
        Assert.Contains(
            $"Batch {batch.Value.BatchNumber}", closed.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Finish it first", closed.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_empty_batch_is_named_as_something_to_discard_not_finish()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "CLOSE2");

        // Nothing was drawn from it, and an empty batch cannot be finished — so telling
        // the supervisor to finish it would be telling him to do the impossible.
        await Production(db).StartBatchAsync(
            new StartBatchRequest(ids.ShiftLineId, null), ids.UserId);

        var closed = await Shifts(db).CloseAsync(ids.ShiftReportId, ids.UserId);

        Assert.False(closed.IsSuccess);
        Assert.Contains("discard it first", closed.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Discarding_the_empty_batch_lets_the_shift_close()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "CLOSE3");

        var batch = await Production(db).StartBatchAsync(
            new StartBatchRequest(ids.ShiftLineId, null), ids.UserId);

        var discarded = await Production(db).DiscardBatchAsync(batch.Value!.Id);
        Assert.True(discarded.IsSuccess, discarded.Message);

        var closed = await Shifts(db).CloseAsync(ids.ShiftReportId, ids.UserId);
        Assert.True(closed.IsSuccess, closed.Message);

        Assert.False(await db.Batches.AnyAsync(b => b.Id == batch.Value.Id));
    }

    [Fact]
    public async Task A_batch_that_made_rolls_is_never_thrown_away()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "CLOSE4");
        var (colourId, recipeId) = await RecipeAsync(db, "CLOSE4", ids.UserId);

        var batch = await Production(db).StartBatchAsync(
            new StartBatchRequest(ids.ShiftLineId, null), ids.UserId);

        await Production(db).CreateRollAsync(
            new CreateRollRequest(batch.Value!.Id, recipeId, colourId, null, null), ids.UserId);

        // It is the only record of what went into those rolls.
        var discarded = await Production(db).DiscardBatchAsync(batch.Value.Id);

        Assert.False(discarded.IsSuccess);
        Assert.Contains("cannot be thrown away", discarded.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_shift_cannot_close_while_a_roll_is_in_the_thermo()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "CLOSE5");
        var (colourId, recipeId) = await RecipeAsync(db, "CLOSE5", ids.UserId);

        var batch = await Production(db).StartBatchAsync(
            new StartBatchRequest(ids.ShiftLineId, null), ids.UserId);

        var roll = await Production(db).CreateRollAsync(
            new CreateRollRequest(batch.Value!.Id, recipeId, colourId, null, null), ids.UserId);

        await Production(db).SaveTestReportAsync(
            roll.Value!.Id,
            new SaveRollTestRequest(95m, 1200m, 9m, 1.2m, 1.2m, 1.2m, 1.2m, null),
            ids.UserId);

        var run = await Thermo(db).StartRunAsync(
            new StartThermoRunRequest(null, roll.Value.Id, ids.ThermoShiftLineId, null, null),
            ids.UserId);
        Assert.True(run.IsSuccess, run.Message);

        await Production(db).FinishBatchAsync(batch.Value.Id);

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

        var batch = await Production(db).StartBatchAsync(
            new StartBatchRequest(ids.ShiftLineId, null), ids.UserId);

        var roll = await Production(db).CreateRollAsync(
            new CreateRollRequest(batch.Value!.Id, recipeId, colourId, null, null), ids.UserId);

        await Production(db).SaveTestReportAsync(
            roll.Value!.Id,
            new SaveRollTestRequest(95m, 1200m, 9m, 1.2m, 1.2m, 1.2m, 1.2m, null),
            ids.UserId);

        var run = await Thermo(db).StartRunAsync(
            new StartThermoRunRequest(null, roll.Value.Id, ids.ThermoShiftLineId, null, null),
            ids.UserId);

        await Thermo(db).FinishRunAsync(
            run.Value!.Id, new FinishThermoRunRequest(run.Value.StartedAt.AddMinutes(40)));

        await Production(db).FinishBatchAsync(batch.Value.Id);

        // Counting what it made can still be done afterwards, so an uncounted run does
        // not block the close — only a roll still inside the machine does.
        var closed = await Shifts(db).CloseAsync(ids.ShiftReportId, ids.UserId);

        Assert.True(closed.IsSuccess, closed.Message);
    }
}
