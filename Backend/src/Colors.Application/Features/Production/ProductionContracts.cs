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
    // thickness) and the measuring (weight and length), in either order. A test saved
    // part-way still needs finishing (section 19.15).
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
    bool? ThicknessInSpec,
    // Whether its recipe is absorbent, so the test asks for the absorbency (19.14), and
    // the percentage once tested.
    bool IsAbsorbent,
    decimal? AbsorbencyPercentage,
    // While the absorbency sample is in the water: when it may be weighed again. Null
    // before the dry weight is saved and after the wet one is (section 19.15).
    DateTimeOffset? AbsorbencyWetDueAt);

public sealed record RollTestReportDto(
    int Id,
    // Each null until it is taken: the test is saved a reading at a time (section 19.15).
    decimal? PlateWeight,
    decimal? ThicknessRs,
    decimal? ThicknessRm,
    decimal? ThicknessLm,
    decimal? ThicknessLs,
    // The mean of the four, once all four are in. Calculated on the server, never stored.
    decimal? AverageThickness,
    // Whether that mean fell inside the product's range when it was measured. Null where
    // the product had no range to be inside.
    bool? ThicknessInSpec,
    // Absorbent rolls only: the two weights, and (wet − dry) ÷ dry × 100 worked out.
    decimal? AbsorbencyDryWeight,
    decimal? AbsorbencyWetWeight,
    decimal? AbsorbencyPercentage,
    DateTimeOffset? AbsorbencyDryWeighedAt,
    DateTimeOffset? AbsorbencyWetDueAt,
    // Every reading the roll asks for is in.
    bool IsComplete,
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
/// given again to correct it, until the roll goes into the thermo. The label is printed
/// after this, because it carries both (section 19.15).
/// </summary>
public sealed record MeasureRollRequest(decimal Weight, decimal Length);

/// <summary>The running roll number, and when it was last put back to 1.</summary>
public sealed record RollNumberDto(int NextNumber, DateTimeOffset? LastResetAt, string? LastResetByName);

/// <summary>
/// Corrects anything the operator chose when he logged the roll (section 19.15) — the
/// recipe, the product, the colour, the out time, the code and the note — until the roll
/// goes into the thermo.
/// </summary>
public sealed record UpdateRollRequest(
    // Null means the code worked out again from the roll's number and the corrected
    // recipe, time and colour — what the screen sends unless somebody typed one.
    string? RollCode,
    DateTimeOffset ProducedAt,
    int RecipeVersionId,
    int ColorId,
    // Required, except on a roll made before products were declared that never had one.
    int? ProductId,
    string? Notes);

/// <summary>
/// The roll test: the sample plate and the four thickness readings, and on an absorbent
/// roll the absorbency.
///
/// The whole test as it stands, sent every time it is saved: a null is a reading not yet
/// taken, or one cleared (section 19.15). Once every reading is in, it moves the roll from
/// <c>Needs Test</c> to <c>Available</c> with the roll's weight and length
/// (<see cref="MeasureRollRequest"/>) — whichever is finished second.
/// </summary>
public sealed record SaveRollTestRequest(
    decimal? PlateWeight,
    decimal? ThicknessRs,
    decimal? ThicknessRm,
    decimal? ThicknessLm,
    decimal? ThicknessLs,
    string? Notes,
    // Absorbent rolls only: a piece weighed dry, then after ten minutes in water. Grams.
    // The wet weight is refused until the dry one has been saved that long (19.15).
    decimal? AbsorbencyDryWeight = null,
    decimal? AbsorbencyWetWeight = null);

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
    ///
    /// With <paramref name="forRollId"/>, the code that roll would have with these choices,
    /// keeping its own number — for the edit screen.
    /// </summary>
    Task<Result<string>> SuggestRollCodeAsync(
        int? shiftLineId,
        int recipeVersionId,
        int colorId,
        DateTimeOffset? outAt,
        int? forRollId = null,
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

    /// <summary>Corrects what was chosen when the roll was logged, while its shift is open.</summary>
    Task<Result<RollDto>> UpdateRollAsync(
        int rollId,
        UpdateRollRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Logs a roll and prints its barcode, both or neither.</summary>
    Task<Result<RollDto>> CreateRollAsync(
        CreateRollRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the test as it stands, complete or not. Complete and measured, the roll is
    /// available to the thermo.
    /// </summary>
    Task<Result<RollDto>> SaveTestReportAsync(
        int rollId,
        SaveRollTestRequest request,
        int userId,
        CancellationToken cancellationToken = default);
}
