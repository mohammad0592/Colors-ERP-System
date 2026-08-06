using Colors.Application.Features.Production;
using Colors.Application.Features.Thermo;
using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Production;
using Colors.Infrastructure.Services.Thermo;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// Line 2 — forming, counting and the bags that come out (specification section 9).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ThermoTests(DatabaseFixture fixture)
{
    private static ThermoService NewService(ColorsDbContext db) =>
        new(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

    private static ProductionService NewProduction(ColorsDbContext db) =>
        new(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

    /// <summary>
    /// A roll off the extruder, measured, and therefore ready for the thermo. Built
    /// through the real service so the test stands on the same path the factory uses.
    /// </summary>
    private static async Task<RollDto> AvailableRollAsync(
        ColorsDbContext db,
        FactoryData.Ids ids,
        string suffix,
        bool absorbent = false)
    {
        var colour = await TestSequences.ColourAsync(db);
        var productType = await db.ProductTypes.FirstAsync();

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
                    CreatedByUserId = ids.UserId,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
            ],
        };
        db.RecipeFamilies.Add(family);
        await db.SaveChangesAsync();

        var production = NewProduction(db);

        var roll = await production.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, family.Versions[0].Id, colour.Id, null, null),
            ids.UserId);
        Assert.True(roll.IsSuccess, roll.Message);

        var measured = await production.SaveTestReportAsync(
            roll.Value!.Id,
            new SaveRollTestRequest(95m, 1200m, 9m, 1.2m, 1.2m, 1.2m, 1.2m, null),
            ids.UserId);
        Assert.True(measured.IsSuccess, measured.Message);

        return measured.Value!;
    }

    /// <summary>A run started and finished, ready to be counted.</summary>
    private static async Task<ThermoRunDto> FinishedRunAsync(
        ColorsDbContext db,
        FactoryData.Ids ids,
        string suffix,
        bool absorbent = false)
    {
        var roll = await AvailableRollAsync(db, ids, suffix, absorbent);
        var service = NewService(db);

        var started = await service.StartRunAsync(
            new StartThermoRunRequest(roll.Barcode, null, ids.ThermoShiftLineId, null, null),
            ids.UserId);
        Assert.True(started.IsSuccess, started.Message);

        var finished = await service.FinishRunAsync(
            started.Value!.Id,
            new FinishThermoRunRequest(started.Value.StartedAt.AddMinutes(50)));
        Assert.True(finished.IsSuccess, finished.Message);

        return finished.Value!;
    }

    [Fact]
    public async Task Scanning_a_roll_starts_a_run_and_takes_it_out_of_stock()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR1");
        var roll = await AvailableRollAsync(db, ids, "THR1");

        // The operator scans. He never types the roll number, recipe, colour or product.
        var run = await NewService(db).StartRunAsync(
            new StartThermoRunRequest(roll.Barcode, null, ids.ThermoShiftLineId, null, null),
            ids.UserId);

        Assert.True(run.IsSuccess, run.Message);
        Assert.Equal(roll.RollCode, run.Value!.RollCode);

        var saved = await db.Rolls.AsNoTracking().FirstAsync(r => r.Id == roll.Id);
        Assert.Equal(RollStatus.InThermo, saved.Status);
    }

    [Fact]
    public async Task A_roll_that_has_not_been_measured_cannot_be_formed()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR2");
        var roll = await AvailableRollAsync(db, ids, "THR2");

        var entity = await db.Rolls.FirstAsync(r => r.Id == roll.Id);
        entity.Status = RollStatus.NeedsTest;
        await db.SaveChangesAsync();

        // Once the roll is formed into plates there is nothing left to measure.
        var run = await NewService(db).StartRunAsync(
            new StartThermoRunRequest(roll.Barcode, null, ids.ThermoShiftLineId, null, null),
            ids.UserId);

        Assert.False(run.IsSuccess);
        Assert.Contains("measured", run.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_roll_is_formed_once()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR3");
        var roll = await AvailableRollAsync(db, ids, "THR3");
        var service = NewService(db);

        await service.StartRunAsync(
            new StartThermoRunRequest(roll.Barcode, null, ids.ThermoShiftLineId, null, null),
            ids.UserId);

        var again = await service.StartRunAsync(
            new StartThermoRunRequest(roll.Barcode, null, ids.ThermoShiftLineId, null, null),
            ids.UserId);

        Assert.False(again.IsSuccess);
    }

    [Fact]
    public async Task A_line_that_does_not_form_bags_refuses_the_run()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR4");
        var roll = await AvailableRollAsync(db, ids, "THR4");

        // The extruder line, which has no mould and does not form anything.
        var run = await NewService(db).StartRunAsync(
            new StartThermoRunRequest(roll.Barcode, null, ids.ShiftLineId, null, null),
            ids.UserId);

        Assert.False(run.IsSuccess);
        Assert.Contains("does not form bags", run.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_line_with_no_mould_refuses_the_run()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR5");
        var roll = await AvailableRollAsync(db, ids, "THR5");

        var thermoLine = await db.ShiftLines.FirstAsync(l => l.Id == ids.ThermoShiftLineId);
        thermoLine.MouldId = null;
        await db.SaveChangesAsync();

        // Without a mould there is no way to know what is being made, and the product is
        // never typed. Better to say so now than at the end of the run.
        var run = await NewService(db).StartRunAsync(
            new StartThermoRunRequest(roll.Barcode, null, ids.ThermoShiftLineId, null, null),
            ids.UserId);

        Assert.False(run.IsSuccess);
        Assert.Contains("mould", run.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_total_time_is_worked_out_from_the_two_timestamps()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR6");
        var run = await FinishedRunAsync(db, ids, "THR6");

        // الزمن الكلي on the paper form. Never stored, so it can never disagree.
        Assert.Equal(50, run.TotalTimeMinutes);
    }

    [Fact]
    public async Task Saving_the_counts_creates_a_bag_each_with_its_own_barcode()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR7");
        var run = await FinishedRunAsync(db, ids, "THR7");

        var counted = await NewService(db).SaveTestReportAsync(
            run.Id,
            new SaveThermoTestRequest(14, 4.2m, 10.5m, 0m, null),
            ids.UserId);

        Assert.True(counted.IsSuccess, counted.Message);
        Assert.Equal(14, counted.Value!.Bags.Count);

        // A bag nobody can scan cannot go on a pallet, so the labels and the bags are
        // one act.
        Assert.All(counted.Value.Bags, bag =>
            Assert.StartsWith("B", bag.Barcode, StringComparison.Ordinal));

        Assert.Equal(14, counted.Value.Bags.Select(b => b.Barcode).Distinct().Count());
    }

    [Fact]
    public async Task The_piece_count_is_the_bags_times_the_product()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR8");
        var run = await FinishedRunAsync(db, ids, "THR8");

        var counted = await NewService(db).SaveTestReportAsync(
            run.Id,
            new SaveThermoTestRequest(14, 4.2m, 10.5m, 0m, null),
            ids.UserId);

        // The real form: 14 bags → 7000 plates, at 500 to a bag.
        Assert.Equal(7000, counted.Value!.TestReport!.PieceCount);
        Assert.All(counted.Value.Bags, bag => Assert.Equal(500, bag.PieceCount));
    }

    [Fact]
    public async Task The_piece_count_does_not_change_when_the_product_is_edited_later()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR9");
        var run = await FinishedRunAsync(db, ids, "THR9");

        await NewService(db).SaveTestReportAsync(
            run.Id,
            new SaveThermoTestRequest(10, 4.2m, 10.5m, 0m, null),
            ids.UserId);

        // Somebody corrects the product in Master Data next year.
        var product = await db.Products.FirstAsync(p => p.Id == ids.NormalProductId);
        product.PiecesPerBag = 480;
        await db.SaveChangesAsync();

        var again = await NewService(db).GetRunAsync(run.Id);

        // History must still say what it said. This is why the count is stored and not
        // worked out live (specification section 0.1).
        Assert.Equal(5000, again.Value!.TestReport!.PieceCount);
    }

    [Fact]
    public async Task Nobody_chooses_the_product_the_mould_and_the_recipe_do()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR10");

        var normalRun = await FinishedRunAsync(db, ids, "THR10a");
        var normal = await NewService(db).SaveTestReportAsync(
            normalRun.Id, new SaveThermoTestRequest(5, 4m, 10m, 0m, null), ids.UserId);

        var absorbentRun = await FinishedRunAsync(db, ids, "THR10b", absorbent: true);
        var absorbent = await NewService(db).SaveTestReportAsync(
            absorbentRun.Id, new SaveThermoTestRequest(5, 4m, 10m, 12.5m, null), ids.UserId);

        // Same mould, two products. Which one comes out is what was mixed into the roll.
        Assert.Equal(ids.NormalProductId, normal.Value!.TestReport!.ProductId);
        Assert.Equal(ids.AbsorbentProductId, absorbent.Value!.TestReport!.ProductId);
    }

    [Fact]
    public async Task An_absorbency_on_a_normal_roll_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR11");
        var run = await FinishedRunAsync(db, ids, "THR11");

        // Absorbency comes from what was mixed, so a Normal roll cannot have absorbed
        // anything — this is somebody filling in the wrong row.
        var counted = await NewService(db).SaveTestReportAsync(
            run.Id,
            new SaveThermoTestRequest(10, 4.2m, 10.5m, 12.5m, null),
            ids.UserId);

        Assert.False(counted.IsSuccess);
        Assert.Contains("not absorbent", counted.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_mould_with_no_product_for_this_material_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR12");
        var run = await FinishedRunAsync(db, ids, "THR12", absorbent: true);

        // The factory stops making absorbent plates on this mould.
        var product = await db.Products.FirstAsync(p => p.Id == ids.AbsorbentProductId);
        db.Products.Remove(product);
        await db.SaveChangesAsync();

        // Refused plainly, rather than quietly producing bags marked as something the
        // factory does not make.
        var counted = await NewService(db).SaveTestReportAsync(
            run.Id,
            new SaveThermoTestRequest(5, 4m, 10m, 12.5m, null),
            ids.UserId);

        Assert.False(counted.IsSuccess);
        Assert.Contains("does not make", counted.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_roll_still_in_the_machine_cannot_be_counted()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR13");
        var roll = await AvailableRollAsync(db, ids, "THR13");
        var service = NewService(db);

        var started = await service.StartRunAsync(
            new StartThermoRunRequest(roll.Barcode, null, ids.ThermoShiftLineId, null, null),
            ids.UserId);

        // The bags are counted at the end of the run, so the run has to have ended.
        var counted = await service.SaveTestReportAsync(
            started.Value!.Id,
            new SaveThermoTestRequest(10, 4.2m, 10.5m, 0m, null),
            ids.UserId);

        Assert.False(counted.IsSuccess);
        Assert.Contains("still in the machine", counted.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Finishing_a_run_marks_the_roll_processed()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR14");
        var run = await FinishedRunAsync(db, ids, "THR14");

        var roll = await db.Rolls.AsNoTracking().FirstAsync(r => r.Id == run.RollId);
        Assert.Equal(RollStatus.Processed, roll.Status);
    }

    [Fact]
    public async Task A_run_is_counted_once()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR15");
        var run = await FinishedRunAsync(db, ids, "THR15");
        var request = new SaveThermoTestRequest(6, 4m, 10m, 0m, null);

        await NewService(db).SaveTestReportAsync(run.Id, request, ids.UserId);
        var again = await NewService(db).SaveTestReportAsync(run.Id, request, ids.UserId);

        Assert.False(again.IsSuccess);
    }

    [Fact]
    public async Task The_rolls_own_measurements_are_shown_not_asked_for_again()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR16");
        var run = await FinishedRunAsync(db, ids, "THR16");

        // They are on the paper form in this position, so the man expects to see them —
        // but they already exist against the roll and are never re-entered.
        Assert.NotNull(run.RollReadings);
        Assert.Equal(95m, run.RollReadings.Weight);
        Assert.Equal(1.2m, run.RollReadings.AverageThickness);
    }

    [Fact]
    public async Task The_run_says_what_it_threw_away()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR16b");
        var run = await FinishedRunAsync(db, ids, "THR16b");
        var service = NewService(db);

        // Finished but not counted: the plates are not known yet, so neither is the
        // scrap. A dash on the screen, never a zero — zero would read as a run that
        // wasted nothing.
        Assert.Null(run.ScrapWeight);

        // 10 bags of 500 plates at 10 g each = 5,000 plates = 50 kg of product, out of
        // a 95 kg roll.
        var counted = await service.SaveTestReportAsync(
            run.Id, new SaveThermoTestRequest(10, 10m, 10m, 0m, null), ids.UserId);

        Assert.True(counted.IsSuccess, counted.Message);
        Assert.Equal(45m, counted.Value!.ScrapWeight);

        // And it is the thermo's own fact, so it comes back on the list the operator
        // reads — not only on a screen belonging to the recycler
        // (specification section 9).
        var listed = (await service.GetRunsAsync(ids.ThermoShiftLineId))
            .First(r => r.Id == run.Id);

        Assert.Equal(45m, listed.ScrapWeight);

        // The roll's weight comes with it, so the share can be worked out for display:
        // 45 of 95 kg. A share of the ROLL, unlike the recycler's loss.
        Assert.Equal(95m, listed.RollWeight);
    }

    [Fact]
    public async Task A_run_whose_roll_was_never_weighed_has_no_waste_figure()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR16c");
        var run = await FinishedRunAsync(db, ids, "THR16c");
        var service = NewService(db);

        await service.SaveTestReportAsync(
            run.Id, new SaveThermoTestRequest(10, 10m, 10m, 0m, null), ids.UserId);

        // Take the roll's test away. Without a roll weight there is nothing to subtract
        // from, and a run that cannot say what it lost must not claim it lost nothing.
        var rollTest = await db.RollTestReports.FirstAsync(t => t.RollId == run.RollId);
        db.RollTestReports.Remove(rollTest);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reloaded = await service.GetRunAsync(run.Id);

        Assert.True(reloaded.IsSuccess, reloaded.Message);
        Assert.Null(reloaded.Value!.ScrapWeight);
    }

    [Fact]
    public async Task A_formed_roll_leaves_the_available_list()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR17");
        var roll = await AvailableRollAsync(db, ids, "THR17");
        var service = NewService(db);

        Assert.Contains(await service.GetAvailableRollsAsync(), r => r.Id == roll.Id);

        await service.StartRunAsync(
            new StartThermoRunRequest(roll.Barcode, null, ids.ThermoShiftLineId, null, null),
            ids.UserId);

        Assert.DoesNotContain(await service.GetAvailableRollsAsync(), r => r.Id == roll.Id);
    }

    [Fact]
    public async Task A_label_from_another_factory_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR18");

        var run = await NewService(db).StartRunAsync(
            new StartThermoRunRequest("R999999", null, ids.ThermoShiftLineId, null, null),
            ids.UserId);

        Assert.False(run.IsSuccess);
    }

    [Fact]
    public async Task A_bag_label_scanned_into_the_roll_box_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR21");
        var run = await FinishedRunAsync(db, ids, "THR21");

        var counted = await NewService(db).SaveTestReportAsync(
            run.Id, new SaveThermoTestRequest(3, 4m, 10m, 0m, null), ids.UserId);

        var bagLabel = counted.Value!.Bags[0].Barcode;

        var roll = await AvailableRollAsync(db, ids, "THR21b");
        Assert.NotNull(roll);

        // A wrong kind of label comes back from the barcode table as a *successful*
        // lookup carrying "that is a bag, not a roll". Without checking the type, a bag
        // whose id happened to match a roll's would quietly be formed instead.
        var started = await NewService(db).StartRunAsync(
            new StartThermoRunRequest(bagLabel, null, ids.ThermoShiftLineId, null, null),
            ids.UserId);

        Assert.False(started.IsSuccess);
        Assert.Contains("not a roll", started.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_database_refuses_a_second_run_for_the_same_roll()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "THR19");
        var roll = await AvailableRollAsync(db, ids, "THR19");

        await NewService(db).StartRunAsync(
            new StartThermoRunRequest(roll.Barcode, null, ids.ThermoShiftLineId, null, null),
            ids.UserId);

        // Written straight to the table, past every check in the service and past EF's
        // own one-to-one tracking. A roll goes in whole and is never split, so the
        // unique index is the backstop that no code path can evade — and only raw SQL
        // gets close enough to prove it is really there.
        var duplicate = () => db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "ThermoProductions"
                 ("RollId", "ShiftLineId", "OperatorUserId", "StartedAt")
             VALUES ({roll.Id}, {ids.ThermoShiftLineId}, {ids.UserId}, NOW())
             """);

        await Assert.ThrowsAsync<Npgsql.PostgresException>(duplicate);
    }

}
