using Colors.Domain.Common;
using Colors.Domain.Entities.Barcodes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Services.Barcodes;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// Barcodes, against a real database (specification section 12).
///
/// Rolls, bags and pallets do not exist yet — they arrive in phases 8 to 10 — so these
/// drive the service directly. That is the point: the promises made here are the ones
/// those phases will rely on, and they should be proved before anything depends on them.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class BarcodeServiceTests(DatabaseFixture fixture)
{
    private BarcodeService NewService() =>
        new(fixture.CreateContext(), TimeProvider.System);

    [Fact]
    public async Task Issuing_gives_each_object_its_own_value()
    {
        var service = NewService();

        var first = await service.IssueAsync(BarcodeObjectType.Roll, 1001);
        var second = await service.IssueAsync(BarcodeObjectType.Roll, 1002);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value!.Value, second.Value!.Value);
        Assert.StartsWith("R", first.Value.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issuing_twice_for_one_object_returns_the_barcode_it_already_has()
    {
        var service = NewService();

        var first = await service.IssueAsync(BarcodeObjectType.Bag, 2001);
        var again = await service.IssueAsync(BarcodeObjectType.Bag, 2001);

        // One physical bag, one identity. A retry must not print a second.
        Assert.Equal(first.Value!.Value, again.Value!.Value);
        Assert.Equal(first.Value.Id, again.Value.Id);
    }

    [Fact]
    public async Task Each_type_has_its_own_prefix()
    {
        var service = NewService();

        var roll = await service.IssueAsync(BarcodeObjectType.Roll, 3001);
        var bag = await service.IssueAsync(BarcodeObjectType.Bag, 3002);
        var pallet = await service.IssueAsync(BarcodeObjectType.Pallet, 3003);

        Assert.StartsWith("R", roll.Value!.Value, StringComparison.Ordinal);
        Assert.StartsWith("B", bag.Value!.Value, StringComparison.Ordinal);
        Assert.StartsWith("P", pallet.Value!.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Values_are_never_reused_even_when_the_row_is_rolled_back()
    {
        // The reason for a sequence rather than MAX + 1: a discarded transaction must
        // not free a code for a different object.
        string abandoned;

        await using (var db = fixture.CreateContext())
        {
            var service = new BarcodeService(db, TimeProvider.System);
            await using var transaction = await db.Database.BeginTransactionAsync();

            var issued = await service.IssueAsync(BarcodeObjectType.Roll, 4001);
            abandoned = issued.Value!.Value;

            await transaction.RollbackAsync();
        }

        var next = await NewService().IssueAsync(BarcodeObjectType.Roll, 4002);

        Assert.NotEqual(abandoned, next.Value!.Value);
    }

    [Fact]
    public async Task The_database_refuses_a_duplicate_value()
    {
        var issued = await NewService().IssueAsync(BarcodeObjectType.Pallet, 5001);

        await using var db = fixture.CreateContext();
        db.Barcodes.Add(new Barcode
        {
            Value = issued.Value!.Value,
            ObjectType = BarcodeObjectType.Bag,
            ObjectId = 5002,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        // Application code cannot promise this on its own — two tablets can print in
        // the same millisecond. The unique index is the guarantee.
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_scan_says_what_it_found()
    {
        var service = NewService();
        var issued = await service.IssueAsync(BarcodeObjectType.Bag, 6001);

        var found = await service.LookupAsync(issued.Value!.Value);

        Assert.True(found.Value!.Found);
        Assert.Equal("Bag", found.Value.ObjectType);
        Assert.Equal(6001, found.Value.ObjectId);
    }

    [Theory]
    [InlineData(" {0} ")]
    [InlineData("{0}\n")]
    public async Task A_scan_survives_what_scanners_and_thumbs_add(string wrapper)
    {
        var service = NewService();
        var issued = await service.IssueAsync(BarcodeObjectType.Roll, 7001);

        // Scanners append a newline; a man retyping a torn label adds spaces. Neither
        // should be the difference between finding a roll and not.
        var typed = string.Format(wrapper, issued.Value!.Value.ToLowerInvariant());

        var found = await service.LookupAsync(typed);

        Assert.True(found.Value!.Found);
        Assert.Equal(7001, found.Value.ObjectId);
    }

    [Fact]
    public async Task An_unknown_label_is_an_answer_not_an_error()
    {
        var found = await NewService().LookupAsync("B999999");

        // An unrecognised label is ordinary on a factory floor. The screen shows it
        // rather than blowing up.
        Assert.True(found.IsSuccess);
        Assert.False(found.Value!.Found);
        Assert.Contains("B999999", found.Value.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scanning_a_bag_into_a_pallet_field_says_what_it_actually_is()
    {
        var service = NewService();
        var bag = await service.IssueAsync(BarcodeObjectType.Bag, 8001);

        var found = await service.LookupAsync(bag.Value!.Value, BarcodeObjectType.Pallet);

        // The answer a per-table column could never give: not "nothing found", but
        // "the wrong kind of thing".
        Assert.True(found.Value!.Found);
        Assert.Contains("bag", found.Value.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a pallet", found.Value.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scanning_the_expected_type_passes_through_unchanged()
    {
        var service = NewService();
        var pallet = await service.IssueAsync(BarcodeObjectType.Pallet, 9001);

        var found = await service.LookupAsync(pallet.Value!.Value, BarcodeObjectType.Pallet);

        Assert.True(found.Value!.Found);
        Assert.DoesNotContain("not a", found.Value.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_replaced_label_still_resolves_and_says_so()
    {
        var service = NewService();
        var original = await service.IssueAsync(BarcodeObjectType.Roll, 10001);

        var replacement = await service.ReplaceAsync(original.Value!.Value);
        Assert.True(replacement.IsSuccess);
        Assert.NotEqual(original.Value.Value, replacement.Value!.Value);

        // A roll found weeks later still wearing the old label must be recognised,
        // not come back as unknown.
        var oldScan = await service.LookupAsync(original.Value.Value);
        Assert.True(oldScan.Value!.Found);
        Assert.False(oldScan.Value.IsActive);
        Assert.Contains("replaced", oldScan.Value.Message, StringComparison.OrdinalIgnoreCase);

        var newScan = await service.LookupAsync(replacement.Value.Value);
        Assert.True(newScan.Value!.IsActive);
        Assert.Equal(10001, newScan.Value.ObjectId);
    }

    [Fact]
    public async Task Replacing_twice_is_refused()
    {
        var service = NewService();
        var original = await service.IssueAsync(BarcodeObjectType.Bag, 11001);

        await service.ReplaceAsync(original.Value!.Value);
        var again = await service.ReplaceAsync(original.Value.Value);

        Assert.False(again.IsSuccess);
    }

    [Fact]
    public async Task An_object_keeps_every_label_it_has_worn()
    {
        var service = NewService();
        var original = await service.IssueAsync(BarcodeObjectType.Pallet, 12001);
        await service.ReplaceAsync(original.Value!.Value);

        var history = await service.GetForObjectAsync(BarcodeObjectType.Pallet, 12001);

        Assert.Equal(2, history.Count);
        Assert.Single(history, b => b.IsActive);
    }

    [Fact]
    public async Task An_object_id_of_zero_is_refused()
    {
        var issued = await NewService().IssueAsync(BarcodeObjectType.Roll, 0);

        Assert.False(issued.IsSuccess);
    }

    [Theory]
    [InlineData(true, 500, "B", "AB500B")]
    [InlineData(false, 500, "W", "NOR500W")]
    [InlineData(false, 250, "g", "NOR250G")]
    public void The_product_code_matches_the_label_the_factory_prints(
        bool isAbsorbent,
        int piecesPerBag,
        string colourCode,
        string expected)
    {
        // AB500B is the code photographed on a real bag of black absorbent plates.
        Assert.Equal(expected, ProductCode.For(isAbsorbent, piecesPerBag, colourCode));
    }
}
