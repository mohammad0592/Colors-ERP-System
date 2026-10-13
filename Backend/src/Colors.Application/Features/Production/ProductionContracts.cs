using Colors.Application.Common.Models;

namespace Colors.Application.Features.Production;

// Shapes crossing the API for line 1. Specification section 8.

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
    int RollNumber,
    DateOnly ProductionDate,
    int BatchId,
    int BatchNumber,
    int RecipeVersionId,
    int RecipeNumber,
    string RecipeFamilyName,
    int ColorId,
    string ColorName,
    string Status,
    // What is still to be done before the thermo may take it: the test (plate and
    // thickness) and the measuring (weight and length), in either order.
    bool NeedsTest,
    bool NeedsMeasure,
    string ProducedByName,
    DateTimeOffset ProducedAt,
    // Null until the roll has been measured on the production screen.
    decimal? Weight,
    decimal? Length,
    // Null until it has been tested.
    decimal? AverageThickness,
    // What it was made for, and the range that product asks for. Null on rolls made
    // before products were declared, and the range is null wherever nobody has set one.
    int? ProductId,
    string? ProductName,
    decimal? MinThickness,
    decimal? MaxThickness,
    // Decided when it was measured and kept (specification section 19.1). Null until
    // measured, and null for a product with no range.
    bool? ThicknessInSpec);

public sealed record RollTestReportDto(
    int Id,
    decimal PlateWeight,
    decimal ThicknessRs,
    decimal ThicknessRm,
    decimal ThicknessLm,
    decimal ThicknessLs,
    // The mean of the four. Calculated on the server, never stored.
    decimal AverageThickness,
    // Whether that mean fell inside the product's range when it was measured. Null where
    // the product had no range to be inside.
    bool? ThicknessInSpec,
    string TestedByName,
    DateTimeOffset TestedAt,
    string? Notes);

public sealed record RollDto(
    int Id,
    string RollCode,
    string Barcode,
    int RollNumber,
    DateOnly ProductionDate,
    int BatchId,
    int BatchNumber,
    int RecipeVersionId,
    int RecipeNumber,
    string RecipeFamilyName,
    int ColorId,
    string ColorName,
    int? ProductId,
    string? ProductName,
    decimal? MinThickness,
    decimal? MaxThickness,
    string Status,
    bool NeedsTest,
    bool NeedsMeasure,
    string ProducedByName,
    DateTimeOffset ProducedAt,
    decimal? Weight,
    decimal? Length,
    string? MeasuredByName,
    DateTimeOffset? MeasuredAt,
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
    // What the roll is being made for (specification section 19.1). Required: nullable
    // here only so a missing one is answered in words rather than as a malformed request.
    int? ProductId,
    // The "out time" the operator knows, sent with the tablet's own UTC offset: the
    // date and time in the roll code are the factory's clock, and the server knows no
    // time zone. Null means now, in UTC — only the tests send that.
    DateTimeOffset? ProducedAt,
    string? Notes,
    // The code to write on the roll. Null means the one the system works out, which is
    // nearly always right; the operator types one only when the roll was labelled by
    // hand differently.
    string? RollCode = null);

/// <summary>
/// The roll's weight and length, taken on the production screen as it comes off. May be
/// given again to correct it, until the roll goes into the thermo.
/// </summary>
public sealed record MeasureRollRequest(decimal Weight, decimal Length);

/// <summary>The running roll number, and when it was last put back to 1.</summary>
public sealed record RollNumberDto(int NextNumber, DateTimeOffset? LastResetAt, string? LastResetByName);

/// <summary>
/// Corrects a roll's code or out time after it was logged — a code mistyped on the
/// label, a roll logged late and stamped with the wrong time.
/// </summary>
public sealed record UpdateRollRequest(string RollCode, DateTimeOffset ProducedAt);

/// <summary>
/// The roll test: the sample plate and the four thickness readings, taken once.
///
/// With the roll's weight and length (<see cref="MeasureRollRequest"/>) it moves the roll
/// from <c>Needs Test</c> to <c>Available</c> — whichever of the two comes second.
/// </summary>
public sealed record SaveRollTestRequest(
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




    /// <summary>
    /// Rolls off the extruder.
    ///
    /// <paramref name="currentShiftOnly"/> is what the line screen asks for: the man at
    /// the machine is working this shift and everything older is noise, and the
    /// inventory screen is where the past belongs (specification section 19.6).
    ///
    /// It is deliberately not applied to <paramref name="needsTestOnly"/>. A roll made
    /// at the end of one shift and measured on the next is normal, and hiding it would
    /// leave it at NeedsTest for ever -- invisible, and refused by the thermo.
    /// </summary>
    Task<IReadOnlyList<RollSummaryDto>> GetRollsAsync(
        int? batchId = null,
        bool needsTestOnly = false,
        bool currentShiftOnly = false,
        CancellationToken cancellationToken = default);

    Task<Result<RollDto>> GetRollAsync(int rollId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The code the next roll would get, so the screen can show it before it is saved.
    /// A suggestion only: another roll logged first takes the number.
    /// </summary>
    Task<Result<string>> SuggestRollCodeAsync(
        int shiftLineId,
        int recipeVersionId,
        int colorId,
        DateTimeOffset? outAt,
        CancellationToken cancellationToken = default);

    /// <summary>Records or corrects a roll's weight and length.</summary>
    Task<Result<RollDto>> MeasureRollAsync(
        int rollId,
        MeasureRollRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<RollNumberDto> GetRollNumberAsync(CancellationToken cancellationToken = default);

    /// <summary>Puts the running roll number back to 1. The supervisor's decision.</summary>
    Task<RollNumberDto> ResetRollNumberAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Corrects a roll's code and out time while its shift is still open.</summary>
    Task<Result<RollDto>> UpdateRollAsync(
        int rollId,
        UpdateRollRequest request,
        CancellationToken cancellationToken = default);

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
