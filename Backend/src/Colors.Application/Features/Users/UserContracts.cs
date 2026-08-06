using Colors.Application.Common.Models;

namespace Colors.Application.Features.Users;

/// <summary>Shapes crossing the API for user administration. Specification section 3.</summary>

public sealed record UserDto(
    int Id,
    string EmployeeNumber,
    string FullName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    /// <summary>
    /// Every job this person may do. Many-to-many on purpose: one man is the extruder
    /// operator and its test person today, and the factory wants to split those later
    /// without anything being rebuilt (specification section 3).
    /// </summary>
    IReadOnlyList<string> Roles,
    /// <summary>
    /// True while five wrong passwords are still locking the account. It clears itself
    /// after five minutes, but an administrator can free the person immediately — on a
    /// factory floor, five minutes waiting at a tablet is a real cost.
    /// </summary>
    bool IsLockedOut,
    DateTimeOffset? LockedOutUntil);

/// <summary>
/// A new worker. The employee number is the login name as well, because the factory
/// identifies people by it on paper and many workers have no email.
/// </summary>
public sealed record CreateUserRequest(
    string EmployeeNumber,
    string FullName,
    string Password,
    IReadOnlyList<string> Roles);

/// <summary>
/// Everything an administrator may change about a person afterwards.
///
/// The employee number is here because a typo at creation would otherwise be permanent —
/// and since it is the login name, correcting it changes how that person signs in.
/// </summary>
public sealed record UpdateUserRequest(
    string EmployeeNumber,
    string FullName,
    IReadOnlyList<string> Roles,
    bool IsActive);

/// <summary>
/// A new password, set by an administrator.
///
/// There is no self-service reset: the factory has no email, so the flow that normally
/// carries a reset link does not exist (specification section 3).
/// </summary>
public sealed record ResetPasswordRequest(string NewPassword);

/// <summary>
/// User administration (specification section 3).
///
/// <b>Nobody is ever deleted.</b> Production records name people for ever, so a worker
/// who leaves is made inactive and keeps their history.
///
/// Declared here, implemented in Infrastructure — users live in ASP.NET Identity, which
/// the rest of the application must not know about.
/// </summary>
public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetAllAsync(
        bool includeInactive = true,
        CancellationToken cancellationToken = default);

    Task<Result<UserDto>> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<UserDto>> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the name, the employee number, the roles and whether the person still
    /// works here.
    ///
    /// Refused where it would leave the factory with no active administrator — that is a
    /// lockout only database surgery could undo.
    /// </summary>
    Task<Result<UserDto>> UpdateAsync(
        int id,
        UpdateUserRequest request,
        int actingUserId,
        CancellationToken cancellationToken = default);

    Task<Result<UserDto>> ResetPasswordAsync(
        int id,
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Frees an account locked by wrong passwords, without waiting out the five minutes.</summary>
    Task<Result<UserDto>> UnlockAsync(int id, CancellationToken cancellationToken = default);
}
