using Colors.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Colors.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Identity's default table names (AspNetUsers ...) are renamed to match
        // the table list in specification section 16.
        builder.ToTable("Users");

        builder.Property(u => u.EmployeeNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        // The employee number identifies a person on the factory's paper forms,
        // so two people may never share one.
        builder.HasIndex(u => u.EmployeeNumber)
            .IsUnique()
            .HasDatabaseName("ux_users_employee_number");
    }
}
