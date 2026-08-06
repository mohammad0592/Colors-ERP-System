using Colors.Application.Features.Users;
using Colors.Domain.Constants;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Users;
using Colors.IntegrationTests.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// User administration (specification section 3).
///
/// The rules worth guarding are the ones a screen cannot undo: nobody is deleted, and the
/// factory is never left without a way in.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class UserTests(DatabaseFixture fixture)
{
    /// <summary>
    /// A real <see cref="UserManager{TUser}"/>, because the whole point of this feature is
    /// that Identity does the hashing, the security stamp and the lockout — a fake would
    /// test our own arithmetic instead of the thing that runs.
    /// </summary>
    private static UserManager<ApplicationUser> ManagerFor(ColorsDbContext db)
    {
        var store = new UserStore<ApplicationUser, ApplicationRole, ColorsDbContext, int>(db);

        var options = new IdentityOptions();
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = false;
        options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        return new UserManager<ApplicationUser>(
            store,
            new OptionsWrapper<IdentityOptions>(options),
            new PasswordHasher<ApplicationUser>(),
            [new UserValidator<ApplicationUser>()],
            [new PasswordValidator<ApplicationUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    private static UserService NewService(ColorsDbContext db) =>
        new(db, ManagerFor(db), TimeProvider.System);

    /// <summary>Makes sure the roles the factory has actually exist in this database.</summary>
    private static async Task SeedRolesAsync(ColorsDbContext db)
    {
        foreach (var name in RoleNames.All)
        {
            if (!await db.Roles.AnyAsync(r => r.Name == name))
            {
                db.Roles.Add(new ApplicationRole
                {
                    Name = name,
                    NormalizedName = name.ToUpperInvariant(),
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static string Number() => $"E{Guid.NewGuid().ToString("N")[..7].ToUpperInvariant()}";

    [Fact]
    public async Task A_worker_is_added_with_the_jobs_he_does()
    {
        await using var db = fixture.CreateContext();
        await SeedRolesAsync(db);

        var number = Number();

        var created = await NewService(db).CreateAsync(new CreateUserRequest(
            number,
            "علي حمدان",
            "Colors123",
            [RoleNames.ExtruderOperator, RoleNames.ExtruderTestPerson]));

        Assert.True(created.IsSuccess, created.Message);
        Assert.Equal(number, created.Value!.EmployeeNumber);
        Assert.True(created.Value.IsActive);

        // One man, two jobs — the whole reason roles are many-to-many
        // (specification section 3).
        Assert.Equal(2, created.Value.Roles.Count);
        Assert.Contains(RoleNames.ExtruderOperator, created.Value.Roles);
        Assert.Contains(RoleNames.ExtruderTestPerson, created.Value.Roles);
    }

    [Fact]
    public async Task The_employee_number_is_how_he_signs_in()
    {
        await using var db = fixture.CreateContext();
        await SeedRolesAsync(db);

        var number = Number();
        await NewService(db).CreateAsync(
            new CreateUserRequest(number, "Test Person", "Colors123", []));

        // Login looks the person up by user name, so the two must never drift apart or
        // the man cannot get in.
        var stored = await db.Users.FirstAsync(u => u.EmployeeNumber == number);
        Assert.Equal(number, stored.UserName);

        // And the password went through Identity, so nothing readable was stored.
        Assert.NotNull(stored.PasswordHash);
        Assert.DoesNotContain("Colors123", stored.PasswordHash);
    }

    [Fact]
    public async Task Two_people_cannot_share_an_employee_number()
    {
        await using var db = fixture.CreateContext();
        await SeedRolesAsync(db);
        var service = NewService(db);
        var number = Number();

        await service.CreateAsync(new CreateUserRequest(number, "First", "Colors123", []));

        var again = await service.CreateAsync(
            new CreateUserRequest(number, "Second", "Colors123", []));

        Assert.False(again.IsSuccess);
        Assert.Contains("already belongs", again.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_role_that_is_not_a_real_job_is_refused()
    {
        await using var db = fixture.CreateContext();
        await SeedRolesAsync(db);

        var created = await NewService(db).CreateAsync(
            new CreateUserRequest(Number(), "Test Person", "Colors123", ["ChiefOfEverything"]));

        // Refused rather than quietly dropped: a man left unable to do his work with
        // nothing on screen saying why is worse than a plain refusal.
        Assert.False(created.IsSuccess);
        Assert.Contains("no role called", created.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_weak_password_is_refused_in_identitys_own_words()
    {
        await using var db = fixture.CreateContext();
        await SeedRolesAsync(db);

        var created = await NewService(db).CreateAsync(
            new CreateUserRequest(Number(), "Test Person", "short", []));

        Assert.False(created.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(created.Message));
    }

    [Fact]
    public async Task Roles_are_given_and_taken_away_without_touching_anything_else()
    {
        await using var db = fixture.CreateContext();
        await SeedRolesAsync(db);
        var service = NewService(db);

        var created = await service.CreateAsync(new CreateUserRequest(
            Number(),
            "Test Person",
            "Colors123",
            [RoleNames.ThermoOperator, RoleNames.PackagingOperator]));

        var id = created.Value!.Id;

        // The factory hires a packer: one role moves off this man, nothing is rebuilt.
        var updated = await service.UpdateAsync(
            id,
            new UpdateUserRequest(
                created.Value.EmployeeNumber,
                "Test Person",
                [RoleNames.ThermoOperator],
                true),
            actingUserId: id);

        Assert.True(updated.IsSuccess, updated.Message);
        Assert.Equal([RoleNames.ThermoOperator], updated.Value!.Roles);
    }

    [Fact]
    public async Task Somebody_who_leaves_is_made_inactive_and_keeps_his_history()
    {
        await using var db = fixture.CreateContext();
        await SeedRolesAsync(db);
        var service = NewService(db);

        var created = await service.CreateAsync(
            new CreateUserRequest(Number(), "Test Person", "Colors123", [RoleNames.Supervisor]));

        var id = created.Value!.Id;

        var left = await service.UpdateAsync(
            id,
            new UpdateUserRequest(
                created.Value.EmployeeNumber, "Test Person", [RoleNames.Supervisor], false),
            actingUserId: id);

        Assert.True(left.IsSuccess, left.Message);
        Assert.False(left.Value!.IsActive);

        // Still there. Production records name people for ever, so deleting is never an
        // option (specification section 3).
        Assert.True(await db.Users.AnyAsync(u => u.Id == id));
        Assert.Contains(await service.GetAllAsync(includeInactive: true), u => u.Id == id);
        Assert.DoesNotContain(await service.GetAllAsync(includeInactive: false), u => u.Id == id);
    }

    [Fact]
    public async Task The_last_administrator_cannot_be_deactivated()
    {
        await using var db = fixture.CreateContext();
        await SeedRolesAsync(db);
        var service = NewService(db);

        // Start from a database with no administrator left, then add exactly one.
        foreach (var existing in await db.Users.Where(u => u.IsActive).ToListAsync())
        {
            existing.IsActive = false;
        }

        await db.SaveChangesAsync();

        var only = await service.CreateAsync(new CreateUserRequest(
            Number(), "The Only Administrator", "Colors123", [RoleNames.Administrator]));

        Assert.True(only.IsSuccess, only.Message);
        var id = only.Value!.Id;

        var out1 = await service.UpdateAsync(
            id,
            new UpdateUserRequest(
                only.Value.EmployeeNumber,
                "The Only Administrator",
                [RoleNames.Administrator],
                false),
            actingUserId: id);

        // Nobody could get back in afterwards, and no screen could undo it.
        Assert.False(out1.IsSuccess);
        Assert.Contains("only administrator", out1.Message!, StringComparison.OrdinalIgnoreCase);

        // Taking the role away instead is the same lockout by another route.
        var out2 = await service.UpdateAsync(
            id,
            new UpdateUserRequest(
                only.Value.EmployeeNumber, "The Only Administrator", [RoleNames.Supervisor], true),
            actingUserId: id);

        Assert.False(out2.IsSuccess);

        // Still an administrator, still active.
        var after = await service.GetAsync(id);
        Assert.True(after.Value!.IsActive);
        Assert.Contains(RoleNames.Administrator, after.Value.Roles);
    }

    [Fact]
    public async Task With_a_second_administrator_the_first_may_step_down()
    {
        await using var db = fixture.CreateContext();
        await SeedRolesAsync(db);
        var service = NewService(db);

        var first = await service.CreateAsync(new CreateUserRequest(
            Number(), "First Administrator", "Colors123", [RoleNames.Administrator]));
        var second = await service.CreateAsync(new CreateUserRequest(
            Number(), "Second Administrator", "Colors123", [RoleNames.Administrator]));

        Assert.True(second.IsSuccess, second.Message);

        var stepped = await service.UpdateAsync(
            first.Value!.Id,
            new UpdateUserRequest(
                first.Value.EmployeeNumber, "First Administrator", [RoleNames.Supervisor], true),
            actingUserId: first.Value.Id);

        // Somebody else can still get in, so this is an ordinary change.
        Assert.True(stepped.IsSuccess, stepped.Message);
        Assert.DoesNotContain(RoleNames.Administrator, stepped.Value!.Roles);
    }

    [Fact]
    public async Task A_new_password_works_and_frees_a_locked_account()
    {
        await using var db = fixture.CreateContext();
        await SeedRolesAsync(db);
        var service = NewService(db);

        var created = await service.CreateAsync(
            new CreateUserRequest(Number(), "Test Person", "Colors123", []));

        var id = created.Value!.Id;

        // Five wrong tries lock the account, which is what brings the man to the office.
        var manager = ManagerFor(db);
        var user = await manager.FindByIdAsync(id.ToString());
        await manager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(5));

        Assert.True((await service.GetAsync(id)).Value!.IsLockedOut);

        var reset = await service.ResetPasswordAsync(
            id, new ResetPasswordRequest("Newpass123"));

        Assert.True(reset.IsSuccess, reset.Message);

        // Freed in the same act — a forgotten password is the usual reason for the lock,
        // so making the administrator come back a second time would be silly.
        Assert.False(reset.Value!.IsLockedOut);

        // And the new password is the one that works.
        var reloaded = await ManagerFor(db).FindByIdAsync(id.ToString());
        Assert.True(await ManagerFor(db).CheckPasswordAsync(reloaded!, "Newpass123"));
        Assert.False(await ManagerFor(db).CheckPasswordAsync(reloaded!, "Colors123"));
    }

    [Fact]
    public async Task An_account_can_be_freed_without_changing_the_password()
    {
        await using var db = fixture.CreateContext();
        await SeedRolesAsync(db);
        var service = NewService(db);

        var created = await service.CreateAsync(
            new CreateUserRequest(Number(), "Test Person", "Colors123", []));

        var id = created.Value!.Id;

        var manager = ManagerFor(db);
        var user = await manager.FindByIdAsync(id.ToString());
        await manager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(5));

        var freed = await service.UnlockAsync(id);

        Assert.True(freed.IsSuccess, freed.Message);
        Assert.False(freed.Value!.IsLockedOut);

        // He remembered it after all, so it must still work.
        var reloaded = await ManagerFor(db).FindByIdAsync(id.ToString());
        Assert.True(await ManagerFor(db).CheckPasswordAsync(reloaded!, "Colors123"));
    }
}
