using System.Reflection;
using Colors.Domain.Entities.Barcodes;
using Colors.Domain.Entities.Inventory;
using Colors.Domain.Entities.MasterData;
using Colors.Domain.Entities.Recipes;
using Colors.Domain.Entities.Shifts;
using Colors.Domain.Enums;
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
    public DbSet<Mould> Moulds => Set<Mould>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<MovementType> MovementTypes => Set<MovementType>();

    // Inventory — specification section 6.
    public DbSet<MaterialInventory> MaterialInventory => Set<MaterialInventory>();
    public DbSet<MaterialInventoryMovement> MaterialInventoryMovements =>
        Set<MaterialInventoryMovement>();

    // Material issue and return — specification section 7.
    public DbSet<MaterialIssueTicket> MaterialIssueTickets => Set<MaterialIssueTicket>();
    public DbSet<MaterialIssueTicketLine> MaterialIssueTicketLines =>
        Set<MaterialIssueTicketLine>();

    // Barcodes — specification section 12. One table for rolls, bags and pallets.
    public DbSet<Barcode> Barcodes => Set<Barcode>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();

    // Recipes — specification section 5.
    public DbSet<RecipeFamily> RecipeFamilies => Set<RecipeFamily>();
    public DbSet<RecipeVersion> RecipeVersions => Set<RecipeVersion>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    // Shift reports — specification section 2.
    public DbSet<ShiftReport> ShiftReports => Set<ShiftReport>();
    public DbSet<ShiftLine> ShiftLines => Set<ShiftLine>();
    public DbSet<ShiftWorker> ShiftWorkers => Set<ShiftWorker>();

    /// <summary>
    /// Hands out the recipe numbers the factory says out loud — "recipe 8".
    ///
    /// A sequence rather than MAX + 1, for two reasons: a discarded draft must not
    /// free its number for a different formula, and two supervisors writing recipes
    /// at the same moment must not be handed the same number.
    /// </summary>
    public const string RecipeNumberSequence = "recipe_number_seq";

    /// <summary>
    /// One sequence per barcode type, for the same reasons: two tablets printing at
    /// the same moment must not be handed the same value, and a scrapped roll must
    /// never free its code for a different one (specification section 12).
    /// </summary>
    /// <summary>
    /// The number on the paper ticket the worker carries. A sequence so two tickets
    /// can never share one, and an abandoned ticket never frees its number.
    /// </summary>
    public const string IssueTicketNumberSequence = "issue_ticket_number_seq";

    public const string RollBarcodeSequence = "roll_barcode_seq";
    public const string BagBarcodeSequence = "bag_barcode_seq";
    public const string PalletBarcodeSequence = "pallet_barcode_seq";

    public static string BarcodeSequenceFor(BarcodeObjectType objectType) => objectType switch
    {
        BarcodeObjectType.Roll => RollBarcodeSequence,
        BarcodeObjectType.Bag => BagBarcodeSequence,
        BarcodeObjectType.Pallet => PalletBarcodeSequence,
        _ => throw new ArgumentOutOfRangeException(nameof(objectType), objectType, "No barcode sequence for this type."),
    };

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasSequence<int>(RecipeNumberSequence).StartsAt(1).IncrementsBy(1);
        builder.HasSequence<int>(IssueTicketNumberSequence).StartsAt(1).IncrementsBy(1);

        foreach (var sequence in new[]
                 {
                     RollBarcodeSequence,
                     BagBarcodeSequence,
                     PalletBarcodeSequence,
                 })
        {
            builder.HasSequence<long>(sequence).StartsAt(1).IncrementsBy(1);
        }

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
