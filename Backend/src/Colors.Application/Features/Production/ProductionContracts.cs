using Colors.Application.Common.Models;

namespace Colors.Application.Features.Production;

/// <summary>Shapes crossing the API for line 1. Specification section 8.</summary>

public sealed record BatchSummaryDto(
    int Id,
    int BatchNumber,
    int ShiftLineId,
    string ProductionLineName,
    string ShiftName,
    DateOnly ProductionDate,
    string CreatedByName,
    bool IsFinished,
    int RollCount,
    // Only rolls that have been measured contribute; the rest have no weight yet.
    decimal? TotalRollWeight,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);

public sealed record RollSummaryDto(
    int Id,
    string RollCode,
    string Barcode,
    int DailySerial,
    DateOnly ProductionDate,
    int BatchId,
    int BatchNumber,
    int RecipeVersionId,
    int RecipeNumber,
    string RecipeFamilyName,
    int ColorId,
    string ColorName,
    string Status,
    bool NeedsTest,
    string ProducedByName,
    DateTimeOffset ProducedAt,
    decimal? Weight,
    decimal? AverageThickness);

public sealed record RollTestReportDto(
    int Id,
    decimal Weight,
    decimal Length,
    decimal PlateWeight,
    decimal ThicknessRs,
    decimal ThicknessRm,
    decimal ThicknessLm,
    decimal ThicknessLs,
    // The mean of the four. Calculated on the server, never stored.
    decimal AverageThickness,
    string TestedByName,
    DateTimeOffset TestedAt,
    string? Notes);

public sealed record RollDto(
    int Id,
    string RollCode,
    string Barcode,
    int DailySerial,
    DateOnly ProductionDate,
    int BatchId,
    int BatchNumber,
    int RecipeVersionId,
    int RecipeNumber,
    string RecipeFamilyName,
    int ColorId,
    string ColorName,
    string Status,
    bool NeedsTest,
    string ProducedByName,
    DateTimeOffset ProducedAt,
    string? Notes,
    RollTestReportDto? TestReport);

/// <summary>
/// Logs a roll off the extruder.
///
/// Named by the <b>shift line</b>, not by a batch. The mixer is filled once a shift, so
/// the batch is the extruder's part of that shift — the first roll creates it and every
/// roll after joins it. The operator never sees one (specification section 8).
///
/// The recipe and colour are given per roll, not inherited from the mix: the colouring
/// agent is fed separately at the extruder, so both can change while the same mix is
/// still running.
/// </summary>
public sealed record CreateRollRequest(
    int ShiftLineId,
    int RecipeVersionId,
    int ColorId,
    // The "out time" the operator knows. Null means now — he is usually standing at
    // the machine, but may be logging a roll a few minutes late.
    DateTimeOffset? ProducedAt,
    string? Notes);

/// <summary>
/// The measurements, taken once as the roll leaves the extruder.
///
/// Saving this is what moves the roll from <c>Needs Test</c> to <c>Available</c>.
/// </summary>
public sealed record SaveRollTestRequest(
    decimal Weight,
    decimal Length,
    decimal PlateWeight,
    decimal ThicknessRs,
    decimal ThicknessRm,
    decimal ThicknessLm,
    decimal ThicknessLs,
    string? Notes);

/// <summary>
/// Line 1 — the mixer and the extruder (specification section 8).
///
/// Declared here, implemented in Infrastructure.
/// </summary>
public interface IProductionService
{
    Task<IReadOnlyList<BatchSummaryDto>> GetBatchesAsync(
        int? shiftReportId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default);




    Task<IReadOnlyList<RollSummaryDto>> GetRollsAsync(
        int? batchId = null,
        bool needsTestOnly = false,
        CancellationToken cancellationToken = default);

    Task<Result<RollDto>> GetRollAsync(int rollId, CancellationToken cancellationToken = default);

    /// <summary>Logs a roll and prints its barcode, both or neither.</summary>
    Task<Result<RollDto>> CreateRollAsync(
        CreateRollRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Records the measurements and makes the roll available to the thermo.</summary>
    Task<Result<RollDto>> SaveTestReportAsync(
        int rollId,
        SaveRollTestRequest request,
        int userId,
        CancellationToken cancellationToken = default);
}
