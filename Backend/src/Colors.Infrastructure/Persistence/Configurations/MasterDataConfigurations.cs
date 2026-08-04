using Colors.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Colors.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table shapes for the master data, specification section 4.
///
/// Every name is unique within its table, enforced by the database — application
/// checks alone lose the race when two administrators save at once. Deletes are
/// restricted everywhere: master data is deactivated, never removed, so history
/// keeps resolving.
/// </summary>
public class ProductionLineConfiguration : IEntityTypeConfiguration<ProductionLine>
{
    public void Configure(EntityTypeBuilder<ProductionLine> builder)
    {
        builder.ToTable("ProductionLines");
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ux_production_lines_name");
    }
}

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts");
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ux_shifts_name");
    }
}

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units");
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Symbol).IsRequired().HasMaxLength(10);
        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ux_units_name");
    }
}

public class MaterialCategoryConfiguration : IEntityTypeConfiguration<MaterialCategory>
{
    public void Configure(EntityTypeBuilder<MaterialCategory> builder)
    {
        builder.ToTable("MaterialCategories");
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ux_material_categories_name");
    }
}

public class ColorConfiguration : IEntityTypeConfiguration<Color>
{
    public void Configure(EntityTypeBuilder<Color> builder)
    {
        builder.ToTable("Colors");
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        // One capital letter for the roll code: W, G, Y, B.
        builder.Property(e => e.Code).IsRequired().HasMaxLength(1);
        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ux_colors_name");
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("ux_colors_code");
    }
}

public class PlateSizeConfiguration : IEntityTypeConfiguration<PlateSize>
{
    public void Configure(EntityTypeBuilder<PlateSize> builder)
    {
        builder.ToTable("PlateSizes");
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ux_plate_sizes_name");
    }
}

public class ProductTypeConfiguration : IEntityTypeConfiguration<ProductType>
{
    public void Configure(EntityTypeBuilder<ProductType> builder)
    {
        builder.ToTable("ProductTypes");
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ux_product_types_name");
    }
}

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("Materials");

        builder.Property(e => e.Code).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Notes).HasMaxLength(500);

        // Four decimals: a small bag weighs 0.0475 kg, and three would round it wrong.
        builder.Property(e => e.MinQuantity).HasPrecision(18, 4);
        builder.Property(e => e.UnitWeight).HasPrecision(18, 4);

        // The code is the identity the system relies on, never the name.
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("ux_materials_code");

        builder.HasOne(e => e.Category)
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.BaseUnit)
            .WithMany()
            .HasForeignKey(e => e.BaseUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaterialPackagingConfiguration : IEntityTypeConfiguration<MaterialPackaging>
{
    public void Configure(EntityTypeBuilder<MaterialPackaging> builder)
    {
        builder.ToTable("MaterialPackagings");

        builder.Property(e => e.QuantityInBaseUnit).HasPrecision(18, 4);

        // One row per pack unit per material — "bag of GPPS" cannot be defined twice.
        builder.HasIndex(e => new { e.MaterialId, e.UnitId })
            .IsUnique()
            .HasDatabaseName("ux_material_packagings_material_unit");

        // Pack sizes belong to their material and die with it. Materials themselves
        // are never deleted, so this cascade exists only for completeness.
        builder.HasOne<Material>()
            .WithMany(m => m.Packagings)
            .HasForeignKey(e => e.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Unit)
            .WithMany()
            .HasForeignKey(e => e.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
