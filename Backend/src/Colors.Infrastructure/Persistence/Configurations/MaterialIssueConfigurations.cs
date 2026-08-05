using Colors.Domain.Entities.Inventory;
using Colors.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Colors.Infrastructure.Persistence.Configurations;

public class MaterialIssueTicketConfiguration : IEntityTypeConfiguration<MaterialIssueTicket>
{
    public void Configure(EntityTypeBuilder<MaterialIssueTicket> builder)
    {
        builder.ToTable("MaterialIssueTickets");

        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Notes).HasMaxLength(500);

        // The number the worker reads off the paper ticket. Unique for ever — two
        // tickets numbered 47 would make the question "what happened on ticket 47?"
        // unanswerable.
        builder.HasIndex(e => e.TicketNumber)
            .IsUnique()
            .HasDatabaseName("ux_issue_tickets_number");

        // "Is anything still open on this shift?" — asked every time a shift closes.
        builder.HasIndex(e => new { e.ShiftLineId, e.Status })
            .HasDatabaseName("ix_issue_tickets_line_status");

        builder.HasOne(e => e.ShiftLine)
            .WithMany()
            .HasForeignKey(e => e.ShiftLineId)
            .OnDelete(DeleteBehavior.Restrict);

        foreach (var userFk in new[]
                 {
                     nameof(MaterialIssueTicket.IssuedByUserId),
                     nameof(MaterialIssueTicket.ClosedByUserId),
                 })
        {
            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(userFk)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

public class MaterialIssueTicketLineConfiguration
    : IEntityTypeConfiguration<MaterialIssueTicketLine>
{
    public void Configure(EntityTypeBuilder<MaterialIssueTicketLine> builder)
    {
        builder.ToTable("MaterialIssueTicketLines", t =>
        {
            t.HasCheckConstraint("ck_issue_lines_issued_positive", "\"IssuedQuantity\" > 0");
            t.HasCheckConstraint("ck_issue_lines_returned_not_negative", "\"ReturnedQuantity\" >= 0");
            // More came back than went out is not a correction, it is a mistake — and
            // it would make NetUsed negative, which no report could explain.
            t.HasCheckConstraint(
                "ck_issue_lines_returned_within_issued",
                "\"ReturnedQuantity\" <= \"IssuedQuantity\"");
        });

        builder.Property(e => e.IssuedQuantity).HasPrecision(18, 3);
        builder.Property(e => e.ReturnedQuantity).HasPrecision(18, 3);

        // Calculated from the two weighings, never stored.
        builder.Ignore(e => e.NetUsed);

        // One line per material. Issuing GPPS twice on one ticket would leave two
        // numbers where the report expects one.
        builder.HasIndex(e => new { e.TicketId, e.MaterialId })
            .IsUnique()
            .HasDatabaseName("ux_issue_lines_ticket_material");

        builder.HasOne<MaterialIssueTicket>()
            .WithMany(t => t.Lines)
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Material)
            .WithMany()
            .HasForeignKey(e => e.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
