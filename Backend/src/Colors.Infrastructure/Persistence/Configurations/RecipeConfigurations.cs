using Colors.Domain.Entities.Recipes;
using Colors.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Colors.Infrastructure.Persistence.Configurations;

public class RecipeFamilyConfiguration : IEntityTypeConfiguration<RecipeFamily>
{
    public void Configure(EntityTypeBuilder<RecipeFamily> builder)
    {
        builder.ToTable("RecipeFamilies");

        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Description).HasMaxLength(500);

        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ux_recipe_families_name");

        builder.HasOne(e => e.ProductType)
            .WithMany()
            .HasForeignKey(e => e.ProductTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RecipeVersionConfiguration : IEntityTypeConfiguration<RecipeVersion>
{
    public void Configure(EntityTypeBuilder<RecipeVersion> builder)
    {
        builder.ToTable("RecipeVersions");

        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        // The number the factory says out loud. Unique across every family and never
        // reused, so "recipe 8" always means the same formula.
        builder.HasIndex(e => e.RecipeNumber)
            .IsUnique()
            .HasDatabaseName("ux_recipe_versions_number");

        builder.HasIndex(e => new { e.RecipeFamilyId, e.VersionNumber })
            .IsUnique()
            .HasDatabaseName("ux_recipe_versions_family_version");

        // Exactly one Current version per family (specification section 5). A partial
        // index enforces it in the database, so two administrators promoting at the
        // same moment cannot both succeed.
        builder.HasIndex(e => e.RecipeFamilyId)
            .IsUnique()
            .HasFilter("\"Status\" = 'Current'")
            .HasDatabaseName("ux_recipe_versions_one_current_per_family");

        builder.HasOne(e => e.Family)
            .WithMany(f => f.Versions)
            .HasForeignKey(e => e.RecipeFamilyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("RecipeIngredients");

        // Percentages carry two decimals — the factory writes 1.5, 2.25, never finer.
        builder.Property(e => e.TargetPercentage).HasPrecision(9, 2);
        builder.Property(e => e.MinPercentage).HasPrecision(9, 2);
        builder.Property(e => e.MaxPercentage).HasPrecision(9, 2);

        // A material may appear once per version — two GPPS rows would be ambiguous.
        builder.HasIndex(e => new { e.RecipeVersionId, e.MaterialId })
            .IsUnique()
            .HasDatabaseName("ux_recipe_ingredients_version_material");

        // Ingredients belong to their version and have no meaning without it. Only
        // drafts are ever deleted, so this cascade never touches production history.
        builder.HasOne<RecipeVersion>()
            .WithMany(v => v.Ingredients)
            .HasForeignKey(e => e.RecipeVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Material)
            .WithMany()
            .HasForeignKey(e => e.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
