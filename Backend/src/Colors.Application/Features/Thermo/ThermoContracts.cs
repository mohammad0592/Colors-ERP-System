using Colors.Application.Common.Models;

namespace Colors.Application.Features.Thermo;

// Shapes crossing the API for line 2. Specification section 9.

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
    int? PieceCount,
    // What the roll weighed, and what this run threw away: roll weight less the weight
    // of the plates that came out of it. Both calculated, never stored. Null until the
    // roll has been weighed and the run counted (specification section 9).
    decimal? RollWeight,
    decimal? ScrapWeight,
    // Taken out before it was finished, and why (section 19.23). The roll went back to
    // stock as Not completed; its scrap is said on the run that finishes it.
    bool LeftUnfinished,
    string? UnfinishedReason);

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
    string? Notes,
    // The last correction of the counts, if there was one (section 19.27).
    DateTimeOffset? CorrectedAt = null,
    string? CorrectedByName = null,
    string? CorrectionReason = null);

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
    // What the roll was made for, so what the bags formed from it will be.
    string? ProductName,
    string OperatorName,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int? TotalTimeMinutes,
    string? Notes,
    RollReadingsDto? RollReadings,
    ThermoTestReportDto? TestReport,
    // Roll weight less the weight of the plates it made. Calculated, never stored, and
    // null until the roll has been weighed and the run counted.
    decimal? ScrapWeight,
    IReadOnlyList<ProducedBagDto> Bags,
    bool LeftUnfinished = false,
    string? UnfinishedReason = null);

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
    decimal? Length,
    // What it is formed into. Null until it goes into the thermo, and the start dialog asks
    // (section 19.17); set only on rolls logged when the extruder still asked.
    int? ProductId,
    string? ProductName,
    // So the start dialog offers only the products this recipe makes.
    int RecipeFamilyId,
    // Taken out of the thermo unfinished before, and going in again (section 19.23).
    bool NotCompleted = false);

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
    string? Notes,
    // Only for a roll made before rolls said what they were for. Every other roll
    // already names its product, and this is ignored.
    int? ProductId = null);

/// <summary>Takes the roll out. The run is over; the counting comes next.</summary>
public sealed record FinishThermoRunRequest(DateTimeOffset? FinishedAt);

/// <summary>
/// Takes the roll out before it is finished (specification section 19.23). It goes back
/// to stock as Not completed and may go in again. The reason is required. What it made
/// so far is counted next, as for any run.
/// </summary>
public sealed record LeaveRunUnfinishedRequest(string Reason, DateTimeOffset? FinishedAt = null);

/// <summary>
/// What was counted and measured after forming.
///
/// The product is not here: it is the one the roll was made for. Neither is the piece
/// count, which is the bag count times the product's pieces per bag.
///
/// <b>Saving this creates the bags and their barcodes.</b>
///
/// <c>UtcOffsetMinutes</c> is the tablet's distance from UTC — 180 for UTC+3. Each bag's
/// code carries the date and time it was counted, and the number in it starts again
/// every day at midnight (section 19.25), both on the factory's clock. The server keeps
/// time in UTC, and on a cloud host its own zone is not the factory's, so the tablet
/// says which zone the factory is in.
/// </summary>
/// <summary>
/// Corrects counts that were saved wrong (specification section 19.27) — the same figures
/// as the count, and why. A bag count put up makes the missing bags, with new codes on the
/// factory's clock; put down, it cancels the extra ones, which must not be on a pallet.
/// </summary>
public sealed record CorrectThermoTestRequest(
    int BagCount,
    decimal PieceWeight,
    decimal BagWeight,
    decimal AbsorbentPercentage,
    string? Notes,
    string Reason,
    int UtcOffsetMinutes = 0);

/// <summary>
/// Takes back a run started with the wrong roll (section 19.27), while the roll is still
/// in the machine and nothing has been counted. The roll goes back to stock.
/// </summary>
public sealed record UndoThermoStartRequest(string Reason);

/// <summary>Marks a bag not on a pallet defective, with why (section 19.27).</summary>
public sealed record MarkBagDefectiveRequest(string Reason);

public sealed record SaveThermoTestRequest(
    int BagCount,
    decimal PieceWeight,
    decimal BagWeight,
    decimal AbsorbentPercentage,
    string? Notes,
    int UtcOffsetMinutes = 0);

/// <summary>
/// Line 2 — thermoforming (specification section 9).
///
/// Declared here, implemented in Infrastructure.
/// </summary>
public interface IThermoService
{
    /// <summary>
    /// Runs through the thermo.
    ///
    /// <paramref name="currentShiftOnly"/> is the line screen's view: this shift only,
    /// with the history left to inventory (specification section 19.6).
    ///
    /// Never applied together with <paramref name="openOnly"/>. A run that is still to
    /// be counted is a job waiting for somebody, and a run started before midnight is
    /// counted after it -- scoping the queue to the current shift would strand exactly
    /// the runs section 19.2 is about.
    /// </summary>
    Task<IReadOnlyList<ThermoRunSummaryDto>> GetRunsAsync(
        int? shiftLineId = null,
        bool openOnly = false,
        bool currentShiftOnly = false,
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

    /// <summary>Takes the roll out unfinished and sends it back to stock, with the reason.</summary>
    Task<Result<ThermoRunDto>> LeaveUnfinishedAsync(
        int runId,
        LeaveRunUnfinishedRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Records the counts, creates every bag and prints their barcodes — all or nothing.</summary>
    /// <summary>Corrects saved counts, adding or cancelling bags to match (section 19.27).</summary>
    Task<Result<ThermoRunDto>> CorrectTestReportAsync(
        int runId,
        CorrectThermoTestRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Takes back a run started with the wrong roll; the roll returns to stock.</summary>
    Task<Result<bool>> UndoStartAsync(
        int runId,
        UndoThermoStartRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Marks a bag defective. Only one that is not on a pallet.</summary>
    Task<Result<ProducedBagDto>> MarkBagDefectiveAsync(
        int bagId,
        MarkBagDefectiveRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<Result<ThermoRunDto>> SaveTestReportAsync(
        int runId,
        SaveThermoTestRequest request,
        int userId,
        CancellationToken cancellationToken = default);
}
