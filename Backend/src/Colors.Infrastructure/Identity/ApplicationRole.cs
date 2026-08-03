using Microsoft.AspNetCore.Identity;

namespace Colors.Infrastructure.Identity;

/// <summary>
/// One of the nine jobs in the factory. Names come from
/// <see cref="Domain.Constants.RoleNames"/> and are seeded at startup.
///
/// A user may hold several roles — that is the whole reason Identity's many-to-many
/// join table is used instead of a single RoleId column on the user.
/// </summary>
public class ApplicationRole : IdentityRole<int>
{
    /// <summary>
    /// What this role may do, in plain words, shown on the user-management screen.
    /// "ExtruderTestPerson" means nothing to an administrator; the description does.
    /// </summary>
    public string? Description { get; set; }
}
