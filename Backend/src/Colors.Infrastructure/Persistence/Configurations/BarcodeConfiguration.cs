using Colors.Domain.Entities.Barcodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Colors.Infrastructure.Persistence.Configurations;

public class BarcodeConfiguration : IEntityTypeConfiguration<Barcode>
{
    public void Configure(EntityTypeBuilder<Barcode> builder)
    {
        builder.ToTable("Barcodes");

        builder.Property(e => e.Value).IsRequired().HasMaxLength(50);
        builder.Property(e => e.ObjectType).HasConversion<string>().HasMaxLength(20);

        // The whole point of one table: uniqueness across every object type, enforced
        // in one place. Application code cannot promise this — two tablets can print
        // at the same moment.
        builder.HasIndex(e => e.Value)
            .IsUnique()
            .HasDatabaseName("ux_barcodes_value");

        // "What is this object's barcode?" — asked whenever a label is reprinted.
        builder.HasIndex(e => new { e.ObjectType, e.ObjectId })
            .HasDatabaseName("ix_barcodes_object");
    }
}
