using Colors.Domain.Entities.Shifts;
using Colors.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Colors.Infrastructure.Persistence.Configurations;

public class ShiftReportConfiguration : IEntityTypeConfiguration<ShiftReport>
{
    public void Configure(EntityTypeBuilder<ShiftReport> builder)
    {
        builder.ToTable("ShiftReports");

        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Notes).HasMaxLength(1000);

        builder.Property(e => e.ElectricityStartMeter).HasPrecision(14, 2);
        builder.Property(e => e.ElectricityEndMeter).HasPrecision(14, 2);

        // Calculated on the entity, never stored — two values must not be able to
        // disagree with the third derived from them.
        builder.Ignore(e => e.ElectricityUsed);

        // One shift per day for the whole factory. Enforced here rather than only in
        // code, so two supervisors opening the same shift cannot both succeed and
        // split a day's production across two records.
        builder.HasIndex(e => new { e.ProductionDate, e.ShiftId })
            .IsUnique()
            .HasDatabaseName("ux_shift_reports_date_shift");

        // A second index, ux_shift_reports_single_open, allows only one Open row in the
        // whole table — the factory works one shift at a time (specification section 2).
        // It lives in the migration as SQL rather than here, because it indexes a
        // constant:
        //
        //     CREATE UNIQUE INDEX ... ON "ShiftReports" ((TRUE)) WHERE "Status" = 'Open'
        //
        // Every open row then collides with every other open row. EF cannot express an
        // index on an expression, and cannot see this one, so it will never try to drop
        // it either.

        builder.HasOne(e => e.Shift)
            .WithMany()
            .HasForeignKey(e => e.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        foreach (var userFk in new[]
                 {
                     nameof(ShiftReport.OpenedByUserId),
                     nameof(ShiftReport.ClosedByUserId),
                     nameof(ShiftReport.SupervisorUserId),
                 })
        {
            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(userFk)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

public class ShiftLineConfiguration : IEntityTypeConfiguration<ShiftLine>
{
    public void Configure(EntityTypeBuilder<ShiftLine> builder)
    {
        builder.ToTable("ShiftLines");

        builder.Property(e => e.DowntimeHours).HasPrecision(9, 2);
        builder.Property(e => e.CycleTimeSeconds).HasPrecision(9, 2);

        // Calculated on the entity, never stored.
        builder.Ignore(e => e.ActualProductionHours);

        // A line appears once in a shift.
        builder.HasIndex(e => new { e.ShiftReportId, e.ProductionLineId })
            .IsUnique()
            .HasDatabaseName("ux_shift_lines_report_line");

        // The list is nearly always "this line, most recent first".
        builder.HasIndex(e => e.ProductionLineId)
            .HasDatabaseName("ix_shift_lines_line");

        // A line's record has no meaning without its shift.
        builder.HasOne(e => e.ShiftReport)
            .WithMany(r => r.Lines)
            .HasForeignKey(e => e.ShiftReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ProductionLine)
            .WithMany()
            .HasForeignKey(e => e.ProductionLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Mould)
            .WithMany()
            .HasForeignKey(e => e.MouldId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ShiftWorkerConfiguration : IEntityTypeConfiguration<ShiftWorker>
{
    public void Configure(EntityTypeBuilder<ShiftWorker> builder)
    {
        builder.ToTable("ShiftWorkers");

        // One row per person per line — the same worker cannot be listed twice on the
        // same line, though he may legitimately appear on two lines in one shift.
        builder.HasIndex(e => new { e.ShiftLineId, e.UserId })
            .IsUnique()
            .HasDatabaseName("ux_shift_workers_line_user");

        builder.HasOne<ShiftLine>()
            .WithMany(l => l.Workers)
            .HasForeignKey(e => e.ShiftLineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ShiftWorkerRoleConfiguration : IEntityTypeConfiguration<ShiftWorkerRole>
{
    public void Configure(EntityTypeBuilder<ShiftWorkerRole> builder)
    {
        builder.ToTable("ShiftWorkerRoles");

        // A job listed twice for the same man says nothing the first row did not.
        builder.HasIndex(e => new { e.ShiftWorkerId, e.RoleId })
            .IsUnique()
            .HasDatabaseName("ux_shift_worker_roles_worker_role");

        builder.HasOne<ShiftWorker>()
            .WithMany(w => w.Roles)
            .HasForeignKey(e => e.ShiftWorkerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationRole>()
            .WithMany()
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
