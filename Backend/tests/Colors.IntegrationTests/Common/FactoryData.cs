using Colors.Domain.Constants;
using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Shifts;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Common;

/// <summary>
/// The smallest real factory a test can stand on: a line, a shift, a user, a couple of
/// materials, and the movement types.
///
/// Written by hand rather than by calling the seeders, so a test says out loud what it
/// depends on. A test that quietly relies on seeded data breaks the day somebody
/// renames a row in Master Data, and the failure points nowhere near the cause.
/// </summary>
public static class FactoryData
{
    public sealed record Ids(
        int UserId,
        int ShiftReportId,
        int ShiftLineId,
        int GppsId,
        int TalcId,
        int LargeBagsId);

    public static async Task<Ids> CreateAsync(ColorsDbContext db, string suffix)
    {
        foreach (var (name, direction) in MovementTypeNames.All)
        {
            if (!await db.MovementTypes.AnyAsync(t => t.Name == name))
            {
                db.MovementTypes.Add(new MovementType { Name = name, Direction = direction });
            }
        }

        var user = new ApplicationUser
        {
            UserName = $"TEST{suffix}",
            EmployeeNumber = $"TEST{suffix}",
            FullName = "Test Storekeeper",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);

        var line = new ProductionLine { Name = $"Extruder {suffix}" };
        var shift = new Shift
        {
            Name = $"Shift {suffix}",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0),
        };
        db.ProductionLines.Add(line);
        db.Shifts.Add(shift);

        var kilogram = await db.Units.FirstOrDefaultAsync(u => u.Name == "Kilogram");
        if (kilogram is null)
        {
            kilogram = new Unit { Name = "Kilogram", Symbol = "kg" };
            db.Units.Add(kilogram);
        }

        var category = await db.MaterialCategories.FirstOrDefaultAsync(c => c.Name == "Raw Material");
        if (category is null)
        {
            category = new MaterialCategory { Name = "Raw Material", IssuedOnTickets = true };
            db.MaterialCategories.Add(category);
        }

        // Packaging is the category that must never reach an issue ticket.
        var packaging = await db.MaterialCategories.FirstOrDefaultAsync(c => c.Name == "Packaging Material");
        if (packaging is null)
        {
            packaging = new MaterialCategory { Name = "Packaging Material", IssuedOnTickets = false };
            db.MaterialCategories.Add(packaging);
        }

        var piece = await db.Units.FirstOrDefaultAsync(u => u.Name == "Piece");
        if (piece is null)
        {
            piece = new Unit { Name = "Piece", Symbol = "pcs" };
            db.Units.Add(piece);
        }

        await db.SaveChangesAsync();

        var gpps = new Material
        {
            Code = $"G{suffix}",
            Name = $"GPPS {suffix}",
            CategoryId = category.Id,
            BaseUnitId = kilogram.Id,
            MinQuantity = 0,
        };
        var talc = new Material
        {
            Code = $"T{suffix}",
            Name = $"Talc {suffix}",
            CategoryId = category.Id,
            BaseUnitId = kilogram.Id,
            MinQuantity = 0,
        };
        // Counted in pieces, not weighed — which is half the reason it does not belong
        // on a ticket.
        var largeBags = new Material
        {
            Code = $"P{suffix}",
            Name = $"Large Bags {suffix}",
            CategoryId = packaging.Id,
            BaseUnitId = piece.Id,
            MinQuantity = 0,
        };

        db.Materials.AddRange(gpps, talc, largeBags);

        var report = new ShiftReport
        {
            ProductionDate = new DateOnly(2026, 3, 1).AddDays(Math.Abs(suffix.GetHashCode()) % 300),
            ShiftId = shift.Id,
            Status = Domain.Enums.ShiftReportStatus.Open,
            OpenedByUserId = user.Id,
            OpenedAt = DateTimeOffset.UtcNow,
            Lines = [new ShiftLine { ProductionLineId = line.Id }],
        };
        db.ShiftReports.Add(report);

        await db.SaveChangesAsync();

        return new Ids(user.Id, report.Id, report.Lines[0].Id, gpps.Id, talc.Id, largeBags.Id);
    }
}
