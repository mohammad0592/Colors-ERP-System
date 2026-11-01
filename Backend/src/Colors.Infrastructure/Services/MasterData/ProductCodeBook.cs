using Colors.Domain.Common;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.MasterData;

/// <summary>
/// The factory's product codes, read once for a screen or a report, answering "what code
/// goes on this bag" (specification section 19.25).
///
/// The colour's own code comes first, then the product's code for every colour, and only
/// then the code worked out from the bag (<see cref="ProductCode"/>) — so a product or
/// colour added after the list was written still prints something rather than a blank.
///
/// Read whole rather than per bag: the list is a few dozen rows, and a stock screen of
/// several hundred bags would otherwise ask the database the same question each time.
/// </summary>
public sealed class ProductCodeBook
{
    private readonly Dictionary<(int ProductId, int? ColorId), string> _codes;

    private ProductCodeBook(Dictionary<(int ProductId, int? ColorId), string> codes) => _codes = codes;

    public static async Task<ProductCodeBook> LoadAsync(
        ColorsDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await db.ProductColourCodes
            .AsNoTracking()
            .Select(c => new { c.ProductId, c.ColorId, c.Code })
            .ToListAsync(cancellationToken);

        return new ProductCodeBook(rows.ToDictionary(r => (r.ProductId, r.ColorId), r => r.Code));
    }

    public string For(
        int productId,
        int colorId,
        bool isAbsorbent,
        int piecesPerBag,
        string colourCode)
    {
        if (_codes.TryGetValue((productId, colorId), out var own))
        {
            return own;
        }

        return _codes.TryGetValue((productId, null), out var everyColour)
            ? everyColour
            : ProductCode.For(isAbsorbent, piecesPerBag, colourCode);
    }
}
