using Colors.Application.Features.MasterData;
using Colors.Domain.Entities.MasterData;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.MasterData;

/// <summary>
/// The things the factory makes (specification section 4).
///
/// Its own service rather than one of the name-only lists, because a product carries
/// the packing numbers — pieces per bag, small bags per bag, bags per pallet — and
/// those are what stop 500 and 15 being written into the code.
/// </summary>
public class ProductService(ColorsDbContext db)
    : MasterListService<Product, ProductDto, SaveProductRequest>(db), IProductService
{
    // Read while validating, then written by Apply. The base class's Apply cannot wait
    // on the database, and validation has to load the family anyway to check it exists.
    private bool _familyIsAbsorbent;

    protected override IQueryable<Product> Query() =>
        Db.Products.Include(p => p.Mould).Include(p => p.ProductType).Include(p => p.RecipeFamily);

    protected override ProductDto ToDto(Product entity, bool canDelete) =>
        new(
            entity.Id,
            entity.Name,
            entity.MouldId,
            entity.Mould.Name,
            entity.ProductTypeId,
            entity.ProductType.Name,
            entity.RecipeFamilyId,
            entity.RecipeFamily?.Name,
            entity.IsAbsorbent,
            entity.PiecesPerBag,
            entity.SmallBagsPerBag,
            entity.BagsPerPallet,
            entity.MinThickness,
            entity.MaxThickness,
            entity.IsActive,
            canDelete);

    protected override void Apply(SaveProductRequest request, Product entity)
    {
        entity.Name = request.Name.Trim();
        entity.MouldId = request.MouldId;
        entity.ProductTypeId = request.ProductTypeId;
        entity.RecipeFamilyId = request.RecipeFamilyId;
        // Never typed in: an absorbent product is one made from the absorbent recipe.
        entity.IsAbsorbent = _familyIsAbsorbent;
        entity.PiecesPerBag = request.PiecesPerBag;
        entity.SmallBagsPerBag = request.SmallBagsPerBag;
        entity.BagsPerPallet = request.BagsPerPallet;
        entity.MinThickness = request.MinThickness;
        entity.MaxThickness = request.MaxThickness;
    }

    protected override async Task<string?> ValidateAsync(
        SaveProductRequest request,
        int? existingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "A name is required.";
        }

        if (await NameTakenAsync(request.Name, existingId, cancellationToken))
        {
            return "A product with this name already exists.";
        }

        if (!await Db.Moulds.AnyAsync(m => m.Id == request.MouldId, cancellationToken))
        {
            return "Choose the mould that makes this product.";
        }

        if (!await Db.ProductTypes.AnyAsync(t => t.Id == request.ProductTypeId, cancellationToken))
        {
            return "Choose a product type.";
        }

        var family = await Db.RecipeFamilies
            .Where(f => f.Id == request.RecipeFamilyId && f.IsActive)
            .Select(f => new { f.IsAbsorbent })
            .FirstOrDefaultAsync(cancellationToken);

        if (family is null)
        {
            return "Choose the main recipe this product's rolls are made to.";
        }

        _familyIsAbsorbent = family.IsAbsorbent;

        // The thermo looks a product up by mould and absorbency alone, so that pair
        // must name exactly one thing. Without this a second row would make the lookup
        // ambiguous and the run would have no honest answer.
        var pairTaken = await Db.Products.AnyAsync(
            p => p.MouldId == request.MouldId
                 && p.IsAbsorbent == family.IsAbsorbent
                 && (existingId == null || p.Id != existingId),
            cancellationToken);

        if (pairTaken)
        {
            var mould = await Db.Moulds
                .Where(m => m.Id == request.MouldId)
                .Select(m => m.Name)
                .FirstAsync(cancellationToken);

            return $"{mould} already makes " +
                   (family.IsAbsorbent ? "an absorbent product." : "a normal product.");
        }

        if (request.PiecesPerBag < 1)
        {
            return "Say how many pieces go in one bag.";
        }

        if (request.SmallBagsPerBag < 1)
        {
            return "A bag uses at least one small bag — two for a plate, one for a box.";
        }

        if (request.BagsPerPallet < 1)
        {
            return "Say how many bags complete a pallet.";
        }

        // Either end may be empty -- "more than 3 mm" has no top, and nobody has measured
        // most of these yet -- but a thickness that is given must mean something.
        if (request.MinThickness is <= 0 || request.MaxThickness is <= 0)
        {
            return "A thickness must be more than nothing.";
        }

        return request.MinThickness > request.MaxThickness
            ? "The thinnest cannot be thicker than the thickest."
            : null;
    }

    // Nothing references products yet — bags and pallets arrive in phases 9 and 10.
    // The database's restrict keys will be the backstop, and this is where the count
    // belongs when those tables exist.
}
