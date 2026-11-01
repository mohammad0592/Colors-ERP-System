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
    // True always, for now, and shown on the page rather than hidden. The ticket names
    // the shift line, not the mix, so the honest sentence is "issued to the shift that
    // made this roll" — with one mix per shift that is the same set of materials.
    bool IssuedToShiftNotMix);

/// <summary>The roll's own test, every reading on it, and who took them.</summary>
public sealed record TraceRollTestDto(
    string TestedByName,
    DateTimeOffset TestedAt,
    decimal? PlateWeight,
    // The four corners, as the test sheet has them: right side, right middle, left
    // middle, left side.
    decimal? ThicknessRs,
    decimal? ThicknessRm,
    decimal? ThicknessLm,
    decimal? ThicknessLs,
    decimal? AverageThickness,
    bool? ThicknessInSpec,
    decimal? AbsorbencyDryWeight,
    decimal? AbsorbencyWetWeight,
    decimal? AbsorbencyPercentage,
    string? Notes);

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
    decimal? AverageThickness,
    // Named at the thermo (section 19.17), so empty until the roll has gone in.
    string? ProductName,
    string? Notes,
    string? MeasuredByName,
    DateTimeOffset? MeasuredAt,
    TraceRollTestDto? Test,
    // Cancelled or scrapped (section 19.27): when, by whom and why.
    DateTimeOffset? RemovedAt,
    string? RemovedByName,
    string? RemovalReason);

public sealed record TraceThermoDto(
    int Id,
    string ShiftName,
    DateOnly ProductionDate,
    string OperatorName,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int? TotalTimeMinutes,
    string? ProductName,
    int? BagCount,
    int? PieceCount,
    decimal? PieceWeight,
    decimal? BagWeight,
    decimal? AbsorbentPercentage,
    string ProductionLineName,
    string? Notes,
    // Taken out before it was finished (section 19.23), and why.
    string? UnfinishedReason,
    string? CountedByName,
    DateTimeOffset? CountedAt,
    string? CountNotes,
    // Counts corrected after they were saved (section 19.27).
    DateTimeOffset? CorrectedAt,
    string? CorrectedByName,
    string? CorrectionReason,
    // Roll weight less the plates it made. Calculated, never stored (section 9).
    decimal? ScrapWeight);

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
    int? PalletNumber,
    // AB500B: the kind of bag, as its label prints it (section 19.25).
    string ProductCode,
    DateTimeOffset CreatedAt,
    string? Notes,
    // Marked defective, or cancelled by a count correction (section 19.27).
    DateTimeOffset? RemovedAt,
    string? RemovedByName,
    string? RemovalReason,
    string? PalletBarcode);

/// <summary>
/// One time a bag went onto a pallet. Kept when it came off again, so a bag moved from
/// one pallet to another shows both, and why it was moved.
/// </summary>
public sealed record TracePalletMoveDto(
    int PalletNumber,
    string? PalletBarcode,
    DateTimeOffset AssignedAt,
    string AssignedByName,
    DateTimeOffset? ReversedAt,
    string? ReversedByName,
    string? ReversalReason);

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
    DateTimeOffset? CompletedAt,
    // P in front of its bags' code — PAB500B (section 19.26). Empty while nothing is on it.
    string? GeneralCode,
    string CreatedByName,
    DateTimeOffset? ClosedAt,
    string? ClosedByName,
    DateTimeOffset? ShippedAt,
    string? ShippedByName,
    DateTimeOffset? ShippingReversedAt,
    string? ShippingReversedByName,
    string? ShippingReversalReason,
    DateTimeOffset? CancelledAt,
    string? CancelledByName,
    string? CancellationReason,
    string? Notes);

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
    IReadOnlyList<TraceBagDto> Bags,
    // A roll's runs through the thermo, first to last. More than one when it was taken
    // out unfinished and went in again (section 19.23).
    IReadOnlyList<TraceThermoDto> Runs,
    // A bag's pallets, the ones it came off included.
    IReadOnlyList<TracePalletMoveDto> PalletHistory);

/// <summary>
/// Traceability (specification section 13). Declared here, implemented in Infrastructure.
/// </summary>
public interface ITraceService
{
    /// <summary>Scan or type any barcode — a roll, a bag or a pallet.</summary>
    Task<Result<TraceDto>> GetAsync(string barcode, CancellationToken cancellationToken = default);
}
