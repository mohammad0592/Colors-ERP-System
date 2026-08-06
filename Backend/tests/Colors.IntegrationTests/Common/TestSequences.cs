using Colors.Domain.Entities.MasterData;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Common;

/// <summary>
/// Numbers and letters that must be unique across the whole suite, handed out from one
/// place.
///
/// The tests share one database, so two classes each keeping their own counter will
/// eventually hand out the same value — and the failure lands in whichever test happened
/// to run second, nowhere near the cause. That already happened once with colour codes.
/// </summary>
public static class TestSequences
{
    private static int _letter = -1;
    private static int _recipeNumber = 700_000;

    /// <summary>Recipe numbers are unique for ever, so they are never derived from a hash.</summary>
    public static int NextRecipeNumber() => Interlocked.Increment(ref _recipeNumber);

    private static int _day = -1;

    /// <summary>
    /// A production day of its own for each test factory.
    ///
    /// A roll's serial restarts every day, so two factories sharing a date makes the
    /// second one's first roll number 2 — and a test asserting 1 fails, in a different
    /// test each run. That is what a hashed date did: <c>string.GetHashCode()</c> is
    /// randomised per process, so the collisions moved around and the suite only went
    /// red sometimes.
    /// </summary>
    public static DateOnly NextProductionDate() =>
        new DateOnly(2026, 1, 1).AddDays(Interlocked.Increment(ref _day));

    /// <summary>
    /// A colour, taken from the twenty-six the database can hold.
    ///
    /// A colour code is one capital letter and unique, so twenty-six is the whole
    /// alphabet and the whole supply. That is plenty for a factory with four colours,
    /// but not for a suite that once made a new one in every test — so the tests take
    /// turns and share. Nothing here needs a colour of its own: roll codes are made
    /// unique by their serial and date, not by their letter.
    /// </summary>
    /// <summary>
    /// Kept out of the ordinary rotation and used only for the black colour, so a test
    /// asking for "any colour" can never be handed the one that changes which recipes
    /// are allowed (specification section 5).
    /// </summary>
    private const char BlackCode = 'K';

    /// <summary>
    /// The black colour — the one a Black recipe needs and every other recipe refuses.
    /// Shared, like the rest: a colour code is one letter and unique.
    /// </summary>
    public static async Task<Color> BlackColourAsync(ColorsDbContext db)
    {
        var existing = await db.Colors.FirstOrDefaultAsync(c => c.Code == BlackCode.ToString());
        if (existing is not null)
        {
            return existing;
        }

        var colour = new Color { Name = "Colour Black", Code = BlackCode.ToString(), IsBlack = true };
        db.Colors.Add(colour);
        await db.SaveChangesAsync();

        return colour;
    }

    public static async Task<Color> ColourAsync(ColorsDbContext db)
    {
        // Twenty-five letters, not twenty-six: K belongs to black.
        var letters = "ABCDEFGHIJLMNOPQRSTUVWXYZ";
        var code = letters[Interlocked.Increment(ref _letter) % letters.Length].ToString();

        var existing = await db.Colors.FirstOrDefaultAsync(c => c.Code == code);
        if (existing is not null)
        {
            return existing;
        }

        var colour = new Color { Name = $"Colour {code}", Code = code };
        db.Colors.Add(colour);
        await db.SaveChangesAsync();

        return colour;
    }
}
