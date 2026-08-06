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
        int LargeBagsId,
        // Line 2. The same shift, a second line — which is the whole point of the shift
        // restructure: one shift for the factory, a row underneath for each line.
        int ThermoShiftLineId,
        int MouldId,
        int NormalProductId,
        int AbsorbentProductId);

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

        // The flags say what each line does. Without them every screen refuses, which is
        // exactly what they are for (specification section 4).
        var line = new ProductionLine
        {
            Name = $"Extruder {suffix}",
            MakesRolls = true,
            TakesRawMaterial = true,
        };
        var thermo = new ProductionLine
        {
            Name = $"Thermo {suffix}",
            RecordsMachineSettings = true,
            FormsBags = true,
        };
        var shift = new Shift
        {
            Name = $"Shift {suffix}",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0),
        };
        db.ProductionLines.AddRange(line, thermo);
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

        // The mould bolted into the thermo, and the two products it can make. Which one
        // comes out is not the mould's doing — it is what was mixed into the roll — so
        // both exist and (mould, absorbency) picks between them.
        var mould = new Mould { Name = $"Big Plate Mould {suffix}" };
        db.Moulds.Add(mould);

        var productType = await db.ProductTypes.FirstOrDefaultAsync(t => t.Name == "Plate");
        if (productType is null)
        {
            productType = new ProductType { Name = "Plate" };
            db.ProductTypes.Add(productType);
        }

        await db.SaveChangesAsync();

        var normal = new Product
        {
            Name = $"Big Plate {suffix}",
            MouldId = mould.Id,
            ProductTypeId = productType.Id,
            IsAbsorbent = false,
            PiecesPerBag = 500,
            SmallBagsPerBag = 2,
            BagsPerPallet = 15,
        };
        var absorbent = new Product
        {
            Name = $"Big Plate ABS {suffix}",
            MouldId = mould.Id,
            ProductTypeId = productType.Id,
            IsAbsorbent = true,
            PiecesPerBag = 500,
            SmallBagsPerBag = 2,
            BagsPerPallet = 15,
        };
        db.Products.AddRange(normal, absorbent);

        // One database is one factory, and a factory works one shift at a time
        // (specification section 2) — the database enforces it with a unique index that
        // allows a single Open row. The suite builds a new factory per test in one
        // shared database, so the shift the last test opened has to end before this one
        // starts, exactly as it would on the floor.
        var stillOpen = await db.ShiftReports
            .Where(r => r.Status == Domain.Enums.ShiftReportStatus.Open)
            .ToListAsync();

        foreach (var previous in stillOpen)
        {
            previous.Status = Domain.Enums.ShiftReportStatus.Closed;
            previous.ClosedAt = DateTimeOffset.UtcNow;
        }

        if (stillOpen.Count > 0)
        {
            await db.SaveChangesAsync();
        }

        var report = new ShiftReport
        {
            // A day of its own, so this factory's roll serials start at 1 whatever else
            // the suite is doing. Never a hash: string.GetHashCode() is randomised per
            // process, so collisions moved around and the suite only went red sometimes.
            ProductionDate = TestSequences.NextProductionDate(),
            ShiftId = shift.Id,
            Status = Domain.Enums.ShiftReportStatus.Open,
            OpenedByUserId = user.Id,
            OpenedAt = DateTimeOffset.UtcNow,
            Lines =
            [
                new ShiftLine { ProductionLineId = line.Id },
                new ShiftLine { ProductionLineId = thermo.Id },
            ],
        };
        db.ShiftReports.Add(report);

        await db.SaveChangesAsync();

        // The mould is mounted after the shift lines exist, because it hangs off one.
        report.Lines[1].MouldId = mould.Id;
        await db.SaveChangesAsync();

        return new Ids(
            user.Id,
            report.Id,
            report.Lines[0].Id,
            gpps.Id,
            talc.Id,
            largeBags.Id,
            report.Lines[1].Id,
            mould.Id,
            normal.Id,
            absorbent.Id);
    }
}
