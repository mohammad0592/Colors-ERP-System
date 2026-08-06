using Colors.Domain.Constants;
using Colors.Domain.Entities.MasterData;
using Colors.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Colors.Infrastructure.Persistence.Seed;

/// <summary>
/// The master data named in the specification, created the first time the system runs.
///
/// Runs in every environment: these are the factory's real lines, shifts, units,
/// colours and materials (specification sections 1, 2 and 4), not demo data.
///
/// **Each list is filled only while its table is empty.** An earlier version checked
/// row by row for a missing name, which quietly undid the administrator's work: rename
/// "Large Meal Box" to what the factory actually calls it and the next restart decided
/// the original was missing and created it again. A list is seeded once, and after that
/// it belongs to whoever maintains it.
/// </summary>
public static class MasterDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ColorsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(MasterDataSeeder));

        var before = 0;
        var seededProducts = false;

        // --- The three lines (specification section 1) -----------------------
        // Only the thermo line records forming speed, feed distance and cycle time,
        // so only it is asked for them. Electricity is not here at all: the factory
        // has one meter for the building, so it is read once per shift.
        //
        // The other three say what each line does — which one a batch may start on,
        // which one forms bags, and which one appears on an issue ticket. Seeded as the
        // factory works today; each is a tick box in Master Data afterwards.
        var lines =
            new (string Name, bool Settings, bool Rolls, bool Bags, bool RawMaterial, bool Recycles)[]
        {
            ("Extruder", false, true, false, true, false),
            ("Thermo", true, false, true, false, false),
            ("Recycler", false, false, false, false, true),
        };

        if (!await db.ProductionLines.AnyAsync(cancellationToken))
        {
            foreach (var (name, settings, rolls, bags, rawMaterial, recycles) in lines)
            {
                db.ProductionLines.Add(new ProductionLine
                {
                    Name = name,
                    RecordsMachineSettings = settings,
                    MakesRolls = rolls,
                    FormsBags = bags,
                    TakesRawMaterial = rawMaterial,
                    Recycles = recycles,
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
        if (!await db.Shifts.AnyAsync(cancellationToken))
        {
            foreach (var (name, start, end) in shifts)
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
        if (!await db.Units.AnyAsync(cancellationToken))
        {
            foreach (var (name, symbol) in units)
            {
                db.Units.Add(new Unit { Name = name, Symbol = symbol });
                before++;
            }
        }

        // --- Material categories ----------------------------------------------
        // Only raw material goes out on an issue ticket. Packaging goes to the bench,
        // never comes back, and is counted by the system from what was produced
        // (specification sections 4 and 11).
        if (!await db.MaterialCategories.AnyAsync(cancellationToken))
        {
            var categories = new (string Name, bool IssuedOnTickets)[]
            {
                ("Raw Material", true),
                ("Packaging Material", false),
                ("Consumable", false),
            };

            foreach (var (name, issuedOnTickets) in categories)
            {
                db.MaterialCategories.Add(new MaterialCategory
                {
                    Name = name,
                    IssuedOnTickets = issuedOnTickets,
                });
                before++;
            }
        }

        // --- Colours, with the letters used inside roll codes ------------------
        // IsBlack decides which recipes a colour may be used with: the two Black
        // families need it, and the two Except Black families refuse it (specification
        // section 5). A flag rather than a check on the name or the letter B, which
        // Blue also starts with.
        var colors = new (string Name, string Code, bool IsBlack)[]
        {
            ("White", "W", false),
            ("Green", "G", false),
            ("Yellow", "Y", false),
            ("Black", "B", true),
        };
        if (!await db.Colors.AnyAsync(cancellationToken))
        {
            foreach (var (name, code, isBlack) in colors)
            {
                db.Colors.Add(new Color { Name = name, Code = code, IsBlack = isBlack });
                before++;
            }
        }

        // --- Movement types (specification section 4) --------------------------
        // The direction is data, so a balance is SUM(Quantity × Direction) and a sign
        // error cannot be stored.
        if (!await db.MovementTypes.AnyAsync(cancellationToken))
        {
            foreach (var (name, direction) in MovementTypeNames.All)
            {
                db.MovementTypes.Add(new MovementType { Name = name, Direction = direction });
                before++;
            }
        }

        // --- Product types, moulds and products (specification section 4) ------
        if (!await db.ProductTypes.AnyAsync(cancellationToken))
        {
            foreach (var name in new[] { "Plate", "Meal Box", "Clamshell" })
            {
                db.ProductTypes.Add(new ProductType { Name = name });
                before++;
            }
        }

        // The five templates the factory has. Three of them arrived new. The factory's
        // own name for the small hinged box is still to be confirmed (specification
        // section 18, question 8), so expect these to be renamed — which is exactly
        // why the list is only seeded into an empty table.
        if (!await db.Moulds.AnyAsync(cancellationToken))
        {
            foreach (var name in new[]
                     {
                         "Big Plate",
                         "Small Plate",
                         "Large Meal Box",
                         "Small Meal Box",
                         "3-Compartment Clamshell",
                     })
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
        // A plate goes into a big bag holding two small ones; a meal box or clamshell
        // goes into the small bag directly, with no large bag at all. Both figures are
        // stated rather than one inferred from the other (specification section 10).
        var products = new (string Name, string Mould, string Type, bool Abs, int Pieces, int SmallBags, int LargeBags, int PerPallet)[]
        {
            ("Big Plate — Normal", "Big Plate", "Plate", false, 500, 2, 1, 15),
            ("Big Plate — Absorbent", "Big Plate", "Plate", true, 500, 2, 1, 15),
            ("Small Plate — Normal", "Small Plate", "Plate", false, 500, 2, 1, 15),
            ("Small Plate — Absorbent", "Small Plate", "Plate", true, 500, 2, 1, 15),
            ("Large Meal Box", "Large Meal Box", "Meal Box", false, 250, 1, 0, 21),
            ("Small Meal Box", "Small Meal Box", "Meal Box", false, 250, 1, 0, 21),
            ("3-Compartment Clamshell", "3-Compartment Clamshell", "Clamshell", false, 250, 1, 0, 21),
        };

        // Only into an empty table, and only if the moulds and types are still the ones
        // seeded above — otherwise the names below would not find them.
        if (!await db.Products.AnyAsync(cancellationToken))
        {
            foreach (var p in products)
            {
                var mould = await db.Moulds.SingleOrDefaultAsync(m => m.Name == p.Mould, cancellationToken);
                var type = await db.ProductTypes.SingleOrDefaultAsync(t => t.Name == p.Type, cancellationToken);

                if (mould is null || type is null)
                {
                    logger.LogWarning(
                        "Skipped seeding product {Product}: its mould or product type has been renamed. " +
                        "Add it in Master Data.",
                        p.Name);
                    continue;
                }

                db.Products.Add(new Product
                {
                    Name = p.Name,
                    MouldId = mould.Id,
                    ProductTypeId = type.Id,
                    IsAbsorbent = p.Abs,
                    PiecesPerBag = p.Pieces,
                    SmallBagsPerBag = p.SmallBags,
                    LargeBagsPerBag = p.LargeBags,
                    BagsPerPallet = p.PerPallet,
                });
                before++;
                seededProducts = true;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // --- Materials (specification section 4). Needs the ids saved above. ---
        var kg = await db.Units.SingleAsync(u => u.Name == "Kilogram", cancellationToken);
        var piece = await db.Units.SingleAsync(u => u.Name == "Piece", cancellationToken);
        var raw = await db.MaterialCategories.SingleAsync(c => c.Name == "Raw Material", cancellationToken);
        var packaging = await db.MaterialCategories.SingleAsync(c => c.Name == "Packaging Material", cancellationToken);

        // CountedAs names the three the system works out for itself from what the shift
        // produced. Tape, shrink and the hood are used by length and by feel, so they
        // stay typed (specification section 10).
        var materials = new (string Code, string Name, int CategoryId, int UnitId, decimal? UnitWeight, CountedPackaging CountedAs)[]
        {
            ("MAT0001", "GPPS", raw.Id, kg.Id, null, CountedPackaging.None),
            ("MAT0002", "Recycled Material", raw.Id, kg.Id, null, CountedPackaging.None),
            ("MAT0003", "Talc", raw.Id, kg.Id, null, CountedPackaging.None),
            ("MAT0004", "Nucleating Agent", raw.Id, kg.Id, null, CountedPackaging.None),
            ("MAT0005", "Absorbent Agent", raw.Id, kg.Id, null, CountedPackaging.None),
            ("MAT0006", "Antistatic Agent", raw.Id, kg.Id, null, CountedPackaging.None),
            ("MAT0007", "Coloring Agent", raw.Id, kg.Id, null, CountedPackaging.None),
            ("MAT0008", "Black Coloring Agent", raw.Id, kg.Id, null, CountedPackaging.None),
            ("MAT0009", "Tape", packaging.Id, piece.Id, null, CountedPackaging.None),
            ("MAT0010", "Shrink Wrap", packaging.Id, piece.Id, null, CountedPackaging.None),
            ("MAT0011", "Plastic Hood", packaging.Id, piece.Id, null, CountedPackaging.None),
            // Unit weights measured from the factory's own 2 July form:
            // 5.185 kg / 61 bags and 5.8 kg / 122 bags.
            ("MAT0012", "Large Bags", packaging.Id, piece.Id, 0.085m, CountedPackaging.LargeBag),
            ("MAT0013", "Small Bags", packaging.Id, piece.Id, 0.0475m, CountedPackaging.SmallBag),
            ("MAT0014", "Empty Wooden Pallets", packaging.Id, piece.Id, null, CountedPackaging.WoodenPallet),
        };

        // Matched on the code rather than the name, so renaming a material is safe —
        // but still only into an empty table, so deleting one does not bring it back.
        if (!await db.Materials.AnyAsync(cancellationToken))
        {
            foreach (var (code, name, categoryId, unitId, unitWeight, countedAs) in materials)
            {
                db.Materials.Add(new Material
                {
                    Code = code,
                    Name = name,
                    CategoryId = categoryId,
                    BaseUnitId = unitId,
                    UnitWeight = unitWeight,
                    CountedAs = countedAs,
                    // What the recycler makes. Only one row may carry it
                    // (specification section 11).
                    IsRecycledOutput = code == "MAT0002",
                });
                before++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (before > 0)
        {
            logger.LogInformation(
                "Seeded {Count} master data rows{Products}.",
                before,
                seededProducts ? " including the products" : string.Empty);
        }
    }
}
