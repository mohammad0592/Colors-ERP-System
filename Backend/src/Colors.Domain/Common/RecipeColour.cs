using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Recipes;

namespace Colors.Domain.Common;

/// <summary>
/// Whether a recipe and a colour may be used together (specification section 5).
///
/// The four families are named for it: two are Black, two are Except Black. A Black
/// recipe replaces 35% of the GPPS with recycled material, which is dark, so a white
/// roll cannot be made from it — and black goes on that recipe rather than the plain
/// one, because using recycle is the reason the black recipes exist.
/// </summary>
public static class RecipeColour
{
    /// <summary>
    /// One comparison, both directions. A black-only recipe needs the black colour, and
    /// every other recipe refuses it.
    /// </summary>
    public static bool Agree(RecipeFamily family, Color colour) =>
        family.BlackOnly == colour.IsBlack;

    /// <summary>The sentence to show when they do not, saying which way round it is wrong.</summary>
    public static string RefusalFor(RecipeFamily family, Color colour) =>
        family.BlackOnly
            ? $"{family.Name} uses recycled material, which is dark, so it can only be "
              + $"made in black. {colour.Name} needs a recipe that is not black-only."
            : $"{family.Name} cannot be made in {colour.Name}. Black is made on the "
              + "recipe that uses recycle.";
}
