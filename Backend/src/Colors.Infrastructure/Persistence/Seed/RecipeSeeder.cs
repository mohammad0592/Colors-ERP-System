using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Services.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Colors.Infrastructure.Persistence.Seed;

/// <summary>
/// The factory's four recipes, exactly as the owner wrote them down
/// (specification section 5), each seeded as version 1 and put into production.
///
/// The percentages are parts per hundred resin: GPPS and Recycle total 100, and the
/// additives are measured against that — which is why the numbers do not sum to 100.
///
/// Only added when a family is missing, so a supervisor who has since created new
/// versions is never overwritten.
/// </summary>
public static class RecipeSeeder
{
    private sealed record Ingredient(string MaterialName, bool IsBaseResin, decimal Target, decimal Min, decimal Max);

    private sealed record Family(
        string Name,
        bool UsesRecycle,
        bool IsAbsorbent,
        string Description,
        Ingredient[] Ingredients);

    private static readonly Family[] Families =
    [
        new(
            "Normal (Except Black)",
            UsesRecycle: false,
            IsAbsorbent: false,
            "Plain plates in any colour but black.",
            [
                new("GPPS", true, 100m, 100m, 100m),
                new("Talc", false, 1m, 1m, 1m),
                new("Nucleating Agent", false, 1.8m, 1.5m, 2m),
                new("Coloring Agent", false, 1.6m, 1.5m, 2m),
            ]),
        new(
            "Normal Black",
            UsesRecycle: true,
            IsAbsorbent: false,
            "Black plates. A third of the polymer is the factory's own recycled material.",
            [
                new("GPPS", true, 65m, 65m, 65m),
                new("Recycled Material", true, 35m, 35m, 35m),
                new("Talc", false, 1m, 1m, 1m),
                new("Nucleating Agent", false, 1.8m, 1.5m, 2m),
                new("Black Coloring Agent", false, 2.2m, 2m, 2.5m),
            ]),
        new(
            "ABS (Except Black)",
            UsesRecycle: false,
            IsAbsorbent: true,
            "Absorbent plates in any colour but black.",
            [
                new("GPPS", true, 100m, 100m, 100m),
                new("Absorbent Agent", false, 3.5m, 3m, 4m),
                new("Coloring Agent", false, 1.6m, 1.5m, 2m),
                new("Antistatic Agent", false, 2m, 1.5m, 3m),
                new("Talc", false, 1m, 1m, 1m),
            ]),
        new(
            "ABS Black",
            UsesRecycle: true,
            IsAbsorbent: true,
            "Absorbent black plates, with recycled material in the polymer.",
            [
                new("GPPS", true, 65m, 65m, 65m),
                new("Recycled Material", true, 35m, 35m, 35m),
                new("Absorbent Agent", false, 3.5m, 3m, 4m),
                new("Coloring Agent", false, 1.6m, 1.5m, 2m),
                new("Antistatic Agent", false, 2m, 1.5m, 3m),
                new("Talc", false, 1m, 1m, 1m),
            ]),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<ColorsDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(RecipeSeeder));

        var productType = await db.ProductTypes.FirstOrDefaultAsync(p => p.Name == "Plate", cancellationToken);
        if (productType is null)
        {
            logger.LogWarning("No 'Plate' product type, so recipes were not seeded.");
            return;
        }

        // Recipes are written by a person, and the audit trail should say who. Before
        // anyone has been hired, that is the seeded administrator.
        var author = await db.Set<ApplicationUser>()
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (author is null)
        {
            logger.LogWarning("No users exist yet, so recipes were not seeded.");
            return;
        }

        var materials = await db.Materials.ToDictionaryAsync(m => m.Name, m => m.Id, cancellationToken);
        var created = 0;

        foreach (var family in Families)
        {
            if (await db.RecipeFamilies.AnyAsync(f => f.Name == family.Name, cancellationToken))
            {
                continue;
            }

            var missing = family.Ingredients
                .Where(i => !materials.ContainsKey(i.MaterialName))
                .Select(i => i.MaterialName)
                .ToList();

            if (missing.Count > 0)
            {
                logger.LogWarning(
                    "Skipped recipe family {Family}: missing material(s) {Missing}.",
                    family.Name,
                    string.Join(", ", missing));
                continue;
            }

            // Drawn from the same sequence the service uses, so seeded recipes and
            // ones written later share one run of numbers.
            db.RecipeFamilies.Add(new RecipeFamily
            {
                Name = family.Name,
                ProductTypeId = productType.Id,
                UsesRecycle = family.UsesRecycle,
                IsAbsorbent = family.IsAbsorbent,
                Description = family.Description,
                Versions =
                [
                    new RecipeVersion
                    {
                        RecipeNumber = await RecipeNumbers.NextAsync(db, cancellationToken),
                        VersionNumber = 1,
                        Status = RecipeVersionStatus.Current,
                        CreatedByUserId = author.Id,
                        CreatedAt = DateTimeOffset.UtcNow,
                        Notes = "The recipe as the factory recorded it.",
                        Ingredients = family.Ingredients
                            .Select(i => new RecipeIngredient
                            {
                                MaterialId = materials[i.MaterialName],
                                IsBaseResin = i.IsBaseResin,
                                TargetPercentage = i.Target,
                                MinPercentage = i.Min,
                                MaxPercentage = i.Max,
                            })
                            .ToList(),
                    },
                ],
            });

            created++;
        }

        if (created > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} recipe families with their first version.", created);
        }
    }
}
