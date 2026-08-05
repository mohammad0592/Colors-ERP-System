using Colors.Application.Features.MaterialIssue;
using Colors.Domain.Constants;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Inventory;
using Colors.Infrastructure.Services.MaterialIssue;
using Colors.Infrastructure.Services.ShiftReports;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Colors.IntegrationTests.Features;


/// <summary>
/// Material issue and return, against a real database (specification section 7).
///
/// The subtraction these prove — issued minus returned — is the factory's whole waste
/// control. It exists because of one sentence from the owner: <i>"the workers are not
/// careful about the material."</i>
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MaterialIssueTests(DatabaseFixture fixture)
{
    private static MaterialIssueService NewService(ColorsDbContext db) =>
        new(db, new StockLedger(db, TimeProvider.System), TimeProvider.System);

    private static async Task StockUp(ColorsDbContext db, int materialId, int userId, decimal quantity)
    {
        var ledger = new StockLedger(db, TimeProvider.System);
        var posted = await ledger.PostAsync(
            materialId, MovementTypeNames.Receive, quantity, userId, "test stock");
        Assert.True(posted.IsSuccess, posted.Message);
    }

    private static async Task<decimal> Balance(ColorsDbContext db, int materialId) =>
        await db.MaterialInventory
            .Where(i => i.MaterialId == materialId)
            .Select(i => i.CurrentQuantity)
            .FirstOrDefaultAsync();

    [Fact]
    public async Task Issuing_takes_the_material_out_of_the_store()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ISS1");
        await StockUp(db, ids.GppsId, ids.UserId, 1000);

        var ticket = await NewService(db).CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, "for batch 1",
                [new IssueLineRequest(ids.GppsId, 400)]),
            ids.UserId);

        Assert.True(ticket.IsSuccess, ticket.Message);
        Assert.Equal(600, await Balance(db, ids.GppsId));
    }

    [Fact]
    public async Task A_ticket_gets_a_number_of_its_own()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ISS2");
        await StockUp(db, ids.GppsId, ids.UserId, 1000);
        var service = NewService(db);

        var first = await service.CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, null, [new IssueLineRequest(ids.GppsId, 10)]),
            ids.UserId);
        var second = await service.CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, null, [new IssueLineRequest(ids.GppsId, 10)]),
            ids.UserId);

        Assert.NotEqual(first.Value!.TicketNumber, second.Value!.TicketNumber);
    }

    [Fact]
    public async Task A_ticket_short_of_one_material_issues_none_of_them()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ISS3");
        await StockUp(db, ids.GppsId, ids.UserId, 1000);
        await StockUp(db, ids.TalcId, ids.UserId, 5);

        // Half a ticket issued would leave the store wrong and the worker holding
        // material nothing accounts for.
        var ticket = await NewService(db).CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, null,
            [
                new IssueLineRequest(ids.GppsId, 500),
                new IssueLineRequest(ids.TalcId, 50),
            ]),
            ids.UserId);

        Assert.False(ticket.IsSuccess);
        Assert.Contains("not enough", ticket.Message!, StringComparison.OrdinalIgnoreCase);

        await using var fresh = fixture.CreateContext();
        Assert.Equal(1000, await Balance(fresh, ids.GppsId));
        Assert.Equal(5, await Balance(fresh, ids.TalcId));
        Assert.Empty(await fresh.MaterialIssueTickets.Where(t => t.ShiftLineId == ids.ShiftLineId).ToListAsync());
    }

    [Fact]
    public async Task The_leftover_comes_back_and_net_used_is_the_difference()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ISS4");
        await StockUp(db, ids.GppsId, ids.UserId, 1000);
        var service = NewService(db);

        var ticket = await service.CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, null, [new IssueLineRequest(ids.GppsId, 520)]),
            ids.UserId);

        var returned = await service.RecordReturnsAsync(
            ticket.Value!.Id,
            new RecordReturnsRequest([new ReturnLineRequest(ids.GppsId, 24.5m)]),
            ids.UserId);

        var line = returned.Value!.Lines.Single();
        Assert.Equal(520m, line.IssuedQuantity);
        Assert.Equal(24.5m, line.ReturnedQuantity);
        Assert.Equal(495.5m, line.NetUsed);

        // The leftover is back in the store, so stock is true again.
        Assert.Equal(504.5m, await Balance(db, ids.GppsId));
    }

    [Fact]
    public async Task Leftover_may_come_back_in_more_than_one_trip()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ISS5");
        await StockUp(db, ids.GppsId, ids.UserId, 1000);
        var service = NewService(db);

        var ticket = await service.CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, null, [new IssueLineRequest(ids.GppsId, 100)]),
            ids.UserId);

        await service.RecordReturnsAsync(ticket.Value!.Id,
            new RecordReturnsRequest([new ReturnLineRequest(ids.GppsId, 10)]), ids.UserId);
        var second = await service.RecordReturnsAsync(ticket.Value.Id,
            new RecordReturnsRequest([new ReturnLineRequest(ids.GppsId, 15)]), ids.UserId);

        Assert.Equal(25m, second.Value!.Lines.Single().ReturnedQuantity);
        Assert.Equal(75m, second.Value.Lines.Single().NetUsed);
    }

    [Fact]
    public async Task More_cannot_come_back_than_went_out()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ISS6");
        await StockUp(db, ids.GppsId, ids.UserId, 1000);
        var service = NewService(db);

        var ticket = await service.CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, null, [new IssueLineRequest(ids.GppsId, 100)]),
            ids.UserId);

        await service.RecordReturnsAsync(ticket.Value!.Id,
            new RecordReturnsRequest([new ReturnLineRequest(ids.GppsId, 60)]), ids.UserId);

        // Cumulative: 60 is already back, so 50 more would be 110 out of 100 and
        // NetUsed would go negative — a number no report could explain.
        var tooMuch = await service.RecordReturnsAsync(ticket.Value.Id,
            new RecordReturnsRequest([new ReturnLineRequest(ids.GppsId, 50)]), ids.UserId);

        Assert.False(tooMuch.IsSuccess);
        Assert.Contains("cannot return", tooMuch.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_closed_ticket_accepts_nothing_further()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ISS7");
        await StockUp(db, ids.GppsId, ids.UserId, 1000);
        var service = NewService(db);

        var ticket = await service.CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, null, [new IssueLineRequest(ids.GppsId, 100)]),
            ids.UserId);

        var closed = await service.CloseAsync(ticket.Value!.Id, ids.UserId);
        Assert.True(closed.IsSuccess);
        Assert.False(closed.Value!.IsOpen);

        var late = await service.RecordReturnsAsync(ticket.Value.Id,
            new RecordReturnsRequest([new ReturnLineRequest(ids.GppsId, 5)]), ids.UserId);

        Assert.False(late.IsSuccess);
    }

    [Fact]
    public async Task A_material_may_appear_only_once_on_a_ticket()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ISS8");
        await StockUp(db, ids.GppsId, ids.UserId, 1000);

        var ticket = await NewService(db).CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, null,
            [
                new IssueLineRequest(ids.GppsId, 10),
                new IssueLineRequest(ids.GppsId, 20),
            ]),
            ids.UserId);

        Assert.False(ticket.IsSuccess);
    }

    [Fact]
    public async Task Material_cannot_be_issued_to_a_closed_shift()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ISS9");
        await StockUp(db, ids.GppsId, ids.UserId, 1000);

        var report = await db.ShiftReports.FirstAsync(r => r.Id == ids.ShiftReportId);
        report.Status = Domain.Enums.ShiftReportStatus.Closed;
        await db.SaveChangesAsync();

        var ticket = await NewService(db).CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, null, [new IssueLineRequest(ids.GppsId, 10)]),
            ids.UserId);

        Assert.False(ticket.IsSuccess);
        Assert.Contains("closed", ticket.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Every_movement_names_the_ticket_that_caused_it()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ISS10");
        await StockUp(db, ids.GppsId, ids.UserId, 1000);
        var service = NewService(db);

        var ticket = await service.CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, null, [new IssueLineRequest(ids.GppsId, 100)]),
            ids.UserId);
        await service.RecordReturnsAsync(ticket.Value!.Id,
            new RecordReturnsRequest([new ReturnLineRequest(ids.GppsId, 30)]), ids.UserId);

        // Being able to go both ways — from a stock change to its reason, and from a
        // ticket to every kilogram it moved — is the point of naming the cause.
        var moved = await db.MaterialInventoryMovements
            .Where(m => m.IssueTicketId == ticket.Value.Id)
            .Include(m => m.MovementType)
            .ToListAsync();

        Assert.Equal(2, moved.Count);
        Assert.Single(moved, m => m.MovementType.Name == MovementTypeNames.Issue);
        Assert.Single(moved, m => m.MovementType.Name == MovementTypeNames.Return);
    }

    [Fact]
    public async Task A_shift_cannot_close_while_a_ticket_is_open()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ISS11");
        await StockUp(db, ids.GppsId, ids.UserId, 1000);
        var tickets = NewService(db);

        var ticket = await tickets.CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, null, [new IssueLineRequest(ids.GppsId, 100)]),
            ids.UserId);

        var shifts = new ShiftReportService(db, TimeProvider.System, NullLogger<ShiftReportService>.Instance);

        // The factory's own rule. Closing first would leave the leftover with no
        // ticket to come back to, and the shift's waste figure wrong for ever.
        var tooEarly = await shifts.CloseAsync(ids.ShiftReportId, ids.UserId);
        Assert.False(tooEarly.IsSuccess);
        Assert.Contains(ticket.Value!.TicketNumber.ToString(), tooEarly.Message!, StringComparison.Ordinal);

        await tickets.CloseAsync(ticket.Value.Id, ids.UserId);

        var now = await shifts.CloseAsync(ids.ShiftReportId, ids.UserId);
        Assert.True(now.IsSuccess, now.Message);
    }

    [Fact]
    public async Task Packaging_cannot_go_out_on_a_ticket()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ISS13");
        await StockUp(db, ids.GppsId, ids.UserId, 1000);
        await StockUp(db, ids.LargeBagsId, ids.UserId, 500);

        // It would be counted twice — the system already works packaging out from what
        // was produced — and it would ask somebody to weigh bags counted in pieces.
        var ticket = await NewService(db).CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, null,
                [new IssueLineRequest(ids.LargeBagsId, 20)]),
            ids.UserId);

        Assert.False(ticket.IsSuccess);
        Assert.Contains("raw material", ticket.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_ticket_mixing_raw_material_and_packaging_issues_neither()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ISS14");
        await StockUp(db, ids.GppsId, ids.UserId, 1000);
        await StockUp(db, ids.LargeBagsId, ids.UserId, 500);

        var ticket = await NewService(db).CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, null,
            [
                new IssueLineRequest(ids.GppsId, 100),
                new IssueLineRequest(ids.LargeBagsId, 20),
            ]),
            ids.UserId);

        Assert.False(ticket.IsSuccess);

        await using var fresh = fixture.CreateContext();
        Assert.Equal(1000, await Balance(fresh, ids.GppsId));
        Assert.Equal(500, await Balance(fresh, ids.LargeBagsId));
    }

    [Fact]
    public async Task The_database_refuses_a_return_larger_than_the_issue()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "ISS12");
        await StockUp(db, ids.GppsId, ids.UserId, 1000);

        var ticket = await NewService(db).CreateAsync(
            new CreateIssueTicketRequest(ids.ShiftLineId, null, [new IssueLineRequest(ids.GppsId, 100)]),
            ids.UserId);

        var line = await db.MaterialIssueTicketLines.FirstAsync(l => l.TicketId == ticket.Value!.Id);
        line.ReturnedQuantity = 200;

        // The service checks it, but so does the database — no code path can get round
        // a check constraint.
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
