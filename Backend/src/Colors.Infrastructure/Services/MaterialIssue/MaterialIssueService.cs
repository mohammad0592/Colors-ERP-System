using Colors.Application.Common.Models;
using Colors.Application.Features.MaterialIssue;
using Colors.Domain.Constants;
using Colors.Domain.Entities.Inventory;
using Colors.Domain.Common;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.MaterialIssue;

/// <summary>
/// Material issue and return. Specification section 7.
///
/// The whole point is one subtraction: <c>issued − returned = actually used</c>. Both
/// ends are weighed, so neither is a memory. A shift that used more than its recipe
/// allows is then visible the next morning without anybody doing extra work.
///
/// Issuing takes the material out of the store in the same transaction as the ticket,
/// so stock is true while the shift is still running — not corrected afterwards.
/// </summary>
public class MaterialIssueService(
    ColorsDbContext db,
    StockLedger ledger,
    TimeProvider timeProvider) : IMaterialIssueService
{
    public async Task<IReadOnlyList<IssueTicketSummaryDto>> GetAllAsync(
        int? shiftReportId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        var tickets = await Query()
            .Where(t => shiftReportId == null || t.ShiftLine.ShiftReportId == shiftReportId)
            .Where(t => !openOnly || t.Status == IssueTicketStatus.Open)
            .OrderByDescending(t => t.TicketNumber)
            .Take(200)
            .ToListAsync(cancellationToken);

        var names = await UserNamesAsync(tickets.Select(t => t.IssuedByUserId), cancellationToken);

        return tickets
            .Select(t => new IssueTicketSummaryDto(
                t.Id,
                t.TicketNumber,
                t.ShiftLineId,
                t.ShiftLine.ProductionLine.Name,
                t.ShiftLine.ShiftReport.Shift.Name,
                t.ShiftLine.ShiftReport.ProductionDate,
                t.Status.ToString(),
                t.Status == IssueTicketStatus.Open,
                names.GetValueOrDefault(t.IssuedByUserId, "—"),
                t.Lines.Count,
                t.Lines.Sum(l => l.IssuedQuantity),
                t.Lines.Sum(l => l.ReturnedQuantity),
                t.CreatedAt,
                t.ClosedAt))
            .ToList();
    }

    public async Task<Result<IssueTicketDto>> GetAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var ticket = await Query().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return ticket is null
            ? NotFound()
            : Result<IssueTicketDto>.Success(await ToDtoAsync(ticket, cancellationToken));
    }

    public async Task<Result<IssueTicketDto>> CreateAsync(
        CreateIssueTicketRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (request.Lines.Count == 0)
        {
            return Invalid("A ticket needs at least one material on it.");
        }

        if (request.Lines.Select(l => l.MaterialId).Distinct().Count() != request.Lines.Count)
        {
            return Invalid("Each material may appear once on a ticket. Add the quantities together.");
        }

        if (request.Lines.Any(l => l.Quantity <= 0))
        {
            return Invalid("Every line needs a weight.");
        }

        var shiftLine = await db.ShiftLines
            .Include(l => l.ProductionLine)
            .Include(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .FirstOrDefaultAsync(l => l.Id == request.ShiftLineId, cancellationToken);

        if (shiftLine is null)
        {
            return Invalid("Choose a line of an open shift.");
        }

        // Raw material goes to the line that mixes it. Which lines those are is a tick
        // box in Master Data, not a rule about a line's name (specification section 4).
        if (!shiftLine.ProductionLine.TakesRawMaterial)
        {
            return Invalid(
                $"{shiftLine.ProductionLine.Name} does not take raw material. "
                + "Choose the extruder line.");
        }

        // Material issued to a shift that is already finished could never be returned
        // against it, and its waste figure would belong to nothing.
        if (!ShiftWork.AcceptsWork(shiftLine.ShiftReport.Status))
        {
            return Invalid(ShiftWork.RefusalFor(shiftLine.ShiftReport));
        }

        var materialIds = request.Lines.Select(l => l.MaterialId).ToList();
        var materials = await db.Materials
            .Include(m => m.Category)
            .Where(m => materialIds.Contains(m.Id) && m.IsActive)
            .ToListAsync(cancellationToken);

        if (materials.Count != materialIds.Count)
        {
            return Invalid("Every line must name an active material.");
        }

        // Raw material only. Packaging goes straight to the bench, nothing comes back
        // from it, and the system already counts it from what was produced — putting
        // it on a ticket would count it twice and ask somebody to weigh pieces.
        var notIssuable = materials.Where(m => !m.Category.IssuedOnTickets).ToList();

        if (notIssuable.Count > 0)
        {
            var names = string.Join(", ", notIssuable.Select(m => m.Name));
            return Invalid(
                $"{names} cannot go out on a ticket — only raw material does. "
                + "Packaging is counted at the end of the shift, from what was produced.");
        }

        // One transaction around the whole ticket. Half a ticket issued — three
        // materials out of six, because the fourth was short — would leave the store
        // wrong and the worker holding material nothing accounts for.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var ticket = new MaterialIssueTicket
        {
            TicketNumber = await NextTicketNumberAsync(cancellationToken),
            ShiftLineId = shiftLine.Id,
            IssuedByUserId = userId,
            Status = IssueTicketStatus.Open,
            CreatedAt = timeProvider.GetUtcNow(),
            Notes = Trimmed(request.Notes),
            Lines = request.Lines
                .Select(l => new MaterialIssueTicketLine
                {
                    MaterialId = l.MaterialId,
                    IssuedQuantity = l.Quantity,
                    ReturnedQuantity = 0m,
                })
                .ToList(),
        };

        db.MaterialIssueTickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var line in request.Lines)
        {
            var material = materials.First(m => m.Id == line.MaterialId);

            var posted = await ledger.PostAsync(
                line.MaterialId,
                MovementTypeNames.Issue,
                line.Quantity,
                userId,
                $"Ticket {ticket.TicketNumber} — issued to {shiftLine.ProductionLine.Name}",
                ticket.Id,
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

        return await LoadAsync(ticket.Id, cancellationToken);
    }

    public async Task<Result<IssueTicketDto>> RecordReturnsAsync(
        int id,
        RecordReturnsRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await Query().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        if (ticket.Status == IssueTicketStatus.Closed)
        {
            return Invalid("This ticket is closed. Its figures cannot change.");
        }

        if (request.Lines.Count == 0)
        {
            return Invalid("Say what came back.");
        }

        if (request.Lines.Select(l => l.MaterialId).Distinct().Count() != request.Lines.Count)
        {
            return Invalid("Each material may appear once. Add the quantities together.");
        }

        foreach (var back in request.Lines)
        {
            var line = ticket.Lines.FirstOrDefault(l => l.MaterialId == back.MaterialId);

            if (line is null)
            {
                return Invalid("Something came back that was never issued on this ticket.");
            }

            if (back.Quantity <= 0)
            {
                return Invalid("A return must be more than nothing. Leave a material out if none came back.");
            }

            // Returns are cumulative — leftover comes back in more than one trip — so
            // the check is against everything already returned, not this weighing alone.
            if (line.ReturnedQuantity + back.Quantity > line.IssuedQuantity)
            {
                return Invalid(
                    $"{line.Material.Name}: {line.IssuedQuantity:0.###} went out and "
                    + $"{line.ReturnedQuantity:0.###} is already back. More cannot return than left.");
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        foreach (var back in request.Lines)
        {
            var line = ticket.Lines.First(l => l.MaterialId == back.MaterialId);
            line.ReturnedQuantity += back.Quantity;

            var posted = await ledger.PostAsync(
                back.MaterialId,
                MovementTypeNames.Return,
                back.Quantity,
                userId,
                $"Ticket {ticket.TicketNumber} — leftover weighed back in",
                ticket.Id,
                ticket.ShiftLine.ShiftReportId,
                cancellationToken);

            if (!posted.IsSuccess)
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                return Invalid(posted.Message ?? "The leftover could not be returned.");
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await LoadAsync(id, cancellationToken);
    }

    public async Task<Result<IssueTicketDto>> CloseAsync(
        int id,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await Query().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        if (ticket.Status == IssueTicketStatus.Closed)
        {
            return Invalid("This ticket is already closed.");
        }

        // Nothing is required to come back. A ticket that used every kilogram is
        // ordinary, and forcing a zero return would only teach people to type zeroes.
        ticket.Status = IssueTicketStatus.Closed;
        ticket.ClosedAt = timeProvider.GetUtcNow();
        ticket.ClosedByUserId = userId;

        await db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(id, cancellationToken);
    }

    // ---------- helpers ----------

    private IQueryable<MaterialIssueTicket> Query() =>
        db.MaterialIssueTickets
            .Include(t => t.ShiftLine).ThenInclude(l => l.ProductionLine)
            .Include(t => t.ShiftLine).ThenInclude(l => l.ShiftReport).ThenInclude(r => r.Shift)
            .Include(t => t.Lines).ThenInclude(l => l.Material).ThenInclude(m => m.BaseUnit);

    /// <summary>
    /// A sequence, for the same reason recipe numbers use one: a ticket the worker
    /// carries on paper must have a number nobody else was given, and a ticket
    /// abandoned mid-creation must not free its number for a different one.
    /// </summary>
    private async Task<int> NextTicketNumberAsync(CancellationToken cancellationToken)
    {
        var next = await db.Database
            .SqlQuery<int>($"SELECT nextval({ColorsDbContext.IssueTicketNumberSequence})::int AS \"Value\"")
            .ToListAsync(cancellationToken);

        return next[0];
    }

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

    private async Task<Result<IssueTicketDto>> LoadAsync(int id, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var saved = await Query().FirstAsync(t => t.Id == id, cancellationToken);
        return Result<IssueTicketDto>.Success(await ToDtoAsync(saved, cancellationToken));
    }

    private async Task<IssueTicketDto> ToDtoAsync(
        MaterialIssueTicket ticket,
        CancellationToken cancellationToken)
    {
        var ids = new List<int> { ticket.IssuedByUserId };
        if (ticket.ClosedByUserId is not null)
        {
            ids.Add(ticket.ClosedByUserId.Value);
        }

        var names = await UserNamesAsync(ids, cancellationToken);

        return new IssueTicketDto(
            ticket.Id,
            ticket.TicketNumber,
            ticket.ShiftLineId,
            ticket.ShiftLine.ProductionLine.Name,
            ticket.ShiftLine.ShiftReport.Shift.Name,
            ticket.ShiftLine.ShiftReport.ProductionDate,
            ticket.Status.ToString(),
            ticket.Status == IssueTicketStatus.Open,
            names.GetValueOrDefault(ticket.IssuedByUserId, "—"),
            ticket.ClosedByUserId is null ? null : names.GetValueOrDefault(ticket.ClosedByUserId.Value),
            ticket.CreatedAt,
            ticket.ClosedAt,
            ticket.Notes,
            ticket.Lines
                .OrderBy(l => l.Material.Code)
                .Select(l => new IssueTicketLineDto(
                    l.Id,
                    l.MaterialId,
                    l.Material.Code,
                    l.Material.Name,
                    l.Material.BaseUnit.Symbol,
                    l.IssuedQuantity,
                    l.ReturnedQuantity,
                    l.NetUsed))
                .ToList());
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<IssueTicketDto> NotFound() =>
        Result<IssueTicketDto>.Failure(ErrorCode.NotFound, "This ticket does not exist.");

    private static Result<IssueTicketDto> Invalid(string message) =>
        Result<IssueTicketDto>.Failure(ErrorCode.ValidationFailed, message);
}
