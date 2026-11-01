using Colors.Application.Features.MasterData;
using Colors.Domain.Entities.MasterData;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.MasterData;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// The factory's product codes — the code printed on every bag of a product in a colour
/// (specification section 19.25).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ProductCodeTests(DatabaseFixture fixture)
{
    private static async Task<(Product Product, Color Colour)> ProductAndColourAsync(
        ColorsDbContext db,
        string suffix)
    {
        var ids = await FactoryData.CreateAsync(db, suffix);
        var product = await db.Products.SingleAsync(p => p.Id == ids.NormalProductId);
        var colour = await TestSequences.ColourAsync(db);
        return (product, colour);
    }

    [Fact]
    public async Task A_colours_own_code_comes_first_then_the_every_colour_code_then_the_worked_out_one()
    {
        await using var db = fixture.CreateContext();
        var (product, colour) = await ProductAndColourAsync(db, "PCD1");
        var other = await db.Colors.FirstAsync(c => c.Id != colour.Id);
        var service = new ProductCodeService(db);

        // Nothing written down yet: the code worked out from the bag, so a label is never
        // printed blank.
        var book = await ProductCodeBook.LoadAsync(db, CancellationToken.None);
        Assert.Equal(
            $"NOR{product.PiecesPerBag}{colour.Code}",
            book.For(product.Id, colour.Id, false, product.PiecesPerBag, colour.Code));

        Assert.True((await service.CreateAsync(new SaveProductCodeRequest(product.Id, null, "pcd1 all"))).IsSuccess);
        Assert.True((await service.CreateAsync(new SaveProductCodeRequest(product.Id, colour.Id, "PCD1X"))).IsSuccess);

        book = await ProductCodeBook.LoadAsync(db, CancellationToken.None);
        Assert.Equal("PCD1X", book.For(product.Id, colour.Id, false, product.PiecesPerBag, colour.Code));
        // Typed with a space and in lower case, kept as it is printed.
        Assert.Equal("PCD1ALL", book.For(product.Id, other.Id, false, product.PiecesPerBag, other.Code));
    }

    [Fact]
    public async Task A_code_names_one_kind_of_bag()
    {
        await using var db = fixture.CreateContext();
        var (product, colour) = await ProductAndColourAsync(db, "PCD2");
        var (otherProduct, _) = await ProductAndColourAsync(db, "PCD2b");
        var service = new ProductCodeService(db);

        Assert.True((await service.CreateAsync(new SaveProductCodeRequest(product.Id, colour.Id, "PCD2"))).IsSuccess);

        var sameCode = await service.CreateAsync(new SaveProductCodeRequest(otherProduct.Id, colour.Id, "PCD2"));
        var sameColour = await service.CreateAsync(new SaveProductCodeRequest(product.Id, colour.Id, "PCD2Y"));

        Assert.Equal("productCode.codeTaken", sameCode.MessageCode);
        Assert.Equal("productCode.colourTaken", sameColour.MessageCode);
    }

    [Fact]
    public async Task The_database_refuses_a_second_every_colour_code()
    {
        await using var db = fixture.CreateContext();
        var (product, _) = await ProductAndColourAsync(db, "PCD3");

        db.ProductColourCodes.Add(new ProductColourCode { ProductId = product.Id, Code = "PCD3A" });
        await db.SaveChangesAsync();

        // Behind the service's own check: two empty colours are the same colour here.
        db.ProductColourCodes.Add(new ProductColourCode { ProductId = product.Id, Code = "PCD3B" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
