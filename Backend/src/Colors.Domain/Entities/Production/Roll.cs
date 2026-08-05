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

    /// <summary>Starts at 1 each day and resets the next — the leading number in the roll code.</summary>
    public int DailySerial { get; set; }

    /// <summary>
    /// <c>01WN180726A</c> — serial, colour letter, recipe family code, DDMMYY, shift.
    /// Generated, never typed, and unique.
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

    public int ProducedByUserId { get; set; }

    /// <summary>
    /// A full timestamp, because the owner asked for hour and minute. This is the
    /// "out time" the operator knows — the moment the roll left the extruder — and it
    /// is stored once, here, rather than again on the test report.
    /// </summary>
    public DateTimeOffset ProducedAt { get; set; }

    public RollStatus Status { get; set; } = RollStatus.NeedsTest;

    public string? Notes { get; set; }

    public RollTestReport? TestReport { get; set; }
}
