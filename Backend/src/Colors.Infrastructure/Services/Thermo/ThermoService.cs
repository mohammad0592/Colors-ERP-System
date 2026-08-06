using Colors.Application.Common.Models;
using Colors.Application.Features.Barcodes;
using Colors.Application.Features.Thermo;
using Colors.Domain.Entities.Production;
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
/// chooses what is being made — the mould on the line and the roll's absorbency decide
/// it between them. And saving the counts is what creates the bags, because the factory
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
        CancellationToken cancellationToken = default)
    {
        // The list never shows individual bags, only counts — and a shift can produce
        // thousands of them. Loading them here would drag every bag of every run across
        // the wire to display a number the test report already holds.
        var runs = await ListQuery()
            .Where(p => shiftLineId == null || p.ShiftLineId == shiftLineId)
            .Where(p => !openOnly || p.TestReport == null)
            .OrderByDescending(p => p.StartedAt)
            .Take(300)
            .ToListAsync(cancellationToken);

        var names = await UserNamesAsync(runs.Select(p => p.OperatorUserId), cancellationToken);
        var codes = await BarcodesForAsync(
            BarcodeObjectType.Roll, runs.Select(p => p.RollId), cancellationToken);

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
                p.TestReport?.PieceCount))
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
            .Where(r => r.Status == RollStatus.Available)
            // Oldest first: rolls sit for weeks, and the oldest should move first.
            .OrderBy(r => r.ProductionDate)
            .ThenBy(r => r.DailySerial)
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
                r.TestReport?.Weight))
            .ToList();
    }

    public async Task<Result<ThermoRunDto>> StartRunAsync(
        StartThermoRunRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var shiftLine = await db.ShiftLines
            .Include(l => l.ProductionLine)
            .Include(l => l.Mould)
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

        // Without a mould there is no way to know what is being made, and the product is
        // never typed. Better to say so now than to refuse at the end of the run.
        if (shiftLine.MouldId is null)
        {
            return InvalidRun(
                "No mould is mounted on this line for this shift. Set it on the shift first.");
        }

        var roll = await FindRollAsync(request, cancellationToken);
        if (!roll.IsSuccess)
        {
            return InvalidRun(roll.Message ?? "Scan the roll.");
        }

        var found = roll.Value!;

        if (found.Status != RollStatus.Available)
        {
            return InvalidRun(StatusRefusal(found));
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

        // Nobody chooses the product. The mould comes from the shift, the absorbency from
        // the roll's recipe, and those two are the unique key on Products.
        var product = await db.Products
            .Include(p => p.Mould)
            .FirstOrDefaultAsync(
                p => p.MouldId == run.ShiftLine.MouldId && p.IsAbsorbent == isAbsorbent,
                cancellationToken);

        if (product is null)
        {
            var mould = run.ShiftLine.Mould?.Name ?? "this mould";
            var kind = isAbsorbent ? "an absorbent" : "a normal";
            return InvalidRun(
                $"The factory does not make {kind} product on {mould}. Check the mould on the "
                + "shift, or add the product in Master Data.");
        }

        var pieceCount = request.BagCount * product.PiecesPerBag;
        var now = timeProvider.GetUtcNow();

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

        foreach (var bag in bags)
        {
            var barcode = await barcodes.IssueAsync(
                BarcodeObjectType.Bag, bag.Id, cancellationToken);

            if (!barcode.IsSuccess)
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                return InvalidRun(barcode.Message ?? "The bag labels could not be printed.");
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return await LoadRunAsync(runId, cancellationToken);
    }

    // ---------- helpers ----------

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

        return request.AbsorbentPercentage is < 0 or > 100
            ? "The absorbency is a percentage, between 0 and 100."
            : null;
    }

    /// <summary>
    /// The scan comes first, because that is what the floor does. An id is accepted too,
    /// for the office picking off the list.
    /// </summary>
    private async Task<Result<Roll>> FindRollAsync(
        StartThermoRunRequest request,
        CancellationToken cancellationToken)
    {
        var rolls = db.Rolls
            .Include(r => r.RecipeVersion).ThenInclude(v => v.Family)
            .Include(r => r.Color);

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
                    "That label names a roll that is no longer here.")
                : Result<Roll>.Success(scanned);
        }

        if (request.RollId is null)
        {
            return Result<Roll>.Failure(ErrorCode.ValidationFailed, "Scan the roll.");
        }

        var picked = await rolls.FirstOrDefaultAsync(r => r.Id == request.RollId, cancellationToken);

        return picked is null
            ? Result<Roll>.Failure(ErrorCode.NotFound, "This roll does not exist.")
            : Result<Roll>.Success(picked);
    }

    private static string StatusRefusal(Roll roll) => roll.Status switch
    {
        RollStatus.NeedsTest =>
            $"Roll {roll.RollCode} has not been measured yet. Once it is formed there is "
            + "nothing left to measure, so it must be done first.",
        RollStatus.InThermo => $"Roll {roll.RollCode} is already in the machine.",
        RollStatus.Processed => $"Roll {roll.RollCode} has already been formed.",
        RollStatus.Scrapped => $"Roll {roll.RollCode} was scrapped.",
        _ => $"Roll {roll.RollCode} cannot go into the thermo.",
    };

    /// <summary>Everything a row in the list needs, and nothing more.</summary>
    private IQueryable<ThermoProduction> ListQuery() =>
        db.ThermoProductions
            .Include(p => p.Roll).ThenInclude(r => r.RecipeVersion).ThenInclude(v => v.Family)
            .Include(p => p.Roll).ThenInclude(r => r.Color)
            .Include(p => p.ShiftLine).ThenInclude(l => l.ProductionLine)
            .Include(p => p.ShiftLine).ThenInclude(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .Include(p => p.TestReport).ThenInclude(t => t!.Product);

    /// <summary>
    /// One run in full, bags included. Split, because a run with two collections behind
    /// it multiplies rows against each other in a single query.
    /// </summary>
    private IQueryable<ThermoProduction> RunQuery() =>
        ListQuery()
            .Include(p => p.Roll).ThenInclude(r => r.TestReport)
            .Include(p => p.ShiftLine).ThenInclude(l => l.Mould)
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
            run.ShiftLine.Mould?.Name,
            names.GetValueOrDefault(run.OperatorUserId, "—"),
            run.StartedAt,
            run.FinishedAt,
            run.TotalTimeMinutes,
            run.Notes,
            // Read-only, pulled from the roll rather than typed again here.
            run.Roll.TestReport is null
                ? null
                : new RollReadingsDto(
                    run.Roll.TestReport.Weight,
                    run.Roll.TestReport.Length,
                    run.Roll.TestReport.PlateWeight,
                    run.Roll.TestReport.AverageThickness),
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
                    run.TestReport.Notes),
            run.Bags
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
                .ToList());
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<ThermoRunDto> InvalidRun(string message) =>
        Result<ThermoRunDto>.Failure(ErrorCode.ValidationFailed, message);

    private static Result<ThermoRunDto> RunNotFound() =>
        Result<ThermoRunDto>.Failure(ErrorCode.NotFound, "This run does not exist.");
}
