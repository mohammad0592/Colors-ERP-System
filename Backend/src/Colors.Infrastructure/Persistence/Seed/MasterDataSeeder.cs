using Colors.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Colors.Infrastructure.Persistence.Seed;

/// <summary>
/// The master data named in the specification, created on startup when missing.
///
/// Runs in every environment: these are the factory's real lines, shifts, units,
/// colours and materials (specification sections 1, 2 and 4), not demo data. It only
/// adds what is absent — rows the administrator has since renamed, extended or
/// deactivated are left exactly as they are.
/// </summary>
public static class MasterDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ColorsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(MasterDataSeeder));

        var before = 0;

        // --- The three lines (specification section 1) -----------------------
        // Only the thermo line records forming speed, feed distance and cycle time,
        // so only it is asked for them. Electricity is not here at all: the factory
        // has one meter for the building, so it is read once per shift.
        var lines = new (string Name, bool RecordsMachineSettings)[]
        {
            ("Extruder", false),
            ("Thermo", true),
            ("Recycler", false),
        };

        foreach (var (name, recordsMachineSettings) in lines)
        {
            if (!await db.ProductionLines.AnyAsync(x => x.Name == name, cancellationToken))
            {
                db.ProductionLines.Add(new ProductionLine
                {
                    Name = name,
                    RecordsMachineSettings = recordsMachineSettings,
                });
                before++;
            }
        }

        // --- Shifts A, B, C (specification section 2; confirmed by the factory)
        var shifts = new (string Name, TimeOnly Start, TimeOnly End)[]
        {
            ("A", new TimeOnly(8, 0), new TimeOnly(16, 0)),
            ("B", new TimeOnly(16, 0), new TimeOnly(0, 0)),
            ("C", new TimeOnly(0, 0), new TimeOnly(8, 0)),
        };
        foreach (var (name, start, end) in shifts)
        {
            if (!await db.Shifts.AnyAsync(x => x.Name == name, cancellationToken))
            {
                db.Shifts.Add(new Shift { Name = name, StartTime = start, EndTime = end });
                before++;
            }
        }

        // --- Units ------------------------------------------------------------
        var units = new (string Name, string Symbol)[]
        {
            ("Kilogram", "kg"),
            ("Piece", "pcs"),
            ("Bag", "bag"),
            ("Pallet", "pallet"),
            ("Roll", "roll"),
        };
        foreach (var (name, symbol) in units)
        {
            if (!await db.Units.AnyAsync(x => x.Name == name, cancellationToken))
            {
                db.Units.Add(new Unit { Name = name, Symbol = symbol });
                before++;
            }
        }

        // --- Material categories ----------------------------------------------
        foreach (var name in new[] { "Raw Material", "Packaging Material", "Consumable" })
        {
            if (!await db.MaterialCategories.AnyAsync(x => x.Name == name, cancellationToken))
            {
                db.MaterialCategories.Add(new MaterialCategory { Name = name });
                before++;
            }
        }

        // --- Colours, with the letters used inside roll codes ------------------
        var colors = new (string Name, string Code)[]
        {
            ("White", "W"),
            ("Green", "G"),
            ("Yellow", "Y"),
            ("Black", "B"),
        };
        foreach (var (name, code) in colors)
        {
            if (!await db.Colors.AnyAsync(x => x.Name == name, cancellationToken))
            {
                db.Colors.Add(new Color { Name = name, Code = code });
                before++;
            }
        }

        // --- Product types, moulds and products (specification section 4) ------
        foreach (var name in new[] { "Plate", "Meal Box", "Clamshell" })
        {
            if (!await db.ProductTypes.AnyAsync(x => x.Name == name, cancellationToken))
            {
                db.ProductTypes.Add(new ProductType { Name = name });
                before++;
            }
        }

        // The five templates the factory has. Three of them arrived new.
        foreach (var name in new[]
                 {
                     "Big Plate",
                     "Small Plate",
                     "Large Meal Box",
                     "Small Meal Box",
                     "3-Compartment Clamshell",
                 })
        {
            if (!await db.Moulds.AnyAsync(x => x.Name == name, cancellationToken))
            {
                db.Moulds.Add(new Mould { Name = name });
                before++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // A mould plus an absorbency names exactly one product — that pair is what the
        // thermo looks up, so the two plate moulds carry two rows each.
        //
        // Several of these numbers are provisional: the new moulds arrived the day
        // before this was written and the factory has not finished packing with them
        // (specification section 18, questions 9 to 11). They are rows, so correcting
        // one is an edit in Master Data.
        var products = new (string Name, string Mould, string Type, bool Abs, int Pieces, int SmallBags, int PerPallet)[]
        {
            ("Big Plate — Normal", "Big Plate", "Plate", false, 500, 2, 15),
            ("Big Plate — Absorbent", "Big Plate", "Plate", true, 500, 2, 15),
            ("Small Plate — Normal", "Small Plate", "Plate", false, 500, 2, 15),
            ("Small Plate — Absorbent", "Small Plate", "Plate", true, 500, 2, 15),
            ("Large Meal Box", "Large Meal Box", "Meal Box", false, 250, 1, 21),
            ("Small Meal Box", "Small Meal Box", "Meal Box", false, 250, 1, 21),
            ("3-Compartment Clamshell", "3-Compartment Clamshell", "Clamshell", false, 250, 1, 21),
        };

        foreach (var p in products)
        {
            if (await db.Products.AnyAsync(x => x.Name == p.Name, cancellationToken))
            {
                continue;
            }

            db.Products.Add(new Product
            {
                Name = p.Name,
                MouldId = (await db.Moulds.SingleAsync(m => m.Name == p.Mould, cancellationToken)).Id,
                ProductTypeId = (await db.ProductTypes.SingleAsync(t => t.Name == p.Type, cancellationToken)).Id,
                IsAbsorbent = p.Abs,
                PiecesPerBag = p.Pieces,
                SmallBagsPerBag = p.SmallBags,
                BagsPerPallet = p.PerPallet,
            });
            before++;
        }

        await db.SaveChangesAsync(cancellationToken);

        // --- Materials (specification section 4). Needs the ids saved above. ---
        var kg = await db.Units.SingleAsync(u => u.Name == "Kilogram", cancellationToken);
        var piece = await db.Units.SingleAsync(u => u.Name == "Piece", cancellationToken);
        var raw = await db.MaterialCategories.SingleAsync(c => c.Name == "Raw Material", cancellationToken);
        var packaging = await db.MaterialCategories.SingleAsync(c => c.Name == "Packaging Material", cancellationToken);

        var materials = new (string Code, string Name, int CategoryId, int UnitId, decimal? UnitWeight)[]
        {
            ("MAT0001", "GPPS", raw.Id, kg.Id, null),
            ("MAT0002", "Recycled Material", raw.Id, kg.Id, null),
            ("MAT0003", "Talc", raw.Id, kg.Id, null),
            ("MAT0004", "Nucleating Agent", raw.Id, kg.Id, null),
            ("MAT0005", "Absorbent Agent", raw.Id, kg.Id, null),
            ("MAT0006", "Antistatic Agent", raw.Id, kg.Id, null),
            ("MAT0007", "Coloring Agent", raw.Id, kg.Id, null),
            ("MAT0008", "Black Coloring Agent", raw.Id, kg.Id, null),
            ("MAT0009", "Tape", packaging.Id, piece.Id, null),
            ("MAT0010", "Shrink Wrap", packaging.Id, piece.Id, null),
            ("MAT0011", "Plastic Hood", packaging.Id, piece.Id, null),
            // Unit weights measured from the factory's own 2 July form:
            // 5.185 kg / 61 bags and 5.8 kg / 122 bags.
            ("MAT0012", "Large Bags", packaging.Id, piece.Id, 0.085m),
            ("MAT0013", "Small Bags", packaging.Id, piece.Id, 0.0475m),
            ("MAT0014", "Empty Wooden Pallets", packaging.Id, piece.Id, null),
        };

        foreach (var (code, name, categoryId, unitId, unitWeight) in materials)
        {
            if (!await db.Materials.AnyAsync(m => m.Code == code, cancellationToken))
            {
                db.Materials.Add(new Material
                {
                    Code = code,
                    Name = name,
                    CategoryId = categoryId,
                    BaseUnitId = unitId,
                    UnitWeight = unitWeight,
                });
                before++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (before > 0)
        {
            logger.LogInformation("Seeded {Count} master data rows.", before);
        }
    }
}
