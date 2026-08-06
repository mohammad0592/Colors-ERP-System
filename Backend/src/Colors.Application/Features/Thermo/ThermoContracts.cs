using Colors.Application.Common.Models;

namespace Colors.Application.Features.Thermo;

/// <summary>Shapes crossing the API for line 2. Specification section 9.</summary>

public sealed record ThermoRunSummaryDto(
    int Id,
    int RollId,
    string RollCode,
    string RollBarcode,
    string ColorName,
    int RecipeNumber,
    string RecipeFamilyName,
    bool IsAbsorbent,
    int ShiftLineId,
    string ProductionLineName,
    string ShiftName,
    DateOnly ProductionDate,
    string OperatorName,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    // Worked out from the two timestamps, never stored.
    int? TotalTimeMinutes,
    bool IsFinished,
    bool NeedsTest,
    string? ProductName,
    int? BagCount,
    int? PieceCount);

public sealed record ThermoTestReportDto(
    int Id,
    int ProductId,
    string ProductName,
    int BagCount,
    // BagCount × the product's pieces per bag, frozen at save.
    int PieceCount,
    decimal PieceWeight,
    decimal BagWeight,
    decimal AbsorbentPercentage,
    string TestedByName,
    DateTimeOffset TestedAt,
    string? Notes);

/// <summary>
/// The roll's own measurements, shown read-only beside the thermo form.
///
/// They are on the paper form in this position, so the man expects to see them — but
/// they are never re-entered here, because they already exist against the roll.
/// </summary>
public sealed record RollReadingsDto(
    decimal Weight,
    decimal Length,
    decimal PlateWeight,
    decimal AverageThickness);

public sealed record ThermoRunDto(
    int Id,
    int RollId,
    string RollCode,
    string RollBarcode,
    int ColorId,
    string ColorName,
    int RecipeNumber,
    string RecipeFamilyName,
    bool IsAbsorbent,
    int ShiftLineId,
    string ProductionLineName,
    string ShiftName,
    DateOnly ProductionDate,
    string? MouldName,
    string OperatorName,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int? TotalTimeMinutes,
    string? Notes,
    RollReadingsDto? RollReadings,
    ThermoTestReportDto? TestReport,
    IReadOnlyList<ProducedBagDto> Bags);

public sealed record ProducedBagDto(
    int Id,
    string Barcode,
    string ColorName,
    string ProductName,
    decimal Weight,
    int PieceCount,
    string Status,
    DateTimeOffset CreatedAt);

/// <summary>
/// A roll ready for the thermo, with everything the operator needs to pick it without
/// walking to the store.
/// </summary>
public sealed record AvailableRollDto(
    int Id,
    string RollCode,
    string Barcode,
    string ColorName,
    int RecipeNumber,
    string RecipeFamilyName,
    bool IsAbsorbent,
    DateOnly ProductionDate,
    decimal? Weight,
    decimal? Length);

/// <summary>
/// Puts a roll into the thermo. The operator scans the barcode — he never types the
/// roll number, recipe, colour or product, all of which come from the roll.
/// </summary>
public sealed record StartThermoRunRequest(
    // Either one. The floor scans; the office may pick from the list.
    string? RollBarcode,
    int? RollId,
    int ShiftLineId,
    DateTimeOffset? StartedAt,
    string? Notes);

/// <summary>Takes the roll out. The run is over; the counting comes next.</summary>
public sealed record FinishThermoRunRequest(DateTimeOffset? FinishedAt);

/// <summary>
/// What was counted and measured after forming.
///
/// The product is not here: it is decided by the mould on the line and the absorbency
/// of the roll's recipe. Neither is the piece count, which is the bag count times the
/// product's pieces per bag.
///
/// <b>Saving this creates the bags and their barcodes.</b>
/// </summary>
public sealed record SaveThermoTestRequest(
    int BagCount,
    decimal PieceWeight,
    decimal BagWeight,
    decimal AbsorbentPercentage,
    string? Notes);

/// <summary>
/// Line 2 — thermoforming (specification section 9).
///
/// Declared here, implemented in Infrastructure.
/// </summary>
public interface IThermoService
{
    Task<IReadOnlyList<ThermoRunSummaryDto>> GetRunsAsync(
        int? shiftLineId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default);

    Task<Result<ThermoRunDto>> GetRunAsync(int runId, CancellationToken cancellationToken = default);

    /// <summary>Rolls that have been measured and not yet formed.</summary>
    Task<IReadOnlyList<AvailableRollDto>> GetAvailableRollsAsync(
        CancellationToken cancellationToken = default);

    Task<Result<ThermoRunDto>> StartRunAsync(
        StartThermoRunRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<Result<ThermoRunDto>> FinishRunAsync(
        int runId,
        FinishThermoRunRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Records the counts, creates every bag and prints their barcodes — all or nothing.</summary>
    Task<Result<ThermoRunDto>> SaveTestReportAsync(
        int runId,
        SaveThermoTestRequest request,
        int userId,
        CancellationToken cancellationToken = default);
}
