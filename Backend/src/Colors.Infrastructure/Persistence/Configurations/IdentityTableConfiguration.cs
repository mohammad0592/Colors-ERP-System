using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Colors.Infrastructure.Persistence.Configurations;

/// <summary>
/// Renames the remaining ASP.NET Identity tables so the database matches the table
/// list in specification section 16. Identity would otherwise call them
/// <c>AspNetUserRoles</c>, <c>AspNetUserClaims</c> and so on.
/// </summary>
public class UserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<int>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<int>> builder) =>
        builder.ToTable("UserRoles");
}

public class UserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<int>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<int>> builder) =>
        builder.ToTable("UserClaims");
}

public class UserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<int>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<int>> builder) =>
        builder.ToTable("UserLogins");
}

public class UserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<int>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<int>> builder) =>
        builder.ToTable("UserTokens");
}

public class RoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<int>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<int>> builder) =>
        builder.ToTable("RoleClaims");
}
