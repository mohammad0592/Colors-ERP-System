using ClosedXML.Excel;
using Colors.Application.Features.Production;
using Colors.Application.Features.Thermo;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Reports;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Production;
using Colors.Infrastructure.Services.Reports;
using Colors.Infrastructure.Services.Thermo;
using Colors.IntegrationTests.Common;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// The thermoforming report for one shift, on screen and as the factory's Excel form
/// (specification section 13).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ThermoShiftReportTests(DatabaseFixture fixture)
{
    private static ThermoService NewThermo(ColorsDbContext db) =>
        new(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

    private static ProductionService NewProduction(ColorsDbContext db) =>
        new(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

    /// <summary>A roll made, measured, formed and — unless told not to — counted.</summary>
    private static async Task FormedRollAsync(
        ColorsDbContext db,
        FactoryData.Ids ids,
        string suffix,
        bool count = true)
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

        var production = NewProduction(db);
        var roll = await production.CreateRollAsync(
            new CreateRollRequest(
                ids.ShiftLineId, family.Versions[0].Id, colour.Id, ids.NormalProductId, null, null),
            ids.UserId);
        Assert.True(roll.IsSuccess, roll.Message);

        await production.MeasureRollAsync(roll.Value!.Id, new MeasureRollRequest(95m, 1200m), ids.UserId);
        var measured = await production.SaveTestReportAsync(
            roll.Value!.Id,
            new SaveRollTestRequest(9m, 1.2m, 1.2m, 1.2m, 1.2m, null),
            ids.UserId);
        Assert.True(measured.IsSuccess, measured.Message);

        var thermo = NewThermo(db);
        var started = await thermo.StartRunAsync(
            new StartThermoRunRequest(measured.Value!.Barcode, null, ids.ThermoShiftLineId, null, null),
            ids.UserId);
        Assert.True(started.IsSuccess, started.Message);

        if (!count)
        {
            return;
        }

        var finished = await thermo.FinishRunAsync(
            started.Value!.Id,
            new FinishThermoRunRequest(started.Value.StartedAt.AddMinutes(43)));
        Assert.True(finished.IsSuccess, finished.Message);

        var counted = await thermo.SaveTestReportAsync(
            started.Value.Id,
            new SaveThermoTestRequest(14, 4.2m, 10.5m, 0m, null),
            ids.UserId);
        Assert.True(counted.IsSuccess, counted.Message);
    }

    [Fact]
    public async Task The_report_lists_every_roll_but_totals_only_the_counted_ones()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "TSR1");
        await FormedRollAsync(db, ids, "TSR1a");
        await FormedRollAsync(db, ids, "TSR1b");
        // Still in the machine when the report is read.
        await FormedRollAsync(db, ids, "TSR1c", count: false);

        var result = await new ThermoShiftReportService(db).GetAsync(ids.ShiftReportId);

        Assert.True(result.IsSuccess, result.Message);
        var report = result.Value!;
        Assert.Equal(3, report.Rolls.Count);
        Assert.Equal(2, report.Rolls.Count(r => r.BagCount is not null));

        // Every roll that went in counts towards the weight, as the form adds them. The
        // one still forming has nothing counted, so it stays out of the bags, the plates
        // and the yield — or the shift would read as having lost a whole roll.
        Assert.Equal(285m, report.TotalRollWeight);
        Assert.Equal(28, report.TotalBagCount);
        Assert.Equal(28 * 500, report.TotalPieceCount);

        var product = Assert.Single(report.Products);
        Assert.Equal(190m, product.RollWeight);
        // 14,000 plates at 4.2 g is 58.8 kg of product from 190 kg of roll.
        Assert.Equal(58.8m, product.ProductWeight);
        Assert.Equal(131.2m, product.ScrapWeight);
    }

    [Fact]
    public async Task The_excel_is_the_factory_form_filled_in()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "TSR2");
        await FormedRollAsync(db, ids, "TSR2a");

        var report = (await new ThermoShiftReportService(db).GetAsync(ids.ShiftReportId)).Value!;
        var bytes = new ThermoShiftReportExcel().Build(report);

        using var book = new XLWorkbook(new MemoryStream(bytes));
        var sheet = book.Worksheet(1);

        // The form's own words are still there; the figures sit where the paper has them.
        Assert.Equal("عدد المشاتيح المنتجة", sheet.Cell("A24").GetString());
        Assert.StartsWith(report.Rolls[0].RollCode, sheet.Cell("A13").GetString());
        Assert.Equal(14d, sheet.Cell("G13").GetDouble());
        Assert.Equal("G13*500", sheet.Cell("H13").FormulaA1);
        Assert.Equal("SUM(B13:B21)", sheet.Cell("B23").FormulaA1);
        Assert.Equal(report.Products[0].ProductName, sheet.Cell("J6").GetString());
        Assert.Equal(report.ShiftName, sheet.Cell("H3").GetString());
    }

    [Fact]
    public async Task A_busy_shift_gets_more_rows_rather_than_being_cut_short()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "TSR3");

        // The form has room for nine.
        for (var i = 0; i < 11; i++)
        {
            await FormedRollAsync(db, ids, $"TSR3-{i}");
        }

        var report = (await new ThermoShiftReportService(db).GetAsync(ids.ShiftReportId)).Value!;
        using var book = new XLWorkbook(new MemoryStream(new ThermoShiftReportExcel().Build(report)));
        var sheet = book.Worksheet(1);

        Assert.False(sheet.Cell("A23").IsEmpty());
        Assert.Equal("SUM(B13:B23)", sheet.Cell("B25").FormulaA1);
        // Everything below moved down by two, its words with it.
        Assert.Equal("عدد المشاتيح المنتجة", sheet.Cell("A26").GetString());
        Assert.True(sheet.Cell("K23").IsMerged());
    }
}
