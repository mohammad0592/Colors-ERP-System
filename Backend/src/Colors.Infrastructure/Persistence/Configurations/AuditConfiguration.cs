using Colors.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Colors.Infrastructure.Persistence.Configurations;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        // The specification calls it AuditLog, and the log is what people will look for.
        builder.ToTable("AuditLog");

        builder.Property(e => e.Action).IsRequired().HasMaxLength(60);
        builder.Property(e => e.ObjectType).IsRequired().HasMaxLength(60);
        builder.Property(e => e.Details).HasMaxLength(2000);
        builder.Property(e => e.Result).HasConversion<string>().HasMaxLength(20);

        // The two ways it is read: "what happened on my shift", and "what happened to
        // this thing".
        builder.HasIndex(e => e.Timestamp).HasDatabaseName("ix_audit_when");
        builder.HasIndex(e => e.ShiftReportId).HasDatabaseName("ix_audit_shift");
        builder.HasIndex(e => new { e.ObjectType, e.ObjectId }).HasDatabaseName("ix_audit_object");

        // No foreign keys on purpose. A log line must survive whatever it describes —
        // and it must never be the reason a delete is refused, because the log is a
        // witness, not a participant.
    }
}
