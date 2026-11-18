using Colors.Application.Common.Models;

namespace Colors.Application.Features.ShiftSchedule;

// Shapes crossing the API for the shift schedule. Specification section 19.30.

/// <summary>One line planned to run, and its operator.</summary>
public sealed record ScheduledShiftLineDto(
    int ProductionLineId,
    string ProductionLineName,
    // The job the operator must hold, so the picker offers only those people.
    string? OperatorRole,
    int? OperatorUserId,
    string? OperatorName);

/// <summary>A shift planned for today or tomorrow, and what has become of it.</summary>
public sealed record ScheduledShiftDto(
    int Id,
    DateOnly ProductionDate,
    int ShiftId,
    string ShiftName,
    string StartTime,
    string EndTime,
    int? SupervisorUserId,
    string? SupervisorName,
    bool ExtruderStartingShift,
    IReadOnlyList<ScheduledShiftLineDto> Lines,
    // Planned, Running, Overrunning, Finished or NotOpened. Overrunning is a shift past
    // its end that could not close itself, because work is still open on it and no
    // shift came after to take it over.
    string Status,
    // Still a plan and not yet started, so it can be changed or cancelled.
    bool CanChange,
    int? ShiftReportId,
    string? NotOpenedReason,
    string ScheduledByName,
    DateTimeOffset ScheduledAt,
    string? ChangedByName,
    DateTimeOffset? ChangedAt);

/// <summary>A line as the schedule offers it, with the job its operator must hold.</summary>
public sealed record ScheduleLineOptionDto(
    int Id,
    string Name,
    bool MakesRolls,
    string? OperatorRole);

/// <summary>
/// Today and tomorrow, on the factory's clock. Today is shown because a plan can still be
/// changed until its shift starts; only tomorrow can be planned.
/// </summary>
public sealed record ShiftScheduleDto(
    DateOnly Today,
    DateOnly Tomorrow,
    IReadOnlyList<ScheduledShiftDto> Shifts,
    IReadOnlyList<ScheduleLineOptionDto> Lines);

public sealed record ScheduleShiftLineRequest(int ProductionLineId, int? OperatorUserId);

/// <summary>Plans one of tomorrow's shifts.</summary>
public sealed record ScheduleShiftRequest(
    DateOnly ProductionDate,
    int ShiftId,
    int? SupervisorUserId,
    IReadOnlyList<ScheduleShiftLineRequest> Lines,
    bool ExtruderStartingShift = false);

/// <summary>Changes a plan before its shift starts. The day and the shift stay as they are.</summary>
public sealed record UpdateScheduledShiftRequest(
    int? SupervisorUserId,
    IReadOnlyList<ScheduleShiftLineRequest> Lines,
    bool ExtruderStartingShift = false);

/// <summary>What one check of the clock did — for the log, and for the tests.</summary>
public sealed record ScheduleRunResult(
    IReadOnlyList<int> OpenedShiftReportIds,
    IReadOnlyList<int> ClosedShiftReportIds,
    IReadOnlyList<int> NotOpenedScheduleIds);

/// <summary>
/// The shift schedule: tomorrow's shifts planned the day before, and opened and closed by
/// the system at their times.
///
/// Declared here, implemented in Infrastructure (specification section 0.1).
/// </summary>
public interface IShiftScheduleService
{
    Task<ShiftScheduleDto> GetAsync(CancellationToken cancellationToken = default);

    Task<Result<ScheduledShiftDto>> ScheduleAsync(
        ScheduleShiftRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<Result<ScheduledShiftDto>> UpdateAsync(
        int id,
        UpdateScheduledShiftRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Cancels a plan whose shift has not started.</summary>
    Task<Result<bool>> CancelAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens every planned shift whose time has come, and closes every scheduled shift
    /// whose time is up. Called by the clock every half a minute; safe to call as often
    /// as anyone likes, because each thing it does is done once.
    /// </summary>
    Task<ScheduleRunResult> RunDueAsync(CancellationToken cancellationToken = default);
}
