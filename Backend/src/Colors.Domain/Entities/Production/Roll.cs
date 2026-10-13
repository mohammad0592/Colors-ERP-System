using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;

namespace Colors.Domain.Entities.Production;

/// <summary>
/// One roll off the extruder (specification section 8).
///
/// The roll carries its own recipe and colour rather than inheriting them from the
/// batch, because both can change while a batch is still running: the colouring agent
/// is fed separately at the extruder, so the operator switches colour without
/// stopping. The batch never lies about its materials and the roll never lies about
/// its recipe.
/// </summary>
public class Roll
{
    public int Id { get; set; }

    /// <summary>
    /// The day this roll belongs to, taken from its shift rather than the clock. A
    /// roll logged ten minutes after midnight still belongs to the shift that made it.
    /// </summary>
    public DateOnly ProductionDate { get; set; }

    /// <summary>
    /// The leading number in the roll code, from the factory's running count. It does not
    /// start again each day or shift — only when a supervisor resets it — so two rolls
    /// may share a number, and the date and time in the code tell them apart.
    /// </summary>
    public int RollNumber { get; set; }

    /// <summary>
    /// <c>04N2309260844-White</c> — number, recipe type, date and 24-hour out time,
    /// colour. Worked out when the roll is logged, unless the operator types one; unique.
    ///
    /// It is the barcode too: rolls logged since 19.12 carry this code in their QR, in
    /// capitals. Older rolls keep the separate <c>R000123</c> barcode printed on them.
    /// </summary>
    public required string RollCode { get; set; }

    public int BatchId { get; set; }

    public Batch Batch { get; set; } = null!;

    /// <summary>Which formula this roll was actually made to.</summary>
    public int RecipeVersionId { get; set; }

    public RecipeVersion RecipeVersion { get; set; } = null!;

    /// <summary>Chosen per roll — the colouring agent is fed at the extruder.</summary>
    public int ColorId { get; set; }

    public Color Color { get; set; } = null!;

    /// <summary>
    /// What the operator made this roll for (specification section 19.1).
    ///
    /// Declared, because the thickness cannot be trusted to say it. It is also what the
    /// bags formed from this roll are: the mould is the product, and nothing at the thermo
    /// names it a second time (specification section 19.9).
    ///
    /// On the roll and not the batch, because the batch is the whole shift and the
    /// factory changes product mid-shift. The screen remembers the last one used so the
    /// operator picks it once, not on every roll.
    ///
    /// Null only on rolls made before this existed.
    /// </summary>
    public int? ProductId { get; set; }

    public Product? Product { get; set; }

    public int ProducedByUserId { get; set; }

    /// <summary>
    /// A full timestamp, because the owner asked for hour and minute. This is the
    /// "out time" the operator knows — the moment the roll left the extruder — and it
    /// is stored once, here, rather than again on the test report.
    /// </summary>
    public DateTimeOffset ProducedAt { get; set; }

    // ---- measured at the extruder, on the roll production screen ----------
    // Weight and length moved here from the roll test (specification section 19.12):
    // they are taken where the roll comes off, by whoever logs it, and the test is the
    // plate and the thickness. A roll goes to the thermo when it has both.

    /// <summary>Kilograms. Null until it is weighed.</summary>
    public decimal? Weight { get; set; }

    public decimal? Length { get; set; }

    public int? MeasuredByUserId { get; set; }

    public DateTimeOffset? MeasuredAt { get; set; }

    /// <summary>
    /// NeedsTest until it has been both measured and tested, in either order; Available
    /// after that.
    /// </summary>
    public RollStatus Status { get; set; } = RollStatus.NeedsTest;

    public string? Notes { get; set; }

    public RollTestReport? TestReport { get; set; }
}
