using Colors.Application.Common.Models;
using Colors.Application.Features.Printing;
using Colors.Domain.Common;
using Colors.Domain.Entities.Barcodes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Printing;

/// <summary>
/// The label printer's queue (specification section 19.13).
///
/// A tablet queues labels; the computer with the printer reads the queue, prints, and
/// marks each job done. Done jobs stay, so a label torn the next day can be found and
/// printed again, and the record says who printed what.
/// </summary>
public class PrintQueueService(ColorsDbContext db, TimeProvider timeProvider) : IPrintQueueService
{
    // A thermo run makes a dozen or two bags; a whole shift's labels in one job would be
    // a mistake rather than a request.
    private const int MaxLabelsPerJob = 200;

    public async Task<Result<PrintJobDto>> QueueAsync(
        QueuePrintJobRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        // Cleaned the way every lookup cleans, and each label once.
        var barcodes = (request.Barcodes ?? [])
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(RollCode.AsBarcode)
            .Distinct()
            .ToList();

        if (barcodes.Count == 0)
        {
            return Invalid("There is no label to print.", "print.nothingToPrint");
        }

        if (barcodes.Count > MaxLabelsPerJob)
        {
            return Invalid(
                $"At most {MaxLabelsPerJob} labels go in one job.",
                "print.tooMany",
                MaxLabelsPerJob.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        // Only labels the system issued and still uses: the printer must never be sent
        // something it would print as a blank, nor a label that has been replaced.
        var known = await db.Barcodes
            .Where(b => barcodes.Contains(b.Value) && b.IsActive)
            .Select(b => b.Value)
            .ToListAsync(cancellationToken);

        var unknown = barcodes.Except(known).FirstOrDefault();
        if (unknown is not null)
        {
            return Invalid($"No label in the system matches {unknown}.", "barcode.noMatch", unknown);
        }

        var job = new PrintJob
        {
            Barcodes = barcodes,
            Headline = Trimmed(request.Headline, 300),
            Source = Trimmed(request.Source, 100) ?? "—",
            RequestedByUserId = userId,
            RequestedAt = timeProvider.GetUtcNow(),
            Status = PrintJobStatus.Pending,
        };

        db.PrintJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        return Result<PrintJobDto>.Success((await ToDtosAsync([job], cancellationToken))[0]);
    }

    public async Task<IReadOnlyList<PrintJobDto>> GetAsync(
        bool pendingOnly,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = db.PrintJobs.AsNoTracking();

        var jobs = pendingOnly
            ? await query
                .Where(j => j.Status == PrintJobStatus.Pending)
                .OrderBy(j => j.RequestedAt)
                .Take(Math.Clamp(take, 1, 200))
                .ToListAsync(cancellationToken)
            : await query
                .OrderByDescending(j => j.RequestedAt)
                .Take(Math.Clamp(take, 1, 200))
                .ToListAsync(cancellationToken);

        return await ToDtosAsync(jobs, cancellationToken);
    }

    public Task<Result<PrintJobDto>> MarkPrintedAsync(
        int id,
        int userId,
        CancellationToken cancellationToken = default) =>
        FinishAsync(id, userId, PrintJobStatus.Printed, cancellationToken);

    public Task<Result<PrintJobDto>> CancelAsync(
        int id,
        int userId,
        CancellationToken cancellationToken = default) =>
        FinishAsync(id, userId, PrintJobStatus.Cancelled, cancellationToken);

    private async Task<Result<PrintJobDto>> FinishAsync(
        int id,
        int userId,
        PrintJobStatus status,
        CancellationToken cancellationToken)
    {
        var job = await db.PrintJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
        {
            return Result<PrintJobDto>.Failure(ErrorCode.NotFound, "This print job does not exist.", "print.notFound");
        }

        // Printing a job again from the history is allowed and changes nothing: the first
        // print is the one on record. Only a waiting job moves.
        if (job.Status == PrintJobStatus.Pending)
        {
            job.Status = status;
            job.DoneByUserId = userId;
            job.DoneAt = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result<PrintJobDto>.Success((await ToDtosAsync([job], cancellationToken))[0]);
    }

    private async Task<List<PrintJobDto>> ToDtosAsync(
        IReadOnlyCollection<PrintJob> jobs,
        CancellationToken cancellationToken)
    {
        var ids = jobs.Select(j => j.RequestedByUserId)
            .Concat(jobs.Where(j => j.DoneByUserId is not null).Select(j => j.DoneByUserId!.Value))
            .Distinct()
            .ToList();

        var names = ids.Count == 0
            ? []
            : await db.Set<ApplicationUser>()
                .Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return jobs
            .Select(j => new PrintJobDto(
                j.Id,
                j.Barcodes,
                j.Barcodes.Count,
                j.Headline,
                j.Source,
                names.GetValueOrDefault(j.RequestedByUserId, "—"),
                j.RequestedAt,
                j.Status.ToString(),
                j.DoneByUserId is null ? null : names.GetValueOrDefault(j.DoneByUserId.Value),
                j.DoneAt))
            .ToList();
    }

    private static string? Trimmed(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    private static Result<PrintJobDto> Invalid(string message, string code, params string[] args) =>
        Result<PrintJobDto>.Failure(ErrorCode.ValidationFailed, message, code, args);
}
