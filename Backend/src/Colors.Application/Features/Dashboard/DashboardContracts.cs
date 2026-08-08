using Colors.Application.Common.Models;
using Colors.Application.Features.Reports;

namespace Colors.Application.Features.Dashboard;

// Shapes crossing the API for the home screen. Specification section 13.

/// <summary>
/// One thing waiting for somebody.
///
/// A list rather than a fixed set of fields, so the screen shows what is actually
/// waiting and stays quiet about the rest. A dashboard that always shows seven boxes,
/// five of them zero, teaches people to stop reading it.
/// </summary>
public sealed record DashboardAlertDto(
    // What kind of thing is waiting, so the screen knows where to send the reader.
    string Kind,
    // What to call one of them, and what to call several.
    //
    // Both are given because English plurals cannot be worked out from the singular —
    // a screen that adds an "s" produces "3 Roll waiting to be measureds", which is
    // exactly what happened before this field existed.
    string Label,
    string LabelPlural,
    int Count,
    // A sentence saying why it matters, in the factory's own terms.
    string Detail,
    // True where this stops a shift from closing (specification section 2).
    bool BlocksShiftClose);

public sealed record DashboardShiftDto(
    int ShiftReportId,
    DateOnly ProductionDate,
    string ShiftName,
    string? SupervisorName,
    DateTimeOffset OpenedAt,
    IReadOnlyList<string> LineNames);

public sealed record DashboardDto(
    // The shift running now, or null when the factory is between shifts.
    DashboardShiftDto? OpenShift,
    // What that shift has made so far. The same figures the shift summary report gives,
    // read through the same code so the home screen cannot disagree with it.
    ShiftSummaryReportDto? Summary,
    IReadOnlyList<DashboardAlertDto> NeedsAttention);

/// <summary>
/// The home screen (specification section 13).
///
/// Read-only and open to every signed-in worker: it answers "what is happening, and what
/// is waiting for someone", which everybody on the floor needs.
///
/// Declared here, implemented in Infrastructure.
/// </summary>
public interface IDashboardService
{
    Task<Result<DashboardDto>> GetAsync(CancellationToken cancellationToken = default);
}
