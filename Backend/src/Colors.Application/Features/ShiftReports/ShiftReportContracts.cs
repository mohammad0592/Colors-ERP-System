using Colors.Application.Common.Models;

namespace Colors.Application.Features.ShiftReports;

/// <summary>Shapes crossing the API for shift reports. Specification section 2.</summary>

/// <summary>
/// One person on a line during a shift. <c>RoleInShift</c> is what they did on this
/// shift, which is not the same as the roles they hold: the same man is both extruder
/// operator and extruder test person, so only the shift can say which job he was doing.
/// </summary>
public sealed record ShiftWorkerDto(
    int UserId,
    string EmployeeNumber,
    string FullName,
    int? RoleInShiftId,
    string? RoleInShiftName,
    bool IsTrainee);

public sealed record SaveShiftWorkerRequest(int UserId, int? RoleInShiftId, bool IsTrainee);

/// <summary>One line's part of a shift — its hours, its meter, its machine, its crew.</summary>
public sealed record ShiftLineDto(
    int Id,
    int ProductionLineId,
    string ProductionLineName,
    // From the line itself: true only for the thermo, and it decides whether the
    // screen shows the machine settings at all.
    bool RecordsMachineSettings,
    string? ProductionStartTime,
    string? ProductionEndTime,
    decimal? DowntimeHours,
    // Calculated on the server, so the screen shows the same number the reports use
    // rather than working it out again.
    decimal? ActualProductionHours,
    int? MachineSpeed,
    int? FeedDistanceMm,
    decimal? CycleTimeSeconds,
    IReadOnlyList<ShiftWorkerDto> Workers);

/// <summary>Everything recorded for one line while the shift runs. Times are "HH:mm".</summary>
public sealed record UpdateShiftLineRequest(
    string? ProductionStartTime,
    string? ProductionEndTime,
    decimal? DowntimeHours,
    int? MachineSpeed,
    int? FeedDistanceMm,
    decimal? CycleTimeSeconds,
    IReadOnlyList<SaveShiftWorkerRequest> Workers);

/// <summary>A shift in a list — enough for the table, without its crews.</summary>
public sealed record ShiftReportSummaryDto(
    int Id,
    DateOnly ProductionDate,
    int ShiftId,
    string ShiftName,
    string Status,
    bool IsOpen,
    string? SupervisorName,
    // The lines that ran, in the order they are shown — "Extruder, Thermo".
    IReadOnlyList<string> LineNames,
    int LineCount,
    int WorkerCount,
    // One meter for the whole factory, so this is the shift's own reading.
    decimal? ElectricityUsed,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt);

public sealed record ShiftReportDto(
    int Id,
    DateOnly ProductionDate,
    int ShiftId,
    string ShiftName,
    string Status,
    bool IsOpen,
    int? SupervisorUserId,
    string? SupervisorName,
    // One meter for the whole building, so it is read once per shift.
    decimal? ElectricityStartMeter,
    decimal? ElectricityEndMeter,
    decimal? ElectricityUsed,
    string? Notes,
    string OpenedByName,
    DateTimeOffset OpenedAt,
    string? ClosedByName,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<ShiftLineDto> Lines);

/// <summary>
/// Opens a shift and the lines that are running. Times and readings are filled in as
/// the shift goes on; more lines can be added later if one starts late.
/// </summary>
public sealed record OpenShiftReportRequest(
    DateOnly ProductionDate,
    int ShiftId,
    int? SupervisorUserId,
    IReadOnlyList<int> ProductionLineIds);

/// <summary>The shift's own details. Each line is updated through its own endpoint.</summary>
public sealed record UpdateShiftReportRequest(
    int? SupervisorUserId,
    decimal? ElectricityStartMeter,
    decimal? ElectricityEndMeter,
    string? Notes);

/// <summary>Adds a line that started after the shift was opened.</summary>
public sealed record AddShiftLineRequest(int ProductionLineId);

/// <summary>An administrator reopening a closed shift must say why.</summary>
public sealed record ReopenShiftReportRequest(string Reason);

/// <summary>
/// Shift reports — one date, one shift, for the whole factory, with the lines that
/// ran hanging underneath.
///
/// Declared here, implemented in Infrastructure (specification section 0.1).
/// </summary>
public interface IShiftReportService
{
    Task<IReadOnlyList<ShiftReportSummaryDto>> GetAllAsync(
        int? productionLineId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default);

    Task<Result<ShiftReportDto>> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<ShiftReportDto>> OpenAsync(
        OpenShiftReportRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<Result<ShiftReportDto>> UpdateAsync(
        int id,
        UpdateShiftReportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a line to an open shift — one that started later than the others.</summary>
    Task<Result<ShiftReportDto>> AddLineAsync(
        int id,
        AddShiftLineRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ShiftReportDto>> UpdateLineAsync(
        int id,
        int lineId,
        UpdateShiftLineRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a line that did not run after all. Never one with work on it.</summary>
    Task<Result<ShiftReportDto>> RemoveLineAsync(
        int id,
        int lineId,
        CancellationToken cancellationToken = default);

    /// <summary>Ends the shift and every line on it. Nothing more may be posted afterwards.</summary>
    Task<Result<ShiftReportDto>> CloseAsync(
        int id,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Reopens a closed shift. Administrator only, and the reason is recorded.</summary>
    Task<Result<ShiftReportDto>> ReopenAsync(
        int id,
        ReopenShiftReportRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Discards an empty shift opened by mistake — never one with production on it.</summary>
    Task<Result<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
