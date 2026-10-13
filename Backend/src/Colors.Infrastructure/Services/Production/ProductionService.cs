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
        bool currentShiftOnly = false,
        CancellationToken cancellationToken = default)
    {
        var rolls = await RollQuery()
            .Where(r => batchId == null || r.BatchId == batchId)
            // Rolls still waiting for their test. Measured or not: the two are separate
            // jobs, done in either order (specification section 19.12).
            .Where(r => !needsTestOnly || (r.TestReport == null && r.Status == RollStatus.NeedsTest))
            // The open shift, of which there is exactly one -- a partial unique index
            // in the database says so (specification section 2), so this needs no
            // second thought about which shift is meant.
            .Where(r => !currentShiftOnly
                        || r.Batch.ShiftLine.ShiftReport.Status == ShiftReportStatus.Open)
            .OrderByDescending(r => r.ProducedAt)
            .Take(300)
            .ToListAsync(cancellationToken);

        var names = await UserNamesAsync(rolls.Select(r => r.ProducedByUserId), cancellationToken);
        var codes = await BarcodesForAsync(rolls.Select(r => r.Id), cancellationToken);

        return rolls
            .Select(r => new RollSummaryDto(
                r.Id,
                r.RollCode,
                codes.GetValueOrDefault(r.Id, string.Empty),
                r.RollNumber,
                r.ProductionDate,
                r.BatchId,
                r.Batch.BatchNumber,
                r.RecipeVersionId,
                r.RecipeVersion.RecipeNumber,
                r.RecipeVersion.Family.Name,
                r.ColorId,
                r.Color.Name,
                r.Status.ToString(),
                r.TestReport is null,
                r.Weight is null,
                names.GetValueOrDefault(r.ProducedByUserId, "—"),
                r.ProducedAt,
                r.Weight,
                r.Length,
                r.TestReport?.AverageThickness,
                r.ProductId,
                r.Product?.Name,
                r.Product?.MinThickness,
                r.Product?.MaxThickness,
                r.TestReport?.ThicknessInSpec))
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

        // The old Black families were retired when black became an ordinary colour
        // (specification section 5). Their recipes stay for the rolls already made, but
        // nothing new is made to them.
        if (!recipe.Family.IsActive)
        {
            return InvalidRoll(
                $"{recipe.Family.Name} is no longer used. Choose one of the main recipes.",
                "roll.recipeRetired",
                recipe.Family.Name);
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

        // What the roll is being made for (specification section 19.1). Declared, because
        // the thickness cannot be trusted to say it: the first rolls of a run are the
        // wrong thickness while the machine is being set, and are still for this product.
        if (request.ProductId is null)
        {
            return InvalidRoll("Say which product this roll is for.", "roll.chooseProduct");
        }

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive, cancellationToken);

        if (product is null)
        {
            return InvalidRoll("Choose an active product.", "roll.chooseActiveProduct");
        }

        // Each product is made from one main recipe — Normal plates from Normal, lunch
        // and burger boxes from Lunch Box. Caught here, at the extruder: the bags formed
        // from the roll will be this product, and nothing later checks it again.
        if (product.RecipeFamilyId is not null && product.RecipeFamilyId != recipe.RecipeFamilyId)
        {
            return InvalidRoll(
                $"{product.Name} is not made from {recipe.Family.Name}. Choose a product this recipe makes.",
                "roll.productWrongRecipe",
                product.Name,
                recipe.Family.Name);
        }

        // A product not yet given a main recipe can only be checked the old way, on
        // absorbency, which both the product and the recipe still say.
        if (product.IsAbsorbent != recipe.Family.IsAbsorbent)
        {
            return InvalidRoll(
                product.IsAbsorbent
                    ? $"{product.Name} is absorbent, but recipe {recipe.RecipeNumber} is not."
                    : $"{product.Name} is not absorbent, but recipe {recipe.RecipeNumber} is.",
                product.IsAbsorbent ? "roll.productAbsorbentRecipeNot" : "roll.productNotAbsorbentRecipeIs",
                product.Name,
                recipe.RecipeNumber.ToString());
        }

        var productionDate = shiftLine.ShiftReport.ProductionDate;

        // The roll and its barcode are one act: a roll with no label cannot be found on
        // the floor, and a label with no roll names nothing.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // The mix this shift is running, created by this roll if it is the first.
        var batch = await OpenBatchForAsync(shiftLine, userId, cancellationToken);

        var producedAt = request.ProducedAt ?? timeProvider.GetUtcNow();
        if (producedAt > timeProvider.GetUtcNow().AddMinutes(5))
        {
            await transaction.RollbackAsync(cancellationToken);
            return InvalidRoll("A roll cannot come off the extruder in the future. Check the out time.", "roll.outAtInFuture");
        }

        // The running number, taken inside this transaction: the counter's row stays locked
        // until it commits, so two tablets cannot both be handed 13, and a refusal below
        // rolls it back so the number is not lost.
        var rollNumber = await TakeRollNumberAsync(cancellationToken);

        // The code the system works out, unless the operator gave one — a roll labelled
        // by hand before it was logged keeps the code that is already written on it. The
        // time in it is the out time as the tablet sent it, in the factory's own clock.
        var rollCode = string.IsNullOrWhiteSpace(request.RollCode)
            ? RollCode.For(rollNumber, recipe.Family.Code, producedAt, colour.Name)
            : request.RollCode.Trim();

        var codeError = await RollCodeErrorAsync(rollCode, exceptRollId: null, cancellationToken);
        if (codeError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return codeError;
        }

        var roll = new Roll
        {
            ProductionDate = productionDate,
            RollNumber = rollNumber,
            RollCode = rollCode,
            Batch = batch,
            RecipeVersionId = recipe.Id,
            ColorId = colour.Id,
            ProductId = product.Id,
            ProducedByUserId = userId,
            // Stored as a moment; the offset it arrived with has done its work in the code.
            ProducedAt = producedAt.ToUniversalTime(),
            Status = RollStatus.NeedsTest,
            Notes = Trimmed(request.Notes),
        };

        db.Rolls.Add(roll);
        await db.SaveChangesAsync(cancellationToken);

        // One code: the roll's barcode is its code, in the capitals every scan is cleaned to.
        var barcode = await barcodes.IssueAsync(
            BarcodeObjectType.Roll, roll.Id, RollCode.AsBarcode(rollCode), cancellationToken);
        if (!barcode.IsSuccess)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return InvalidRoll(barcode.Message ?? "The roll's barcode could not be printed.");
        }

        await transaction.CommitAsync(cancellationToken);

        return await LoadRollAsync(roll.Id, cancellationToken);
    }

    public async Task<Result<string>> SuggestRollCodeAsync(
        int shiftLineId,
        int recipeVersionId,
        int colorId,
        DateTimeOffset? outAt,
        CancellationToken cancellationToken = default)
    {
        var shiftLine = await db.ShiftLines
            .Include(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .FirstOrDefaultAsync(l => l.Id == shiftLineId, cancellationToken);
        var recipe = await db.RecipeVersions
            .Include(v => v.Family)
            .FirstOrDefaultAsync(v => v.Id == recipeVersionId, cancellationToken);
        var colour = await db.Colors.FirstOrDefaultAsync(c => c.Id == colorId, cancellationToken);

        if (shiftLine is null || recipe is null || colour is null)
        {
            return Result<string>.Failure(
                ErrorCode.ValidationFailed, "Choose the line, the recipe and the colour first.", "roll.chooseForCode");
        }

        var number = (await GetRollNumberAsync(cancellationToken)).NextNumber;

        return Result<string>.Success(RollCode.For(
            number,
            recipe.Family.Code,
            outAt ?? timeProvider.GetUtcNow(),
            colour.Name));
    }

    public async Task<Result<RollDto>> UpdateRollAsync(
        int rollId,
        UpdateRollRequest request,
        CancellationToken cancellationToken = default)
    {
        var roll = await db.Rolls
            .Include(r => r.Batch).ThenInclude(b => b.ShiftLine).ThenInclude(l => l.ShiftReport)
            .FirstOrDefaultAsync(r => r.Id == rollId, cancellationToken);

        if (roll is null)
        {
            return RollNotFound();
        }

        // A closed shift accepts nothing further (specification section 2) — the roll's
        // record is part of it, and correcting it is the reopened shift's business.
        if (roll.Batch.ShiftLine.ShiftReport.Status == ShiftReportStatus.Closed)
        {
            return InvalidRoll(
                $"Roll {roll.RollCode} belongs to a closed shift. An administrator must reopen it first.",
                "roll.shiftClosed",
                roll.RollCode);
        }

        var code = request.RollCode?.Trim() ?? string.Empty;
        var codeError = await RollCodeErrorAsync(code, roll.Id, cancellationToken);
        if (codeError is not null)
        {
            return codeError;
        }

        if (request.ProducedAt > timeProvider.GetUtcNow().AddMinutes(5))
        {
            return InvalidRoll("A roll cannot come off the extruder in the future. Check the out time.", "roll.outAtInFuture");
        }

        // A roll whose barcode is its code takes the new code into its barcode too — one
        // code, as section 19.12 asks, so a new label is printed. An older roll with a
        // separate R-barcode keeps it, and its printed label goes on scanning.
        var oldBarcode = RollCode.AsBarcode(roll.RollCode);
        var label = await db.Barcodes.FirstOrDefaultAsync(
            b => b.ObjectType == BarcodeObjectType.Roll && b.ObjectId == roll.Id && b.IsActive,
            cancellationToken);
        if (label is not null && label.Value == oldBarcode)
        {
            label.Value = RollCode.AsBarcode(code);
        }

        roll.RollCode = code;
        roll.ProducedAt = request.ProducedAt.ToUniversalTime();
        await db.SaveChangesAsync(cancellationToken);

        return await LoadRollAsync(roll.Id, cancellationToken);
    }

    /// <summary>
    /// Why a code cannot be used, or null. The code is what the factory reads off the roll
    /// and repeats on every bag, so two rolls may never share one.
    /// </summary>
    private async Task<Result<RollDto>?> RollCodeErrorAsync(
        string code,
        int? exceptRollId,
        CancellationToken cancellationToken)
    {
        if (code.Length == 0)
        {
            return InvalidRoll("The roll needs a code.", "roll.codeRequired");
        }

        if (code.Length > 50)
        {
            return InvalidRoll("A roll code is at most 50 characters.", "roll.codeTooLong");
        }

        // Taken by another roll, or — since the code is the barcode — by any other label.
        var asBarcode = RollCode.AsBarcode(code);
        var taken = await db.Rolls.AnyAsync(
                        r => r.RollCode == code && (exceptRollId == null || r.Id != exceptRollId),
                        cancellationToken)
                    || await db.Barcodes.AnyAsync(
                        b => b.Value == asBarcode
                             && !(b.ObjectType == BarcodeObjectType.Roll && b.ObjectId == exceptRollId),
                        cancellationToken);

        return taken
            ? InvalidRoll($"Another roll already has the code {code}.", "roll.codeTaken", code)
            : null;
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
                $"Roll {roll.RollCode} has already been tested. An administrator corrects a "
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

        var report = new RollTestReport
        {
            RollId = roll.Id,
            PlateWeight = request.PlateWeight,
            ThicknessRs = request.ThicknessRs,
            ThicknessRm = request.ThicknessRm,
            ThicknessLm = request.ThicknessLm,
            ThicknessLs = request.ThicknessLs,
            TestedByUserId = userId,
            TestedAt = timeProvider.GetUtcNow(),
            Notes = Trimmed(request.Notes),
        };

        // Judged now against the range as it stands today, and kept: the range is master
        // data and will be edited, and last year's rolls must not change their verdict
        // when it is (specification section 19.1). The average is the report's own, so
        // the figure judged is the figure shown.
        report.ThicknessInSpec = roll.Product?.ThicknessInSpec(report.AverageThickness);

        db.RollTestReports.Add(report);

        // Tested, and if it has been weighed too it may now go to the thermo -- whatever
        // the verdict. An out-of-spec roll is still a roll for its product and is still
        // used; the verdict records what it was, it does not decide what happens to it.
        if (roll.Weight is not null)
        {
            roll.Status = RollStatus.Available;
        }

        await db.SaveChangesAsync(cancellationToken);

        return await LoadRollAsync(rollId, cancellationToken);
    }

    public async Task<Result<RollDto>> MeasureRollAsync(
        int rollId,
        MeasureRollRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var roll = await RollQuery().FirstOrDefaultAsync(r => r.Id == rollId, cancellationToken);
        if (roll is null)
        {
            return RollNotFound();
        }

        // Once it is in the thermo its weight is what the run's scrap is worked out from,
        // and changing it would change a run already counted.
        if (roll.Status is RollStatus.InThermo or RollStatus.Processed or RollStatus.Scrapped)
        {
            return InvalidRoll(
                $"Roll {roll.RollCode} has gone into the thermo. Its weight is part of that run now.",
                "roll.alreadyFormed",
                roll.RollCode);
        }

        // The Roll Log export has one: the operator typed the length into the weight box.
        // On paper it was wrong for ever; here he is still at the machine.
        if (request.Weight is < MinRollWeight or > MaxRollWeight)
        {
            return InvalidRoll(
                $"A roll weighs between {MinRollWeight:0} and {MaxRollWeight:0} kg. "
                + $"{request.Weight:0.###} looks like the length typed into the weight box.",
                "roll.weightOutOfRange",
                MinRollWeight.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
                MaxRollWeight.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
        }

        if (request.Length <= 0)
        {
            return InvalidRoll("The roll's length is missing.", "roll.lengthMissing");
        }

        roll.Weight = request.Weight;
        roll.Length = request.Length;
        roll.MeasuredByUserId = userId;
        roll.MeasuredAt = timeProvider.GetUtcNow();

        // Weighed, and if it has been tested too it is ready for the thermo.
        if (roll.TestReport is not null && roll.Status == RollStatus.NeedsTest)
        {
            roll.Status = RollStatus.Available;
        }

        await db.SaveChangesAsync(cancellationToken);

        return await LoadRollAsync(rollId, cancellationToken);
    }

    public async Task<RollNumberDto> GetRollNumberAsync(CancellationToken cancellationToken = default)
    {
        var counter = await db.RollNumberCounters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == 1, cancellationToken);

        if (counter is null)
        {
            return new RollNumberDto(1, null, null);
        }

        var names = counter.LastResetByUserId is null
            ? []
            : await UserNamesAsync([counter.LastResetByUserId.Value], cancellationToken);

        return new RollNumberDto(
            counter.NextNumber,
            counter.LastResetAt,
            counter.LastResetByUserId is null ? null : names.GetValueOrDefault(counter.LastResetByUserId.Value));
    }

    public async Task<RollNumberDto> ResetRollNumberAsync(int userId, CancellationToken cancellationToken = default)
    {
        var counter = await db.RollNumberCounters.FirstOrDefaultAsync(c => c.Id == 1, cancellationToken);
        if (counter is null)
        {
            counter = new RollNumberCounter { Id = 1 };
            db.RollNumberCounters.Add(counter);
        }

        counter.NextNumber = 1;
        counter.LastResetAt = timeProvider.GetUtcNow();
        counter.LastResetByUserId = userId;
        await db.SaveChangesAsync(cancellationToken);

        return await GetRollNumberAsync(cancellationToken);
    }

    // ---------- helpers ----------

    /// <summary>
    /// Takes the next roll number and moves the counter on, in one statement. The update
    /// locks the row until the caller's transaction ends, so a second tablet waits rather
    /// than reading the same number.
    /// </summary>
    private async Task<int> TakeRollNumberAsync(CancellationToken cancellationToken)
    {
        // The row is put there by the migration; this only covers a database where
        // somebody removed it, so the first roll after does not fail.
        await db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "RollNumberCounters" ("Id", "NextNumber") VALUES (1, 1)
             ON CONFLICT ("Id") DO NOTHING
             """,
            cancellationToken);

        var taken = await db.Database
            .SqlQuery<int>(
                $"""
                 UPDATE "RollNumberCounters" SET "NextNumber" = "NextNumber" + 1
                 WHERE "Id" = 1
                 RETURNING "NextNumber" - 1 AS "Value"
                 """)
            .ToListAsync(cancellationToken);

        return taken[0];
    }

    private static string? Validate(SaveRollTestRequest request)
    {
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
            .Include(r => r.Product)
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
            batch.Rolls.Any(r => r.Weight is not null)
                ? batch.Rolls.Where(r => r.Weight is not null).Sum(r => r.Weight!.Value)
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

        if (roll.MeasuredByUserId is not null)
        {
            ids.Add(roll.MeasuredByUserId.Value);
        }

        var names = await UserNamesAsync(ids, cancellationToken);
        var codes = await BarcodesForAsync([roll.Id], cancellationToken);

        return new RollDto(
            roll.Id,
            roll.RollCode,
            codes.GetValueOrDefault(roll.Id, string.Empty),
            roll.RollNumber,
            roll.ProductionDate,
            roll.BatchId,
            roll.Batch.BatchNumber,
            roll.RecipeVersionId,
            roll.RecipeVersion.RecipeNumber,
            roll.RecipeVersion.Family.Name,
            roll.ColorId,
            roll.Color.Name,
            roll.ProductId,
            roll.Product?.Name,
            roll.Product?.MinThickness,
            roll.Product?.MaxThickness,
            roll.Status.ToString(),
            roll.TestReport is null,
            roll.Weight is null,
            names.GetValueOrDefault(roll.ProducedByUserId, "—"),
            roll.ProducedAt,
            roll.Weight,
            roll.Length,
            roll.MeasuredByUserId is null ? null : names.GetValueOrDefault(roll.MeasuredByUserId.Value),
            roll.MeasuredAt,
            roll.Notes,
            roll.TestReport is null
                ? null
                : new RollTestReportDto(
                    roll.TestReport.Id,
                    roll.TestReport.PlateWeight,
                    roll.TestReport.ThicknessRs,
                    roll.TestReport.ThicknessRm,
                    roll.TestReport.ThicknessLm,
                    roll.TestReport.ThicknessLs,
                    roll.TestReport.AverageThickness,
                    roll.TestReport.ThicknessInSpec,
                    names.GetValueOrDefault(roll.TestReport.TestedByUserId, "—"),
                    roll.TestReport.TestedAt,
                    roll.TestReport.Notes));
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();


    private static Result<RollDto> InvalidRoll(string message) =>
        Result<RollDto>.Failure(ErrorCode.ValidationFailed, message);

    /// <summary>The same refusal, named so the screens can say it in Arabic.</summary>
    private static Result<RollDto> InvalidRoll(string message, string code, params string[] args) =>
        Result<RollDto>.Failure(ErrorCode.ValidationFailed, message, code, args);

    private static Result<RollDto> RollNotFound() =>
        Result<RollDto>.Failure(ErrorCode.NotFound, "This roll does not exist.", "roll.notFound");
}
