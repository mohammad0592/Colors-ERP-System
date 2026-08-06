using Colors.Application.Common.Models;
using Colors.Application.Features.Users;
using Colors.Domain.Constants;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Users;

/// <summary>
/// User administration (specification section 3).
///
/// Identity does the work that must never be written by hand: hashing, the security
/// stamp, lockout. This service is the factory's rules around it — the employee number
/// is the login name, a person is deactivated rather than deleted, and the factory can
/// never be left without an administrator.
/// </summary>
public class UserService(
    ColorsDbContext db,
    UserManager<ApplicationUser> users,
    TimeProvider timeProvider) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> GetAllAsync(
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        var people = await db.Users
            .Where(u => includeInactive || u.IsActive)
            .OrderBy(u => u.EmployeeNumber)
            .ToListAsync(cancellationToken);

        var roles = await RolesByUserAsync(cancellationToken);

        return people.Select(u => ToDto(u, roles)).ToList();
    }

    public async Task<Result<UserDto>> GetAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        return user is null
            ? NotFound()
            : Result<UserDto>.Success(ToDto(user, await RolesByUserAsync(cancellationToken)));
    }

    public async Task<Result<UserDto>> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var number = Trimmed(request.EmployeeNumber);
        var name = Trimmed(request.FullName);

        if (number is null)
        {
            return Invalid("An employee number is needed — it is how the floor knows him.");
        }

        if (name is null)
        {
            return Invalid("A full name is needed.");
        }

        var wanted = await CheckRolesAsync(request.Roles, cancellationToken);
        if (!wanted.IsSuccess)
        {
            return Invalid(wanted.Message!);
        }

        // The employee number is the login name, so Identity's own uniqueness check on
        // UserName is the one that matters — but this reads better than its message.
        if (await db.Users.AnyAsync(u => u.EmployeeNumber == number, cancellationToken))
        {
            return Invalid($"Employee number {number} already belongs to somebody.");
        }

        var user = new ApplicationUser
        {
            UserName = number,
            EmployeeNumber = number,
            FullName = name,
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return Invalid(Explain(created));
        }

        if (wanted.Value!.Count > 0)
        {
            var given = await users.AddToRolesAsync(user, wanted.Value);
            if (!given.Succeeded)
            {
                return Invalid(Explain(given));
            }
        }

        return await GetAsync(user.Id, cancellationToken);
    }

    public async Task<Result<UserDto>> UpdateAsync(
        int id,
        UpdateUserRequest request,
        int actingUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var number = Trimmed(request.EmployeeNumber);
        var name = Trimmed(request.FullName);

        if (number is null)
        {
            return Invalid("An employee number is needed — it is how the floor knows him.");
        }

        if (name is null)
        {
            return Invalid("A full name is needed.");
        }

        var wanted = await CheckRolesAsync(request.Roles, cancellationToken);
        if (!wanted.IsSuccess)
        {
            return Invalid(wanted.Message!);
        }

        if (await db.Users.AnyAsync(
                u => u.EmployeeNumber == number && u.Id != id, cancellationToken))
        {
            return Invalid($"Employee number {number} already belongs to somebody.");
        }

        // The factory must never be left without a way in. Losing the last active
        // administrator is not a mistake anybody can undo from a screen — it needs
        // somebody at the database.
        var stillAdministrator = request.IsActive
            && wanted.Value!.Contains(RoleNames.Administrator);

        if (!stillAdministrator && await IsLastAdministratorAsync(id, cancellationToken))
        {
            return Invalid(
                $"{user.FullName} is the only administrator left. Give somebody else the "
                + "administrator role first, or nobody can get back in.");
        }

        var had = await users.GetRolesAsync(user);
        var remove = had.Except(wanted.Value!).ToList();
        var add = wanted.Value!.Except(had).ToList();

        if (remove.Count > 0)
        {
            var removed = await users.RemoveFromRolesAsync(user, remove);
            if (!removed.Succeeded)
            {
                return Invalid(Explain(removed));
            }
        }

        if (add.Count > 0)
        {
            var added = await users.AddToRolesAsync(user, add);
            if (!added.Succeeded)
            {
                return Invalid(Explain(added));
            }
        }

        user.EmployeeNumber = number;
        user.UserName = number;
        user.FullName = name;
        user.IsActive = request.IsActive;

        var saved = await users.UpdateAsync(user);
        if (!saved.Succeeded)
        {
            return Invalid(Explain(saved));
        }

        // Nobody is ever deleted: production records name people for ever
        // (specification section 3). Deactivating is the whole of "he left".
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<UserDto>> ResetPasswordAsync(
        int id,
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        // Not through a reset token. Those exist to carry a link in an email, and this
        // application deliberately registers no token providers: the factory has no
        // email and section 3 gives resets to administrators only.
        //
        // So the password is taken off and a new one put on. Validated *first*, because
        // removing a password and then failing to add one would leave the man with no
        // way in at all.
        foreach (var validator in users.PasswordValidators)
        {
            var allowed = await validator.ValidateAsync(users, user, request.NewPassword);
            if (!allowed.Succeeded)
            {
                return Invalid(Explain(allowed));
            }
        }

        var removed = await users.RemovePasswordAsync(user);
        if (!removed.Succeeded)
        {
            return Invalid(Explain(removed));
        }

        // Moves the security stamp too, so any session this person still has open stops
        // being valid.
        var added = await users.AddPasswordAsync(user, request.NewPassword);
        if (!added.Succeeded)
        {
            return Invalid(Explain(added));
        }

        // A forgotten password is the usual reason an account is locked, so clearing the
        // lock here saves the administrator a second trip.
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<UserDto>> UnlockAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);

        return await GetAsync(id, cancellationToken);
    }

    // ---------- helpers ----------

    /// <summary>
    /// True where this person is the only administrator still working here. Counted from
    /// the join table rather than from a list of names, so a role renamed in Identity
    /// cannot quietly disable the check.
    /// </summary>
    private async Task<bool> IsLastAdministratorAsync(int id, CancellationToken cancellationToken)
    {
        var role = await db.Roles
            .Where(r => r.Name == RoleNames.Administrator)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (role == 0)
        {
            return false;
        }

        var others = await db.UserRoles
            .Where(ur => ur.RoleId == role && ur.UserId != id)
            .Join(db.Users, ur => ur.UserId, u => u.Id, (ur, u) => u.IsActive)
            .CountAsync(active => active, cancellationToken);

        var isOne = await db.UserRoles
            .AnyAsync(ur => ur.RoleId == role && ur.UserId == id, cancellationToken);

        return isOne && others == 0;
    }

    /// <summary>
    /// The roles asked for, checked against the ones that exist.
    ///
    /// A role that is not a real job is refused rather than ignored: a screen that
    /// silently drops a role would leave somebody unable to do their work with nothing
    /// on screen explaining why.
    /// </summary>
    private async Task<Result<List<string>>> CheckRolesAsync(
        IReadOnlyList<string> wanted,
        CancellationToken cancellationToken)
    {
        var asked = wanted
            .Select(r => r.Trim())
            .Where(r => r.Length > 0)
            .Distinct()
            .ToList();

        if (asked.Count == 0)
        {
            return Result<List<string>>.Success([]);
        }

        var known = await db.Roles
            .Where(r => r.Name != null && asked.Contains(r.Name))
            .Select(r => r.Name!)
            .ToListAsync(cancellationToken);

        var unknown = asked.Except(known).ToList();

        return unknown.Count > 0
            ? Result<List<string>>.Failure(
                ErrorCode.ValidationFailed,
                $"There is no role called {string.Join(", ", unknown)}.")
            : Result<List<string>>.Success(known);
    }

    private async Task<Dictionary<int, List<string>>> RolesByUserAsync(
        CancellationToken cancellationToken)
    {
        var pairs = await db.UserRoles
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .Where(x => x.Name != null)
            .ToListAsync(cancellationToken);

        return pairs
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Name!).OrderBy(n => n).ToList());
    }

    private UserDto ToDto(ApplicationUser user, Dictionary<int, List<string>> roles) =>
        new(
            user.Id,
            user.EmployeeNumber,
            user.FullName,
            user.IsActive,
            user.CreatedAt,
            roles.GetValueOrDefault(user.Id, []),
            user.LockoutEnd is not null && user.LockoutEnd > timeProvider.GetUtcNow(),
            user.LockoutEnd);

    /// <summary>
    /// Identity's own words, joined. They are written for a person — "Passwords must
    /// have at least one digit" — so passing them through beats inventing our own.
    /// </summary>
    private static string Explain(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Description));

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<UserDto> Invalid(string message) =>
        Result<UserDto>.Failure(ErrorCode.ValidationFailed, message);

    private static Result<UserDto> NotFound() =>
        Result<UserDto>.Failure(ErrorCode.NotFound, "This person does not exist.");
}
