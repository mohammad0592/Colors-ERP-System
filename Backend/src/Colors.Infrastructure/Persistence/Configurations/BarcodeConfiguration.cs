using Colors.Domain.Entities.Barcodes;
using Colors.Infrastructure.Identity;
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

public class PrintJobConfiguration : IEntityTypeConfiguration<PrintJob>
{
    public void Configure(EntityTypeBuilder<PrintJob> builder)
    {
        builder.ToTable("PrintJobs");

        // A list of codes, kept as a PostgreSQL text array: a job is printed whole, never
        // queried label by label, so a table of its own would only add a join.
        builder.Property(e => e.Barcodes).HasColumnType("text[]");
        builder.Property(e => e.Headline).HasMaxLength(300);
        builder.Property(e => e.Source).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        // "What is waiting, oldest first" — the one question the computer asks all day.
        builder.HasIndex(e => new { e.Status, e.RequestedAt }).HasDatabaseName("ix_print_jobs_status_requested");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.DoneByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
