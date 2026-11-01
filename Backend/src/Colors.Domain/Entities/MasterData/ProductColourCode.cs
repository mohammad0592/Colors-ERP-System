namespace Colors.Domain.Entities.MasterData;

/// <summary>
/// The product code printed on a bag — <c>AB500B</c>, <c>LB03W</c>, <c>BB</c>
/// (specification section 19.25). Every bag of the same product in the same colour
/// carries the same one.
///
/// A list the factory keeps, not a rule worked out from the product. The factory's own
/// codes follow no single rule: blue is <c>BLUE</c> on one plate and <c>BL</c> on
/// another, and the burger boxes have one code whatever their colour. A rule would have
/// to be bent for each of those, so the codes are written down as the factory uses them,
/// and changed in Master Data.
/// </summary>
public class ProductColourCode
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    /// <summary>
    /// The colour this code is for. Empty means every colour of the product — the burger
    /// boxes are <c>BB</c> and <c>BS</c> whatever they are made in. A colour's own code,
    /// where there is one, comes before it.
    /// </summary>
    public int? ColorId { get; set; }

    public Color? Color { get; set; }

    /// <summary>Capitals, unique across every product and colour.</summary>
    public string Code { get; set; } = string.Empty;
}
