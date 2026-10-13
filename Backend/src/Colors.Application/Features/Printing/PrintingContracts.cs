using Colors.Application.Common.Models;

namespace Colors.Application.Features.Printing;

// The label printer's queue (specification section 19.13).

public sealed record PrintJobDto(
    int Id,
    IReadOnlyList<string> Barcodes,
    int LabelCount,
    string? Headline,
    string Source,
    string RequestedByName,
    DateTimeOffset RequestedAt,
    string Status,
    string? DoneByName,
    DateTimeOffset? DoneAt);

/// <summary>Labels to print, from whichever screen made them.</summary>
public sealed record QueuePrintJobRequest(
    IReadOnlyList<string> Barcodes,
    string? Headline,
    string? Source);

/// <summary>
/// The queue between the tablets, which have no printer, and the computer that has one.
/// </summary>
public interface IPrintQueueService
{
    Task<Result<PrintJobDto>> QueueAsync(
        QueuePrintJobRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Waiting jobs, oldest first, so labels come out in the order they were made; or, with
    /// <paramref name="pendingOnly"/> false, the most recent jobs of any status.
    /// </summary>
    Task<IReadOnlyList<PrintJobDto>> GetAsync(
        bool pendingOnly,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<Result<PrintJobDto>> MarkPrintedAsync(
        int id,
        int userId,
        CancellationToken cancellationToken = default);

    Task<Result<PrintJobDto>> CancelAsync(
        int id,
        int userId,
        CancellationToken cancellationToken = default);
}
