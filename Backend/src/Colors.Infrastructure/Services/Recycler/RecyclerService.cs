using Colors.Application.Common.Models;
using Colors.Application.Features.Recycler;
using Colors.Domain.Common;
using Colors.Domain.Constants;
using Colors.Domain.Entities.Production;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Recycler;

/// <summary>
/// The recycler (specification section 11).
///
/// The smallest line in the factory and the smallest record in the system: scrap weighed
/// in, recycled material weighed out, and the output added back to the store. Two
/// numbers, written once at the end of the shift.
///
/// The loss between them is never stored — both inputs sit on the row and are frozen the
/// moment it is written, which is the calculated-or-stored rule exactly.
/// </summary>
public class RecyclerService(
    ColorsDbContext db,
    StockLedger ledger,
    TimeProvider timeProvider) : IRecyclerService
{
    public async Task<IReadOnlyList<RecyclerProductionDto>> GetAllAsync(
        int? shiftReportId = null,
        CancellationToken cancellationToken = default)
    {
        var records = await Query()
            .Where(r => shiftReportId == null || r.ShiftLine.ShiftReportId == shiftReportId)
            .OrderByDescending(r => r.RecordedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var names = await UserNamesAsync(records.Select(r => r.RecordedByUserId), cancellationToken);

        return records.Select(r => ToDto(r, names)).ToList();
    }

    public async Task<Result<RecyclerDraftDto>> GetDraftAsync(
        int shiftLineId,
        CancellationToken cancellationToken = default)
    {
        var shiftLine = await db.ShiftLines
            .Include(l => l.ProductionLine)
            .Include(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .FirstOrDefaultAsync(l => l.Id == shiftLineId, cancellationToken);

        if (shiftLine is null)
        {
            return Result<RecyclerDraftDto>.Failure(
                ErrorCode.NotFound, "This line of the shift does not exist.");
        }

        var existing = await Query().FirstOrDefaultAsync(
            r => r.ShiftLineId == shiftLineId, cancellationToken);

        var names = existing is null
            ? []
            : await UserNamesAsync([existing.RecordedByUserId], cancellationToken);

        var material = await RecycledMaterialAsync(cancellationToken);

        return Result<RecyclerDraftDto>.Success(new RecyclerDraftDto(
            shiftLineId,
            shiftLine.ProductionLine.Name,
            shiftLine.ShiftReport.Shift.Name,
            shiftLine.ShiftReport.ProductionDate,
            material?.Name,
            existing is not null,
            existing is null ? null : ToDto(existing, names)));
    }

    public async Task<Result<RecyclerProductionDto>> SaveAsync(
        SaveRecyclerProductionRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var shiftLine = await db.ShiftLines
            .Include(l => l.ProductionLine)
            .Include(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .FirstOrDefaultAsync(l => l.Id == request.ShiftLineId, cancellationToken);

        if (shiftLine is null)
        {
            return Invalid("Choose a line of an open shift.");
        }

        // Scrap is recorded where it is ground (specification section 4).
        if (!shiftLine.ProductionLine.Recycles)
        {
            return Invalid(
                $"{shiftLine.ProductionLine.Name} does not recycle. Choose the recycler "
                + "line.");
        }

        if (!ShiftWork.AcceptsWork(shiftLine.ShiftReport.Status))
        {
            return Invalid(ShiftWork.RefusalFor(shiftLine.ShiftReport));
        }

        if (request.ScrapWeight < 0 || request.RecycledMaterialWeight < 0)
        {
            return Invalid("A weight cannot be less than nothing.");
        }

        // Either may be zero on its own — a shift can collect scrap without grinding it,
        // or grind yesterday's pile without collecting any. Both zero is not a record.
        if (request.ScrapWeight == 0 && request.RecycledMaterialWeight == 0)
        {
            return Invalid("Weigh the scrap, what came out of it, or both.");
        }

        // Deliberately no rule that the output must be smaller. The recycler grinds what
        // is in front of it, and that can include a pile left from an earlier shift
        // (specification section 11).

        if (await db.RecyclerProductions
                .AnyAsync(r => r.ShiftLineId == request.ShiftLineId, cancellationToken))
        {
            return Invalid(
                "The recycler has already been recorded for this line. It is written "
                + "once, at the end of the shift.");
        }

        var material = await RecycledMaterialAsync(cancellationToken);

        // The record and the stock it creates are one act. A record saved without its
        // movement would claim material the store never received.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var record = new RecyclerProduction
        {
            ShiftLineId = shiftLine.Id,
            ScrapWeight = request.ScrapWeight,
            RecycledMaterialWeight = request.RecycledMaterialWeight,
            RecordedByUserId = userId,
            RecordedAt = timeProvider.GetUtcNow(),
            Notes = Trimmed(request.Notes),
        };

        db.RecyclerProductions.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        if (material is not null && request.RecycledMaterialWeight > 0)
        {
            var posted = await ledger.PostAsync(
                material.Id,
                MovementTypeNames.ProductionOutput,
                request.RecycledMaterialWeight,
                userId,
                $"Recycled on {shiftLine.ProductionLine.Name}, shift "
                + $"{shiftLine.ShiftReport.Shift.Name} "
                + $"{shiftLine.ShiftReport.ProductionDate:dd/MM/yyyy}",
                null,
                shiftLine.ShiftReportId,
                cancellationToken);

            if (!posted.IsSuccess)
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                return Invalid($"{material.Name}: {posted.Message}");
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return await LoadAsync(record.Id, cancellationToken);
    }

    // ---------- helpers ----------

    /// <summary>
    /// The one material the recycler makes, or null where the factory has not said which
    /// it is. Only one row can carry the flag — a unique index sees to that.
    /// </summary>
    private Task<Domain.Entities.MasterData.Material?> RecycledMaterialAsync(
        CancellationToken cancellationToken) =>
        db.Materials.FirstOrDefaultAsync(m => m.IsRecycledOutput && m.IsActive, cancellationToken);

    private IQueryable<RecyclerProduction> Query() =>
        db.RecyclerProductions
            .Include(r => r.ShiftLine).ThenInclude(l => l.ProductionLine)
            .Include(r => r.ShiftLine).ThenInclude(l => l.ShiftReport).ThenInclude(s => s.Shift);

    private static RecyclerProductionDto ToDto(
        RecyclerProduction r,
        Dictionary<int, string> names) =>
        new(
            r.Id,
            r.ShiftLineId,
            r.ShiftLine.ProductionLine.Name,
            r.ShiftLine.ShiftReport.Shift.Name,
            r.ShiftLine.ShiftReport.ProductionDate,
            r.ScrapWeight,
            r.RecycledMaterialWeight,
            r.LossPercentage is null ? null : Math.Round(r.LossPercentage.Value, 2),
            names.GetValueOrDefault(r.RecordedByUserId, "—"),
            r.RecordedAt,
            r.Notes);

    private async Task<Dictionary<int, string>> UserNamesAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        return await db.Set<ApplicationUser>()
            .Where(u => wanted.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }

    private async Task<Result<RecyclerProductionDto>> LoadAsync(
        int id,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var record = await Query().FirstAsync(r => r.Id == id, cancellationToken);
        var names = await UserNamesAsync([record.RecordedByUserId], cancellationToken);

        return Result<RecyclerProductionDto>.Success(ToDto(record, names));
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<RecyclerProductionDto> Invalid(string message) =>
        Result<RecyclerProductionDto>.Failure(ErrorCode.ValidationFailed, message);
}
