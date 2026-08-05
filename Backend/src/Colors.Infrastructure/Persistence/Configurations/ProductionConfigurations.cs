using Colors.Domain.Entities.Production;
using Colors.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Colors.Infrastructure.Persistence.Configurations;

public class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> builder)
    {
        builder.ToTable("Batches");

        builder.Property(e => e.Notes).HasMaxLength(500);

        // "What happened on batch 47?" must have one answer for ever.
        builder.HasIndex(e => e.BatchNumber)
            .IsUnique()
            .HasDatabaseName("ux_batches_number");

        builder.HasIndex(e => e.ShiftLineId).HasDatabaseName("ix_batches_shift_line");

        builder.HasOne(e => e.ShiftLine)
            .WithMany()
            .HasForeignKey(e => e.ShiftLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RollConfiguration : IEntityTypeConfiguration<Roll>
{
    public void Configure(EntityTypeBuilder<Roll> builder)
    {
        builder.ToTable("Rolls");

        builder.Property(e => e.RollCode).IsRequired().HasMaxLength(30);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Notes).HasMaxLength(500);

        // The code the factory reads off the roll and repeats on every bag label.
        builder.HasIndex(e => e.RollCode)
            .IsUnique()
            .HasDatabaseName("ux_rolls_code");

        // The serial restarts each day, so it is only unique within its day — and that
        // pair is what stops two tablets handing out roll 13 twice.
        builder.HasIndex(e => new { e.ProductionDate, e.DailySerial })
            .IsUnique()
            .HasDatabaseName("ux_rolls_date_serial");

        // "What is in stock?" — the question the thermo asks all day.
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_rolls_status");

        builder.HasOne(e => e.Batch)
            .WithMany(b => b.Rolls)
            .HasForeignKey(e => e.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.RecipeVersion)
            .WithMany()
            .HasForeignKey(e => e.RecipeVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Color)
            .WithMany()
            .HasForeignKey(e => e.ColorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.ProducedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RollTestReportConfiguration : IEntityTypeConfiguration<RollTestReport>
{
    public void Configure(EntityTypeBuilder<RollTestReport> builder)
    {
        builder.ToTable("RollTestReports", t =>
            // The Roll Log export contains a roll weighing 350 — the operator typed the
            // length into the weight box. Ranges are checked in the service while he is
            // still at the machine; these are the outer walls no path gets past.
            t.HasCheckConstraint(
                "ck_roll_tests_positive",
                "\"Weight\" > 0 AND \"Length\" > 0 AND \"PlateWeight\" > 0"));

        builder.Property(e => e.Weight).HasPrecision(9, 3);
        builder.Property(e => e.Length).HasPrecision(9, 3);
        builder.Property(e => e.PlateWeight).HasPrecision(9, 3);
        builder.Property(e => e.ThicknessRs).HasPrecision(9, 3);
        builder.Property(e => e.ThicknessRm).HasPrecision(9, 3);
        builder.Property(e => e.ThicknessLm).HasPrecision(9, 3);
        builder.Property(e => e.ThicknessLs).HasPrecision(9, 3);
        builder.Property(e => e.Notes).HasMaxLength(500);

        // The mean of the four readings, worked out on the entity.
        builder.Ignore(e => e.AverageThickness);

        // One report per roll. The measurement happens once, as it leaves the machine.
        builder.HasIndex(e => e.RollId)
            .IsUnique()
            .HasDatabaseName("ux_roll_tests_roll");

        // Nullable one-to-one: a roll exists before its measurements do, so a required
        // relationship would make it impossible to save the roll at all.
        builder.HasOne(e => e.Roll)
            .WithOne(r => r.TestReport)
            .HasForeignKey<RollTestReport>(e => e.RollId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.TestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
