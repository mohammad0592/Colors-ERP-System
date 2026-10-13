using Colors.Application.Features.Printing;
using Colors.Domain.Entities.Barcodes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Printing;
using Colors.IntegrationTests.Common;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// The queue between the tablets, which have no printer, and the computer that has one
/// (specification section 19.13).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PrintQueueTests(DatabaseFixture fixture)
{
    private static PrintQueueService NewService(ColorsDbContext db) => new(db, TimeProvider.System);

    // Made-up objects far above any the other tests make: the suite shares one database,
    // and a second active label for a real bag would confuse the tests that count them.
    private static int _nextObjectId = 900_000_000;

    /// <summary>A label the system issued, so the queue will take it.</summary>
    private static async Task<string> LabelAsync(ColorsDbContext db, string value)
    {
        db.Barcodes.Add(new Barcode
        {
            Value = value,
            ObjectType = BarcodeObjectType.Bag,
            ObjectId = Interlocked.Increment(ref _nextObjectId),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return value;
    }

    [Fact]
    public async Task A_tablet_queues_labels_and_the_computer_prints_them()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRQ1");
        var first = await LabelAsync(db, "PRQ1-A");
        var second = await LabelAsync(db, "PRQ1-B");
        var service = NewService(db);

        // Sent in whatever case the screen had, and twice over; queued once, cleaned.
        var queued = await service.QueueAsync(
            new QueuePrintJobRequest(["prq1-a", first, second], "Roll logged", "/production/rolls"),
            ids.UserId);

        Assert.True(queued.IsSuccess, queued.Message);
        Assert.Equal([first, second], queued.Value!.Barcodes);
        Assert.Equal("Pending", queued.Value.Status);

        var waiting = await service.GetAsync(pendingOnly: true);
        Assert.Contains(waiting, j => j.Id == queued.Value.Id);

        var printed = await service.MarkPrintedAsync(queued.Value.Id, ids.UserId);
        Assert.Equal("Printed", printed.Value!.Status);
        Assert.NotNull(printed.Value.DoneAt);

        // Off the waiting list, but kept: a label torn tomorrow is printed again from here.
        Assert.DoesNotContain(await service.GetAsync(pendingOnly: true), j => j.Id == queued.Value.Id);
        Assert.Contains(await service.GetAsync(pendingOnly: false, take: 200), j => j.Id == queued.Value.Id);
    }

    [Fact]
    public async Task A_label_the_system_never_issued_is_not_queued()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRQ2");

        var queued = await NewService(db).QueueAsync(
            new QueuePrintJobRequest(["NOT-A-LABEL-PRQ2"], null, null), ids.UserId);

        Assert.False(queued.IsSuccess);
        Assert.Equal("barcode.noMatch", queued.MessageCode);
    }

    [Fact]
    public async Task An_empty_job_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRQ3");

        var queued = await NewService(db).QueueAsync(new QueuePrintJobRequest([" "], null, null), ids.UserId);

        Assert.Equal("print.nothingToPrint", queued.MessageCode);
    }

    [Fact]
    public async Task A_cancelled_job_leaves_the_queue_and_printing_it_again_changes_nothing()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PRQ4");
        var label = await LabelAsync(db, "PRQ4-A");
        var service = NewService(db);
        var job = (await service.QueueAsync(new QueuePrintJobRequest([label], null, null), ids.UserId)).Value!;

        var cancelled = await service.CancelAsync(job.Id, ids.UserId);
        var thenPrinted = await service.MarkPrintedAsync(job.Id, ids.UserId);

        Assert.Equal("Cancelled", cancelled.Value!.Status);
        // Only a waiting job moves; the first decision is the one on record.
        Assert.Equal("Cancelled", thenPrinted.Value!.Status);
    }
}
