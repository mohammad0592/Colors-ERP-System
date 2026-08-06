using Colors.Application.Features.MaterialIssue;
using Colors.Application.Features.Production;
using Colors.Application.Features.Recycler;
using Colors.Application.Features.Reports;
using Colors.Application.Features.Thermo;
using Colors.Domain.Constants;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Inventory;
using Colors.Infrastructure.Services.MaterialIssue;
using Colors.Infrastructure.Services.Production;
using Colors.Infrastructure.Services.Recycler;
using Colors.Infrastructure.Services.Reports;
using Colors.Infrastructure.Services.Thermo;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// Reports (specification section 13).
///
/// Every figure is read from records that already exist, so these tests build a real
/// shift through the real services and then check the report says what the shift did.
/// A report that agreed with a fixture but not with the factory would be worthless.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ReportsTests(DatabaseFixture fixture)
{
    private static ReportsService NewService(ColorsDbContext db) => new(db);

    /// <summary>
    /// A recipe of 65% GPPS on the base, with talc at 1% on top — the shape of the
    /// factory's own recipes (specification section 5).
    /// </summary>
    private static async Task<RecipeVersion> RecipeAsync(
        ColorsDbContext db,
        FactoryData.Ids ids,
        string suffix)
    {
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
                    CreatedByUserId = ids.UserId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Ingredients =
                    [
                        // The base resin: these two are the 100% everything else is a
                        // share of.
                        new RecipeIngredient
                        {
                            MaterialId = ids.GppsId,
                            IsBaseResin = true,
                            TargetPercentage = 100m,
                            MinPercentage = 100m,
                            MaxPercentage = 100m,
                        },
                        new RecipeIngredient
                        {
                            MaterialId = ids.TalcId,
                            IsBaseResin = false,
                            TargetPercentage = 1m,
                            MinPercentage = 0.5m,
                            MaxPercentage = 1.5m,
                        },
                    ],
                },
            ],
        };

        db.RecipeFamilies.Add(family);
        await db.SaveChangesAsync();

        return family.Versions[0];
    }

    /// <summary>Puts material on the shift through a real ticket, issued and returned.</summary>
    private static async Task IssueAsync(
        ColorsDbContext db,
        FactoryData.Ids ids,
        (int MaterialId, decimal Issued, decimal Returned)[] lines)
    {
        var ledger = new StockLedger(db, TimeProvider.System);
        foreach (var line in lines.DistinctBy(l => l.MaterialId))
        {
            await ledger.PostAsync(
                line.MaterialId, MovementTypeNames.Receive, 10_000m, ids.UserId, "opening");
        }

        var service = new MaterialIssueService(db, ledger, TimeProvider.System);

        var ticket = await service.CreateAsync(
            new CreateIssueTicketRequest(
                ids.ShiftLineId,
                null,
                lines.Select(l => new IssueLineRequest(l.MaterialId, l.Issued)).ToList()),
            ids.UserId);

        Assert.True(ticket.IsSuccess, ticket.Message);

        var returns = lines
            .Where(l => l.Returned > 0)
            .Select(l => new ReturnLineRequest(l.MaterialId, l.Returned))
            .ToList();

        if (returns.Count > 0)
        {
            var recorded = await service.RecordReturnsAsync(
                ticket.Value!.Id, new RecordReturnsRequest(returns), ids.UserId);

            Assert.True(recorded.IsSuccess, recorded.Message);
        }

        var closed = await service.CloseAsync(ticket.Value!.Id, ids.UserId);
        Assert.True(closed.IsSuccess, closed.Message);
    }

    /// <summary>A roll made to the given recipe, weighed, formed and counted.</summary>
    private static async Task FormedRollAsync(
        ColorsDbContext db,
        FactoryData.Ids ids,
        RecipeVersion recipe,
        decimal rollWeight,
        int bagCount,
        decimal pieceWeight)
    {
        var colour = await TestSequences.ColourAsync(db);

        var production = new ProductionService(
            db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);
        var thermo = new ThermoService(
            db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

        var roll = await production.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipe.Id, colour.Id, null, null),
            ids.UserId);
        Assert.True(roll.IsSuccess, roll.Message);

        var measured = await production.SaveTestReportAsync(
            roll.Value!.Id,
            new SaveRollTestRequest(rollWeight, 1200m, 9m, 1.2m, 1.2m, 1.2m, 1.2m, null),
            ids.UserId);

        // Loud, because a roll weight outside the 50–150 kg range is refused here and a
        // silent failure would surface much later as a puzzling report.
        Assert.True(measured.IsSuccess, measured.Message);

        var run = await thermo.StartRunAsync(
            new StartThermoRunRequest(null, roll.Value.Id, ids.ThermoShiftLineId, null, null),
            ids.UserId);
        Assert.True(run.IsSuccess, run.Message);

        await thermo.FinishRunAsync(
            run.Value!.Id, new FinishThermoRunRequest(run.Value.StartedAt.AddMinutes(45)));

        var counted = await thermo.SaveTestReportAsync(
            run.Value.Id,
            new SaveThermoTestRequest(bagCount, pieceWeight, 10m, 0m, null),
            ids.UserId);

        Assert.True(counted.IsSuccess, counted.Message);
    }

    [Fact]
    public async Task The_waste_report_holds_what_was_used_against_the_recipe()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RPT1");
        var recipe = await RecipeAsync(db, ids, "RPT1");

        // 1,000 kg of GPPS actually used, and 12 kg of talc where 1% of 1,000 is 10.
        await IssueAsync(db, ids, [
            (ids.GppsId, 1200m, 200m),
            (ids.TalcId, 15m, 3m),
        ]);

        await FormedRollAsync(db, ids, recipe, 95m, 10, 10m);

        var report = await NewService(db).GetMaterialWasteAsync(ids.ShiftReportId);
        Assert.True(report.IsSuccess, report.Message);

        // One recipe on the shift, so there is a requirement to hold the usage against.
        Assert.Equal(1, report.Value!.RecipeCount);
        Assert.Equal(recipe.RecipeNumber, report.Value.RecipeNumber);

        // The base resin is what the percentages are shares of, read off the recipe's
        // own flag rather than any material's name.
        Assert.Equal(1000m, report.Value.ResinUsed);

        var gpps = report.Value.Lines.First(l => l.MaterialId == ids.GppsId);
        Assert.True(gpps.IsBaseResin);
        Assert.Equal(1200m, gpps.Issued);
        Assert.Equal(200m, gpps.Returned);
        Assert.Equal(1000m, gpps.NetUsed);

        var talc = report.Value.Lines.First(l => l.MaterialId == ids.TalcId);
        Assert.Equal(12m, talc.NetUsed);

        // 1% of the 1,000 kg of resin is 10 kg. Two more went in than the recipe asks.
        Assert.Equal(10m, talc.Required);
        Assert.Equal(2m, talc.Difference);
        Assert.Equal(20m, talc.DifferencePercentage);

        // 1.2% used against a 0.5–1.5% range, so it is inside it — over the target is
        // not the same as out of range, and the report says which.
        Assert.False(talc.OutsideRange);
    }

    [Fact]
    public async Task A_material_used_outside_its_range_is_marked()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RPT2");
        var recipe = await RecipeAsync(db, ids, "RPT2");

        // 20 kg of talc on 1,000 kg of resin is 2% — the supervisor allows up to 1.5%.
        await IssueAsync(db, ids, [
            (ids.GppsId, 1000m, 0m),
            (ids.TalcId, 20m, 0m),
        ]);

        await FormedRollAsync(db, ids, recipe, 95m, 10, 10m);

        var report = await NewService(db).GetMaterialWasteAsync(ids.ShiftReportId);
        var talc = report.Value!.Lines.First(l => l.MaterialId == ids.TalcId);

        Assert.True(talc.OutsideRange);
    }

    [Fact]
    public async Task Two_recipes_in_one_shift_leave_the_requirement_empty()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RPT3");
        var first = await RecipeAsync(db, ids, "RPT3a");
        var second = await RecipeAsync(db, ids, "RPT3b");

        await IssueAsync(db, ids, [(ids.GppsId, 1000m, 0m), (ids.TalcId, 10m, 0m)]);

        await FormedRollAsync(db, ids, first, 95m, 10, 10m);
        await FormedRollAsync(db, ids, second, 95m, 10, 10m);

        var report = await NewService(db).GetMaterialWasteAsync(ids.ShiftReportId);

        // Two sets of percentages and no way to say which the material was for. The
        // usage is still shown; the requirement is left empty rather than averaged.
        Assert.Equal(2, report.Value!.RecipeCount);
        Assert.Null(report.Value.RecipeNumber);
        Assert.All(report.Value.Lines, l => Assert.Null(l.Required));

        // What was used is a fact either way.
        Assert.Equal(1000m, report.Value.Lines.First(l => l.MaterialId == ids.GppsId).NetUsed);
    }

    [Fact]
    public async Task A_material_the_recipe_asks_for_and_nobody_issued_still_shows()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RPT4");
        var recipe = await RecipeAsync(db, ids, "RPT4");

        // Only the resin went out. The talc the recipe asks for never left the store.
        await IssueAsync(db, ids, [(ids.GppsId, 1000m, 0m)]);
        await FormedRollAsync(db, ids, recipe, 95m, 10, 10m);

        var report = await NewService(db).GetMaterialWasteAsync(ids.ShiftReportId);
        var talc = report.Value!.Lines.First(l => l.MaterialId == ids.TalcId);

        // A hole is exactly what this report exists to show, so it is a row reading
        // zero against ten rather than a row that is simply absent.
        Assert.Equal(0m, talc.NetUsed);
        Assert.Equal(10m, talc.Required);
        Assert.Equal(-10m, talc.Difference);
    }

    [Fact]
    public async Task The_shift_summary_uses_each_rolls_own_plate_weight()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RPT5");
        var recipe = await RecipeAsync(db, ids, "RPT5");

        // Two rolls of the same product, measured at different plate weights — the one
        // thing section 13 says to watch, because a shared figure would rewrite the loss.
        // 100 kg roll → 5,000 plates at 10 g = 50 kg product, 50 kg lost.
        await FormedRollAsync(db, ids, recipe, 100m, 10, 10m);
        // 100 kg roll → 5,000 plates at 8 g = 40 kg product, 60 kg lost.
        await FormedRollAsync(db, ids, recipe, 100m, 10, 8m);

        var report = await NewService(db).GetShiftSummaryAsync(ids.ShiftReportId);
        Assert.True(report.IsSuccess, report.Message);

        Assert.Equal(2, report.Value!.RollsFormed);
        Assert.Equal(200m, report.Value.RollWeightUsed);
        Assert.Equal(20, report.Value.BagCount);
        Assert.Equal(10_000, report.Value.PieceCount);

        // 50 + 40, not 10,000 × one shared plate weight.
        Assert.Equal(90m, report.Value.ProductWeight);
        Assert.Equal(110m, report.Value.LossWeight);
        Assert.Equal(55m, report.Value.LossPercentage);
    }

    [Fact]
    public async Task The_shift_summary_breaks_down_by_product()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RPT6");
        var recipe = await RecipeAsync(db, ids, "RPT6");

        await FormedRollAsync(db, ids, recipe, 100m, 10, 10m);

        var report = await NewService(db).GetShiftSummaryAsync(ids.ShiftReportId);

        var product = Assert.Single(report.Value!.Products);
        Assert.Equal(1, product.RollsUsed);
        Assert.Equal(100m, product.RollWeightUsed);
        Assert.Equal(50m, product.ProductWeight);
        Assert.Equal(50m, product.LossWeight);
        Assert.Equal(50m, product.LossPercentage);
    }

    [Fact]
    public async Task The_shift_summary_carries_the_packing_and_recycling_sides()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RPT7");

        // Only one material may be the recycled output, and the suite shares a database.
        var material = await db.Materials.FirstOrDefaultAsync(m => m.IsRecycledOutput);
        if (material is null)
        {
            var category = await db.MaterialCategories.FirstAsync(c => c.IssuedOnTickets);
            var unit = await db.Units.FirstAsync(u => u.Name == "Kilogram");
            material = new Domain.Entities.MasterData.Material
            {
                Code = "RRPT7",
                Name = "Recycled Material",
                CategoryId = category.Id,
                BaseUnitId = unit.Id,
                MinQuantity = 0,
                IsRecycledOutput = true,
            };
            db.Materials.Add(material);
            await db.SaveChangesAsync();
        }

        var line = new Domain.Entities.MasterData.ProductionLine
        {
            Name = "Recycler RPT7",
            Recycles = true,
        };
        db.ProductionLines.Add(line);
        await db.SaveChangesAsync();

        var report = await db.ShiftReports
            .Include(r => r.Lines)
            .FirstAsync(r => r.Id == ids.ShiftReportId);

        var shiftLine = new Domain.Entities.Shifts.ShiftLine { ProductionLineId = line.Id };
        report.Lines.Add(shiftLine);
        await db.SaveChangesAsync();

        await new RecyclerService(db, new StockLedger(db, TimeProvider.System), TimeProvider.System)
            .SaveAsync(new SaveRecyclerProductionRequest(shiftLine.Id, 175m, null), ids.UserId);

        var summary = await NewService(db).GetShiftSummaryAsync(ids.ShiftReportId);

        Assert.Equal(175m, summary.Value!.RecycledMaterialProduced);
    }

    [Fact]
    public async Task A_shift_that_did_nothing_reports_nothing_rather_than_failing()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RPT8");

        var waste = await NewService(db).GetMaterialWasteAsync(ids.ShiftReportId);
        var summary = await NewService(db).GetShiftSummaryAsync(ids.ShiftReportId);

        Assert.True(waste.IsSuccess, waste.Message);
        Assert.Empty(waste.Value!.Lines);
        Assert.Equal(0, waste.Value.RecipeCount);

        Assert.True(summary.IsSuccess, summary.Message);
        Assert.Empty(summary.Value!.Products);

        // Not zero percent, which would read as a shift that wasted nothing.
        Assert.Null(summary.Value.LossPercentage);
    }

    /// <summary>The day the factory this test built worked on.</summary>
    private static async Task<DateOnly> DayOfAsync(ColorsDbContext db, FactoryData.Ids ids) =>
        await db.ShiftReports
            .Where(r => r.Id == ids.ShiftReportId)
            .Select(r => r.ProductionDate)
            .FirstAsync();

    [Fact]
    public async Task Consumption_by_shift_shows_what_that_shift_used()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RPT9");
        var recipe = await RecipeAsync(db, ids, "RPT9");

        await IssueAsync(db, ids, [(ids.GppsId, 300m, 100m), (ids.TalcId, 6m, 1m)]);
        await FormedRollAsync(db, ids, recipe, 100m, 10, 10m);

        var day = await DayOfAsync(db, ids);
        var report = await NewService(db).GetConsumptionAsync(
            day, day, ConsumptionGrouping.Shift);

        Assert.True(report.IsSuccess, report.Message);

        var group = Assert.Single(report.Value!.Groups, g => g.ShiftReportId == ids.ShiftReportId);

        // 200 of GPPS and 5 of talc.
        Assert.Equal(205m, group.TotalUsed);
        Assert.Equal(1, group.Shifts);
        Assert.Equal(1, group.RollsProduced);
        Assert.Equal(100m, group.RollWeightProduced);

        var gpps = group.Materials.First(m => m.MaterialId == ids.GppsId);
        Assert.Equal(300m, gpps.Issued);
        Assert.Equal(100m, gpps.Returned);
        Assert.Equal(200m, gpps.NetUsed);

        // 200 kg of GPPS across 100 kg of roll is 2 kg per kilogram — the figure that
        // lets a long shift and a short one be read against each other.
        Assert.Equal(2m, gpps.PerKilogramOfRoll);
    }

    [Fact]
    public async Task Consumption_by_recipe_adds_its_shifts_together()
    {
        await using var db = fixture.CreateContext();

        var first = await FactoryData.CreateAsync(db, "RPT10a");
        var recipe = await RecipeAsync(db, first, "RPT10");
        await IssueAsync(db, first, [(first.GppsId, 300m, 0m)]);
        await FormedRollAsync(db, first, recipe, 100m, 10, 10m);
        var dayOne = await DayOfAsync(db, first);

        // A second shift on another day, run to the same recipe.
        var second = await FactoryData.CreateAsync(db, "RPT10b");
        await IssueAsync(db, second, [(second.GppsId, 200m, 0m)]);
        await FormedRollAsync(db, second, recipe, 100m, 10, 10m);
        var dayTwo = await DayOfAsync(db, second);

        var report = await NewService(db).GetConsumptionAsync(
            dayOne < dayTwo ? dayOne : dayTwo,
            dayOne > dayTwo ? dayOne : dayTwo,
            ConsumptionGrouping.Recipe);

        var group = Assert.Single(
            report.Value!.Groups, g => g.RecipeNumber == recipe.RecipeNumber);

        // Two shifts, one row: 300 + 200.
        Assert.Equal(2, group.Shifts);
        Assert.Equal(500m, group.TotalUsed);
        Assert.Equal(200m, group.RollWeightProduced);
    }

    [Fact]
    public async Task A_shift_that_switched_recipe_is_left_out_of_the_recipe_report()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RPT11");
        var one = await RecipeAsync(db, ids, "RPT11a");
        var two = await RecipeAsync(db, ids, "RPT11b");

        await IssueAsync(db, ids, [(ids.GppsId, 400m, 0m)]);
        await FormedRollAsync(db, ids, one, 100m, 10, 10m);
        await FormedRollAsync(db, ids, two, 100m, 10, 10m);

        var day = await DayOfAsync(db, ids);

        // By shift it is one plain row — a shift always knows what it used.
        var byShift = await NewService(db).GetConsumptionAsync(
            day, day, ConsumptionGrouping.Shift);

        Assert.Contains(byShift.Value!.Groups, g => g.ShiftReportId == ids.ShiftReportId);

        // By recipe it cannot be attributed to either, so it is left out — and counted,
        // so the reader knows something is missing rather than reading a short total as
        // the whole truth.
        var byRecipe = await NewService(db).GetConsumptionAsync(
            day, day, ConsumptionGrouping.Recipe);

        Assert.DoesNotContain(
            byRecipe.Value!.Groups, g => g.RecipeNumber == one.RecipeNumber);
        Assert.DoesNotContain(
            byRecipe.Value.Groups, g => g.RecipeNumber == two.RecipeNumber);
        Assert.True(byRecipe.Value.MixedRecipeShifts >= 1);

        // And it is left out rather than swept into a nameless row. Every group on a
        // by-recipe report names its recipe, or the report is claiming a total for
        // material it cannot attribute.
        Assert.All(byRecipe.Value.Groups, g => Assert.NotNull(g.RecipeNumber));
    }

    [Fact]
    public async Task A_range_the_wrong_way_round_is_refused()
    {
        await using var db = fixture.CreateContext();

        var report = await NewService(db).GetConsumptionAsync(
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 1),
            ConsumptionGrouping.Shift);

        Assert.False(report.IsSuccess);
    }

    [Fact]
    public async Task A_shift_that_does_not_exist_is_not_found()
    {
        await using var db = fixture.CreateContext();

        var waste = await NewService(db).GetMaterialWasteAsync(999_999);

        Assert.False(waste.IsSuccess);
        Assert.Equal(Application.Common.Models.ErrorCode.NotFound, waste.ErrorCode);
    }
}
