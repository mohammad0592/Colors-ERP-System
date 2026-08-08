using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Production;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Audit;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// The audit log (specification section 15).
///
/// It exists for the two things that leave no other trace: decisions and corrections,
/// which the record shows only the result of, and refusals, which change nothing at all.
/// Routine production is deliberately not here — a roll already names the man who made it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AuditTests(DatabaseFixture fixture)
{
    private static async Task<int> LinesForAsync(
        ColorsDbContext db,
        string objectType,
        int objectId)
    {
        return await db.AuditEntries
            .CountAsync(e => e.ObjectType == objectType && e.ObjectId == objectId);
    }

    [Fact]
    public async Task Changing_master_data_writes_a_line_saying_what_moved()
    {
        await using var db = fixture.CreateContext();

        // A unit rather than a colour: colour codes are one character and the suite
        // shares one database, so there are only twenty-six to go round.
        var unit = new Unit { Name = $"Audit {Guid.NewGuid():N}"[..20], Symbol = "ax" };
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        // Creating it is worth a line: master data decides what every other screen shows.
        Assert.Equal(1, await LinesForAsync(db, nameof(Unit), unit.Id));

        unit.Name = $"Renamed {Guid.NewGuid():N}"[..20];
        await db.SaveChangesAsync();

        var lines = await db.AuditEntries
            .Where(e => e.ObjectType == nameof(Unit) && e.ObjectId == unit.Id)
            .OrderBy(e => e.Id)
            .ToListAsync();

        Assert.Equal(2, lines.Count);
        Assert.Equal(AuditResult.Success, lines[1].Result);

        // The line says what actually moved, so a supervisor reading it a month later
        // does not have to guess.
        Assert.Contains("Name", lines[1].Details);
        Assert.Contains(unit.Name, lines[1].Details);
    }

    [Fact]
    public async Task Routine_production_is_not_audited()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "AUD2");

        var before = await db.AuditEntries.CountAsync();

        // A shift opening, a line, a batch — all ordinary, and every one of those rows
        // already names the man who made it. Auditing them would bury the lines that
        // matter under a thousand that do not.
        var batch = new Batch
        {
            BatchNumber = 990_001,
            ShiftLineId = ids.ShiftLineId,
            CreatedByUserId = ids.UserId,
            StartedAt = DateTimeOffset.UtcNow,
        };

        db.Batches.Add(batch);
        await db.SaveChangesAsync();

        Assert.Equal(before, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task A_correction_is_audited_even_though_the_creation_was_not()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "AUD3");

        var report = await db.ShiftReports.FirstAsync(r => r.Id == ids.ShiftReportId);

        var before = await LinesForAsync(db, nameof(Domain.Entities.Shifts.ShiftReport), report.Id);

        // Opening a shift is routine and the row already says who opened it. Closing it,
        // and above all reopening it, is a decision somebody has to answer for.
        report.Status = ShiftReportStatus.Closed;
        report.ClosedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var after = await LinesForAsync(db, nameof(Domain.Entities.Shifts.ShiftReport), report.Id);

        Assert.Equal(before + 1, after);

        var line = await db.AuditEntries
            .Where(e => e.ObjectType == nameof(Domain.Entities.Shifts.ShiftReport)
                        && e.ObjectId == report.Id)
            .OrderByDescending(e => e.Id)
            .FirstAsync();

        Assert.Equal(AuditResult.Success, line.Result);
        Assert.Contains("Status", line.Details);
    }

    [Fact]
    public async Task A_new_row_gets_the_key_the_database_gave_it()
    {
        await using var db = fixture.CreateContext();

        var unit = new Unit { Name = $"Audit Unit {Guid.NewGuid():N}"[..20], Symbol = "au" };
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        var line = await db.AuditEntries
            .Where(e => e.ObjectType == nameof(Unit))
            .OrderByDescending(e => e.Id)
            .FirstAsync();

        // Without filling the key in after the save, this would read "something was
        // added" with no way to say which thing.
        Assert.Equal(unit.Id, line.ObjectId);
        Assert.NotEqual(0, unit.Id);
    }

    [Fact]
    public async Task The_log_never_audits_itself()
    {
        await using var db = fixture.CreateContext();

        var unit = new Unit { Name = $"Loop {Guid.NewGuid():N}"[..20], Symbol = "ay" };
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        // One line for the unit, and nothing at all about the line itself — otherwise
        // writing a line would write a line, and it would never stop.
        Assert.Equal(0, await db.AuditEntries.CountAsync(e => e.ObjectType == "AuditEntry"));
    }

    [Fact]
    public async Task A_password_never_reaches_the_log()
    {
        await using var db = fixture.CreateContext();

        var user = await db.Users.FirstAsync();
        user.PasswordHash = "a-different-hash-entirely";
        user.FullName += " (edited)";
        await db.SaveChangesAsync();

        var line = await db.AuditEntries
            .Where(e => e.ObjectType == "ApplicationUser" && e.ObjectId == user.Id)
            .OrderByDescending(e => e.Id)
            .FirstAsync();

        // The name change is the interesting part and it is there. The hash is not, and
        // must never be: a log is read by people who are not entitled to it.
        Assert.Contains("FullName", line.Details);
        Assert.DoesNotContain("PasswordHash", line.Details);
        Assert.DoesNotContain("a-different-hash-entirely", line.Details);
    }

    [Fact]
    public async Task Reading_the_log_finds_a_thing_under_every_name_it_is_logged_under()
    {
        await using var db = fixture.CreateContext();
        var service = new AuditService(db);

        var marker = Guid.NewGuid().ToString("N")[..8];

        // The two shapes the same thing takes in the log: a correction recorded against
        // the entity it changed, and a refusal recorded against what the screen asked
        // for, because a refusal never touched an entity at all.
        db.AuditEntries.AddRange(
            new Domain.Entities.System.AuditEntry
            {
                Action = "Modified",
                ObjectType = $"WoodenPallet{marker}",
                ObjectId = 1,
                Result = AuditResult.Success,
                Timestamp = DateTimeOffset.UtcNow,
            },
            new Domain.Entities.System.AuditEntry
            {
                Action = "Pallets.ScanBag",
                ObjectType = $"PalletDto{marker}",
                Result = AuditResult.Rejected,
                Details = "No label in the system matches B123456.",
                Timestamp = DateTimeOffset.UtcNow,
            });

        await db.SaveChangesAsync();

        // One name finds half the answer...
        var onlyEntity = await service.GetAsync(objectTypes: [$"WoodenPallet{marker}"]);
        Assert.Single(onlyEntity);

        // ...and the group finds both, which is what a supervisor asked for.
        var both = await service.GetAsync(
            objectTypes: [$"WoodenPallet{marker}", $"PalletDto{marker}"]);

        Assert.Equal(2, both.Count);
        Assert.Contains(both, l => l.Result == "Success");
        Assert.Contains(both, l => l.Result == "Rejected");
    }

    [Fact]
    public async Task Only_what_was_refused_can_be_asked_for()
    {
        await using var db = fixture.CreateContext();
        var service = new AuditService(db);

        var marker = Guid.NewGuid().ToString("N")[..8];

        db.AuditEntries.AddRange(
            new Domain.Entities.System.AuditEntry
            {
                Action = "Modified",
                ObjectType = marker,
                Result = AuditResult.Success,
                Timestamp = DateTimeOffset.UtcNow,
            },
            new Domain.Entities.System.AuditEntry
            {
                Action = "Pallets.ScanBag",
                ObjectType = marker,
                Result = AuditResult.Rejected,
                Timestamp = DateTimeOffset.UtcNow,
            });

        await db.SaveChangesAsync();

        var refused = await service.GetAsync(objectTypes: [marker], refusalsOnly: true);

        // The line nothing else in the system records.
        var line = Assert.Single(refused);
        Assert.Equal("Rejected", line.Result);
    }
}
