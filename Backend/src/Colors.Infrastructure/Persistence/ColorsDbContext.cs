using System.Reflection;
using Colors.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Persistence;

/// <summary>
/// The single database context for the whole system.
///
/// Inherits the Identity tables (users, roles, the join between them, claims, logins,
/// tokens) and will gain the factory tables as each phase is built.
///
/// Entity configuration lives in <c>Persistence/Configurations</c>, one file per entity,
/// rather than in a single growing <see cref="OnModelCreating"/>.
/// </summary>
public class ColorsDbContext(DbContextOptions<ColorsDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, int>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
