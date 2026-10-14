using Colors.Application.Common.Models;
using Colors.Application.Features.Barcodes;
using Colors.Application.Features.Production;
using Colors.Domain.Common;
using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Production;
using Colors.Domain.Entities.Recipes;
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
            // Rolls whose test is not finished — not started, or saved part-way (19.15).
            // Measured or not: the two are separate jobs, done in either order (19.12).
            .Where(r => !needsTestOnly
                        || (r.Status == RollStatus.NeedsTest
                            && (r.TestReport == null
                                || r.TestReport.PlateWeight == null
                                || r.TestReport.ThicknessRs == null
                                || r.TestReport.ThicknessRm == null
                                || r.TestReport.ThicknessLm == null
                                || r.TestReport.ThicknessLs == null
                                || (r.RecipeVersion.Family.IsAbsorbent && r.TestReport.AbsorbencyWetWeight == null))))
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
                !TestComplete(r),
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
                r.TestReport?.ThicknessInSpec,
                r.RecipeVersion.Family.IsAbsorbent,
                r.TestReport?.AbsorbencyPercentage,
                WetDueAt(r.TestReport)))
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

        var (choiceError, choices) = await ChoicesAsync(
            request.RecipeVersionId, request.ColorId, request.ProductId, current: null, cancellationToken);
        if (choiceError is not null)
        {
            return choiceError;
        }

        var (recipe, colour, product) = choices!;

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
            ProductId = product!.Id,
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
        int? shiftLineId,
        int recipeVersionId,
        int colorId,
        DateTimeOffset? outAt,
        int? forRollId = null,
        CancellationToken cancellationToken = default)
    {
        var lineKnown = forRollId is not null
                        || await db.ShiftLines.AnyAsync(l => l.Id == shiftLineId, cancellationToken);
        var recipe = await db.RecipeVersions
            .Include(v => v.Family)
            .FirstOrDefaultAsync(v => v.Id == recipeVersionId, cancellationToken);
        var colour = await db.Colors.FirstOrDefaultAsync(c => c.Id == colorId, cancellationToken);

        if (!lineKnown || recipe is null || colour is null)
        {
            return Result<string>.Failure(
                ErrorCode.ValidationFailed, "Choose the line, the recipe and the colour first.", "roll.chooseForCode");
        }

        // A roll being corrected keeps its own number; a new one would take the next.
        int? ownNumber = forRollId is null
            ? null
            : await db.Rolls.Where(r => r.Id == forRollId).Select(r => (int?)r.RollNumber)
                .FirstOrDefaultAsync(cancellationToken);
        if (forRollId is not null && ownNumber is null)
        {
            return Result<string>.Failure(ErrorCode.NotFound, "This roll does not exist.", "roll.notFound");
        }

        var number = ownNumber ?? (await GetRollNumberAsync(cancellationToken)).NextNumber;

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
            .Include(r => r.RecipeVersion).ThenInclude(v => v.Family)
            .Include(r => r.Color)
            .Include(r => r.Product)
            .Include(r => r.TestReport)
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

        // Once formed, its bags carry its product and its colour, and changing the roll
        // would make them disagree with what they were made from.
        var whatItIsChanges = request.RecipeVersionId != roll.RecipeVersionId
                              || request.ColorId != roll.ColorId
                              || request.ProductId != roll.ProductId;
        if (whatItIsChanges && roll.Status is not (RollStatus.NeedsTest or RollStatus.Available))
        {
            return InvalidRoll(
                $"Roll {roll.RollCode} has gone into the thermo. What it was made of is part of that run now.",
                "roll.choicesAlreadyFormed",
                roll.RollCode);
        }

        var (choiceError, choices) = await ChoicesAsync(
            request.RecipeVersionId, request.ColorId, request.ProductId, roll, cancellationToken);
        if (choiceError is not null)
        {
            return choiceError;
        }

        var (recipe, colour, product) = choices!;

        if (request.ProducedAt > timeProvider.GetUtcNow().AddMinutes(5))
        {
            return InvalidRoll("A roll cannot come off the extruder in the future. Check the out time.", "roll.outAtInFuture");
        }

        // No code given: worked out again from the roll's own number and the corrected
        // recipe, time and colour, as it was when the roll was logged (section 19.15).
        var code = request.RollCode is null
            ? RollCode.For(roll.RollNumber, recipe.Family.Code, request.ProducedAt, colour.Name)
            : request.RollCode.Trim();
        var codeError = await RollCodeErrorAsync(code, roll.Id, cancellationToken);
        if (codeError is not null)
        {
            return codeError;
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
        roll.RecipeVersionId = recipe.Id;
        roll.RecipeVersion = recipe;
        roll.ColorId = colour.Id;
        roll.Color = colour;
        roll.ProductId = product?.Id;
        roll.Product = product;
        roll.Notes = Trimmed(request.Notes);

        if (roll.TestReport is { } report)
        {
            // Not absorbent any more: the absorbency belonged to the recipe it was not.
            if (!recipe.Family.IsAbsorbent)
            {
                report.AbsorbencyDryWeight = null;
                report.AbsorbencyWetWeight = null;
                report.AbsorbencyDryWeighedAt = null;
            }

            // Judged again against the product it is now for.
            report.ThicknessInSpec = report.AverageThickness is { } average
                ? product?.ThicknessInSpec(average)
                : null;
        }

        // An absorbent recipe asks for one more reading, so a finished test may not be.
        UpdateReadiness(roll);
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

        // Corrected freely until it goes into the thermo; after that the run was made
        // from these figures.
        if (roll.Status is not (RollStatus.NeedsTest or RollStatus.Available))
        {
            return InvalidRoll(
                $"Roll {roll.RollCode} has gone into the thermo. Its test is part of that run now.",
                "roll.testAlreadyFormed",
                roll.RollCode);
        }

        var error = Validate(request);
        if (error is not null)
        {
            return error;
        }

        var report = roll.TestReport;
        var now = timeProvider.GetUtcNow();

        // An absorbent roll is tested for how much water it takes up; every other roll is
        // not, and a figure on it would be somebody filling in the wrong roll.
        var absorbencyError = AbsorbencyError(request, roll.RecipeVersion.Family.IsAbsorbent, report, now);
        if (absorbencyError is not null)
        {
            return absorbencyError;
        }

        if (report is null)
        {
            report = new RollTestReport { RollId = roll.Id };
            roll.TestReport = report;
            db.RollTestReports.Add(report);
        }

        report.PlateWeight = request.PlateWeight;
        report.ThicknessRs = request.ThicknessRs;
        report.ThicknessRm = request.ThicknessRm;
        report.ThicknessLm = request.ThicknessLm;
        report.ThicknessLs = request.ThicknessLs;

        // The clock starts when the dry weight is first saved, and is not started again
        // by correcting it: it is the same piece, already in the water.
        report.AbsorbencyDryWeighedAt = request.AbsorbencyDryWeight is null
            ? null
            : report.AbsorbencyDryWeighedAt ?? now;
        report.AbsorbencyDryWeight = request.AbsorbencyDryWeight;
        report.AbsorbencyWetWeight = request.AbsorbencyWetWeight;
        report.TestedByUserId = userId;
        report.TestedAt = now;
        report.Notes = Trimmed(request.Notes);

        // Judged now against the range as it stands today, and kept: the range is master
        // data and will be edited, and last year's rolls must not change their verdict
        // when it is (specification section 19.1). The average is the report's own, so
        // the figure judged is the figure shown.
        report.ThicknessInSpec = report.AverageThickness is { } average
            ? roll.Product?.ThicknessInSpec(average)
            : null;

        // Finished, and if it has been weighed too it may now go to the thermo -- whatever
        // the verdict. An out-of-spec roll is still a roll for its product and is still
        // used; the verdict records what it was, it does not decide what happens to it.
        UpdateReadiness(roll);

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

        // Weighed, and if its test is finished too it is ready for the thermo.
        UpdateReadiness(roll);

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

    private static Result<RollDto>? AbsorbencyError(
        SaveRollTestRequest request,
        bool isAbsorbent,
        RollTestReport? saved,
        DateTimeOffset now)
    {
        var dry = request.AbsorbencyDryWeight;
        var wet = request.AbsorbencyWetWeight;

        if (!isAbsorbent)
        {
            return dry is null && wet is null
                ? null
                : InvalidRoll("This roll is not absorbent. Leave the absorbency empty.", "roll.notAbsorbent");
        }

        if (wet is null)
        {
            return null;
        }

        // The piece is weighed dry and saved before it goes into the water, so the clock
        // has something to count from.
        if (dry is null || saved?.AbsorbencyDryWeighedAt is null)
        {
            return InvalidRoll(
                "Save the piece's dry weight first, then weigh it again after ten minutes in water.",
                "roll.absorbencyDryFirst");
        }

        // Soaked, it can only have taken water on. Lighter means the two were swapped.
        if (wet < dry)
        {
            return InvalidRoll(
                "The weight after the water is less than before. Check the two weights are not swapped.",
                "roll.absorbencyBackwards");
        }

        // A first wet weight is the one taken out of the water, and before ten minutes it
        // was not soaked for the test's ten. Correcting one already saved is not held up.
        var due = saved.AbsorbencyDryWeighedAt.Value + RollTestReport.SoakTime;
        if (saved.AbsorbencyWetWeight is null && now < due)
        {
            var minutes = (int)Math.Ceiling((due - now).TotalMinutes);
            return InvalidRoll(
                $"The piece has been in the water less than ten minutes. Weigh it again in {minutes} min.",
                "roll.absorbencySoaking",
                minutes.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return null;
    }

    /// <summary>
    /// Why the readings cannot be saved, or null. Any of them may still be missing — the
    /// test is saved as it goes — but one that is there must be a real reading.
    /// </summary>
    private static Result<RollDto>? Validate(SaveRollTestRequest request)
    {
        decimal?[] readings =
        [
            request.PlateWeight, request.ThicknessRs, request.ThicknessRm, request.ThicknessLm,
            request.ThicknessLs, request.AbsorbencyDryWeight, request.AbsorbencyWetWeight,
        ];

        if (readings.All(r => r is null))
        {
            return InvalidRoll("Type at least one reading before saving.", "roll.testEmpty");
        }

        return readings.Any(r => r is <= 0)
            ? InvalidRoll("A reading must be more than zero. Leave a box empty if it is not taken yet.", "roll.readingNotPositive")
            : null;
    }

    /// <summary>
    /// Needs Test until it is both measured and fully tested, in either order; Available
    /// after that. Goes back if a correction leaves either unfinished. A roll already in the
    /// thermo or beyond is not touched.
    /// </summary>
    private static void UpdateReadiness(Roll roll)
    {
        if (roll.Status is RollStatus.NeedsTest or RollStatus.Available)
        {
            roll.Status = roll.Weight is not null && TestComplete(roll)
                ? RollStatus.Available
                : RollStatus.NeedsTest;
        }
    }

    private static bool TestComplete(Roll roll) =>
        roll.TestReport?.IsComplete(roll.RecipeVersion.Family.IsAbsorbent) == true;

    /// <summary>While the absorbency sample is soaking, when it may be weighed again.</summary>
    private static DateTimeOffset? WetDueAt(RollTestReport? report) =>
        report is { AbsorbencyDryWeighedAt: { } dryAt, AbsorbencyWetWeight: null }
            ? dryAt + RollTestReport.SoakTime
            : null;

    /// <summary>What a roll is made to: its recipe, its colour, and the product it is for.</summary>
    private sealed record RollChoices(RecipeVersion Recipe, Color Colour, Product? Product);

    /// <summary>
    /// The recipe, colour and product a roll is logged or corrected to, checked against one
    /// another — or why they cannot be used. <paramref name="current"/> is the roll being
    /// corrected, or null for a new one.
    /// </summary>
    private async Task<(Result<RollDto>? Error, RollChoices? Choices)> ChoicesAsync(
        int recipeVersionId,
        int colorId,
        int? productId,
        Roll? current,
        CancellationToken cancellationToken)
    {
        var recipe = await db.RecipeVersions
            .Include(v => v.Family)
            .FirstOrDefaultAsync(v => v.Id == recipeVersionId, cancellationToken);

        if (recipe is null)
        {
            return (InvalidRoll("Choose the recipe this roll was made to."), null);
        }

        // What the roll already has may stay, even retired or switched off since: the roll
        // was made to it. Only a change has to be to something still in use.
        var recipeChanged = current is null || current.RecipeVersionId != recipe.Id;

        // A draft is a formula somebody is still writing. Rolls made to it could never
        // be reproduced, because a draft may still change.
        if (recipeChanged && recipe.Status == RecipeVersionStatus.Draft)
        {
            return (InvalidRoll(
                $"Recipe {recipe.RecipeNumber} is still a draft. Put it into production first."), null);
        }

        // The old Black families were retired when black became an ordinary colour
        // (specification section 5). Their recipes stay for the rolls already made, but
        // nothing new is made to them.
        if (recipeChanged && !recipe.Family.IsActive)
        {
            return (InvalidRoll(
                $"{recipe.Family.Name} is no longer used. Choose one of the main recipes.",
                "roll.recipeRetired",
                recipe.Family.Name), null);
        }

        var colour = await db.Colors
            .FirstOrDefaultAsync(
                c => c.Id == colorId && (c.IsActive || (current != null && current.ColorId == colorId)),
                cancellationToken);

        if (colour is null)
        {
            return (InvalidRoll("Choose an active colour."), null);
        }

        if (string.IsNullOrWhiteSpace(recipe.Family.Code))
        {
            return (InvalidRoll(
                $"{recipe.Family.Name} has no code for the roll code. Set one in Master Data."), null);
        }

        // What the roll is being made for (specification section 19.1). Declared, because
        // the thickness cannot be trusted to say it: the first rolls of a run are the
        // wrong thickness while the machine is being set, and are still for this product.
        if (productId is null)
        {
            // A roll made before products were declared may be corrected without one; any
            // other roll must say what it is for.
            return current is { ProductId: null }
                ? (null, new RollChoices(recipe, colour, null))
                : (InvalidRoll("Say which product this roll is for.", "roll.chooseProduct"), null);
        }

        var product = await db.Products
            .FirstOrDefaultAsync(
                p => p.Id == productId && (p.IsActive || (current != null && current.ProductId == productId)),
                cancellationToken);

        if (product is null)
        {
            return (InvalidRoll("Choose an active product.", "roll.chooseActiveProduct"), null);
        }

        // Each product is made from one main recipe — Normal plates from Normal, lunch
        // and burger boxes from Lunch Box. Caught here, at the extruder: the bags formed
        // from the roll will be this product, and nothing later checks it again.
        if (product.RecipeFamilyId is not null && product.RecipeFamilyId != recipe.RecipeFamilyId)
        {
            return (InvalidRoll(
                $"{product.Name} is not made from {recipe.Family.Name}. Choose a product this recipe makes.",
                "roll.productWrongRecipe",
                product.Name,
                recipe.Family.Name), null);
        }

        // A product not yet given a main recipe can only be checked the old way, on
        // absorbency, which both the product and the recipe still say.
        if (product.IsAbsorbent != recipe.Family.IsAbsorbent)
        {
            return (InvalidRoll(
                product.IsAbsorbent
                    ? $"{product.Name} is absorbent, but recipe {recipe.RecipeNumber} is not."
                    : $"{product.Name} is not absorbent, but recipe {recipe.RecipeNumber} is.",
                product.IsAbsorbent ? "roll.productAbsorbentRecipeNot" : "roll.productNotAbsorbentRecipeIs",
                product.Name,
                recipe.RecipeNumber.ToString()), null);
        }

        return (null, new RollChoices(recipe, colour, product));
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
            !TestComplete(roll),
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
                    roll.TestReport.AbsorbencyDryWeight,
                    roll.TestReport.AbsorbencyWetWeight,
                    roll.TestReport.AbsorbencyPercentage,
                    roll.TestReport.AbsorbencyDryWeighedAt,
                    WetDueAt(roll.TestReport),
                    TestComplete(roll),
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
