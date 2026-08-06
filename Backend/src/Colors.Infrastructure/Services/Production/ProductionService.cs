using Colors.Application.Common.Models;
using Colors.Application.Features.Barcodes;
using Colors.Application.Features.Production;
using Colors.Domain.Common;
using Colors.Domain.Entities.Production;
using Colors.Domain.Entities.Shifts;
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

    /// <summary>
    /// The mix this shift line is running, created by the first roll of the shift.
    ///
    /// The mixer is filled once a shift, so the batch is the extruder's part of that
    /// shift and nobody opens one by hand (specification section 8). Creating it here
    /// rather than on a button has two consequences worth naming: an empty batch can no
    /// longer exist, and "one mix per shift" stops being a fact the factory reports and
    /// becomes one the data enforces — a roll joins the open batch or creates it, and
    /// there is no second one to create.
    /// </summary>
    private async Task<Batch> OpenBatchForAsync(
        ShiftLine shiftLine,
        int userId,
        CancellationToken cancellationToken)
    {
        var running = await db.Batches
            .FirstOrDefaultAsync(
                b => b.ShiftLineId == shiftLine.Id && b.FinishedAt == null,
                cancellationToken);

        if (running is not null)
        {
            return running;
        }

        var batch = new Batch
        {
            BatchNumber = await NextNumberAsync(ColorsDbContext.BatchNumberSequence, cancellationToken),
            ShiftLineId = shiftLine.Id,
            CreatedByUserId = userId,
            StartedAt = timeProvider.GetUtcNow(),
        };

        db.Batches.Add(batch);
        return batch;
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
        var shiftLine = await db.ShiftLines
            .Include(l => l.ProductionLine)
            .Include(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .FirstOrDefaultAsync(l => l.Id == request.ShiftLineId, cancellationToken);

        if (shiftLine is null)
        {
            return InvalidRoll("Choose a line of an open shift.");
        }

        // The thermo forms what the mixer already made and the recycler grinds scrap —
        // neither produces a roll (specification section 4).
        if (!shiftLine.ProductionLine.MakesRolls)
        {
            return InvalidRoll(
                $"{shiftLine.ProductionLine.Name} does not make rolls. Choose the extruder line.");
        }

        if (!ShiftWork.AcceptsWork(shiftLine.ShiftReport.Status))
        {
            return InvalidRoll(ShiftWork.RefusalFor(shiftLine.ShiftReport));
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

        // The recipe and the colour have to agree. A Black recipe is 35% recycled
        // material, which is dark, so it cannot be made in white — and black is made on
        // that recipe rather than the plain one, which is what "Except Black" means
        // (specification section 5).
        if (!RecipeColour.Agree(recipe.Family, colour))
        {
            return InvalidRoll(RecipeColour.RefusalFor(recipe.Family, colour));
        }

        if (string.IsNullOrWhiteSpace(recipe.Family.Code))
        {
            return InvalidRoll(
                $"{recipe.Family.Name} has no code for the roll code. Set one in Master Data.");
        }

        var productionDate = shiftLine.ShiftReport.ProductionDate;

        // The roll and its barcode are one act: a roll with no label cannot be found on
        // the floor, and a label with no roll names nothing.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // The mix this shift is running, created by this roll if it is the first.
        var batch = await OpenBatchForAsync(shiftLine, userId, cancellationToken);

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
                shiftLine.ShiftReport.Shift.Name),
            Batch = batch,
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


    private static Result<RollDto> InvalidRoll(string message) =>
        Result<RollDto>.Failure(ErrorCode.ValidationFailed, message);

    private static Result<RollDto> RollNotFound() =>
        Result<RollDto>.Failure(ErrorCode.NotFound, "This roll does not exist.");
}
