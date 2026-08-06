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

public class ThermoProductionConfiguration : IEntityTypeConfiguration<ThermoProduction>
{
    public void Configure(EntityTypeBuilder<ThermoProduction> builder)
    {
        builder.ToTable("ThermoProductions", t =>
            // A roll cannot come out before it went in.
            t.HasCheckConstraint(
                "ck_thermo_finished_after_started",
                "\"FinishedAt\" IS NULL OR \"FinishedAt\" >= \"StartedAt\""));

        builder.Property(e => e.Notes).HasMaxLength(500);

        // The total time in the machine, worked out from the two timestamps here.
        builder.Ignore(e => e.TotalTimeMinutes);

        // One roll goes in whole and is never split, so it is formed exactly once.
        builder.HasIndex(e => e.RollId)
            .IsUnique()
            .HasDatabaseName("ux_thermo_productions_roll");

        builder.HasIndex(e => e.ShiftLineId).HasDatabaseName("ix_thermo_productions_shift_line");

        builder.HasOne(e => e.Roll)
            .WithOne()
            .HasForeignKey<ThermoProduction>(e => e.RollId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ShiftLine)
            .WithMany()
            .HasForeignKey(e => e.ShiftLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.OperatorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ThermoTestReportConfiguration : IEntityTypeConfiguration<ThermoTestReport>
{
    public void Configure(EntityTypeBuilder<ThermoTestReport> builder)
    {
        builder.ToTable("ThermoTestReports", t =>
            // The outer walls. The service refuses these with a sentence the operator
            // can act on; this is what no code path can get past.
            t.HasCheckConstraint(
                "ck_thermo_tests_positive",
                "\"BagCount\" > 0 AND \"PieceCount\" > 0 AND \"PieceWeight\" > 0 "
                + "AND \"BagWeight\" > 0 "
                + "AND \"AbsorbentPercentage\" >= 0 AND \"AbsorbentPercentage\" <= 100"));

        builder.Property(e => e.PieceWeight).HasPrecision(9, 3);
        builder.Property(e => e.BagWeight).HasPrecision(9, 3);
        builder.Property(e => e.AbsorbentPercentage).HasPrecision(5, 2);
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.HasIndex(e => e.ThermoProductionId)
            .IsUnique()
            .HasDatabaseName("ux_thermo_tests_production");

        // Nullable one-to-one for the same reason as the roll's: the run exists before
        // anyone has counted what came out of it.
        builder.HasOne(e => e.ThermoProduction)
            .WithOne(p => p.TestReport)
            .HasForeignKey<ThermoTestReport>(e => e.ThermoProductionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.TestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProducedBagConfiguration : IEntityTypeConfiguration<ProducedBag>
{
    public void Configure(EntityTypeBuilder<ProducedBag> builder)
    {
        builder.ToTable("ProducedBags", t =>
            t.HasCheckConstraint(
                "ck_produced_bags_positive",
                "\"Weight\" > 0 AND \"PieceCount\" > 0"));

        builder.Property(e => e.Weight).HasPrecision(9, 3);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.HasIndex(e => e.ThermoProductionId)
            .HasDatabaseName("ix_produced_bags_production");

        // "Which bags can go on a pallet?" — asked every time one is built.
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_produced_bags_status");

        builder.HasOne(e => e.ThermoProduction)
            .WithMany(p => p.Bags)
            .HasForeignKey(e => e.ThermoProductionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Color)
            .WithMany()
            .HasForeignKey(e => e.ColorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RecyclerProductionConfiguration : IEntityTypeConfiguration<RecyclerProduction>
{
    public void Configure(EntityTypeBuilder<RecyclerProduction> builder)
    {
        builder.ToTable("RecyclerProductions", t =>
        {
            // Neither weight can be negative, and a record where nothing was weighed at
            // all is not a record of anything (specification section 11).
            t.HasCheckConstraint(
                "ck_recycler_weights",
                "\"ScrapWeight\" >= 0 AND \"RecycledMaterialWeight\" >= 0 "
                + "AND (\"ScrapWeight\" > 0 OR \"RecycledMaterialWeight\" > 0)");
        });

        builder.Property(e => e.ScrapWeight).HasPrecision(18, 3);
        builder.Property(e => e.RecycledMaterialWeight).HasPrecision(18, 3);
        builder.Property(e => e.Notes).HasMaxLength(500);

        // Worked out from the two weights on the row.
        builder.Ignore(e => e.LossPercentage);

        // Once per line of the shift. A second record would add the same recycled
        // material to the store twice, and no reader could say which was meant.
        builder.HasIndex(e => e.ShiftLineId)
            .IsUnique()
            .HasDatabaseName("ux_recycler_shift_line");

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
