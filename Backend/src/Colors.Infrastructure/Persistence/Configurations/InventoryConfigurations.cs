using Colors.Domain.Entities.Inventory;
using Colors.Domain.Entities.MasterData;
using Colors.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Colors.Infrastructure.Persistence.Configurations;

public class MovementTypeConfiguration : IEntityTypeConfiguration<MovementType>
{
    public void Configure(EntityTypeBuilder<MovementType> builder)
    {
        builder.ToTable("MovementTypes", t =>
            // Only two directions exist. Anything else is a bug that would corrupt every
            // balance quietly, so the database refuses it outright.
            t.HasCheckConstraint("ck_movement_types_direction", "\"Direction\" IN (1, -1)"));

        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ux_movement_types_name");

        builder.Ignore(e => e.IsIncoming);
    }
}

public class MaterialInventoryConfiguration : IEntityTypeConfiguration<MaterialInventory>
{
    public void Configure(EntityTypeBuilder<MaterialInventory> builder)
    {
        builder.ToTable("MaterialInventory", t =>
            // Stock may never go negative (specification section 6). The service checks
            // it too, under a row lock, but this is the guarantee: two tablets cannot
            // both pass a check in code and both write.
            t.HasCheckConstraint("ck_material_inventory_not_negative", "\"CurrentQuantity\" >= 0"));

        // The material is the key — one material cannot have two balances.
        builder.HasKey(e => e.MaterialId);

        builder.Property(e => e.CurrentQuantity).HasPrecision(18, 3);

        builder.HasOne(e => e.Material)
            .WithOne()
            .HasForeignKey<MaterialInventory>(e => e.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaterialInventoryMovementConfiguration
    : IEntityTypeConfiguration<MaterialInventoryMovement>
{
    public void Configure(EntityTypeBuilder<MaterialInventoryMovement> builder)
    {
        builder.ToTable("MaterialInventoryMovements", t =>
            // The direction lives on the movement type, so the quantity itself is
            // always positive. A negative here would flip a balance the wrong way.
            t.HasCheckConstraint("ck_material_movements_positive", "\"Quantity\" > 0"));

        builder.Property(e => e.Quantity).HasPrecision(18, 3);
        builder.Property(e => e.Notes).HasMaxLength(500);

        // "What has this material done lately" is the query the history screen asks.
        builder.HasIndex(e => new { e.MaterialId, e.MovementDate })
            .HasDatabaseName("ix_material_movements_material_date");

        builder.HasOne(e => e.Material)
            .WithMany()
            .HasForeignKey(e => e.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.MovementType)
            .WithMany()
            .HasForeignKey(e => e.MovementTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ShiftReport)
            .WithMany()
            .HasForeignKey(e => e.ShiftReportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
