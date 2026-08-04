using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Recipes;

/// <summary>
/// Hands out the next recipe number from the database sequence.
///
/// Not MAX + 1: discarding a draft would free its number, and a different formula
/// would later become "recipe 6" — while somebody on the floor still remembers the
/// first one. A sequence only ever moves forward, and two people creating recipes
/// at the same instant get different numbers.
/// </summary>
public static class RecipeNumbers
{
    public static async Task<int> NextAsync(ColorsDbContext db, CancellationToken cancellationToken)
    {
        var next = await db.Database
            .SqlQuery<int>($"SELECT nextval('recipe_number_seq')::int AS \"Value\"")
            .ToListAsync(cancellationToken);

        return next[0];
    }
}
