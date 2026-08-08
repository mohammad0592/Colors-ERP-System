using Colors.Application.Common.Models;

namespace Colors.Application.Features.MaterialIssue;

// Shapes crossing the API for issue tickets. Specification section 7.

public sealed record IssueTicketLineDto(
    int Id,
    int MaterialId,
    string MaterialCode,
    string MaterialName,
    string BaseUnitSymbol,
    decimal IssuedQuantity,
    decimal ReturnedQuantity,
    // Issued minus returned — what was really used. Calculated, never stored.
    decimal NetUsed);

public sealed record IssueTicketSummaryDto(
    int Id,
    int TicketNumber,
    int ShiftLineId,
    string ProductionLineName,
    string ShiftName,
    DateOnly ProductionDate,
    string Status,
    bool IsOpen,
    string IssuedByName,
    int LineCount,
    decimal TotalIssued,
    decimal TotalReturned,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt);

public sealed record IssueTicketDto(
    int Id,
    int TicketNumber,
    int ShiftLineId,
    string ProductionLineName,
    string ShiftName,
    DateOnly ProductionDate,
    string Status,
    bool IsOpen,
    string IssuedByName,
    string? ClosedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    string? Notes,
    IReadOnlyList<IssueTicketLineDto> Lines);

/// <summary>One material on a new ticket, weighed out of the store.</summary>
public sealed record IssueLineRequest(int MaterialId, decimal Quantity);

/// <summary>
/// Issuing material. Every line leaves the store the moment the ticket is created —
/// that is what makes the stock figure true while the shift is still running.
/// </summary>
public sealed record CreateIssueTicketRequest(
    int ShiftLineId,
    string? Notes,
    IReadOnlyList<IssueLineRequest> Lines);

/// <summary>What came back, weighed. A material not named here returned nothing.</summary>
public sealed record ReturnLineRequest(int MaterialId, decimal Quantity);

public sealed record RecordReturnsRequest(IReadOnlyList<ReturnLineRequest> Lines);

/// <summary>
/// Material issue and return (specification section 7).
///
/// Declared here, implemented in Infrastructure.
/// </summary>
public interface IMaterialIssueService
{
    Task<IReadOnlyList<IssueTicketSummaryDto>> GetAllAsync(
        int? shiftReportId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default);

    Task<Result<IssueTicketDto>> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Issues the material and takes it out of the store, both or neither.</summary>
    Task<Result<IssueTicketDto>> CreateAsync(
        CreateIssueTicketRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Weighs the leftover back in. May be done more than once as it comes back.</summary>
    Task<Result<IssueTicketDto>> RecordReturnsAsync(
        int id,
        RecordReturnsRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Closes the ticket. What is not returned by now counts as used.</summary>
    Task<Result<IssueTicketDto>> CloseAsync(
        int id,
        int userId,
        CancellationToken cancellationToken = default);
}
