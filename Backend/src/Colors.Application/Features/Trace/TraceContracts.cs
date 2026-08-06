using Colors.Application.Common.Models;

namespace Colors.Application.Features.Trace;

/// <summary>
/// Where one thing came from, and what it became (specification section 13).
///
/// Every report in that section is a summary. This is the opposite: a man holds one
/// label and wants everything behind it. Nothing new is stored for it — every link is
/// already a foreign key, so this is a read-only view over records that exist and it
/// cannot disagree with them.
/// </summary>

/// <summary>One material on a ticket issued to the shift that mixed.</summary>
public sealed record TraceMaterialDto(
    int TicketNumber,
    string Material,
    decimal Issued,
    decimal Returned,
    // What the shift actually consumed — the whole point of weighing both ends.
    decimal Used,
    string UnitSymbol);

/// <summary>The mix, and what went into the shift that made it.</summary>
public sealed record TraceMixDto(
    int BatchNumber,
    string ShiftName,
    DateOnly ProductionDate,
    string ProductionLineName,
    IReadOnlyList<TraceMaterialDto> Materials,
    /// <summary>
    /// True always, for now, and shown on the page rather than hidden. The ticket names
    /// the shift line, not the mix, so the honest sentence is "issued to the shift that
    /// made this roll" — with one mix per shift that is the same set of materials.
    /// </summary>
    bool IssuedToShiftNotMix);

public sealed record TraceRollDto(
    int Id,
    string RollCode,
    string Barcode,
    int RecipeNumber,
    string RecipeFamilyName,
    string ColorName,
    string ShiftName,
    DateOnly ProductionDate,
    string ProducedByName,
    DateTimeOffset ProducedAt,
    string Status,
    // All four come from the roll's measurements, so all four are empty together.
    decimal? Weight,
    decimal? Length,
    decimal? PlateWeight,
    decimal? AverageThickness);

public sealed record TraceThermoDto(
    int Id,
    string ShiftName,
    DateOnly ProductionDate,
    string OperatorName,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int? TotalTimeMinutes,
    string? MouldName,
    string? ProductName,
    int? BagCount,
    int? PieceCount,
    decimal? PieceWeight,
    decimal? BagWeight,
    decimal? AbsorbentPercentage);

/// <summary>A bag, always carrying the roll it came from — that is the whole point.</summary>
public sealed record TraceBagDto(
    int Id,
    string Barcode,
    string RollCode,
    string ProductName,
    string ColorName,
    decimal Weight,
    int PieceCount,
    string Status,
    int? PalletNumber);

public sealed record TracePalletDto(
    int Id,
    int PalletNumber,
    string Barcode,
    string? ProductName,
    string? ColorName,
    string Status,
    int BagCount,
    int? Capacity,
    int PieceCount,
    decimal Weight,
    string ShiftName,
    DateOnly ProductionDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

/// <summary>
/// The whole chain for one barcode. Which parts are filled in depends on what was
/// scanned, and the screen simply shows the ones that are there.
/// </summary>
public sealed record TraceDto(
    string Barcode,
    // Roll · Bag · Pallet — what the label turned out to name.
    string Kind,
    string Headline,
    TraceMixDto? Mix,
    TraceRollDto? Roll,
    TraceThermoDto? Thermo,
    TraceBagDto? Bag,
    TracePalletDto? Pallet,
    // Forwards. A roll lists the bags it made; a pallet lists the bags on it, each
    // naming its own roll — which is how a pallet built from three rolls reads.
    IReadOnlyList<TraceBagDto> Bags);

/// <summary>
/// Traceability (specification section 13). Declared here, implemented in Infrastructure.
/// </summary>
public interface ITraceService
{
    /// <summary>Scan or type any barcode — a roll, a bag or a pallet.</summary>
    Task<Result<TraceDto>> GetAsync(string barcode, CancellationToken cancellationToken = default);
}
