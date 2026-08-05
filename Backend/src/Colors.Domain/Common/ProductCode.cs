namespace Colors.Domain.Common;

/// <summary>
/// The product code the factory already prints on every bag label — <c>AB500B</c>
/// (specification section 12).
///
/// <c>AB500B</c> reads as: absorbent, 500 pieces to a bag, black. It identifies the
/// <i>kind</i> of bag, which is why it cannot be the barcode: every bag of black
/// absorbent 500 carries the same one, so scanning it could never say <i>which</i> bag.
/// It stays on the label as text because that is what people read.
///
/// Always derived from the bag's own attributes, never stored, so it cannot end up
/// disagreeing with them.
/// </summary>
public static class ProductCode
{
    public static string For(bool isAbsorbent, int piecesPerBag, string colourCode) =>
        $"{(isAbsorbent ? "AB" : "NOR")}{piecesPerBag}{colourCode.Trim().ToUpperInvariant()}";
}
