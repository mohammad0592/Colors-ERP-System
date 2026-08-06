using Colors.Application.Features.Packaging;
using Colors.Application.Features.Pallets;
using Colors.Application.Features.Production;
using Colors.Application.Features.Thermo;
using Colors.Domain.Constants;
using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Inventory;
using Colors.Infrastructure.Services.Packaging;
using Colors.Infrastructure.Services.Pallets;
using Colors.Infrastructure.Services.Production;
using Colors.Infrastructure.Services.Thermo;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// What packaging a shift used (specification section 10).
///
/// Three materials are not typed at all — what the shift produced already says how many
/// were used. Typing them is how the factory's own 2 July form ended up saying 6.1 large
/// bags where 61 were used.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PackagingTests(DatabaseFixture fixture)
{
    private static PackagingService NewService(ColorsDbContext db) =>
        new(db, new StockLedger(db, TimeProvider.System), TimeProvider.System);

    /// <summary>The three counted materials, plus one that is typed by hand.</summary>
    private static async Task<(int Large, int Small, int Pallets, int Tape)> PackagingAsync(
        ColorsDbContext db,
        string suffix)
    {
        var category = await db.MaterialCategories.FirstAsync(c => !c.IssuedOnTickets);
        var unit = await db.Units.FirstAsync(u => u.Name == "Piece");

        async Task<int> AddAsync(string name, CountedPackaging countedAs, decimal? unitWeight)
        {
            // Only one material may claim each role, and the tests share one database.
            var existing = await db.Materials
                .FirstOrDefaultAsync(m => m.CountedAs == countedAs && countedAs != CountedPackaging.None);

            if (existing is not null)
            {
                return existing.Id;
            }

            var material = new Material
            {
                Code = $"{name[..3].ToUpperInvariant()}{suffix}",
                Name = $"{name} {suffix}",
                CategoryId = category.Id,
                BaseUnitId = unit.Id,
                MinQuantity = 0,
                UnitWeight = unitWeight,
                CountedAs = countedAs,
            };

            db.Materials.Add(material);
            await db.SaveChangesAsync();
            return material.Id;
        }

        return (
            await AddAsync("Large Bags", CountedPackaging.LargeBag, 0.085m),
            await AddAsync("Small Bags", CountedPackaging.SmallBag, 0.0475m),
            await AddAsync("Wooden Pallets", CountedPackaging.WoodenPallet, null),
            await AddAsync("Tape", CountedPackaging.None, null));
    }

    /// <summary>
    /// A shift that made some bags, built through the real services.
    ///
    /// Returns the bags themselves: the suite shares one database, so a test that asked
    /// the pallet for "available bags" would be handed other tests' bags too — of other
    /// products, which the pallet then refuses.
    /// </summary>
    private static async Task<IReadOnlyList<ProducedBagDto>> BagsMadeAsync(
        ColorsDbContext db,
        FactoryData.Ids ids,
        string suffix,
        int bagCount)
    {
        var colour = await TestSequences.ColourAsync(db);
        var productType = await db.ProductTypes.FirstAsync();

        var family = new RecipeFamily
        {
            Name = $"Family {suffix}",
            Code = "N",
            ProductTypeId = productType.Id,
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

        var production = new ProductionService(
            db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);
        var thermo = new ThermoService(
            db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

        var roll = await production.CreateRollAsync(
            new CreateRollRequest(ids.ShiftLineId, family.Versions[0].Id, colour.Id, null, null),
            ids.UserId);

        await production.SaveTestReportAsync(
            roll.Value!.Id,
            new SaveRollTestRequest(95m, 1200m, 9m, 1.2m, 1.2m, 1.2m, 1.2m, null),
            ids.UserId);

        var run = await thermo.StartRunAsync(
            new StartThermoRunRequest(null, roll.Value.Id, ids.ThermoShiftLineId, null, null),
            ids.UserId);

        await thermo.FinishRunAsync(
            run.Value!.Id, new FinishThermoRunRequest(run.Value.StartedAt.AddMinutes(45)));

        var counted = await thermo.SaveTestReportAsync(
            run.Value.Id,
            new SaveThermoTestRequest(bagCount, 4m, 10m, 0m, null),
            ids.UserId);

        Assert.True(counted.IsSuccess, counted.Message);
        return counted.Value!.Bags;
    }

    [Fact]
    public async Task The_three_counted_materials_come_from_what_the_shift_made()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PKG1");
        var m = await PackagingAsync(db, "PKG1");

        // The Big Plate product takes one large bag and two small ones per packed bag.
        await BagsMadeAsync(db, ids, "PKG1", 10);

        var draft = await NewService(db).GetDraftAsync(ids.ThermoShiftLineId);
        Assert.True(draft.IsSuccess, draft.Message);

        var lines = draft.Value!.Lines.ToDictionary(l => l.MaterialId);

        Assert.Equal(10, draft.Value.BagsProduced);
        Assert.Equal(10m, lines[m.Large].Quantity);
        Assert.Equal(20m, lines[m.Small].Quantity);

        // Nothing was completed, so no pallet wood has been used up.
        Assert.Equal(0m, lines[m.Pallets].Quantity);

        // Tape is used by length and by feel, so it stays for a person to type.
        Assert.False(lines[m.Tape].IsCounted);
        Assert.True(lines[m.Large].IsCounted);
    }

    [Fact]
    public async Task Only_completed_pallets_are_counted()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PKG2");
        var m = await PackagingAsync(db, "PKG2");
        var bags = await BagsMadeAsync(db, ids, "PKG2", 16);

        var pallets = new PalletService(
            db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

        var pallet = await pallets.StartPalletAsync(
            new StartPalletRequest(ids.ThermoShiftLineId, null), ids.UserId);
        Assert.True(pallet.IsSuccess, pallet.Message);
        var palletId = pallet.Value!.Id;

        // Fifteen fills a plate pallet; the pallet's wood is used when it is finished,
        // not while it is still being built.
        foreach (var bag in bags.Take(14))
        {
            await pallets.ScanBagAsync(
                palletId, new ScanBagRequest(bag.Barcode, null), ids.UserId);
        }

        var before = await NewService(db).GetDraftAsync(ids.ThermoShiftLineId);
        Assert.Equal(0, before.Value!.PalletsCompleted);

        var last = await pallets.ScanBagAsync(
            palletId, new ScanBagRequest(bags[14].Barcode, null), ids.UserId);
        Assert.True(last.IsSuccess, last.Message);

        var after = await NewService(db).GetDraftAsync(ids.ThermoShiftLineId);
        Assert.Equal(1, after.Value!.PalletsCompleted);
        Assert.Equal(1m, after.Value.Lines.Single(l => l.MaterialId == m.Pallets).Quantity);
    }

    [Fact]
    public async Task Saving_takes_the_packaging_out_of_the_store()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PKG3");
        var m = await PackagingAsync(db, "PKG3");
        await BagsMadeAsync(db, ids, "PKG3", 6);

        var ledger = new StockLedger(db, TimeProvider.System);
        await ledger.PostAsync(m.Large, MovementTypeNames.Receive, 100m, ids.UserId, "opening");
        await ledger.PostAsync(m.Small, MovementTypeNames.Receive, 100m, ids.UserId, "opening");
        await ledger.PostAsync(m.Tape, MovementTypeNames.Receive, 10m, ids.UserId, "opening");

        var largeBefore = await StockAsync(db, m.Large);
        var smallBefore = await StockAsync(db, m.Small);
        var tapeBefore = await StockAsync(db, m.Tape);

        var saved = await NewService(db).SaveAsync(
            new SavePackagingRequest(
                ids.ThermoShiftLineId,
                [new SavePackagingLineRequest(m.Tape, 0.5m, null)],
                null),
            ids.UserId);

        Assert.True(saved.IsSuccess, saved.Message);

        // Six bags of big plates: six large bags, twelve small.
        Assert.Equal(largeBefore - 6m, await StockAsync(db, m.Large));
        Assert.Equal(smallBefore - 12m, await StockAsync(db, m.Small));

        // Half a roll of tape, which is why the quantity is decimal.
        Assert.Equal(tapeBefore - 0.5m, await StockAsync(db, m.Tape));

        // Its own movement type, so packaging never lands in the material waste figures.
        var movement = await db.MaterialInventoryMovements
            .Include(mv => mv.MovementType)
            .Where(mv => mv.MaterialId == m.Large)
            .OrderByDescending(mv => mv.Id)
            .FirstAsync();

        Assert.Equal(MovementTypeNames.PackagingConsumption, movement.MovementType.Name);
    }

    [Fact]
    public async Task The_counted_quantity_is_not_taken_from_the_screen()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PKG4");
        var m = await PackagingAsync(db, "PKG4");
        await BagsMadeAsync(db, ids, "PKG4", 5);

        var ledger = new StockLedger(db, TimeProvider.System);
        await ledger.PostAsync(m.Large, MovementTypeNames.Receive, 100m, ids.UserId, "opening");
        await ledger.PostAsync(m.Small, MovementTypeNames.Receive, 100m, ids.UserId, "opening");

        // An old tablet, or somebody posting straight to the endpoint, claiming 999.
        var saved = await NewService(db).SaveAsync(
            new SavePackagingRequest(
                ids.ThermoShiftLineId,
                [new SavePackagingLineRequest(m.Large, 999m, null)],
                null),
            ids.UserId);

        Assert.True(saved.IsSuccess, saved.Message);

        var line = saved.Value!.Lines.Single(l => l.MaterialId == m.Large);
        Assert.Equal(5m, line.Quantity);
    }

    [Fact]
    public async Task The_weighed_figure_is_checked_against_the_counted_one()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PKG5");
        var m = await PackagingAsync(db, "PKG5");
        await BagsMadeAsync(db, ids, "PKG5", 61);

        var ledger = new StockLedger(db, TimeProvider.System);
        await ledger.PostAsync(m.Large, MovementTypeNames.Receive, 200m, ids.UserId, "opening");
        await ledger.PostAsync(m.Small, MovementTypeNames.Receive, 200m, ids.UserId, "opening");

        // The factory's own 2 July figures: 61 large bags weighed 5.185 kg.
        var saved = await NewService(db).SaveAsync(
            new SavePackagingRequest(
                ids.ThermoShiftLineId,
                [new SavePackagingLineRequest(m.Large, 0m, 5.185m)],
                null),
            ids.UserId);

        var line = saved.Value!.Lines.Single(l => l.MaterialId == m.Large);

        Assert.Equal(61m, line.Quantity);
        Assert.Equal(5.185m, line.ExpectedWeight);

        // Counted and weighed agree exactly, which is what makes the check worth having.
        Assert.Equal(0m, line.WeightDifference);
    }

    [Fact]
    public async Task Packaging_is_recorded_once_for_a_line()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PKG6");
        var m = await PackagingAsync(db, "PKG6");
        await BagsMadeAsync(db, ids, "PKG6", 4);

        var ledger = new StockLedger(db, TimeProvider.System);
        await ledger.PostAsync(m.Large, MovementTypeNames.Receive, 100m, ids.UserId, "opening");
        await ledger.PostAsync(m.Small, MovementTypeNames.Receive, 100m, ids.UserId, "opening");

        var request = new SavePackagingRequest(ids.ThermoShiftLineId, [], null);

        Assert.True((await NewService(db).SaveAsync(request, ids.UserId)).IsSuccess);

        // Written once, at the end. A second record would double every figure.
        var again = await NewService(db).SaveAsync(request, ids.UserId);

        Assert.False(again.IsSuccess);
        Assert.Contains("already been recorded", again.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_line_that_packs_nothing_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PKG7");
        await PackagingAsync(db, "PKG7");

        // The extruder makes rolls; nothing is packed there.
        var saved = await NewService(db).SaveAsync(
            new SavePackagingRequest(ids.ShiftLineId, [], null), ids.UserId);

        Assert.False(saved.IsSuccess);
        Assert.Contains("does not pack", saved.Message!, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<decimal> StockAsync(ColorsDbContext db, int materialId)
    {
        db.ChangeTracker.Clear();
        return await db.MaterialInventory
            .Where(i => i.MaterialId == materialId)
            .Select(i => i.CurrentQuantity)
            .FirstAsync();
    }
}
