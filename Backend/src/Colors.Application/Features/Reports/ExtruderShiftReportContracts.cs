using Colors.Application.Common.Models;

namespace Colors.Application.Features.Reports;

// The extruder's report for one shift (specification section 13.1): every roll made on it,
// with everything known about each. The factory has no paper form for the extruder — the
// nearest is the Roll Log's export — so the layout is the system's own. Every figure is
// read from records that already exist; nothing here is stored.

/// <summary>One roll made on the shift, with everything recorded against it.</summary>
public sealed record ExtruderReportRollDto(
    int RollNumber,
    string RollCode,
    // The out time. A moment, not a clock reading: the screen shows it in the tablet's
    // time and the Excel export in the time of the browser that asked for it.
    DateTimeOffset ProducedAt,
    int BatchNumber,
    int RecipeNumber,
    string RecipeFamilyName,
    bool IsAbsorbent,
    string? ProductName,
    string ColorName,
    string Status,
    // Measured on the production screen. Null until then.
    decimal? Weight,
    decimal? Length,
    // The roll test, each null until taken (section 19.15).
    decimal? PlateWeight,
    decimal? ThicknessRs,
    decimal? ThicknessRm,
    decimal? ThicknessLm,
    decimal? ThicknessLs,
    decimal? AverageThickness,
    // The range the product asks for, as it stands today, and the verdict kept when the
    // roll was tested — which is the one that counts (section 19.1).
    decimal? MinThickness,
    decimal? MaxThickness,
    bool? ThicknessInSpec,
    decimal? AbsorbencyDryWeight,
    decimal? AbsorbencyWetWeight,
    decimal? AbsorbencyPercentage,
    // Every reading the roll asks for is in.
    bool TestComplete,
    string ProducedByName,
    string? MeasuredByName,
    string? TestedByName,
    // Where it went: the thermo shift that formed it, when it has gone in.
    DateOnly? FormedOnDate,
    string? FormedOnShiftName,
    string? Notes,
    string? TestNotes);

/// <summary>The shift's rolls for one product: how many, how heavy, how thick.</summary>
public sealed record ExtruderReportProductDto(
    string ProductName,
    int RollCount,
    // Measured rolls only — the rest have no weight yet.
    decimal TotalWeight,
    decimal TotalLength,
    // The mean of the tested rolls' averages. Null when none is tested.
    decimal? AverageThickness,
    int OutOfSpecCount);

public sealed record ExtruderShiftReportDto(
    int ShiftReportId,
    DateOnly ProductionDate,
    string ShiftName,
    string Status,
    string LineName,
    string? OperatorName,
    string? OperatorEmployeeNumber,
    // "HH:mm"
    string? ProductionStartTime,
    string? ProductionEndTime,
    decimal? DowntimeHours,
    decimal? ActualProductionHours,
    IReadOnlyList<string> Workers,
    IReadOnlyList<string> Trainees,
    string? Notes,
    // The mix the shift ran — one, as a rule (specification section 8).
    IReadOnlyList<int> BatchNumbers,
    IReadOnlyList<ExtruderReportRollDto> Rolls,
    int RollCount,
    int MeasuredCount,
    int TestedCount,
    // Measured and tested: free for the thermo, or already in it.
    int ReadyCount,
    int OutOfSpecCount,
    decimal TotalWeight,
    decimal TotalLength,
    IReadOnlyList<ExtruderReportProductDto> Products);

/// <summary>The extruder's report for one shift.</summary>
public interface IExtruderShiftReportService
{
    Task<Result<ExtruderShiftReportDto>> GetAsync(
        int shiftReportId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Writes the report as an Excel file to print or send on. Declared here; the spreadsheet
/// library lives in Infrastructure.
/// </summary>
public interface IExtruderShiftReportExcel
{
    /// <param name="utcOffset">The factory's clock against UTC, from the browser that
    /// asked: the server knows no time zone, and the out times must read as the man at the
    /// machine wrote them.</param>
    byte[] Build(ExtruderShiftReportDto report, TimeSpan utcOffset);
}
