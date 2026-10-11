using Colors.Application.Common.Models;

namespace Colors.Application.Features.Reports;

// The thermoforming line's daily report — the paper form headed "Daily Production
// Report for the Forming Department" (تقرير الإنتاج اليومي لقسم التشكيل), one per shift.
// Every figure is read from records that already exist; nothing here is stored.

/// <summary>One roll formed on the shift, as a row of the form.</summary>
public sealed record ThermoReportRollDto(
    string RollCode,
    string ColorName,
    decimal? RollWeight,
    decimal? RollLength,
    decimal? AverageThickness,
    int? TotalTimeMinutes,
    string? ProductName,
    // What one bag of the product holds — the 250 or 500 the form multiplies by.
    int? PiecesPerBag,
    // Null until the bags have been counted.
    int? BagCount,
    int? PieceCount,
    // Grams, as weighed. The paper form worked it out from the bag weight instead.
    decimal? PieceWeight,
    decimal? BagWeight,
    bool IsAbsorbent,
    decimal? AbsorbentPercentage,
    // The share of recycled material in the roll's recipe, when it has any.
    decimal? RecycledPercentage,
    // The shift the roll was made on at the extruder — often an earlier one.
    string RollShiftName,
    // Roll weight less the plates it made. Null until counted.
    decimal? ScrapWeight);

/// <summary>One product's yield over the shift: what went in, what came out.</summary>
public sealed record ThermoReportProductDto(
    string ProductName,
    // The code printed on the bags — NOR500W, AB500B.
    string ProductCode,
    decimal RollWeight,
    int PieceCount,
    decimal ProductWeight,
    decimal ScrapWeight,
    // A share of the roll weight, 0–100. Null when no roll weight was counted.
    decimal? ScrapPercentage);

/// <summary>A pallet built on the shift.</summary>
public sealed record ThermoReportPalletDto(
    int PalletNumber,
    string Status,
    int BagCount,
    int? PiecesPerBag,
    int? PieceCount,
    string? ProductName,
    string? ColorName,
    // The rolls its bags came from.
    IReadOnlyList<string> RollCodes);

/// <summary>A packaging material used on the shift, as recorded at the end of it.</summary>
public sealed record ThermoReportPackagingDto(
    string MaterialName,
    string UnitSymbol,
    decimal Quantity,
    decimal? Weight);

public sealed record ThermoShiftReportDto(
    int ShiftReportId,
    DateOnly ProductionDate,
    string ShiftName,
    string Status,
    string LineName,
    string? OperatorName,
    string? OperatorEmployeeNumber,
    decimal? ElectricityStartMeter,
    decimal? ElectricityEndMeter,
    decimal? ElectricityUsed,
    decimal? CycleTimeSeconds,
    int? FeedDistanceMm,
    int? MachineSpeed,
    // "HH:mm"
    string? ProductionStartTime,
    string? ProductionEndTime,
    decimal? DowntimeHours,
    decimal? ActualProductionHours,
    IReadOnlyList<string> Workers,
    IReadOnlyList<string> Trainees,
    string? Notes,
    IReadOnlyList<ThermoReportRollDto> Rolls,
    decimal TotalRollWeight,
    int TotalBagCount,
    int TotalPieceCount,
    IReadOnlyList<ThermoReportProductDto> Products,
    IReadOnlyList<ThermoReportPalletDto> Pallets,
    // False until somebody records the packaging at the end of the shift.
    bool PackagingRecorded,
    IReadOnlyList<ThermoReportPackagingDto> Packaging);

/// <summary>The thermoforming report for one shift.</summary>
public interface IThermoShiftReportService
{
    Task<Result<ThermoShiftReportDto>> GetAsync(
        int shiftReportId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Writes the report into the factory's own Excel form, so the file looks like the paper
/// it replaces. Declared here; the spreadsheet library lives in Infrastructure.
/// </summary>
public interface IThermoShiftReportExcel
{
    byte[] Build(ThermoShiftReportDto report);
}
