using Colors.Domain.Entities.MasterData;
using Colors.Domain.Enums;

namespace Colors.Domain.Entities.Production;

/// <summary>
/// One packed bag (specification section 9).
///
/// The packed bag is the unit that carries a barcode, goes on a pallet and gets
/// counted. The small bags used as an inner liner are packaging material, not tracked
/// objects — <c>Product.SmallBagsPerBag</c> says how many each one consumes.
///
/// Created automatically when the test report is saved, because the bag count does not
/// exist until the end of the run. One row per bag, one barcode per bag.
/// </summary>
public class ProducedBag
{
    public int Id { get; set; }

    public int ThermoProductionId { get; set; }

    /// <summary>The whole chain back to the materials: → roll → batch → recipe.</summary>
    public ThermoProduction ThermoProduction { get; set; } = null!;

    /// <summary>
    /// Copied at creation, with <see cref="ProductId"/> — the design's one deliberate
    /// duplication. A pallet scan checks two values in one comparison instead of
    /// walking five joins, and it happens thousands of times a day. Written once and
    /// never editable, so the copy cannot drift (specification section 0.1).
    /// </summary>
    public int ColorId { get; set; }

    public Color Color { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    /// <summary>Kilograms. Copied from the run's measured bag weight and frozen there.</summary>
    public decimal Weight { get; set; }

    /// <summary>Frozen at creation from the product, for the same reason as <see cref="ThermoTestReport.PieceCount"/>.</summary>
    public int PieceCount { get; set; }

    public ProducedBagStatus Status { get; set; } = ProducedBagStatus.Available;

    public DateTimeOffset CreatedAt { get; set; }

    public string? Notes { get; set; }
}
