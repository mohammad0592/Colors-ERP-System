using Colors.Application.Common.Auditing;
using Colors.Domain.Entities.Inventory;
using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Packaging;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Entities.Shifts;
using Colors.Domain.Entities.System;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Colors.Infrastructure.Persistence.Auditing;

/// <summary>
/// Writes the audit log for everything that <b>succeeds</b> (specification section 15).
///
/// It sits on <c>SaveChanges</c> rather than in the services, so no business code has to
/// remember to call it and none of it can forget. A refusal never reaches
/// <c>SaveChanges</c> at all — those are written from the API layer instead, which is the
/// only other place every one of them passes through.
///
/// <b>What is audited, and what is not.</b> Most of the factory's work already names its
/// author: a roll knows who made it, a ticket knows who issued it. Copying that here
/// would double the record and bury the lines that matter under a thousand that do not.
/// So this audits the things that leave no other trace:
///
/// <list type="bullet">
/// <item><b>Master data and recipes</b> — every change, because they silently change what
/// every past and future screen means.</item>
/// <item><b>People and their roles</b> — who may do what.</item>
/// <item><b>Corrections</b> — a bag taken off a pallet, a pallet cancelled, a shift
/// reopened, a ticket closed. For these the creation is routine and already recorded, so
/// only the <i>change</i> is audited.</item>
/// </list>
/// </summary>
public class AuditInterceptor(ICurrentActor actor, ICurrentEntry entry) : SaveChangesInterceptor
{
    /// <summary>
    /// Lines written for rows that had no key yet, paired with the row itself so the key
    /// can be filled in once the database has given one.
    ///
    /// Instance state, which is safe because this is registered per request alongside the
    /// context it listens to, and one context never saves twice at the same moment.
    /// </summary>
    private readonly List<(EntityEntry Entry, AuditEntry Line)> _awaitingKey = [];

    /// <summary>
    /// Any change to these is worth a line. They are the rows that decide what every
    /// other screen shows, and nothing else records that they were edited.
    /// </summary>
    private static readonly HashSet<Type> Always =
    [
        typeof(Material), typeof(MaterialCategory), typeof(MaterialPackaging),
        typeof(Product), typeof(ProductType), typeof(Mould), typeof(Color),
        typeof(Unit), typeof(ProductionLine), typeof(Shift), typeof(MovementType),
        typeof(RecipeFamily), typeof(RecipeVersion), typeof(RecipeIngredient),
        typeof(ApplicationUser), typeof(ApplicationRole),
    ];

    /// <summary>
    /// Creating one of these is routine production and the row already names its author.
    /// Changing one afterwards is a decision or a correction, and that is the line a
    /// supervisor wants.
    /// </summary>
    private static readonly HashSet<Type> OnChangeOnly =
    [
        typeof(ShiftReport), typeof(BagPalletAssignment), typeof(WoodenPallet),
        typeof(MaterialIssueTicket),
    ];

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not ColorsDbContext db)
        {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var worth = db.ChangeTracker.Entries()
            .Where(IsWorthWriting)
            .ToList();

        if (worth.Count == 0)
        {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        // Looked up through the context being saved, not asked of the actor: the
        // context's own options resolve this interceptor, so anything it depends on must
        // never need the context back.
        //
        // One shift is open at a time, which is a rule the database enforces
        // (specification section 2).
        var shiftReportId = await db.ShiftReports
            .Where(r => r.Status == ShiftReportStatus.Open)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        _awaitingKey.Clear();

        foreach (var entry in worth)
        {
            // Read before saving: afterwards an Added row has moved to Unchanged and the
            // original values that make a "changed from x to y" line are gone.
            var line = new AuditEntry
            {
                UserId = actor.UserId,
                ShiftReportId = shiftReportId,
                Action = entry.State.ToString(),
                ObjectType = entry.Metadata.ClrType.Name,
                ObjectId = KeyOf(entry),
                Result = AuditResult.Success,
                Details = WithEntryMethod(Describe(entry)),
                Timestamp = now,
            };

            db.Set<AuditEntry>().Add(line);

            if (line.ObjectId is null && entry.State == EntityState.Added)
            {
                _awaitingKey.Add((entry, line));
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Fills in the keys the database has just handed out.
    ///
    /// A new row has no id until it is saved, so its line goes in without one and is
    /// completed here. Without this a created recipe or material would be audited as
    /// "something was added" with no way to say which.
    /// </summary>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (_awaitingKey.Count == 0 || eventData.Context is not ColorsDbContext db)
        {
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        var filled = false;

        foreach (var (entry, line) in _awaitingKey)
        {
            var id = KeyOf(entry);
            if (id is not null)
            {
                line.ObjectId = id;
                filled = true;
            }
        }

        _awaitingKey.Clear();

        if (filled)
        {
            // Audit rows are skipped by this interceptor, so this save writes the keys
            // and stops — it cannot start again.
            await db.SaveChangesAsync(cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private static bool IsWorthWriting(EntityEntry entry)
    {
        if (entry.Entity is AuditEntry)
        {
            // Auditing the audit log would never stop.
            return false;
        }

        if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            return false;
        }

        var type = entry.Metadata.ClrType;

        if (Always.Contains(type))
        {
            return true;
        }

        return OnChangeOnly.Contains(type) && entry.State != EntityState.Added;
    }

    private static int? KeyOf(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();
        if (key is null)
        {
            return null;
        }

        var property = entry.Property(key.Name);

        // A row that has not been saved yet carries a *temporary* key — a large negative
        // number EF invents to track it by. Writing that into the log would look like a
        // real id and point at nothing, so it counts as "not known yet" and is filled in
        // once the database has given a real one.
        if (property.IsTemporary)
        {
            return null;
        }

        return property.CurrentValue is int id && id != 0 ? id : null;
    }

    /// <summary>
    /// What actually changed, in the shortest form that still answers "what did he do".
    ///
    /// Only the properties that moved, and only for a change — listing every column of a
    /// new row would be noise, and the row itself is right there to read.
    /// </summary>
    /// <summary>
    /// Adds how the code arrived, where the request said (specification section 12).
    ///
    /// Appended to the description rather than given a column of its own: it is true of
    /// the <i>request</i>, not of the row, and most rows are saved by a screen with no
    /// code on it at all. A column would be null on nearly every line.
    /// </summary>
    private string? WithEntryMethod(string? described)
    {
        var method = entry.Method;
        if (method == EntryMethod.Unknown)
        {
            return described;
        }

        return described is null ? $"{method}" : $"{described} [{method}]";
    }

    private static string? Describe(EntityEntry entry)
    {
        if (entry.State != EntityState.Modified)
        {
            return null;
        }

        var changed = entry.Properties
            .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
            // A password hash is not a thing to write into a log, even changed.
            .Where(p => !p.Metadata.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                        && !p.Metadata.Name.Contains("Stamp", StringComparison.OrdinalIgnoreCase)
                        && !p.Metadata.Name.Contains("Token", StringComparison.OrdinalIgnoreCase))
            .Select(p => $"{p.Metadata.Name}: {p.OriginalValue ?? "—"} → {p.CurrentValue ?? "—"}")
            .ToList();

        return changed.Count == 0 ? null : string.Join("; ", changed);
    }
}
