using Microsoft.AspNetCore.Identity;

namespace Colors.Infrastructure.Identity;

/// <summary>
/// A person who logs into the system. Specification section 3.
///
/// Lives in Infrastructure, not Domain, on purpose: it inherits from ASP.NET Identity,
/// and Domain must not know that ASP.NET exists. Domain entities refer to a person by
/// <c>int UserId</c> only — which is all the factory floor cares about.
///
/// The base class supplies UserName, Email, PasswordHash, security stamp, lockout and
/// two-factor fields. We never write a password hashing routine ourselves.
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    /// <summary>
    /// The factory's own employee number, e.g. <c>EMP0006</c>. It appears on the paper
    /// forms, so it is how a person is identified on the floor. Unique, and used as the
    /// login name — many workers have no company email.
    /// </summary>
    public required string EmployeeNumber { get; set; }

    /// <summary>Full name as written on the shift report, e.g. م. علي حمدان.</summary>
    public required string FullName { get; set; }

    /// <summary>
    /// False when the person leaves. Never delete a user: historical production records
    /// name them forever, and specification section 16 keeps history permanent.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>When the account was created. Set by the server, never by the client.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
