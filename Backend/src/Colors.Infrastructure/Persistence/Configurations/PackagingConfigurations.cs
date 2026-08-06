using Colors.Domain.Entities.Packaging;
using Colors.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Colors.Infrastructure.Persistence.Configurations;

public class WoodenPalletConfiguration : IEntityTypeConfiguration<WoodenPallet>
{
    public void Configure(EntityTypeBuilder<WoodenPallet> builder)
    {
        builder.ToTable("WoodenPallets", t =>
        {
            // A pallet cannot be shipped before it was finished, or finished before it
            // was started.
            t.HasCheckConstraint(
                "ck_pallets_dates_in_order",
                "(\"CompletedAt\" IS NULL OR \"CompletedAt\" >= \"CreatedAt\") "
                + "AND (\"ShippedAt\" IS NULL OR \"CompletedAt\" IS NOT NULL) "
                + "AND (\"ShippedAt\" IS NULL OR \"ShippedAt\" >= \"CompletedAt\")");

            // The two travel together: an empty pallet has neither, a pallet that has
            // taken its first bag has both. One without the other is a half-set pallet
            // that no code path should be able to write.
            t.HasCheckConstraint(
                "ck_pallets_colour_and_product_together",
                "(\"ColorId\" IS NULL) = (\"ProductId\" IS NULL)");
        });

        builder.Property(e => e.Notes).HasMaxLength(500);

        // The status is worked out from the dates and the bags on it.
        builder.Ignore(e => e.Status);

        builder.HasIndex(e => e.PalletNumber)
            .IsUnique()
            .HasDatabaseName("ux_pallets_number");

        builder.HasIndex(e => e.ShiftLineId).HasDatabaseName("ix_pallets_shift_line");

        builder.HasOne(e => e.ShiftLine)
            .WithMany()
            .HasForeignKey(e => e.ShiftLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Color)
            .WithMany()
            .HasForeignKey(e => e.ColorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PackagingConsumptionConfiguration : IEntityTypeConfiguration<PackagingConsumption>
{
    public void Configure(EntityTypeBuilder<PackagingConsumption> builder)
    {
        builder.ToTable("PackagingConsumptions");

        builder.Property(e => e.Notes).HasMaxLength(500);

        // Recorded once, at the end of the shift. A second record for the same line
        // would double every figure and there would be no way to say which was meant.
        builder.HasIndex(e => e.ShiftLineId)
            .IsUnique()
            .HasDatabaseName("ux_packaging_shift_line");

        builder.HasOne(e => e.ShiftLine)
            .WithMany()
            .HasForeignKey(e => e.ShiftLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PackagingConsumptionLineConfiguration
    : IEntityTypeConfiguration<PackagingConsumptionLine>
{
    public void Configure(EntityTypeBuilder<PackagingConsumptionLine> builder)
    {
        builder.ToTable("PackagingConsumptionLines", t =>
            // Nothing was used a negative number of times, and a weight of zero is not
            // a weighing — it is a box nobody filled in.
            t.HasCheckConstraint(
                "ck_packaging_lines_positive",
                "\"Quantity\" >= 0 AND (\"Weight\" IS NULL OR \"Weight\" > 0)"));

        builder.Property(e => e.Quantity).HasPrecision(12, 3);
        builder.Property(e => e.Weight).HasPrecision(12, 3);

        // Worked out from the quantity and the material's unit weight.
        builder.Ignore(e => e.ExpectedWeight);

        // One line per material. Two lines for large bags would have to be added
        // together by every reader, and one of them would forget.
        builder.HasIndex(e => new { e.ConsumptionId, e.MaterialId })
            .IsUnique()
            .HasDatabaseName("ux_packaging_lines_material");

        builder.HasOne(e => e.Consumption)
            .WithMany(c => c.Lines)
            .HasForeignKey(e => e.ConsumptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Material)
            .WithMany()
            .HasForeignKey(e => e.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BagPalletAssignmentConfiguration : IEntityTypeConfiguration<BagPalletAssignment>
{
    public void Configure(EntityTypeBuilder<BagPalletAssignment> builder)
    {
        builder.ToTable("BagPalletAssignments", t =>
            // A reversal is who, when and why. Any one of them alone is not a
            // correction, it is a half-written one.
            t.HasCheckConstraint(
                "ck_bag_pallet_reversal_complete",
                "(\"ReversedAt\" IS NULL AND \"ReversedByUserId\" IS NULL "
                + "AND \"ReversalReason\" IS NULL) "
                + "OR (\"ReversedAt\" IS NOT NULL AND \"ReversedByUserId\" IS NOT NULL "
                + "AND \"ReversalReason\" IS NOT NULL)"));

        builder.Property(e => e.ReversalReason).HasMaxLength(300);

        builder.Ignore(e => e.IsActive);

        // The rule the database enforces, because application code cannot: two tablets
        // can scan the same bag in the same moment.
        //
        // Partial, and it has to be. Rows are never deleted, so a plain unique index
        // would mean a bag scanned onto the wrong pallet could never go onto the right
        // one — the second row would be refused for ever. Restricting it to assignments
        // that have not been reversed keeps everything that matters: one pallet at a
        // time, no double scan, and the mistake still in the history.
        builder.HasIndex(e => e.ProducedBagId)
            .IsUnique()
            .HasFilter("\"ReversedAt\" IS NULL")
            .HasDatabaseName("ux_bag_pallet_bag");

        builder.HasIndex(e => e.WoodenPalletId).HasDatabaseName("ix_bag_pallet_pallet");

        builder.HasOne(e => e.ProducedBag)
            .WithMany()
            .HasForeignKey(e => e.ProducedBagId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.WoodenPallet)
            .WithMany(p => p.Assignments)
            .HasForeignKey(e => e.WoodenPalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.ReversedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
