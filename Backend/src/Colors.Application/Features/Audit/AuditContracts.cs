namespace Colors.Application.Features.Audit;

/// <summary>Shapes crossing the API for the audit log. Specification section 15.</summary>

public sealed record AuditEntryDto(
    int Id,
    int? UserId,
    string? UserName,
    int? ShiftReportId,
    string? ShiftLabel,
    string Action,
    string ObjectType,
    int? ObjectId,
    string Result,
    string? Details,
    DateTimeOffset Timestamp);

/// <summary>
/// Reading the audit log (specification section 15).
///
/// Reading only. Nothing in this system writes a line from business code, and nothing
/// anywhere edits or deletes one — a log that can be tidied is not a log.
/// </summary>
public interface IAuditService
{
    Task<IReadOnlyList<AuditEntryDto>> GetAsync(
        int? shiftReportId = null,
        string? objectType = null,
        bool refusalsOnly = false,
        int take = 200,
        CancellationToken cancellationToken = default);
}
