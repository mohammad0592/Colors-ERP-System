using Colors.Application.Features.Audit;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Audit;

/// <summary>
/// Reading the audit log (specification section 15).
///
/// Reading only, on purpose. Lines are written by the interceptor and by the refusal
/// path, and nothing anywhere edits or deletes one.
/// </summary>
public class AuditService(ColorsDbContext db) : IAuditService
{
    public async Task<IReadOnlyList<AuditEntryDto>> GetAsync(
        int? shiftReportId = null,
        IReadOnlyList<string>? objectTypes = null,
        bool refusalsOnly = false,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        var wantedTypes = objectTypes?.Where(t => t.Length > 0).ToList();

        var lines = await db.AuditEntries
            .Where(e => shiftReportId == null || e.ShiftReportId == shiftReportId)
            .Where(e => wantedTypes == null || wantedTypes.Count == 0
                        || wantedTypes.Contains(e.ObjectType))
            .Where(e => !refusalsOnly || e.Result == AuditResult.Rejected)
            // Newest first: the question is almost always "what just happened".
            .OrderByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.Id)
            .Take(Math.Clamp(take, 1, 1000))
            .ToListAsync(cancellationToken);

        var names = await NamesAsync(lines.Select(l => l.UserId), cancellationToken);
        var shifts = await ShiftsAsync(lines.Select(l => l.ShiftReportId), cancellationToken);

        return lines.Select(l => new AuditEntryDto(
            l.Id,
            l.UserId,
            l.UserId is null ? null : names.GetValueOrDefault(l.UserId.Value),
            l.ShiftReportId,
            l.ShiftReportId is null ? null : shifts.GetValueOrDefault(l.ShiftReportId.Value),
            l.Action,
            l.ObjectType,
            l.ObjectId,
            l.Result.ToString(),
            l.Details,
            l.Timestamp)).ToList();
    }

    private async Task<Dictionary<int, string>> NamesAsync(
        IEnumerable<int?> ids,
        CancellationToken cancellationToken)
    {
        var wanted = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        // Read from the table rather than through Identity: a line about somebody who
        // has since left must still say who it was.
        return await db.Set<ApplicationUser>()
            .Where(u => wanted.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }

    private async Task<Dictionary<int, string>> ShiftsAsync(
        IEnumerable<int?> ids,
        CancellationToken cancellationToken)
    {
        var wanted = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        // The parts come back raw and are joined here. Letting the database turn the date
        // into text gives whatever format it prefers — which came out as 08/07/2026 for
        // the seventh of August, the one order this factory never uses.
        var shifts = await db.ShiftReports
            .Where(r => wanted.Contains(r.Id))
            .Select(r => new { r.Id, r.Shift.Name, r.ProductionDate })
            .ToListAsync(cancellationToken);

        return shifts.ToDictionary(
            r => r.Id,
            r => $"{r.Name} {r.ProductionDate:dd/MM/yyyy}");
    }
}
