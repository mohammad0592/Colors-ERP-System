using Colors.Application.Features.Production;
using Colors.Application.Features.Recycler;
using Colors.Application.Features.Thermo;
using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Inventory;
using Colors.Infrastructure.Services.Production;
using Colors.Infrastructure.Services.Recycler;
using Colors.Infrastructure.Services.Thermo;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// The recycler (specification section 11).
///
/// Two weights and one movement. The interesting parts are the ones the factory chose:
/// more may come out than went in, and the loss is worked out rather than stored.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RecyclerTests(DatabaseFixture fixture)
{
    private static RecyclerService NewService(ColorsDbContext db) =>
        new(db, new StockLedger(db, TimeProvider.System), TimeProvider.System);

    /// <summary>
    /// A recycling line on this shift, plus the material its output goes into.
    ///
    /// Only one material in the database may be the recycled output, and the suite
    /// shares one, so an existing one is reused rather than a second created — which the
    /// unique index would refuse anyway.
    /// </summary>
    private static async Task<(int ShiftLineId, int MaterialId)> RecyclerLineAsync(
        ColorsDbContext db,
        FactoryData.Ids ids,
        string suffix)
    {
        var line = new ProductionLine { Name = $"Recycler {suffix}", Recycles = true };
        db.ProductionLines.Add(line);
        await db.SaveChangesAsync();

        var report = await db.ShiftReports
            .Include(r => r.Lines)
            .FirstAsync(r => r.Id == ids.ShiftReportId);

        var shiftLine = new Domain.Entities.Shifts.ShiftLine { ProductionLineId = line.Id };
        report.Lines.Add(shiftLine);
        await db.SaveChangesAsync();

        var material = await db.Materials.FirstOrDefaultAsync(m => m.IsRecycledOutput);
        if (material is null)
        {
            var category = await db.MaterialCategories.FirstAsync(c => c.IssuedOnTickets);
            var unit = await db.Units.FirstAsync(u => u.Name == "Kilogram");

            material = new Material
            {
                Code = $"R{suffix}",
                Name = "Recycled Material",
                CategoryId = category.Id,
                BaseUnitId = unit.Id,
                MinQuantity = 0,
                IsRecycledOutput = true,
            };
            db.Materials.Add(material);
            await db.SaveChangesAsync();
        }

        return (shiftLine.Id, material.Id);
    }

    private static async Task<decimal> StockAsync(ColorsDbContext db, int materialId) =>
        await db.MaterialInventory
            .Where(i => i.MaterialId == materialId)
            .Select(i => i.CurrentQuantity)
            .FirstOrDefaultAsync();

    [Fact]
    public async Task Recording_the_scrap_adds_the_output_to_the_store()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "REC1");
        var (shiftLineId, materialId) = await RecyclerLineAsync(db, ids, "REC1");

        var before = await StockAsync(db, materialId);

        var saved = await NewService(db).SaveAsync(
            new SaveRecyclerProductionRequest(shiftLineId, 200m, 180m, null), ids.UserId);

        Assert.True(saved.IsSuccess, saved.Message);
        Assert.Equal(200m, saved.Value!.ScrapWeight);
        Assert.Equal(180m, saved.Value.RecycledMaterialWeight);

        // 20 kg lost out of 200.
        Assert.Equal(10m, saved.Value.LossPercentage);

        // Only the output reaches the store. Scrap was never in it.
        Assert.Equal(before + 180m, await StockAsync(db, materialId));
    }

    [Fact]
    public async Task More_can_come_out_than_went_in()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "REC2");
        var (shiftLineId, materialId) = await RecyclerLineAsync(db, ids, "REC2");

        var before = await StockAsync(db, materialId);

        // The recycler ground a pile left from an earlier shift. Refusing this would
        // make the operator write a figure he did not weigh (specification section 11).
        var saved = await NewService(db).SaveAsync(
            new SaveRecyclerProductionRequest(shiftLineId, 200m, 220m, null), ids.UserId);

        Assert.True(saved.IsSuccess, saved.Message);

        // Negative loss is information, not an error: this shift ground more than it
        // collected.
        Assert.Equal(-10m, saved.Value!.LossPercentage);
        Assert.Equal(before + 220m, await StockAsync(db, materialId));
    }

    [Fact]
    public async Task With_no_scrap_weighed_the_loss_is_not_a_number()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "REC3");
        var (shiftLineId, materialId) = await RecyclerLineAsync(db, ids, "REC3");

        var before = await StockAsync(db, materialId);

        var saved = await NewService(db).SaveAsync(
            new SaveRecyclerProductionRequest(shiftLineId, 0m, 150m, null), ids.UserId);

        Assert.True(saved.IsSuccess, saved.Message);

        // Not zero. A share of nothing is not a number, and 0% would read as a perfect
        // shift.
        Assert.Null(saved.Value!.LossPercentage);
        Assert.Equal(before + 150m, await StockAsync(db, materialId));
    }

    [Fact]
    public async Task A_record_where_nothing_was_weighed_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "REC4");
        var (shiftLineId, _) = await RecyclerLineAsync(db, ids, "REC4");

        var saved = await NewService(db).SaveAsync(
            new SaveRecyclerProductionRequest(shiftLineId, 0m, 0m, null), ids.UserId);

        Assert.False(saved.IsSuccess);
        Assert.False(await db.RecyclerProductions.AnyAsync(r => r.ShiftLineId == shiftLineId));
    }

    [Fact]
    public async Task A_negative_weight_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "REC5");
        var (shiftLineId, _) = await RecyclerLineAsync(db, ids, "REC5");

        var saved = await NewService(db).SaveAsync(
            new SaveRecyclerProductionRequest(shiftLineId, 100m, -5m, null), ids.UserId);

        Assert.False(saved.IsSuccess);
    }

    [Fact]
    public async Task Scrap_cannot_be_recorded_on_a_line_that_does_not_recycle()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "REC6");

        // The extruder. It makes rolls; it does not grind them.
        var saved = await NewService(db).SaveAsync(
            new SaveRecyclerProductionRequest(ids.ShiftLineId, 100m, 90m, null), ids.UserId);

        Assert.False(saved.IsSuccess);
        Assert.Contains("does not recycle", saved.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task It_is_written_once_for_the_line()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "REC7");
        var (shiftLineId, materialId) = await RecyclerLineAsync(db, ids, "REC7");
        var service = NewService(db);

        var first = await service.SaveAsync(
            new SaveRecyclerProductionRequest(shiftLineId, 100m, 90m, null), ids.UserId);
        Assert.True(first.IsSuccess, first.Message);

        var after = await StockAsync(db, materialId);

        var second = await service.SaveAsync(
            new SaveRecyclerProductionRequest(shiftLineId, 50m, 40m, null), ids.UserId);

        // A second record would add the same output to the store twice.
        Assert.False(second.IsSuccess);
        Assert.Contains("already been recorded", second.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(after, await StockAsync(db, materialId));
    }

    [Fact]
    public async Task The_database_refuses_a_second_record_for_one_line()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "REC8");
        var (shiftLineId, _) = await RecyclerLineAsync(db, ids, "REC8");

        await NewService(db).SaveAsync(
            new SaveRecyclerProductionRequest(shiftLineId, 100m, 90m, null), ids.UserId);

        // Straight to the table, past every check in the service — two tablets can save
        // in the same moment, so the index is the only real guard.
        var duplicate = () => db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "RecyclerProductions"
                 ("ShiftLineId", "ScrapWeight", "RecycledMaterialWeight",
                  "RecordedByUserId", "RecordedAt")
             VALUES ({shiftLineId}, 10, 9, {ids.UserId}, NOW())
             """);

        await Assert.ThrowsAsync<Npgsql.PostgresException>(duplicate);
    }

    [Fact]
    public async Task Scrap_cannot_be_recorded_on_a_closed_shift()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "REC9");
        var (shiftLineId, _) = await RecyclerLineAsync(db, ids, "REC9");

        var report = await db.ShiftReports.FirstAsync(r => r.Id == ids.ShiftReportId);
        report.Status = ShiftReportStatus.Closed;
        await db.SaveChangesAsync();

        var saved = await NewService(db).SaveAsync(
            new SaveRecyclerProductionRequest(shiftLineId, 100m, 90m, null), ids.UserId);

        Assert.False(saved.IsSuccess);
    }

    [Fact]
    public async Task The_draft_names_the_material_and_says_nothing_about_the_thermo()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "REC10");
        var (shiftLineId, _) = await RecyclerLineAsync(db, ids, "REC10");

        var colour = await TestSequences.ColourAsync(db);
        var productType = await db.ProductTypes.FirstAsync();

        var family = new RecipeFamily
        {
            Name = "Family REC10",
            Code = "N",
            ProductTypeId = productType.Id,
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

        var production = new ProductionService(
            db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);
        var thermo = new ThermoService(
            db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

        var roll = await production.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, family.Versions[0].Id, colour.Id, null, null),
            ids.UserId);

        // A 100 kg roll.
        await production.SaveTestReportAsync(
            roll.Value!.Id,
            new SaveRollTestRequest(100m, 1200m, 9m, 1.2m, 1.2m, 1.2m, 1.2m, null),
            ids.UserId);

        var run = await thermo.StartRunAsync(
            new StartThermoRunRequest(null, roll.Value.Id, ids.ThermoShiftLineId, null, null),
            ids.UserId);

        await thermo.FinishRunAsync(
            run.Value!.Id, new FinishThermoRunRequest(run.Value.StartedAt.AddMinutes(45)));

        // 10 bags of 500 plates at 10 g each = 5,000 plates = 50 kg of product.
        await thermo.SaveTestReportAsync(
            run.Value.Id,
            new SaveThermoTestRequest(10, 10m, 10m, 0m, null),
            ids.UserId);

        var draft = await NewService(db).GetDraftAsync(shiftLineId);

        Assert.True(draft.IsSuccess, draft.Message);
        Assert.False(draft.Value!.AlreadyRecorded);

        // The screen has to name the pile the output goes into.
        Assert.NotNull(draft.Value.RecycledMaterialName);

        // The thermo formed a roll on this shift and lost 50 kg doing it, but that
        // figure is the thermo's and does not come through here. It lives on the thermo's
        // own screens so it is visible on a shift where the recycler never ran
        // (specification section 11) — see ThermoTests.
        Assert.DoesNotContain(
            "Thermo",
            string.Join(" ", typeof(RecyclerDraftDto).GetProperties().Select(p => p.Name)),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_two_percentages_answer_different_questions()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "REC12");
        var (shiftLineId, _) = await RecyclerLineAsync(db, ids, "REC12");

        // The factory's own figures: a 445.5 kg roll that left 171 kg of scrap, and a
        // grinder that turned 123 kg of scrap into 100 kg of material.
        var saved = await NewService(db).SaveAsync(
            new SaveRecyclerProductionRequest(shiftLineId, 123m, 100m, null), ids.UserId);

        Assert.True(saved.IsSuccess, saved.Message);

        // 23 kg lost out of 123 kg ground — a share of the SCRAP.
        Assert.Equal(18.70m, saved.Value!.LossPercentage);

        // Not 38.38%, which is 171 over 445.5 — a share of the ROLL, and a fact about
        // the thermo rather than the recycler (specification section 11). Nothing in
        // this record can produce that number, and it must never be labelled the same.
        Assert.NotEqual(Math.Round(171m / 445.5m * 100m, 2), saved.Value.LossPercentage);
    }

    [Fact]
    public async Task The_draft_shows_what_was_already_recorded()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "REC11");
        var (shiftLineId, _) = await RecyclerLineAsync(db, ids, "REC11");
        var service = NewService(db);

        await service.SaveAsync(
            new SaveRecyclerProductionRequest(shiftLineId, 120m, 100m, "Ground twice"),
            ids.UserId);

        var draft = await service.GetDraftAsync(shiftLineId);

        Assert.True(draft.Value!.AlreadyRecorded);
        Assert.Equal(120m, draft.Value.Recorded!.ScrapWeight);
        Assert.Equal("Ground twice", draft.Value.Recorded.Notes);
        Assert.NotNull(draft.Value.Recorded.RecordedByName);
    }
}
