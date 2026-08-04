using System.Reflection;
using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Recipes;
using Colors.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Persistence;

/// <summary>
/// The single database context for the whole system.
///
/// Inherits the Identity tables (users, roles, the join between them, claims, logins,
/// tokens) and will gain the factory tables as each phase is built.
///
/// Entity configuration lives in <c>Persistence/Configurations</c>, one file per entity,
/// rather than in a single growing <see cref="OnModelCreating"/>.
/// </summary>
public class ColorsDbContext(DbContextOptions<ColorsDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, int>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Master data — specification section 4.
    public DbSet<ProductionLine> ProductionLines => Set<ProductionLine>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<MaterialCategory> MaterialCategories => Set<MaterialCategory>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<MaterialPackaging> MaterialPackagings => Set<MaterialPackaging>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<PlateSize> PlateSizes => Set<PlateSize>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();

    // Recipes — specification section 5.
    public DbSet<RecipeFamily> RecipeFamilies => Set<RecipeFamily>();
    public DbSet<RecipeVersion> RecipeVersions => Set<RecipeVersion>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    /// <summary>
    /// Hands out the recipe numbers the factory says out loud — "recipe 8".
    ///
    /// A sequence rather than MAX + 1, for two reasons: a discarded draft must not
    /// free its number for a different formula, and two supervisors writing recipes
    /// at the same moment must not be handed the same number.
    /// </summary>
    public const string RecipeNumberSequence = "recipe_number_seq";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasSequence<int>(RecipeNumberSequence).StartsAt(1).IncrementsBy(1);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
