using Colors.Application.Features.Production;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Dashboard;
using Colors.Infrastructure.Services.Production;
using Colors.Infrastructure.Services.Reports;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// The home screen (specification section 13).
///
/// Two questions: what is running, and what is waiting for somebody. The interesting
/// part is what it stays quiet about — a dashboard full of zeroes is one nobody reads.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DashboardTests(DatabaseFixture fixture)
{
    private static DashboardService NewService(ColorsDbContext db) =>
        new(db, new ReportsService(db));

    [Fact]
    public async Task It_names_the_shift_that_is_running()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "DSH1");

        var dashboard = await NewService(db).GetAsync();

        Assert.True(dashboard.IsSuccess, dashboard.Message);
        Assert.NotNull(dashboard.Value!.OpenShift);
        Assert.Equal(ids.ShiftReportId, dashboard.Value.OpenShift.ShiftReportId);

        // The lines that are running, so the reader knows what the shift covers.
        Assert.Contains(dashboard.Value.OpenShift.LineNames, n => n.StartsWith("Extruder"));
        Assert.Contains(dashboard.Value.OpenShift.LineNames, n => n.StartsWith("Thermo"));

        // And the shift's own figures, read through the reports service so the home
        // screen cannot disagree with the shift summary.
        Assert.NotNull(dashboard.Value.Summary);
        Assert.Equal(ids.ShiftReportId, dashboard.Value.Summary.ShiftReportId);
    }

    [Fact]
    public async Task With_no_shift_open_it_says_so_rather_than_failing()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "DSH2");

        var report = await db.ShiftReports.FirstAsync(r => r.Id == ids.ShiftReportId);
        report.Status = ShiftReportStatus.Closed;
        await db.SaveChangesAsync();

        var dashboard = await NewService(db).GetAsync();

        // Between shifts is a normal state for a factory, not an error.
        Assert.True(dashboard.IsSuccess, dashboard.Message);
        Assert.Null(dashboard.Value!.OpenShift);
        Assert.Null(dashboard.Value.Summary);
    }

    [Fact]
    public async Task A_roll_waiting_to_be_measured_is_shown()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "DSH3");

        var before = await CountAsync(db, "roll-needs-test");

        var production = new ProductionService(
            db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

        var colour = await TestSequences.ColourAsync(db);
        var recipe = await RecipeVersionAsync(db, ids, "DSH3");

        var roll = await production.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, recipe, colour.Id, null, null),
            ids.UserId);

        Assert.True(roll.IsSuccess, roll.Message);

        // Straight off the extruder and not yet measured — somebody has to go and do it
        // before the roll can be formed.
        Assert.Equal(before + 1, await CountAsync(db, "roll-needs-test"));
    }

    [Fact]
    public async Task An_open_ticket_is_marked_as_stopping_the_shift_from_closing()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "DSH4");

        var ledger = new Colors.Infrastructure.Services.Inventory.StockLedger(
            db, TimeProvider.System);
        await ledger.PostAsync(
            ids.GppsId, Domain.Constants.MovementTypeNames.Receive, 500m, ids.UserId, "opening");

        var issue = new Colors.Infrastructure.Services.MaterialIssue.MaterialIssueService(
            db, ledger, TimeProvider.System);

        var ticket = await issue.CreateAsync(
            new Application.Features.MaterialIssue.CreateIssueTicketRequest(
                ids.ShiftLineId,
                null,
                [new Application.Features.MaterialIssue.IssueLineRequest(ids.GppsId, 100m)]),
            ids.UserId);

        Assert.True(ticket.IsSuccess, ticket.Message);

        var dashboard = await NewService(db).GetAsync();
        var alert = Assert.Single(
            dashboard.Value!.NeedsAttention, a => a.Kind == "ticket-open");

        Assert.Equal(1, alert.Count);

        // Marked, because the supervisor reading this needs to know it is not merely
        // untidy — the shift will refuse to close (specification section 2).
        Assert.True(alert.BlocksShiftClose);
    }

    [Fact]
    public async Task Nothing_waiting_means_nothing_is_listed()
    {
        await using var db = fixture.CreateContext();
        await FactoryData.CreateAsync(db, "DSH5");

        var dashboard = await NewService(db).GetAsync();

        // Whatever else the shared database holds, an alert that reads zero must never
        // be on the list: a screen of zeroes is one people stop reading.
        Assert.All(dashboard.Value!.NeedsAttention, a => Assert.True(a.Count > 0));
    }

    private static async Task<int> CountAsync(ColorsDbContext db, string kind)
    {
        var dashboard = await NewService(db).GetAsync();

        return dashboard.Value!.NeedsAttention
            .Where(a => a.Kind == kind)
            .Select(a => a.Count)
            .FirstOrDefault();
    }

    private static async Task<int> RecipeVersionAsync(
        ColorsDbContext db,
        FactoryData.Ids ids,
        string suffix)
    {
        var productType = await db.ProductTypes.FirstAsync();

        var family = new Domain.Entities.Recipes.RecipeFamily
        {
            Name = $"Family {suffix}",
            Code = "N",
            ProductTypeId = productType.Id,
            Versions =
            [
                new Domain.Entities.Recipes.RecipeVersion
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
}
