using Colors.Domain.Entities.System;
using Colors.Domain.Enums;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Api.Auditing;

/// <summary>
/// Writes the audit log for everything that is <b>refused</b> (specification section 15).
///
/// Section 15 asks for one <c>SaveChanges</c> interceptor "so nothing is missed", and
/// separately asks that failed actions be logged too. Those two cannot be the same
/// mechanism: a refusal changes no data, so it never reaches <c>SaveChanges</c> at all.
///
/// This is the other half. Every refusal in the system comes back as a failed
/// <c>Result</c> through one method on the controller base, so this is the single place
/// they all pass — the same "nothing is missed" argument, applied to the other path.
///
/// <b>It opens its own scope.</b> The line is written after the man already has his
/// answer, by which time the request's database context may be gone. A fresh one, and
/// the few facts it needs handed to it, keeps the two apart.
///
/// <b>It never throws.</b> Failing to write a log line must not turn a plain refusal the
/// man could have read into a server error he cannot.
/// </summary>
public class RefusalLog(IServiceScopeFactory scopes, ILogger<RefusalLog> logger)
{
    public async Task WriteAsync(int? userId, string action, string objectType, string? message)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ColorsDbContext>();

            // One shift is open at a time, which is a rule the database enforces
            // (specification section 2). It is a fact about the factory, not about this
            // request, so looking it up here is safe.
            var shiftReportId = await db.ShiftReports
                .Where(r => r.Status == ShiftReportStatus.Open)
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync();

            db.AuditEntries.Add(new AuditEntry
            {
                UserId = userId,
                ShiftReportId = shiftReportId,
                Action = Fit(action, 60),
                ObjectType = Fit(objectType, 60),
                ObjectId = null,
                Result = AuditResult.Rejected,
                Details = message is null ? null : Fit(message, 2000),
                Timestamp = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();
        }
        catch (Exception caught)
        {
            // The man on the floor already has his answer. Losing the line is worth
            // knowing about, but not worth turning his refusal into a crash.
            logger.LogError(
                caught, "Could not write the refusal of {Action} to the audit log.", action);
        }
    }

    private static string Fit(string value, int length) =>
        value.Length <= length ? value : value[..length];
}
