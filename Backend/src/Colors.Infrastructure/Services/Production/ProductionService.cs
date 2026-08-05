using Colors.Application.Common.Models;
using Colors.Application.Features.Barcodes;
using Colors.Application.Features.Production;
using Colors.Domain.Common;
using Colors.Domain.Entities.Production;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Production;

/// <summary>
/// Line 1 — the mixer and the extruder. Specification section 8.
///
/// Two rules carry this phase. A roll's code is generated, never typed, and is unique
/// for ever. And a roll cannot reach the thermo until its measurements exist — not
/// because anyone judges them, but because once the roll is formed into plates there
/// is nothing left to measure.
/// </summary>
public class ProductionService(
    ColorsDbContext db,
    IBarcodeService barcodes,
    TimeProvider timeProvider) : IProductionService
{
    /// <summary>
    /// The Roll Log export contains a roll weighing 350 — the operator typed the length
    /// into the weight box. Caught here while he is still standing at the machine; on
    /// paper it was wrong for ever.
    /// </summary>
    private const decimal MinRollWeight = 50m;
    private const decimal MaxRollWeight = 150m;

    public async Task<IReadOnlyList<BatchSummaryDto>> GetBatchesAsync(
        int? shiftReportId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        var batches = await BatchQuery()
            .Where(b => shiftReportId == null || b.ShiftLine.ShiftReportId == shiftReportId)
            .Where(b => !openOnly || b.FinishedAt == null)
            .OrderByDescending(b => b.BatchNumber)
            .Take(200)
            .ToListAsync(cancellationToken);

        var names = await UserNamesAsync(batches.Select(b => b.CreatedByUserId), cancellationToken);

        return batches.Select(b => ToSummary(b, names)).ToList();
    }

    public async Task<Result<BatchSummaryDto>> StartBatchAsync(
        StartBatchRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var shiftLine = await db.ShiftLines
            .Include(l => l.ProductionLine)
            .Include(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .FirstOrDefaultAsync(l => l.Id == request.ShiftLineId, cancellationToken);

        if (shiftLine is null)
        {
            return InvalidBatch("Choose a line of an open shift.");
        }

        // A batch is a mix. The thermo forms what the mixer already made, and the
        // recycler grinds scrap — neither can start one (specification section 4).
        if (!shiftLine.ProductionLine.MakesRolls)
        {
            return InvalidBatch(
                $"{shiftLine.ProductionLine.Name} does not mix. Choose the extruder line.");
        }

        // A batch never crosses a shift, because all material goes back to the store at
        // shift end — so a mix started against a finished shift could never be true.
        if (shiftLine.ShiftReport.Status != ShiftReportStatus.Open)
        {
            return InvalidBatch(
                $"Shift {shiftLine.ShiftReport.Shift.Name} on "
                + $"{shiftLine.ShiftReport.ProductionDate:dd/MM/yyyy} is closed.");
        }

        var batch = new Batch
        {
            BatchNumber = await NextNumberAsync(ColorsDbContext.BatchNumberSequence, cancellationToken),
            ShiftLineId = shiftLine.Id,
            CreatedByUserId = userId,
            StartedAt = timeProvider.GetUtcNow(),
            Notes = Trimmed(request.Notes),
        };

        db.Batches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);

        return await LoadBatchAsync(batch.Id, cancellationToken);
    }

    public async Task<Result<BatchSummaryDto>> FinishBatchAsync(
        int batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await BatchQuery().FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
        if (batch is null)
        {
            return Result<BatchSummaryDto>.Failure(ErrorCode.NotFound, "This batch does not exist.");
        }

        if (batch.FinishedAt is not null)
        {
            return InvalidBatch("This batch is already finished.");
        }

        if (batch.Rolls.Count == 0)
        {
            return InvalidBatch("This batch produced no rolls. Log them before finishing it.");
        }

        batch.FinishedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return await LoadBatchAsync(batchId, cancellationToken);
    }

    public async Task<IReadOnlyList<RollSummaryDto>> GetRollsAsync(
        int? batchId = null,
        bool needsTestOnly = false,
        CancellationToken cancellationToken = default)
    {
        var rolls = await RollQuery()
            .Where(r => batchId == null || r.BatchId == batchId)
            .Where(r => !needsTestOnly || r.Status == RollStatus.NeedsTest)
            .OrderByDescending(r => r.ProductionDate)
            .ThenByDescending(r => r.DailySerial)
            .Take(300)
            .ToListAsync(cancellationToken);

        var names = await UserNamesAsync(rolls.Select(r => r.ProducedByUserId), cancellationToken);
        var codes = await BarcodesForAsync(rolls.Select(r => r.Id), cancellationToken);

        return rolls
            .Select(r => new RollSummaryDto(
                r.Id,
                r.RollCode,
                codes.GetValueOrDefault(r.Id, string.Empty),
                r.DailySerial,
                r.ProductionDate,
                r.BatchId,
                r.Batch.BatchNumber,
                r.RecipeVersionId,
                r.RecipeVersion.RecipeNumber,
                r.RecipeVersion.Family.Name,
                r.ColorId,
                r.Color.Name,
                r.Status.ToString(),
                r.Status == RollStatus.NeedsTest,
                names.GetValueOrDefault(r.ProducedByUserId, "—"),
                r.ProducedAt,
                r.TestReport?.Weight,
                r.TestReport?.AverageThickness))
            .ToList();
    }

    public async Task<Result<RollDto>> GetRollAsync(
        int rollId,
        CancellationToken cancellationToken = default)
    {
        var roll = await RollQuery().FirstOrDefaultAsync(r => r.Id == rollId, cancellationToken);

        return roll is null
            ? RollNotFound()
            : Result<RollDto>.Success(await ToRollDtoAsync(roll, cancellationToken));
    }

    public async Task<Result<RollDto>> CreateRollAsync(
        CreateRollRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.Batches
            .Include(b => b.ShiftLine).ThenInclude(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .FirstOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken);

        if (batch is null)
        {
            return InvalidRoll("Choose a batch.");
        }

        if (batch.FinishedAt is not null)
        {
            return InvalidRoll($"Batch {batch.BatchNumber} is finished. Start a new one.");
        }

        if (batch.ShiftLine.ShiftReport.Status != ShiftReportStatus.Open)
        {
            return InvalidRoll("The shift this batch belongs to is closed.");
        }

        var recipe = await db.RecipeVersions
            .Include(v => v.Family)
            .FirstOrDefaultAsync(v => v.Id == request.RecipeVersionId, cancellationToken);

        if (recipe is null)
        {
            return InvalidRoll("Choose the recipe this roll was made to.");
        }

        // A draft is a formula somebody is still writing. Rolls made to it could never
        // be reproduced, because a draft may still change.
        if (recipe.Status == RecipeVersionStatus.Draft)
        {
            return InvalidRoll(
                $"Recipe {recipe.RecipeNumber} is still a draft. Put it into production first.");
        }

        var colour = await db.Colors
            .FirstOrDefaultAsync(c => c.Id == request.ColorId && c.IsActive, cancellationToken);

        if (colour is null)
        {
            return InvalidRoll("Choose an active colour.");
        }

        if (string.IsNullOrWhiteSpace(recipe.Family.Code))
        {
            return InvalidRoll(
                $"{recipe.Family.Name} has no code for the roll code. Set one in Master Data.");
        }

        var productionDate = batch.ShiftLine.ShiftReport.ProductionDate;

        // The roll and its barcode are one act: a roll with no label cannot be found on
        // the floor, and a label with no roll names nothing.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var serial = await NextDailySerialAsync(productionDate, cancellationToken);

        var roll = new Roll
        {
            ProductionDate = productionDate,
            DailySerial = serial,
            RollCode = RollCode.For(
                serial,
                colour.Code,
                recipe.Family.Code,
                productionDate,
                batch.ShiftLine.ShiftReport.Shift.Name),
            BatchId = batch.Id,
            RecipeVersionId = recipe.Id,
            ColorId = colour.Id,
            ProducedByUserId = userId,
            ProducedAt = request.ProducedAt ?? timeProvider.GetUtcNow(),
            Status = RollStatus.NeedsTest,
            Notes = Trimmed(request.Notes),
        };

        db.Rolls.Add(roll);
        await db.SaveChangesAsync(cancellationToken);

        var barcode = await barcodes.IssueAsync(BarcodeObjectType.Roll, roll.Id, cancellationToken);
        if (!barcode.IsSuccess)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return InvalidRoll(barcode.Message ?? "The roll's barcode could not be printed.");
        }

        await transaction.CommitAsync(cancellationToken);

        return await LoadRollAsync(roll.Id, cancellationToken);
    }

    public async Task<Result<RollDto>> SaveTestReportAsync(
        int rollId,
        SaveRollTestRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var roll = await RollQuery().FirstOrDefaultAsync(r => r.Id == rollId, cancellationToken);
        if (roll is null)
        {
            return RollNotFound();
        }

        if (roll.TestReport is not null)
        {
            return InvalidRoll(
                $"Roll {roll.RollCode} has already been measured. An administrator corrects a "
                + "reading that was written down wrong.");
        }

        if (roll.Status is RollStatus.Processed or RollStatus.Scrapped)
        {
            return InvalidRoll($"Roll {roll.RollCode} is {roll.Status.ToString().ToLowerInvariant()}.");
        }

        var error = Validate(request);
        if (error is not null)
        {
            return InvalidRoll(error);
        }

        db.RollTestReports.Add(new RollTestReport
        {
            RollId = roll.Id,
            Weight = request.Weight,
            Length = request.Length,
            PlateWeight = request.PlateWeight,
            ThicknessRs = request.ThicknessRs,
            ThicknessRm = request.ThicknessRm,
            ThicknessLm = request.ThicknessLm,
            ThicknessLs = request.ThicknessLs,
            TestedByUserId = userId,
            TestedAt = timeProvider.GetUtcNow(),
            Notes = Trimmed(request.Notes),
        });

        // Measured, so it may now go to the thermo. This is not approval — nothing was
        // compared against a limit.
        roll.Status = RollStatus.Available;

        await db.SaveChangesAsync(cancellationToken);

        return await LoadRollAsync(rollId, cancellationToken);
    }

    // ---------- helpers ----------

    private static string? Validate(SaveRollTestRequest request)
    {
        if (request.Weight is < MinRollWeight or > MaxRollWeight)
        {
            return $"A roll weighs between {MinRollWeight:0} and {MaxRollWeight:0} kg. "
                   + $"{request.Weight:0.###} looks like the length typed into the weight box.";
        }

        if (request.Length <= 0)
        {
            return "The roll's length is missing.";
        }

        if (request.PlateWeight <= 0)
        {
            return "The sample plate's weight is missing.";
        }

        decimal[] readings =
            [request.ThicknessRs, request.ThicknessRm, request.ThicknessLm, request.ThicknessLs];

        return readings.Any(r => r <= 0)
            ? "All four thickness readings are needed — RS, RM, LM and LS."
            : null;
    }

    /// <summary>
    /// The next serial for this day. Taken under the transaction that creates the roll,
    /// with a unique index on (date, serial) behind it — two tablets logging a roll in
    /// the same moment must not both be handed number 13.
    /// </summary>
    private async Task<int> NextDailySerialAsync(
        DateOnly productionDate,
        CancellationToken cancellationToken)
    {
        var highest = await db.Rolls
            .Where(r => r.ProductionDate == productionDate)
            .Select(r => (int?)r.DailySerial)
            .MaxAsync(cancellationToken);

        return (highest ?? 0) + 1;
    }

    private async Task<int> NextNumberAsync(string sequence, CancellationToken cancellationToken)
    {
        var next = await db.Database
            .SqlQuery<int>($"SELECT nextval({sequence})::int AS \"Value\"")
            .ToListAsync(cancellationToken);

        return next[0];
    }

    private IQueryable<Batch> BatchQuery() =>
        db.Batches
            .Include(b => b.ShiftLine).ThenInclude(l => l.ProductionLine)
            .Include(b => b.ShiftLine).ThenInclude(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .Include(b => b.Rolls).ThenInclude(r => r.TestReport);

    private IQueryable<Roll> RollQuery() =>
        db.Rolls
            .Include(r => r.Batch)
            .Include(r => r.RecipeVersion).ThenInclude(v => v.Family)
            .Include(r => r.Color)
            .Include(r => r.TestReport);

    private static BatchSummaryDto ToSummary(Batch batch, Dictionary<int, string> names) =>
        new(
            batch.Id,
            batch.BatchNumber,
            batch.ShiftLineId,
            batch.ShiftLine.ProductionLine.Name,
            batch.ShiftLine.ShiftReport.Shift.Name,
            batch.ShiftLine.ShiftReport.ProductionDate,
            names.GetValueOrDefault(batch.CreatedByUserId, "—"),
            batch.FinishedAt is not null,
            batch.Rolls.Count,
            batch.Rolls.Any(r => r.TestReport is not null)
                ? batch.Rolls.Where(r => r.TestReport is not null).Sum(r => r.TestReport!.Weight)
                : null,
            batch.StartedAt,
            batch.FinishedAt);

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

    private async Task<Dictionary<int, string>> BarcodesForAsync(
        IEnumerable<int> rollIds,
        CancellationToken cancellationToken)
    {
        var wanted = rollIds.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        return await db.Barcodes
            .Where(b => b.ObjectType == BarcodeObjectType.Roll
                        && wanted.Contains(b.ObjectId)
                        && b.IsActive)
            .ToDictionaryAsync(b => b.ObjectId, b => b.Value, cancellationToken);
    }

    private async Task<Result<BatchSummaryDto>> LoadBatchAsync(int id, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var batch = await BatchQuery().FirstAsync(b => b.Id == id, cancellationToken);
        var names = await UserNamesAsync([batch.CreatedByUserId], cancellationToken);

        return Result<BatchSummaryDto>.Success(ToSummary(batch, names));
    }

    private async Task<Result<RollDto>> LoadRollAsync(int id, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var roll = await RollQuery().FirstAsync(r => r.Id == id, cancellationToken);

        return Result<RollDto>.Success(await ToRollDtoAsync(roll, cancellationToken));
    }

    private async Task<RollDto> ToRollDtoAsync(Roll roll, CancellationToken cancellationToken)
    {
        var ids = new List<int> { roll.ProducedByUserId };
        if (roll.TestReport is not null)
        {
            ids.Add(roll.TestReport.TestedByUserId);
        }

        var names = await UserNamesAsync(ids, cancellationToken);
        var codes = await BarcodesForAsync([roll.Id], cancellationToken);

        return new RollDto(
            roll.Id,
            roll.RollCode,
            codes.GetValueOrDefault(roll.Id, string.Empty),
            roll.DailySerial,
            roll.ProductionDate,
            roll.BatchId,
            roll.Batch.BatchNumber,
            roll.RecipeVersionId,
            roll.RecipeVersion.RecipeNumber,
            roll.RecipeVersion.Family.Name,
            roll.ColorId,
            roll.Color.Name,
            roll.Status.ToString(),
            roll.Status == RollStatus.NeedsTest,
            names.GetValueOrDefault(roll.ProducedByUserId, "—"),
            roll.ProducedAt,
            roll.Notes,
            roll.TestReport is null
                ? null
                : new RollTestReportDto(
                    roll.TestReport.Id,
                    roll.TestReport.Weight,
                    roll.TestReport.Length,
                    roll.TestReport.PlateWeight,
                    roll.TestReport.ThicknessRs,
                    roll.TestReport.ThicknessRm,
                    roll.TestReport.ThicknessLm,
                    roll.TestReport.ThicknessLs,
                    roll.TestReport.AverageThickness,
                    names.GetValueOrDefault(roll.TestReport.TestedByUserId, "—"),
                    roll.TestReport.TestedAt,
                    roll.TestReport.Notes));
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<BatchSummaryDto> InvalidBatch(string message) =>
        Result<BatchSummaryDto>.Failure(ErrorCode.ValidationFailed, message);

    private static Result<RollDto> InvalidRoll(string message) =>
        Result<RollDto>.Failure(ErrorCode.ValidationFailed, message);

    private static Result<RollDto> RollNotFound() =>
        Result<RollDto>.Failure(ErrorCode.NotFound, "This roll does not exist.");
}
