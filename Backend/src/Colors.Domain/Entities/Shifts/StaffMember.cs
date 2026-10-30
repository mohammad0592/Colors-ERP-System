namespace Colors.Domain.Entities.Shifts;

/// <summary>
/// Somebody who works on the floor but does not use the system — no account, no
/// password, no role (specification section 19.19). Kept only so a shift can say they
/// worked it.
///
/// Apart from the system's users on purpose. A user is an account: he signs in, holds
/// roles and answers for what he records. Most of the floor never touches a tablet, and
/// giving each of them an account they never use would fill the user list with logins
/// nobody can sign in to and the administrator has to look after.
/// </summary>
public class StaffMember
{
    public int Id { get; set; }

    public required string FullName { get; set; }

    /// <summary>The factory's number for him, where he has one. Unique when given.</summary>
    public string? EmployeeNumber { get; set; }

    /// <summary>
    /// Off when he has left. Never deleted: the shifts he worked go on naming him.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public int CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
