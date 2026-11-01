namespace Colors.Domain.Common;

/// <summary>
/// The product code worked out from a bag's own attributes — <c>AB500B</c> reads as
/// absorbent, 500 pieces to a bag, black (specification section 12).
///
/// Only the fallback now. The codes the factory prints are kept in Master Data
/// (<c>ProductColourCode</c>, section 19.25), because they follow no single rule; this
/// fills in for a product and colour nobody has given a code yet, so a label is never
/// printed with the code missing.
///
/// It identifies the <i>kind</i> of bag, which is why it cannot be the barcode: every bag
/// of black absorbent 500 carries the same one, so scanning it could never say
/// <i>which</i> bag.
/// </summary>
public static class ProductCode
{
    public static string For(bool isAbsorbent, int piecesPerBag, string colourCode) =>
        $"{(isAbsorbent ? "AB" : "NOR")}{piecesPerBag}{colourCode.Trim().ToUpperInvariant()}";
}
