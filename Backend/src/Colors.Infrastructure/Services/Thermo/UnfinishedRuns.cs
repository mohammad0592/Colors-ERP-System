using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Thermo;

/// <summary>
/// What a roll's earlier, unfinished runs made (specification section 19.23).
///
/// A roll taken out before it was finished goes back to stock and into the machine again
/// later, so one roll can have several runs. Its weight is counted once — on the run that
/// finishes it — and that run's scrap is the roll's weight less the plates of <i>every</i>
/// run, so the plates made before are needed there.
/// </summary>
public static class UnfinishedRuns
{
    /// <summary>The earlier runs of one roll, taken together.</summary>
    /// <param name="Runs">How many times it was taken out unfinished.</param>
    /// <param name="Pieces">The plates those runs made.</param>
    /// <param name="PlatesKg">What those plates weigh. Null while one of the runs is not
    /// counted yet — the scrap cannot be said until it is.</param>
    public sealed record EarlierWork(int Runs, int Pieces, decimal? PlatesKg);

    /// <summary>
    /// For each roll, what its unfinished runs made. A roll with none is in the
    /// dictionary as no runs and nothing made, so callers need not tell the two apart.
    /// </summary>
    public static async Task<Dictionary<int, EarlierWork>> ForRollsAsync(
        ColorsDbContext db,
        IEnumerable<int> rollIds,
        CancellationToken cancellationToken)
    {
        var wanted = rollIds.Distinct().ToList();
        var result = wanted.ToDictionary(id => id, _ => new EarlierWork(0, 0, 0m));
        if (wanted.Count == 0)
        {
            return result;
        }

        var earlier = await db.ThermoProductions
            .Where(p => wanted.Contains(p.RollId) && p.UnfinishedReason != null)
            .Select(p => new
            {
                p.RollId,
                Pieces = p.TestReport == null ? (int?)null : p.TestReport.PieceCount,
                PieceWeight = p.TestReport == null ? (decimal?)null : p.TestReport.PieceWeight,
            })
            .ToListAsync(cancellationToken);

        foreach (var group in earlier.GroupBy(p => p.RollId))
        {
            var allCounted = group.All(p => p.Pieces is not null);
            result[group.Key] = new EarlierWork(
                group.Count(),
                group.Sum(p => p.Pieces ?? 0),
                allCounted
                    ? Math.Round(group.Sum(p => p.Pieces!.Value * p.PieceWeight!.Value / 1000m), 3)
                    : null);
        }

        return result;
    }
}
