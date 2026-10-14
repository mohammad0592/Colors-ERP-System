using ClosedXML.Excel;
using Colors.Application.Features.Production;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Reports;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Production;
using Colors.Infrastructure.Services.Reports;
using Colors.IntegrationTests.Common;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// The extruder's report for one shift, on screen and as Excel (specification section 13.1).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ExtruderShiftReportTests(DatabaseFixture fixture)
{
    private static ProductionService NewProduction(ColorsDbContext db) =>
        new(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

    private static async Task<int> RecipeAsync(ColorsDbContext db, FactoryData.Ids ids, string suffix)
    {
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
        return family.Versions[0].Id;
    }

    /// <summary>Three rolls: one finished, one only measured, one only logged.</summary>
    private static async Task<int> ShiftWithRollsAsync(ColorsDbContext db, FactoryData.Ids ids, string suffix)
    {
        var recipeId = await RecipeAsync(db, ids, suffix);
        var colour = await TestSequences.ColourAsync(db);
        var production = NewProduction(db);

        var made = new List<RollDto>();
        for (var i = 0; i < 3; i++)
        {
            var roll = await production.CreateRollAsync(
                new CreateRollRequest(
                    ids.ShiftLineId, recipeId, colour.Id, ids.NormalProductId,
                    DateTimeOffset.UtcNow.AddMinutes(-60 + (i * 20)), i == 0 ? "First of the shift" : null),
                ids.UserId);
            Assert.True(roll.IsSuccess, roll.Message);
            made.Add(roll.Value!);
        }

        await production.MeasureRollAsync(made[0].Id, new MeasureRollRequest(95m, 1200m), ids.UserId);
        await production.SaveTestReportAsync(
            made[0].Id, new SaveRollTestRequest(9.1m, 1.2m, 1.25m, 1.3m, 1.25m, "Edge a little thin"), ids.UserId);
        await production.MeasureRollAsync(made[1].Id, new MeasureRollRequest(90m, 1100m), ids.UserId);

        return ids.ShiftReportId;
    }

    [Fact]
    public async Task Every_roll_of_the_shift_is_listed_with_everything_recorded_against_it()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ESR1");
        var shiftReportId = await ShiftWithRollsAsync(db, ids, "ESR1");

        var result = await new ExtruderShiftReportService(db).GetAsync(shiftReportId);

        Assert.True(result.IsSuccess, result.Message);
        var report = result.Value!;
        Assert.Equal(3, report.RollCount);
        // In the order they came off the machine.
        Assert.Equal(report.Rolls.OrderBy(r => r.ProducedAt).Select(r => r.RollCode), report.Rolls.Select(r => r.RollCode));

        var finished = report.Rolls[0];
        Assert.Equal(95m, finished.Weight);
        Assert.Equal(9.1m, finished.PlateWeight);
        Assert.Equal(1.25m, finished.AverageThickness);
        Assert.True(finished.TestComplete);
        Assert.Equal("Available", finished.Status);
        Assert.Equal("First of the shift", finished.Notes);
        Assert.Equal("Edge a little thin", finished.TestNotes);
        Assert.NotNull(finished.MeasuredByName);
        Assert.NotNull(finished.TestedByName);

        // Only logged: there, with nothing measured, rather than left off.
        Assert.Null(report.Rolls[2].Weight);
        Assert.False(report.Rolls[2].TestComplete);

        Assert.Equal(2, report.MeasuredCount);
        Assert.Equal(1, report.TestedCount);
        Assert.Equal(1, report.ReadyCount);
        Assert.Equal(185m, report.TotalWeight);
        Assert.Equal(2300m, report.TotalLength);
        Assert.Single(report.BatchNumbers);

        var product = Assert.Single(report.Products);
        Assert.Equal(3, product.RollCount);
        Assert.Equal(185m, product.TotalWeight);
        Assert.Equal(1.25m, product.AverageThickness);
    }

    [Fact]
    public async Task The_excel_has_a_row_per_roll_and_formulas_that_follow_a_correction()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ESR2");
        var shiftReportId = await ShiftWithRollsAsync(db, ids, "ESR2");
        var report = (await new ExtruderShiftReportService(db).GetAsync(shiftReportId)).Value!;

        var bytes = new ExtruderShiftReportExcel().Build(report, TimeSpan.FromHours(3));

        using var book = new XLWorkbook(new MemoryStream(bytes));
        var sheet = book.Worksheet(1);
        var first = ExtruderShiftReportExcel.HeaderRow + 1;
        var totals = first + 3;

        Assert.Equal(report.Rolls[0].RollCode, sheet.Cell($"B{first}").GetString());
        // The out time in the factory's clock, not the server's.
        Assert.Equal(
            report.Rolls[0].ProducedAt.ToOffset(TimeSpan.FromHours(3)).ToString("HH:mm"),
            sheet.Cell($"C{first}").GetString());
        Assert.Equal(95d, sheet.Cell($"I{first}").GetDouble());
        Assert.Equal($"IF(COUNT(L{first}:O{first})=4,ROUND(AVERAGE(L{first}:O{first}),3),\"\")", sheet.Cell($"P{first}").FormulaA1);
        // A reading not taken is an empty cell, never a zero.
        Assert.True(sheet.Cell($"I{first + 2}").IsEmpty());
        Assert.Equal($"SUM(I{first}:I{first + 2})", sheet.Cell($"I{totals}").FormulaA1);
    }

    [Fact]
    public async Task A_shift_without_the_extruder_is_answered_in_words()
    {
        await using var db = fixture.CreateContext();

        var result = await new ExtruderShiftReportService(db).GetAsync(int.MaxValue);

        Assert.Equal("shift.notFound", result.MessageCode);
    }
}
