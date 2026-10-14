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
        // The Roll Log export contains a roll weighing 350 — the operator typed the length
        // into the weight box. Ranges are checked in the service while he is still at the
        // machine; this is the outer wall no path gets past.
        builder.ToTable("Rolls", t => t.HasCheckConstraint(
            "ck_rolls_measured_positive",
            "(\"Weight\" IS NULL OR \"Weight\" > 0) AND (\"Length\" IS NULL OR \"Length\" > 0)"));

        // Room for the colour's name at the end: 04N2309260844-White.
        builder.Property(e => e.RollCode).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Weight).HasPrecision(9, 3);
        builder.Property(e => e.Length).HasPrecision(9, 3);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Notes).HasMaxLength(500);

        // The code the factory reads off the roll and repeats on every bag label.
        builder.HasIndex(e => e.RollCode)
            .IsUnique()
            .HasDatabaseName("ux_rolls_code");

        // Not unique: the number starts again when a supervisor resets it. The code is
        // unique, and the counter hands each number out once between resets.

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

        builder.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Color)
            .WithMany()
            .HasForeignKey(e => e.ColorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.ProducedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.MeasuredByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RollNumberCounterConfiguration : IEntityTypeConfiguration<RollNumberCounter>
{
    public void Configure(EntityTypeBuilder<RollNumberCounter> builder)
    {
        builder.ToTable("RollNumberCounters", t =>
            t.HasCheckConstraint("ck_roll_number_positive", "\"NextNumber\" > 0"));

        // Handed out by the database, never by the code, so it is not generated here.
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.LastResetByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RollTestReportConfiguration : IEntityTypeConfiguration<RollTestReport>
{
    public void Configure(EntityTypeBuilder<RollTestReport> builder)
    {
        builder.ToTable("RollTestReports", t =>
        {
            // Readings arrive one at a time (section 19.15), so each may be missing — but a
            // reading that is there is a real one.
            t.HasCheckConstraint("ck_roll_tests_positive", "\"PlateWeight\" IS NULL OR \"PlateWeight\" > 0");
            // The dry weight comes first and the wet one ten minutes later, so the dry may
            // stand alone; the wet never does, and a soaked piece is never lighter.
            t.HasCheckConstraint(
                "ck_roll_tests_absorbency",
                "(\"AbsorbencyDryWeight\" IS NULL OR \"AbsorbencyDryWeight\" > 0) AND "
                + "(\"AbsorbencyWetWeight\" IS NULL OR \"AbsorbencyWetWeight\" >= \"AbsorbencyDryWeight\")");
        });

        builder.Property(e => e.PlateWeight).HasPrecision(9, 3);
        builder.Property(e => e.ThicknessRs).HasPrecision(9, 3);
        builder.Property(e => e.ThicknessRm).HasPrecision(9, 3);
        builder.Property(e => e.ThicknessLm).HasPrecision(9, 3);
        builder.Property(e => e.ThicknessLs).HasPrecision(9, 3);
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.Property(e => e.AbsorbencyDryWeight).HasPrecision(9, 3);
        builder.Property(e => e.AbsorbencyWetWeight).HasPrecision(9, 3);

        // The mean of the four readings, and the absorbency, worked out on the entity.
        builder.Ignore(e => e.AverageThickness);
        builder.Ignore(e => e.AbsorbencyPercentage);

        // One report per roll, filled in and corrected in place.
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
            // A record saying the recycler produced nothing is not a record of anything.
            // If it did not run, nothing is written (specification section 11).
            t.HasCheckConstraint(
                "ck_recycler_weight_positive",
                "\"RecycledMaterialWeight\" > 0");
        });

        builder.Property(e => e.RecycledMaterialWeight).HasPrecision(18, 3);
        builder.Property(e => e.Notes).HasMaxLength(500);

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
