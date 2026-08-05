using Colors.Application.Features.Pallets;
using Colors.Application.Features.Production;
using Colors.Application.Features.Thermo;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Pallets;
using Colors.Infrastructure.Services.Production;
using Colors.Infrastructure.Services.Thermo;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// Pallets — the first bag decides what the pallet is (specification section 10).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PalletTests(DatabaseFixture fixture)
{
    private static PalletService NewService(ColorsDbContext db) =>
        new(db, new BarcodeService(db, TimeProvider.System), TimeProvider.System);

    /// <summary>
    /// A run of bags, built the long way through the real services — roll, measure,
    /// form, count — because that is the only way bags exist at all.
    /// </summary>
    private static async Task<IReadOnlyList<ProducedBagDto>> BagsAsync(
        ColorsDbContext db,
        FactoryData.Ids ids,
        string suffix,
        int bagCount,
        bool absorbent = false)
    {
        var colour = await TestSequences.ColourAsync(db);
        var productType = await db.ProductTypes.FirstAsync();

        var family = new RecipeFamily
        {
            Name = $"Family {suffix}",
            Code = absorbent ? "Abs" : "N",
            ProductTypeId = productType.Id,
            IsAbsorbent = absorbent,
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

        var batch = await production.StartBatchAsync(
            new StartBatchRequest(ids.ShiftLineId, null), ids.UserId);

        var roll = await production.CreateRollAsync(
            new CreateRollRequest(batch.Value!.Id, family.Versions[0].Id, colour.Id, null, null),
            ids.UserId);

        await production.SaveTestReportAsync(
            roll.Value!.Id,
            new SaveRollTestRequest(95m, 1200m, 9m, 1.2m, 1.2m, 1.2m, 1.2m, null),
            ids.UserId);

        var run = await thermo.StartRunAsync(
            new StartThermoRunRequest(null, roll.Value.Id, ids.ThermoShiftLineId, null, null),
            ids.UserId);
        Assert.True(run.IsSuccess, run.Message);

        await thermo.FinishRunAsync(
            run.Value!.Id, new FinishThermoRunRequest(run.Value.StartedAt.AddMinutes(45)));

        var counted = await thermo.SaveTestReportAsync(
            run.Value.Id,
            new SaveThermoTestRequest(bagCount, 4m, 10m, absorbent ? 12.5m : 0m, null),
            ids.UserId);

        Assert.True(counted.IsSuccess, counted.Message);
        return counted.Value!.Bags;
    }

    private static async Task<PalletDto> PalletAsync(ColorsDbContext db, FactoryData.Ids ids)
    {
        var started = await NewService(db).StartPalletAsync(
            new StartPalletRequest(ids.ThermoShiftLineId, null), ids.UserId);

        Assert.True(started.IsSuccess, started.Message);
        return started.Value!;
    }

    [Fact]
    public async Task A_new_pallet_is_empty_and_has_no_colour_or_product()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL1");
        var pallet = await PalletAsync(db, ids);

        Assert.Equal(PalletStatus.Empty.ToString(), pallet.Status);
        Assert.Null(pallet.ColorId);
        Assert.Null(pallet.ProductId);
        Assert.Equal(0, pallet.BagCount);

        // The label and the pallet are one act, as with a roll and a bag.
        Assert.StartsWith("P", pallet.Barcode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_first_bag_gives_the_pallet_its_colour_and_product()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL2");
        var bags = await BagsAsync(db, ids, "PAL2", 3);
        var pallet = await PalletAsync(db, ids);

        // The factory's own words: "when the pallet is empty, the pallet will take the
        // first scanned bag's characteristics."
        var scanned = await NewService(db).ScanBagAsync(
            pallet.Id, new ScanBagRequest(bags[0].Barcode, null), ids.UserId);

        Assert.True(scanned.IsSuccess, scanned.Message);
        Assert.Equal(bags[0].ColorName, scanned.Value!.ColorName);
        Assert.Equal(bags[0].ProductName, scanned.Value.ProductName);
        Assert.Equal(PalletStatus.Opened.ToString(), scanned.Value.Status);
        Assert.Equal(1, scanned.Value.BagCount);
    }

    [Fact]
    public async Task A_bag_of_another_product_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL3");
        var normal = await BagsAsync(db, ids, "PAL3a", 2);
        var absorbent = await BagsAsync(db, ids, "PAL3b", 2, absorbent: true);
        var pallet = await PalletAsync(db, ids);
        var service = NewService(db);

        await service.ScanBagAsync(
            pallet.Id, new ScanBagRequest(normal[0].Barcode, null), ids.UserId);

        // Same mould, different material — so a different product, and a different pallet.
        var wrong = await service.ScanBagAsync(
            pallet.Id, new ScanBagRequest(absorbent[0].Barcode, null), ids.UserId);

        Assert.False(wrong.IsSuccess);
        Assert.Contains("different pallet", wrong.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_pallet_completes_at_the_products_own_figure()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL4");

        // The Big Plate product completes a pallet at 15.
        var bags = await BagsAsync(db, ids, "PAL4", 16);
        var pallet = await PalletAsync(db, ids);
        var service = NewService(db);

        PalletDto? latest = null;
        for (var i = 0; i < 15; i++)
        {
            var scanned = await service.ScanBagAsync(
                pallet.Id, new ScanBagRequest(bags[i].Barcode, null), ids.UserId);
            Assert.True(scanned.IsSuccess, scanned.Message);
            latest = scanned.Value;
        }

        Assert.Equal(15, latest!.Capacity);
        Assert.Equal(15, latest.BagCount);
        Assert.Equal(PalletStatus.Completed.ToString(), latest.Status);
        Assert.NotNull(latest.CompletedAt);

        // Full means full — the sixteenth bag needs a new pallet.
        var late = await service.ScanBagAsync(
            pallet.Id, new ScanBagRequest(bags[15].Barcode, null), ids.UserId);

        Assert.False(late.IsSuccess);
        Assert.Contains("full", late.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_counts_are_worked_out_from_the_bags()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL5");
        var bags = await BagsAsync(db, ids, "PAL5", 3);
        var pallet = await PalletAsync(db, ids);
        var service = NewService(db);

        foreach (var bag in bags.Take(3))
        {
            await service.ScanBagAsync(
                pallet.Id, new ScanBagRequest(bag.Barcode, null), ids.UserId);
        }

        var loaded = await service.GetPalletAsync(pallet.Id);

        // Three bags of 500 plates at 10 kg each. Nothing stored, all counted.
        Assert.Equal(3, loaded.Value!.BagCount);
        Assert.Equal(1500, loaded.Value.PieceCount);
        Assert.Equal(30m, loaded.Value.Weight);
    }

    [Fact]
    public async Task A_bag_cannot_go_on_two_pallets()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL6");
        var bags = await BagsAsync(db, ids, "PAL6", 2);
        var first = await PalletAsync(db, ids);
        var second = await PalletAsync(db, ids);
        var service = NewService(db);

        await service.ScanBagAsync(
            first.Id, new ScanBagRequest(bags[0].Barcode, null), ids.UserId);

        var again = await service.ScanBagAsync(
            second.Id, new ScanBagRequest(bags[0].Barcode, null), ids.UserId);

        Assert.False(again.IsSuccess);

        // The refusal names the pallet, so nobody walks the line looking for it.
        Assert.Contains(
            $"pallet {first.PalletNumber}", again.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Taking_a_bag_off_sends_it_back_and_keeps_the_scan()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL7");
        var bags = await BagsAsync(db, ids, "PAL7", 2);
        var pallet = await PalletAsync(db, ids);
        var service = NewService(db);

        var scanned = await service.ScanBagAsync(
            pallet.Id, new ScanBagRequest(bags[0].Barcode, null), ids.UserId);

        var assignmentId = scanned.Value!.Bags[0].AssignmentId;

        var reversed = await service.ReverseAssignmentAsync(
            assignmentId, new ReverseAssignmentRequest("Scanned onto the wrong pallet"), ids.UserId);

        Assert.True(reversed.IsSuccess, reversed.Message);

        // Off the pallet's count, but still in its history with the reason.
        Assert.Equal(0, reversed.Value!.BagCount);
        Assert.Single(reversed.Value.Bags);
        Assert.False(reversed.Value.Bags[0].IsActive);
        Assert.Equal("Scanned onto the wrong pallet", reversed.Value.Bags[0].ReversalReason);
        Assert.NotNull(reversed.Value.Bags[0].ReversedByName);

        var bag = await db.ProducedBags.AsNoTracking().FirstAsync(b => b.Id == bags[0].Id);
        Assert.Equal(ProducedBagStatus.Available, bag.Status);
    }

    [Fact]
    public async Task A_bag_taken_off_can_go_on_the_right_pallet()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL8");
        var bags = await BagsAsync(db, ids, "PAL8", 2);
        var wrong = await PalletAsync(db, ids);
        var right = await PalletAsync(db, ids);
        var service = NewService(db);

        var scanned = await service.ScanBagAsync(
            wrong.Id, new ScanBagRequest(bags[0].Barcode, null), ids.UserId);

        await service.ReverseAssignmentAsync(
            scanned.Value!.Bags[0].AssignmentId,
            new ReverseAssignmentRequest("Wrong pallet"),
            ids.UserId);

        // This is what the partial unique index buys. A plain one would refuse the
        // second row for ever and the bag could never be packed at all.
        var moved = await service.ScanBagAsync(
            right.Id, new ScanBagRequest(bags[0].Barcode, null), ids.UserId);

        Assert.True(moved.IsSuccess, moved.Message);
        Assert.Equal(1, moved.Value!.BagCount);
    }

    [Fact]
    public async Task Taking_a_bag_off_a_full_pallet_opens_it_again()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL9");
        var bags = await BagsAsync(db, ids, "PAL9", 15);
        var pallet = await PalletAsync(db, ids);
        var service = NewService(db);

        PalletDto? latest = null;
        foreach (var bag in bags)
        {
            latest = (await service.ScanBagAsync(
                pallet.Id, new ScanBagRequest(bag.Barcode, null), ids.UserId)).Value;
        }

        Assert.Equal(PalletStatus.Completed.ToString(), latest!.Status);

        var reversed = await service.ReverseAssignmentAsync(
            latest.Bags[0].AssignmentId,
            new ReverseAssignmentRequest("Torn bag"),
            ids.UserId);

        // Fourteen bags is not a full pallet, so it is open again and can take another.
        Assert.Equal(PalletStatus.Opened.ToString(), reversed.Value!.Status);
        Assert.Null(reversed.Value.CompletedAt);
        Assert.Equal(14, reversed.Value.BagCount);
    }

    [Fact]
    public async Task A_reversal_needs_a_reason()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL10");
        var bags = await BagsAsync(db, ids, "PAL10", 2);
        var pallet = await PalletAsync(db, ids);
        var service = NewService(db);

        var scanned = await service.ScanBagAsync(
            pallet.Id, new ScanBagRequest(bags[0].Barcode, null), ids.UserId);

        var reversed = await service.ReverseAssignmentAsync(
            scanned.Value!.Bags[0].AssignmentId,
            new ReverseAssignmentRequest("   "),
            ids.UserId);

        Assert.False(reversed.IsSuccess);
    }

    [Fact]
    public async Task A_bag_cannot_be_taken_off_twice()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL11");
        var bags = await BagsAsync(db, ids, "PAL11", 2);
        var pallet = await PalletAsync(db, ids);
        var service = NewService(db);

        var scanned = await service.ScanBagAsync(
            pallet.Id, new ScanBagRequest(bags[0].Barcode, null), ids.UserId);
        var id = scanned.Value!.Bags[0].AssignmentId;

        await service.ReverseAssignmentAsync(id, new ReverseAssignmentRequest("First"), ids.UserId);
        var again = await service.ReverseAssignmentAsync(
            id, new ReverseAssignmentRequest("Second"), ids.UserId);

        Assert.False(again.IsSuccess);
    }

    [Fact]
    public async Task A_pallet_cannot_be_built_on_a_line_that_makes_no_bags()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL12");

        var pallet = await NewService(db).StartPalletAsync(
            new StartPalletRequest(ids.ShiftLineId, null), ids.UserId);

        Assert.False(pallet.IsSuccess);
        Assert.Contains("does not make bags", pallet.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_pallet_cannot_be_started_on_a_closed_shift()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL13");

        var report = await db.ShiftReports.FirstAsync(r => r.Id == ids.ShiftReportId);
        report.Status = ShiftReportStatus.Closed;
        await db.SaveChangesAsync();

        var pallet = await NewService(db).StartPalletAsync(
            new StartPalletRequest(ids.ThermoShiftLineId, null), ids.UserId);

        Assert.False(pallet.IsSuccess);
    }

    [Fact]
    public async Task An_open_pallet_is_only_offered_bags_it_can_take()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL14");
        var normal = await BagsAsync(db, ids, "PAL14a", 2);
        var absorbent = await BagsAsync(db, ids, "PAL14b", 2, absorbent: true);
        var pallet = await PalletAsync(db, ids);
        var service = NewService(db);

        // Empty, so anything goes.
        var before = await service.GetAvailableBagsAsync(pallet.Id);
        Assert.Contains(before, b => b.Id == normal[0].Id);
        Assert.Contains(before, b => b.Id == absorbent[0].Id);

        await service.ScanBagAsync(
            pallet.Id, new ScanBagRequest(normal[0].Barcode, null), ids.UserId);

        // Now fixed — offering the rest would only be offering a refusal.
        var after = await service.GetAvailableBagsAsync(pallet.Id);
        Assert.Contains(after, b => b.Id == normal[1].Id);
        Assert.DoesNotContain(after, b => b.Id == absorbent[0].Id);
    }

    [Fact]
    public async Task A_packed_bag_leaves_the_available_list()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL15");
        var bags = await BagsAsync(db, ids, "PAL15", 2);
        var pallet = await PalletAsync(db, ids);
        var service = NewService(db);

        Assert.Contains(await service.GetAvailableBagsAsync(), b => b.Id == bags[0].Id);

        await service.ScanBagAsync(
            pallet.Id, new ScanBagRequest(bags[0].Barcode, null), ids.UserId);

        Assert.DoesNotContain(await service.GetAvailableBagsAsync(), b => b.Id == bags[0].Id);
    }

    [Fact]
    public async Task A_label_from_another_factory_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL16");
        var pallet = await PalletAsync(db, ids);

        var scanned = await NewService(db).ScanBagAsync(
            pallet.Id, new ScanBagRequest("B999999", null), ids.UserId);

        Assert.False(scanned.IsSuccess);
    }

    [Fact]
    public async Task A_pallet_label_scanned_into_the_bag_box_is_refused()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL17");
        var pallet = await PalletAsync(db, ids);

        // The barcode table knows the difference, so this is an answer rather than a
        // failed search (specification section 12).
        var scanned = await NewService(db).ScanBagAsync(
            pallet.Id, new ScanBagRequest(pallet.Barcode, null), ids.UserId);

        Assert.False(scanned.IsSuccess);
        Assert.Contains("not a bag", scanned.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_database_refuses_a_second_live_assignment_for_one_bag()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "PAL18");
        var bags = await BagsAsync(db, ids, "PAL18", 2);
        var first = await PalletAsync(db, ids);
        var second = await PalletAsync(db, ids);

        await NewService(db).ScanBagAsync(
            first.Id, new ScanBagRequest(bags[0].Barcode, null), ids.UserId);

        // Straight to the table, past every check in the service. Two tablets can scan
        // the same bag in the same moment, so this index is the only real guard.
        var duplicate = () => db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "BagPalletAssignments"
                 ("ProducedBagId", "WoodenPalletId", "AssignedByUserId", "AssignedAt")
             VALUES ({bags[0].Id}, {second.Id}, {ids.UserId}, NOW())
             """);

        await Assert.ThrowsAsync<Npgsql.PostgresException>(duplicate);
    }
}
