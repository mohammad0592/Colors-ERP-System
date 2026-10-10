using Colors.Domain.Entities.Recipes;
using Colors.Domain.Enums;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Services.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Colors.Infrastructure.Persistence.Seed;

/// <summary>
/// The factory's three main recipes (specification section 5), each seeded as version 1
/// and put into production, and the products each one makes.
///
/// The percentages are parts per hundred resin: GPPS is the 100, and the additives are
/// measured against it — which is why the numbers do not sum to 100. The factory gave the
/// materials; the figures are the ones it used before, to be corrected by writing a new
/// version.
///
/// Only added when a family is missing, so a supervisor who has since created new
/// versions is never overwritten.
/// </summary>
public static class RecipeSeeder
{
    private sealed record Ingredient(string MaterialName, bool IsBaseResin, decimal Percentage);

    private sealed record Family(
        string Name,
        // The family's part of a roll code (specification section 8).
        string Code,
        bool IsAbsorbent,
        string Description,
        Ingredient[] Ingredients);

    private const string NormalCode = "N";
    private const string AbsorbentCode = "Abs";
    private const string LunchBoxCode = "LN";

    private static readonly Family[] Families =
    [
        new(
            "Normal",
            NormalCode,
            IsAbsorbent: false,
            "Rolls for the normal big and small plates.",
            [
                new("GPPS", true, 100m),
                new("Talc", false, 1m),
                new("Coloring Agent", false, 1.6m),
                new("Nucleating Agent", false, 1.8m),
            ]),
        new(
            "Absorbent",
            AbsorbentCode,
            IsAbsorbent: true,
            "Rolls for the absorbent big and small plates.",
            [
                new("GPPS", true, 100m),
                new("Talc", false, 1m),
                new("Coloring Agent", false, 1.6m),
                new("Nucleating Agent", false, 1.8m),
                new("Absorbent Agent", false, 3.5m),
            ]),
        new(
            "Lunch Box",
            LunchBoxCode,
            IsAbsorbent: false,
            "Rolls for the lunch boxes and burger boxes. The same materials as Normal.",
            [
                new("GPPS", true, 100m),
                new("Talc", false, 1m),
                new("Coloring Agent", false, 1.6m),
                new("Nucleating Agent", false, 1.8m),
            ]),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<ColorsDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(RecipeSeeder));

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
                Code = family.Code,
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
                                TargetPercentage = i.Percentage,
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

        await LinkProductsAsync(db, logger, cancellationToken);
    }

    /// <summary>
    /// Gives every product without a main recipe the one it is made from: absorbent
    /// plates from Absorbent, the other plates from Normal, and the boxes from Lunch Box.
    ///
    /// Only for products that predate the link — the seeded ones, and any written in
    /// Master Data before it asked. Anything saved since has chosen its own, and is left
    /// alone. Reading the product type's name is acceptable here because it runs once,
    /// on rows that were seeded with exactly these names; nothing after it does.
    /// </summary>
    private static async Task LinkProductsAsync(
        ColorsDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var unlinked = await db.Products
            .Include(p => p.ProductType)
            .Where(p => p.RecipeFamilyId == null)
            .ToListAsync(cancellationToken);

        if (unlinked.Count == 0)
        {
            return;
        }

        var byCode = await db.RecipeFamilies
            .Where(f => f.IsActive)
            .ToListAsync(cancellationToken);

        int? IdOf(string code) =>
            byCode.FirstOrDefault(f => string.Equals(f.Code, code, StringComparison.OrdinalIgnoreCase))?.Id;

        var linked = 0;

        foreach (var product in unlinked)
        {
            var familyId = product.IsAbsorbent
                ? IdOf(AbsorbentCode)
                : product.ProductType.Name == "Plate"
                    ? IdOf(NormalCode)
                    : IdOf(LunchBoxCode);

            if (familyId is null)
            {
                continue;
            }

            product.RecipeFamilyId = familyId;
            linked++;
        }

        if (linked > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Linked {Count} products to the main recipe they are made from.", linked);
        }
    }
}
