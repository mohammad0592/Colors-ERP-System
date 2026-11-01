using Colors.Application.Common.Models;
using Colors.Application.Features.Barcodes;
using Colors.Application.Features.Thermo;
using Colors.Domain.Entities.Production;
using Colors.Domain.Entities.System;
using Colors.Domain.Common;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Thermo;

/// <summary>
/// Line 2 — thermoforming. Specification section 9.
///
/// Three rules carry this phase. A roll goes in whole and is formed once. Nobody
/// chooses what is being made — the roll named its product at the extruder, and the
/// bags are that. And saving the counts is what creates the bags, because the factory
/// counts them at the end of the run, so until then the number does not exist.
/// </summary>
public class ThermoService(
    ColorsDbContext db,
    IBarcodeService barcodes,
    TimeProvider timeProvider) : IThermoService
{
    public async Task<IReadOnlyList<ThermoRunSummaryDto>> GetRunsAsync(
        int? shiftLineId = null,
        bool openOnly = false,
        bool currentShiftOnly = false,
        CancellationToken cancellationToken = default)
    {
        // The list never shows individual bags, only counts — and a shift can produce
        // thousands of them. Loading them here would drag every bag of every run across
        // the wire to display a number the test report already holds.
        var runs = await ListQuery()
            .Where(p => shiftLineId == null || p.ShiftLineId == shiftLineId)
            .Where(p => !openOnly || p.TestReport == null)
            // One shift is open at a time, which the database enforces.
            .Where(p => !currentShiftOnly
                        || p.ShiftLine.ShiftReport.Status == ShiftReportStatus.Open)
            .OrderByDescending(p => p.StartedAt)
            .Take(300)
            .ToListAsync(cancellationToken);

        var names = await UserNamesAsync(runs.Select(p => p.OperatorUserId), cancellationToken);
        var codes = await BarcodesForAsync(
            BarcodeObjectType.Roll, runs.Select(p => p.RollId), cancellationToken);
        var earlier = await UnfinishedRuns.ForRollsAsync(db, runs.Select(p => p.RollId), cancellationToken);

        return runs
            .Select(p => new ThermoRunSummaryDto(
                p.Id,
                p.RollId,
                p.Roll.RollCode,
                codes.GetValueOrDefault(p.RollId, string.Empty),
                p.Roll.Color.Name,
                p.Roll.RecipeVersion.RecipeNumber,
                p.Roll.RecipeVersion.Family.Name,
                p.Roll.RecipeVersion.Family.IsAbsorbent,
                p.ShiftLineId,
                p.ShiftLine.ProductionLine.Name,
                p.ShiftLine.ShiftReport.Shift.Name,
                p.ShiftLine.ShiftReport.ProductionDate,
                names.GetValueOrDefault(p.OperatorUserId, "—"),
                p.StartedAt,
                p.FinishedAt,
                p.TotalTimeMinutes,
                p.FinishedAt is not null,
                p.TestReport is null,
                p.TestReport?.Product.Name,
                p.TestReport?.BagCount,
                p.TestReport?.PieceCount,
                p.Roll.Weight,
                ThermoScrap.For(p, earlier[p.RollId].PlatesKg),
                p.LeftUnfinished,
                p.UnfinishedReason))
            .ToList();
    }

    public async Task<Result<ThermoRunDto>> GetRunAsync(
        int runId,
        CancellationToken cancellationToken = default)
    {
        var run = await RunQuery().FirstOrDefaultAsync(p => p.Id == runId, cancellationToken);

        return run is null
            ? RunNotFound()
            : Result<ThermoRunDto>.Success(await ToRunDtoAsync(run, cancellationToken));
    }

    public async Task<IReadOnlyList<AvailableRollDto>> GetAvailableRollsAsync(
        CancellationToken cancellationToken = default)
    {
        var rolls = await db.Rolls
            .Include(r => r.RecipeVersion).ThenInclude(v => v.Family)
            .Include(r => r.Color)
            .Include(r => r.TestReport)
            .Include(r => r.Product)
            // In stock, or back in stock after being taken out unfinished (section 19.23).
            .Where(r => r.Status == RollStatus.Available || r.Status == RollStatus.NotCompleted)
            // Oldest first: rolls sit for weeks, and the oldest should move first.
            .OrderBy(r => r.ProducedAt)
            .Take(300)
            .ToListAsync(cancellationToken);

        var codes = await BarcodesForAsync(
            BarcodeObjectType.Roll, rolls.Select(r => r.Id), cancellationToken);

        return rolls
            .Select(r => new AvailableRollDto(
                r.Id,
                r.RollCode,
                codes.GetValueOrDefault(r.Id, string.Empty),
                r.Color.Name,
                r.RecipeVersion.RecipeNumber,
                r.RecipeVersion.Family.Name,
                r.RecipeVersion.Family.IsAbsorbent,
                r.ProductionDate,
                r.Weight,
                r.Length,
                r.ProductId,
                r.Product?.Name,
                r.RecipeVersion.RecipeFamilyId,
                r.Status == RollStatus.NotCompleted))
            .ToList();
    }

    public async Task<Result<ThermoRunDto>> StartRunAsync(
        StartThermoRunRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var shiftLine = await db.ShiftLines
            .Include(l => l.ProductionLine)
            .Include(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .FirstOrDefaultAsync(l => l.Id == request.ShiftLineId, cancellationToken);

        if (shiftLine is null)
        {
            return InvalidRun("Choose a line of an open shift.");
        }

        if (!shiftLine.ProductionLine.FormsBags)
        {
            return InvalidRun(
                $"{shiftLine.ProductionLine.Name} does not form bags. Choose the thermo line.");
        }

        if (!ShiftWork.AcceptsWork(shiftLine.ShiftReport.Status))
        {
            return InvalidRun(ShiftWork.RefusalFor(shiftLine.ShiftReport));
        }

        var roll = await FindRollAsync(request, cancellationToken);
        if (!roll.IsSuccess)
        {
            return InvalidRun(roll.Message ?? "Scan the roll.");
        }

        var found = roll.Value!;

        // A roll taken out unfinished goes in again like any roll in stock (section 19.23).
        if (found.Status is not (RollStatus.Available or RollStatus.NotCompleted))
        {
            return InvalidRun(StatusRefusal(found));
        }

        // The roll says what it was made for, and the bags formed from it are that product
        // (specification section 19.9). Nothing on the line says what is in the machine:
        // the mould is the product, and the roll already named it at the extruder.
        //
        // The product is named here, when the roll goes in: the extruder names only the
        // recipe (section 19.17). Written onto the roll, so the bags and every report after
        // read it from there. A roll that already has one — logged when the extruder still
        // asked — keeps it.
        if (found.ProductId is null)
        {
            var refusal = await DeclareProductAsync(found, request.ProductId, cancellationToken);
            if (refusal is not null)
            {
                return refusal;
            }
        }

        var startedAt = request.StartedAt ?? timeProvider.GetUtcNow();

        var run = new ThermoProduction
        {
            RollId = found.Id,
            ShiftLineId = shiftLine.Id,
            OperatorUserId = userId,
            StartedAt = startedAt,
            Notes = Trimmed(request.Notes),
        };

        // The roll leaves the store the moment it goes in, so nobody else can pick it.
        found.Status = RollStatus.InThermo;

        db.ThermoProductions.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        return await LoadRunAsync(run.Id, cancellationToken);
    }

    public async Task<Result<ThermoRunDto>> FinishRunAsync(
        int runId,
        FinishThermoRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var run = await RunQuery().FirstOrDefaultAsync(p => p.Id == runId, cancellationToken);
        if (run is null)
        {
            return RunNotFound();
        }

        if (run.FinishedAt is not null)
        {
            return InvalidRun($"Roll {run.Roll.RollCode} is already out of the machine.");
        }

        var finishedAt = request.FinishedAt ?? timeProvider.GetUtcNow();

        if (finishedAt < run.StartedAt)
        {
            return InvalidRun("The roll cannot come out before it went in.");
        }

        run.FinishedAt = finishedAt;

        // Formed. What came out of it is counted next, on the test form.
        run.Roll.Status = RollStatus.Processed;

        await db.SaveChangesAsync(cancellationToken);

        return await LoadRunAsync(runId, cancellationToken);
    }

    public async Task<Result<ThermoRunDto>> LeaveUnfinishedAsync(
        int runId,
        LeaveRunUnfinishedRequest request,
        CancellationToken cancellationToken = default)
    {
        // A roll back in stock half used needs the reason with it: whoever picks it up next
        // has to know what happened (section 19.23).
        var reason = Trimmed(request.Reason);
        if (reason is null)
        {
            return InvalidRun(
                "Say why the roll could not be finished.",
                "thermo.unfinishedReasonRequired");
        }

        var run = await RunQuery().FirstOrDefaultAsync(p => p.Id == runId, cancellationToken);
        if (run is null)
        {
            return RunNotFound();
        }

        if (run.FinishedAt is not null)
        {
            return InvalidRun(
                $"Roll {run.Roll.RollCode} is already out of the machine.",
                "thermo.alreadyOut",
                run.Roll.RollCode);
        }

        var finishedAt = request.FinishedAt ?? timeProvider.GetUtcNow();
        if (finishedAt < run.StartedAt)
        {
            return InvalidRun("The roll cannot come out before it went in.");
        }

        run.FinishedAt = finishedAt;
        run.UnfinishedReason = reason;

        // Back in stock, half used, and free to go in again. What it made so far is
        // counted next, as for any run.
        run.Roll.Status = RollStatus.NotCompleted;

        await db.SaveChangesAsync(cancellationToken);

        return await LoadRunAsync(runId, cancellationToken);
    }

    public async Task<Result<ThermoRunDto>> SaveTestReportAsync(
        int runId,
        SaveThermoTestRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var run = await RunQuery().FirstOrDefaultAsync(p => p.Id == runId, cancellationToken);
        if (run is null)
        {
            return RunNotFound();
        }

        if (run.TestReport is not null)
        {
            return InvalidRun(
                $"Roll {run.Roll.RollCode} has already been counted. An administrator "
                + "corrects a number that was written down wrong.");
        }

        // The bags are counted at the end of the run, so the run has to have ended.
        if (run.FinishedAt is null)
        {
            return InvalidRun(
                $"Roll {run.Roll.RollCode} is still in the machine. Take it out first, "
                + "then count what it made.");
        }

        var error = Validate(request);
        if (error is not null)
        {
            return InvalidRun(error);
        }

        var isAbsorbent = run.Roll.RecipeVersion.Family.IsAbsorbent;

        // Absorbency comes from what was mixed, so a Normal roll cannot have absorbed
        // anything. A number here would be somebody filling in the wrong row.
        if (!isAbsorbent && request.AbsorbentPercentage != 0)
        {
            return InvalidRun(
                $"Roll {run.Roll.RollCode} was made to {run.Roll.RecipeVersion.Family.Name}, "
                + "which is not absorbent. Leave the absorbency at zero.");
        }

        // Nobody chooses the product here. The roll named it at the extruder, or when it
        // went into the machine if it was made before rolls did (specification 19.9).
        var product = run.Roll.Product;

        if (product is null)
        {
            return InvalidRun(
                $"Roll {run.Roll.RollCode} does not say what it was made for, so the bags "
                + "cannot be named.");
        }

        var pieceCount = request.BagCount * product.PiecesPerBag;
        var now = timeProvider.GetUtcNow();

        // The factory's own clock, for the bag codes: the date and time in them, and the
        // day whose count the numbers come from (section 19.25).
        var local = now.ToOffset(TimeSpan.FromMinutes(request.UtcOffsetMinutes));

        // The counts and the bags they describe are one act: bags with no report would
        // belong to nothing, and a report claiming bags nobody can scan is worse.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        db.ThermoTestReports.Add(new ThermoTestReport
        {
            ThermoProductionId = run.Id,
            ProductId = product.Id,
            BagCount = request.BagCount,
            PieceCount = pieceCount,
            PieceWeight = request.PieceWeight,
            BagWeight = request.BagWeight,
            AbsorbentPercentage = request.AbsorbentPercentage,
            TestedByUserId = userId,
            TestedAt = now,
            Notes = Trimmed(request.Notes),
        });

        var bags = Enumerable.Range(0, request.BagCount)
            .Select(_ => new ProducedBag
            {
                ThermoProductionId = run.Id,
                ColorId = run.Roll.ColorId,
                ProductId = product.Id,
                Weight = request.BagWeight,
                PieceCount = product.PiecesPerBag,
                Status = ProducedBagStatus.Available,
                CreatedAt = now,
            })
            .ToList();

        db.ProducedBags.AddRange(bags);
        await db.SaveChangesAsync(cancellationToken);

        var coded = await CodeBagsAsync(bags, local, cancellationToken);
        if (coded is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return InvalidRun(coded);
        }

        await transaction.CommitAsync(cancellationToken);

        return await LoadRunAsync(runId, cancellationToken);
    }

    public async Task<Result<ThermoRunDto>> CorrectTestReportAsync(
        int runId,
        CorrectThermoTestRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var run = await RunQuery().FirstOrDefaultAsync(p => p.Id == runId, cancellationToken);
        if (run is null)
        {
            return RunNotFound();
        }

        var report = run.TestReport;
        if (report is null)
        {
            return InvalidRun(
                $"Roll {run.Roll.RollCode} has not been counted yet. Count it instead.",
                "thermo.notCountedYet",
                run.Roll.RollCode);
        }

        var reason = Trimmed(request.Reason);
        if (reason is null)
        {
            return InvalidRun("Say why the counts are being corrected.", "correction.reasonRequired");
        }

        var error = Validate(new SaveThermoTestRequest(
            request.BagCount,
            request.PieceWeight,
            request.BagWeight,
            request.AbsorbentPercentage,
            request.Notes,
            request.UtcOffsetMinutes));
        if (error is not null)
        {
            return InvalidRun(error);
        }

        if (!run.Roll.RecipeVersion.Family.IsAbsorbent && request.AbsorbentPercentage != 0)
        {
            return InvalidRun(
                $"Roll {run.Roll.RollCode} was made to {run.Roll.RecipeVersion.Family.Name}, "
                + "which is not absorbent. Leave the absorbency at zero.");
        }

        // The bags that exist. One cancelled by an earlier correction never did.
        var live = run.Bags.Where(b => b.Status != ProducedBagStatus.Cancelled).OrderBy(b => b.Id).ToList();
        var piecesPerBag = report.PieceCount / report.BagCount;
        var now = timeProvider.GetUtcNow();
        var difference = request.BagCount - live.Count;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (difference < 0)
        {
            // Too many were made. The extra ones go, newest first, and only off a pallet:
            // a bag on a pallet is under other bags and in somebody's count.
            var extra = live
                .Where(b => b.Status == ProducedBagStatus.Available)
                .OrderByDescending(b => b.Id)
                .Take(-difference)
                .ToList();

            if (extra.Count < -difference)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InvalidRun(
                    $"{-difference} bags have to go, and only {extra.Count} of this roll's bags are off a "
                    + "pallet. Take the rest off their pallets first, then correct the count.",
                    "thermo.correctionBagsOnPallets",
                    (-difference).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    extra.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            var extraIds = extra.Select(b => b.Id).ToList();
            foreach (var bag in extra)
            {
                bag.Status = ProducedBagStatus.Cancelled;
                bag.RemovedAt = now;
                bag.RemovedByUserId = userId;
                bag.RemovalReason = reason;
            }

            // Their labels stop scanning: a bag that never existed must not reach a pallet.
            await db.Barcodes
                .Where(b => b.ObjectType == BarcodeObjectType.Bag && extraIds.Contains(b.ObjectId) && b.IsActive)
                .ExecuteUpdateAsync(set => set.SetProperty(b => b.IsActive, false), cancellationToken);
        }

        // The weight follows the corrected figure on every bag of the run, as it was copied
        // from it at the count.
        foreach (var bag in run.Bags.Where(b => b.Status != ProducedBagStatus.Cancelled))
        {
            bag.Weight = request.BagWeight;
        }

        report.BagCount = request.BagCount;
        report.PieceCount = request.BagCount * piecesPerBag;
        report.PieceWeight = request.PieceWeight;
        report.BagWeight = request.BagWeight;
        report.AbsorbentPercentage = request.AbsorbentPercentage;
        report.Notes = Trimmed(request.Notes);
        report.CorrectedAt = now;
        report.CorrectedByUserId = userId;
        report.CorrectionReason = reason;

        if (difference > 0)
        {
            // Too few were made: the missing ones now, coded as of now, as any bag is.
            var added = Enumerable.Range(0, difference)
                .Select(_ => new ProducedBag
                {
                    ThermoProductionId = run.Id,
                    ColorId = run.Roll.ColorId,
                    ProductId = report.ProductId,
                    Weight = request.BagWeight,
                    PieceCount = piecesPerBag,
                    Status = ProducedBagStatus.Available,
                    CreatedAt = now,
                })
                .ToList();

            db.ProducedBags.AddRange(added);
            await db.SaveChangesAsync(cancellationToken);

            var coded = await CodeBagsAsync(
                added, now.ToOffset(TimeSpan.FromMinutes(request.UtcOffsetMinutes)), cancellationToken);
            if (coded is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                return InvalidRun(coded);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await LoadRunAsync(runId, cancellationToken);
    }

    public async Task<Result<bool>> UndoStartAsync(
        int runId,
        UndoThermoStartRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var run = await db.ThermoProductions
            .Include(p => p.Roll)
            .Include(p => p.TestReport)
            .Include(p => p.ShiftLine)
            .FirstOrDefaultAsync(p => p.Id == runId, cancellationToken);
        if (run is null)
        {
            return Result<bool>.Failure(ErrorCode.NotFound, "This run does not exist.", "thermo.runNotFound");
        }

        // Only while the roll is still in the machine. Once it is out, it was formed —
        // plates were made — and the way back is to leave it unfinished.
        if (run.FinishedAt is not null || run.TestReport is not null)
        {
            return Result<bool>.Failure(
                ErrorCode.ValidationFailed,
                $"Roll {run.Roll.RollCode} has already been taken out. Only a roll still in the machine "
                + "is taken back as a wrong start.",
                "thermo.undoOnlyInMachine",
                run.Roll.RollCode);
        }

        var reason = Trimmed(request.Reason);
        if (reason is null)
        {
            return Result<bool>.Failure(
                ErrorCode.ValidationFailed, "Say why the start is being taken back.", "correction.reasonRequired");
        }

        // Back where it was: in stock, or not completed if an earlier run left it so.
        var workedBefore = await db.ThermoProductions
            .AnyAsync(p => p.RollId == run.RollId && p.Id != run.Id, cancellationToken);
        run.Roll.Status = workedBefore ? RollStatus.NotCompleted : RollStatus.Available;

        // The run made nothing and nothing hangs off it, so it goes. The audit log keeps
        // that it happened, who took it back and why.
        db.ThermoProductions.Remove(run);
        db.AuditEntries.Add(new AuditEntry
        {
            UserId = userId,
            ShiftReportId = run.ShiftLine.ShiftReportId,
            Action = "Thermo.UndoStart",
            ObjectType = "Roll",
            ObjectId = run.RollId,
            Result = AuditResult.Success,
            Details = $"Roll {run.Roll.RollCode} taken back out of the thermo, started by mistake: {reason}",
            Timestamp = timeProvider.GetUtcNow(),
        });

        await db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    public async Task<Result<ProducedBagDto>> MarkBagDefectiveAsync(
        int bagId,
        MarkBagDefectiveRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var bag = await db.ProducedBags
            .Include(b => b.Color)
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.Id == bagId, cancellationToken);
        if (bag is null)
        {
            return Result<ProducedBagDto>.Failure(ErrorCode.NotFound, "This bag does not exist.", "bag.notFound");
        }

        // A bag on a pallet is taken off first, which the pallet's history records.
        if (bag.Status != ProducedBagStatus.Available)
        {
            return Result<ProducedBagDto>.Failure(
                ErrorCode.ValidationFailed,
                bag.Status == ProducedBagStatus.Assigned
                    ? "This bag is on a pallet. Take it off the pallet first."
                    : "Only a bag waiting for a pallet can be marked defective.",
                bag.Status == ProducedBagStatus.Assigned ? "bag.onPallet" : "bag.notAvailable");
        }

        var reason = Trimmed(request.Reason);
        if (reason is null)
        {
            return Result<ProducedBagDto>.Failure(
                ErrorCode.ValidationFailed, "Say what is wrong with the bag.", "correction.reasonRequired");
        }

        bag.Status = ProducedBagStatus.Defective;
        bag.RemovedAt = timeProvider.GetUtcNow();
        bag.RemovedByUserId = userId;
        bag.RemovalReason = reason;
        await db.SaveChangesAsync(cancellationToken);

        var codes = await BarcodesForAsync(BarcodeObjectType.Bag, [bag.Id], cancellationToken);
        return Result<ProducedBagDto>.Success(new ProducedBagDto(
            bag.Id,
            codes.GetValueOrDefault(bag.Id, string.Empty),
            bag.Color.Name,
            bag.Product.Name,
            bag.Weight,
            bag.PieceCount,
            bag.Status.ToString(),
            bag.CreatedAt));
    }

    // ---------- helpers ----------

    /// <summary>
    /// Gives each new bag its code, which is its barcode (section 19.25): B, its number that
    /// day, the date and the time — B0050111261758. Returns why not, or null.
    ///
    /// The day's numbers are taken inside the caller's transaction: the counter's row stays
    /// locked until it commits, so two tablets saving at once cannot both be handed 005,
    /// and a refusal rolls it back so the numbers are not lost.
    /// </summary>
    private async Task<string?> CodeBagsAsync(
        List<ProducedBag> bags,
        DateTimeOffset local,
        CancellationToken cancellationToken)
    {
        var firstNumber = await TakeBagNumbersAsync(
            DateOnly.FromDateTime(local.DateTime), bags.Count, cancellationToken);

        foreach (var (bag, index) in bags.Select((bag, index) => (bag, index)))
        {
            var barcode = await barcodes.IssueAsync(
                BarcodeObjectType.Bag,
                bag.Id,
                BagCode.For(firstNumber + index, local),
                cancellationToken);

            if (!barcode.IsSuccess)
            {
                return barcode.Message ?? "The bag labels could not be printed.";
            }
        }

        return null;
    }

    /// <summary>
    /// A roll weighs fifty to a hundred and fifty kilos and holds a few thousand pieces,
    /// so these are the walls a typo hits, not a quality judgement.
    /// </summary>
    private const int MaxBagsPerRoll = 200;

    private static string? Validate(SaveThermoTestRequest request)
    {
        if (request.BagCount <= 0)
        {
            return "How many bags did the roll make?";
        }

        if (request.BagCount > MaxBagsPerRoll)
        {
            return $"One roll does not make {request.BagCount} bags. Check the number.";
        }

        if (request.PieceWeight <= 0)
        {
            return "The weight of one piece is missing.";
        }

        if (request.BagWeight <= 0)
        {
            return "The weight of one bag is missing.";
        }

        if (request.UtcOffsetMinutes is < MinUtcOffsetMinutes or > MaxUtcOffsetMinutes)
        {
            return "The tablet's time zone is not one that exists. Check its clock settings.";
        }

        return request.AbsorbentPercentage is < 0 or > 100
            ? "The absorbency is a percentage, between 0 and 100."
            : null;
    }

    // The widest time zones there are, UTC-12 to UTC+14.
    private const int MinUtcOffsetMinutes = -12 * 60;
    private const int MaxUtcOffsetMinutes = 14 * 60;

    /// <summary>
    /// Takes <paramref name="count"/> numbers from the day's count and moves it on, in one
    /// statement, returning the first. The first bag of a day adds its row, starting at 1;
    /// that is the whole of the daily reset. The update locks the row until the caller's
    /// transaction ends, so a second tablet waits rather than reading the same numbers.
    /// </summary>
    private async Task<int> TakeBagNumbersAsync(
        DateOnly day,
        int count,
        CancellationToken cancellationToken)
    {
        var taken = await db.Database
            .SqlQuery<int>(
                $"""
                 INSERT INTO "BagNumberCounters" ("Day", "NextNumber") VALUES ({day}, 1 + {count})
                 ON CONFLICT ("Day") DO UPDATE
                     SET "NextNumber" = "BagNumberCounters"."NextNumber" + {count}
                 RETURNING "NextNumber" - {count} AS "Value"
                 """)
            .ToListAsync(cancellationToken);

        return taken[0];
    }

    /// <summary>
    /// The scan comes first, because that is what the floor does. An id is accepted too,
    /// for the office picking off the list.
    /// </summary>
    /// <summary>
    /// Gives a roll the product it is being formed into: an active product, made from the
    /// roll's recipe.
    /// </summary>
    private async Task<Result<ThermoRunDto>?> DeclareProductAsync(
        Roll roll,
        int? productId,
        CancellationToken cancellationToken)
    {
        if (productId is null)
        {
            return InvalidRun(
                $"Choose the product roll {roll.RollCode} is being formed into.",
                "thermo.chooseProduct",
                roll.RollCode);
        }

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive, cancellationToken);

        if (product is null)
        {
            return InvalidRun("Choose an active product.", "roll.chooseActiveProduct");
        }

        var family = roll.RecipeVersion.Family;
        var fits = product.RecipeFamilyId is null
            ? product.IsAbsorbent == family.IsAbsorbent
            : product.RecipeFamilyId == family.Id;

        if (!fits)
        {
            return InvalidRun(
                $"{product.Name} is not made from {family.Name}. Choose a product this recipe makes.",
                "roll.productWrongRecipe",
                product.Name,
                family.Name);
        }

        roll.ProductId = product.Id;
        roll.Product = product;
        return null;
    }

    private async Task<Result<Roll>> FindRollAsync(
        StartThermoRunRequest request,
        CancellationToken cancellationToken)
    {
        var rolls = db.Rolls
            .Include(r => r.RecipeVersion).ThenInclude(v => v.Family)
            .Include(r => r.Color)
            .Include(r => r.Product);

        if (!string.IsNullOrWhiteSpace(request.RollBarcode))
        {
            var scan = await barcodes.LookupAsync(
                request.RollBarcode.Trim(), BarcodeObjectType.Roll, cancellationToken);

            // A wrong kind of label comes back *successful*, carrying "that is a bag,
            // not a roll". The type has to be checked here — without it, a bag label
            // whose id happens to match a roll's would quietly form the wrong roll.
            if (!scan.IsSuccess
                || scan.Value is null
                || !scan.Value.Found
                || scan.Value.ObjectType != BarcodeObjectType.Roll.ToString())
            {
                return Result<Roll>.Failure(
                    ErrorCode.ValidationFailed,
                    scan.Value?.Message ?? scan.Message ?? "That label is not one of ours.");
            }

            var scanned = await rolls
                .FirstOrDefaultAsync(r => r.Id == scan.Value.ObjectId, cancellationToken);

            return scanned is null
                ? Result<Roll>.Failure(
                    ErrorCode.NotFound,
                    "That label names a roll that is no longer here.", "thermo.rollGone")
                : Result<Roll>.Success(scanned);
        }

        if (request.RollId is null)
        {
            return Result<Roll>.Failure(ErrorCode.ValidationFailed, "Scan the roll.", "thermo.scanTheRoll");
        }

        var picked = await rolls.FirstOrDefaultAsync(r => r.Id == request.RollId, cancellationToken);

        return picked is null
            ? Result<Roll>.Failure(ErrorCode.NotFound, "This roll does not exist.", "roll.notFound")
            : Result<Roll>.Success(picked);
    }

    private static string StatusRefusal(Roll roll) => roll.Status switch
    {
        RollStatus.NeedsTest =>
            $"Roll {roll.RollCode} has not been measured and tested yet. Once it is formed "
            + "there is nothing left to measure, so it must be done first.",
        RollStatus.InThermo => $"Roll {roll.RollCode} is already in the machine.",
        RollStatus.Processed => $"Roll {roll.RollCode} has already been formed.",
        RollStatus.Scrapped => $"Roll {roll.RollCode} was scrapped.",
        RollStatus.Cancelled => $"Roll {roll.RollCode} was cancelled — it was logged by mistake.",
        _ => $"Roll {roll.RollCode} cannot go into the thermo.",
    };

    /// <summary>Everything a row in the list needs, and nothing more.</summary>
    private IQueryable<ThermoProduction> ListQuery() =>
        db.ThermoProductions
            .Include(p => p.Roll).ThenInclude(r => r.RecipeVersion).ThenInclude(v => v.Family)
            .Include(p => p.Roll).ThenInclude(r => r.Color)
            // The roll's own test carries its weight, which is what the run's scrap is
            // worked out from (specification section 9).
            .Include(p => p.Roll).ThenInclude(r => r.TestReport)
            .Include(p => p.Roll).ThenInclude(r => r.Product)
            .Include(p => p.ShiftLine).ThenInclude(l => l.ProductionLine)
            .Include(p => p.ShiftLine).ThenInclude(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .Include(p => p.TestReport).ThenInclude(t => t!.Product);

    /// <summary>
    /// One run in full, bags included. Split, because a run with two collections behind
    /// it multiplies rows against each other in a single query.
    /// </summary>
    private IQueryable<ThermoProduction> RunQuery() =>
        ListQuery()
            .Include(p => p.Bags).ThenInclude(b => b.Product)
            .Include(p => p.Bags).ThenInclude(b => b.Color)
            .AsSplitQuery();

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
        BarcodeObjectType objectType,
        IEnumerable<int> objectIds,
        CancellationToken cancellationToken)
    {
        var wanted = objectIds.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        return await db.Barcodes
            .Where(b => b.ObjectType == objectType && wanted.Contains(b.ObjectId) && b.IsActive)
            .ToDictionaryAsync(b => b.ObjectId, b => b.Value, cancellationToken);
    }

    private async Task<Result<ThermoRunDto>> LoadRunAsync(int id, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var run = await RunQuery().FirstAsync(p => p.Id == id, cancellationToken);

        return Result<ThermoRunDto>.Success(await ToRunDtoAsync(run, cancellationToken));
    }

    private async Task<ThermoRunDto> ToRunDtoAsync(
        ThermoProduction run,
        CancellationToken cancellationToken)
    {
        var ids = new List<int> { run.OperatorUserId };
        if (run.TestReport is not null)
        {
            ids.Add(run.TestReport.TestedByUserId);
            if (run.TestReport.CorrectedByUserId is not null)
            {
                ids.Add(run.TestReport.CorrectedByUserId.Value);
            }
        }

        var names = await UserNamesAsync(ids, cancellationToken);
        var rollCodes = await BarcodesForAsync(
            BarcodeObjectType.Roll, [run.RollId], cancellationToken);
        var bagCodes = await BarcodesForAsync(
            BarcodeObjectType.Bag, run.Bags.Select(b => b.Id), cancellationToken);

        return new ThermoRunDto(
            run.Id,
            run.RollId,
            run.Roll.RollCode,
            rollCodes.GetValueOrDefault(run.RollId, string.Empty),
            run.Roll.ColorId,
            run.Roll.Color.Name,
            run.Roll.RecipeVersion.RecipeNumber,
            run.Roll.RecipeVersion.Family.Name,
            run.Roll.RecipeVersion.Family.IsAbsorbent,
            run.ShiftLineId,
            run.ShiftLine.ProductionLine.Name,
            run.ShiftLine.ShiftReport.Shift.Name,
            run.ShiftLine.ShiftReport.ProductionDate,
            run.Roll.Product?.Name,
            names.GetValueOrDefault(run.OperatorUserId, "—"),
            run.StartedAt,
            run.FinishedAt,
            run.TotalTimeMinutes,
            run.Notes,
            // Read-only, pulled from the roll rather than typed again here.
            run.Roll is { Weight: { } weight, Length: { } length, TestReport: { PlateWeight: { } plate, AverageThickness: { } thickness } }
                ? new RollReadingsDto(weight, length, plate, thickness)
                : null,
            run.TestReport is null
                ? null
                : new ThermoTestReportDto(
                    run.TestReport.Id,
                    run.TestReport.ProductId,
                    run.TestReport.Product.Name,
                    run.TestReport.BagCount,
                    run.TestReport.PieceCount,
                    run.TestReport.PieceWeight,
                    run.TestReport.BagWeight,
                    run.TestReport.AbsorbentPercentage,
                    names.GetValueOrDefault(run.TestReport.TestedByUserId, "—"),
                    run.TestReport.TestedAt,
                    run.TestReport.Notes,
                    run.TestReport.CorrectedAt,
                    run.TestReport.CorrectedByUserId is null
                        ? null
                        : names.GetValueOrDefault(run.TestReport.CorrectedByUserId.Value),
                    run.TestReport.CorrectionReason),
            ThermoScrap.For(run, (await UnfinishedRuns.ForRollsAsync(db, [run.RollId], cancellationToken))[run.RollId].PlatesKg),
            run.Bags
                .Where(b => b.Status != ProducedBagStatus.Cancelled)
                .OrderBy(b => b.Id)
                .Select(b => new ProducedBagDto(
                    b.Id,
                    bagCodes.GetValueOrDefault(b.Id, string.Empty),
                    b.Color.Name,
                    b.Product.Name,
                    b.Weight,
                    b.PieceCount,
                    b.Status.ToString(),
                    b.CreatedAt))
                .ToList(),
            run.LeftUnfinished,
            run.UnfinishedReason);
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<ThermoRunDto> InvalidRun(string message) =>
        Result<ThermoRunDto>.Failure(ErrorCode.ValidationFailed, message);

    /// <summary>The same refusal, named so the screens can say it in Arabic.</summary>
    private static Result<ThermoRunDto> InvalidRun(string message, string code, params string[] args) =>
        Result<ThermoRunDto>.Failure(ErrorCode.ValidationFailed, message, code, args);

    private static Result<ThermoRunDto> RunNotFound() =>
        Result<ThermoRunDto>.Failure(ErrorCode.NotFound, "This run does not exist.", "thermo.runNotFound");
}
